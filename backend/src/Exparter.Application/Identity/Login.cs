using Exparter.Application.Common;
using Exparter.Domain.Identity;
using FluentValidation;
using Mediator;

namespace Exparter.Application.Identity;

/// <param name="Identifier">The account's email address or username.</param>
public sealed record LoginCommand(string Identifier, string Password) : ICommand<AuthResult>;

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Identifier).NotEmpty().MaximumLength(254);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(128);
    }
}

public sealed class LoginCommandHandler(
    IUserRepository users,
    IPasswordHasher passwordHasher,
    TokenIssuer tokenIssuer,
    IUnitOfWork unitOfWork) : ICommandHandler<LoginCommand, AuthResult>
{
    public async ValueTask<AuthResult> Handle(LoginCommand command, CancellationToken cancellationToken)
    {
        // A username can never contain "@", so its presence tells the two apart.
        var user = command.Identifier.Contains('@')
            ? await users.GetByEmailAsync(User.NormalizeEmail(command.Identifier), cancellationToken)
            : await users.GetByUsernameAsync(Username.Normalize(command.Identifier), cancellationToken);

        if (user is null || !passwordHasher.Verify(user.PasswordHash, command.Password))
        {
            throw IdentityAppErrors.InvalidCredentials();
        }

        if (user.Status == UserStatus.Suspended)
        {
            throw IdentityAppErrors.AccountSuspended();
        }

        var restored = user.CancelDeletion();
        var tokens = tokenIssuer.IssueForSignIn(user);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new AuthResult(tokens.AccessToken, tokens.RefreshToken, tokens.ExpiresIn, AccountDto.From(user), restored);
    }
}
