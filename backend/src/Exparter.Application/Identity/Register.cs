using Exparter.Application.Common;
using Exparter.Domain.Identity;
using FluentValidation;
using Mediator;

namespace Exparter.Application.Identity;

public sealed record RegisterCommand(string Email, string Username, string Password, bool AgeConfirmed)
    : ICommand<AuthResult>;

public sealed class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().MaximumLength(254).EmailAddress();
        RuleFor(x => x.Username).ValidUsername();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8).MaximumLength(128);
        RuleFor(x => x.AgeConfirmed).Equal(true).WithMessage("You must confirm that you are 18 or older.");
    }
}

public sealed class RegisterCommandHandler(
    IUserRepository users,
    IOneTimeTokenRepository oneTimeTokens,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    TokenIssuer tokenIssuer,
    IEmailSender emailSender,
    IPublisher publisher,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<RegisterCommand, AuthResult>
{
    public async ValueTask<AuthResult> Handle(RegisterCommand command, CancellationToken cancellationToken)
    {
        if (await users.GetByEmailAsync(User.NormalizeEmail(command.Email), cancellationToken) is not null)
        {
            throw IdentityAppErrors.EmailAlreadyRegistered();
        }

        if (await users.GetByUsernameAsync(command.Username, cancellationToken) is not null)
        {
            throw IdentityAppErrors.UsernameAlreadyTaken();
        }

        var now = timeProvider.GetUtcNow();
        var user = User.Register(
            command.Email,
            command.Username,
            passwordHasher.Hash(command.Password),
            command.AgeConfirmed,
            now);
        users.Add(user);

        var verification = tokenService.GenerateOpaqueToken();
        oneTimeTokens.Add(OneTimeToken.Issue(user.Id, OneTimeTokenPurpose.EmailVerification, verification.Hash, now));

        var tokens = tokenIssuer.IssueForSignIn(user);

        // Published before saving: whatever other modules add for the new user is committed
        // together with the account, or not at all.
        await publisher.Publish(new UserRegistered(user.Id), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await emailSender.SendEmailVerificationAsync(user.Email, verification.Value, cancellationToken);

        return new AuthResult(tokens.AccessToken, tokens.RefreshToken, tokens.ExpiresIn, AccountDto.From(user), AccountRestored: false);
    }
}
