using Exparter.Application.Common;
using FluentValidation;
using Mediator;

namespace Exparter.Application.Identity;

public sealed record GetAccountQuery(Guid UserId) : IQuery<AccountDto>;

public sealed class GetAccountQueryHandler(IUserRepository users) : IQueryHandler<GetAccountQuery, AccountDto>
{
    public async ValueTask<AccountDto> Handle(GetAccountQuery query, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(query.UserId, cancellationToken)
            ?? throw IdentityAppErrors.AccountNotFound();
        return AccountDto.From(user);
    }
}

public sealed record RequestAccountDeletionCommand(Guid UserId, string Password) : ICommand<AccountDeletionScheduled>;

public sealed class RequestAccountDeletionCommandValidator : AbstractValidator<RequestAccountDeletionCommand>
{
    public RequestAccountDeletionCommandValidator()
    {
        RuleFor(x => x.Password).NotEmpty().MaximumLength(128);
    }
}

public sealed class RequestAccountDeletionCommandHandler(
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    IPasswordHasher passwordHasher,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<RequestAccountDeletionCommand, AccountDeletionScheduled>
{
    public async ValueTask<AccountDeletionScheduled> Handle(
        RequestAccountDeletionCommand command,
        CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(command.UserId, cancellationToken)
            ?? throw IdentityAppErrors.AccountNotFound();
        if (!passwordHasher.Verify(user.PasswordHash, command.Password))
        {
            throw IdentityAppErrors.InvalidPassword();
        }

        var now = timeProvider.GetUtcNow();
        user.RequestDeletion(now);
        foreach (var refreshToken in await refreshTokens.ListActiveByUserAsync(user.Id, now, cancellationToken))
        {
            refreshToken.Revoke(now);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new AccountDeletionScheduled(user.PermanentDeletionAt!.Value);
    }
}
