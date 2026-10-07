using Exparter.Application.Common;
using Exparter.Domain.Identity;
using FluentValidation;
using Mediator;

namespace Exparter.Application.Identity;

public sealed record ChangeUsernameCommand(Guid UserId, string Username) : ICommand<AccountDto>;

public sealed class ChangeUsernameCommandValidator : AbstractValidator<ChangeUsernameCommand>
{
    public ChangeUsernameCommandValidator()
    {
        RuleFor(x => x.Username).ValidUsername();
    }
}

public sealed class ChangeUsernameCommandHandler(IUserRepository users, IUnitOfWork unitOfWork)
    : ICommandHandler<ChangeUsernameCommand, AccountDto>
{
    public async ValueTask<AccountDto> Handle(ChangeUsernameCommand command, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(command.UserId, cancellationToken)
            ?? throw IdentityAppErrors.AccountNotFound();

        // Sending the current username again is a no-op rather than a "taken" conflict with oneself.
        if (user.Username != command.Username)
        {
            if (await users.GetByUsernameAsync(command.Username, cancellationToken) is not null)
            {
                throw IdentityAppErrors.UsernameAlreadyTaken();
            }

            user.ChangeUsername(command.Username);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return AccountDto.From(user);
    }
}

internal static class UsernameValidation
{
    public static IRuleBuilderOptions<T, string> ValidUsername<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty()
            // A missing field arrives as null despite the non-nullable type; NotEmpty reports it.
            .Must(username => username is null || Username.IsWellFormed(username))
            .WithMessage("The username must be 3-30 characters of lowercase letters, digits, underscores and periods, with no period at the start or end and no consecutive periods.");
}
