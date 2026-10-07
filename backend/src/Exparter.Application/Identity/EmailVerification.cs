using Exparter.Application.Common;
using Exparter.Domain.Identity;
using FluentValidation;
using Mediator;

namespace Exparter.Application.Identity;

public sealed record RequestEmailVerificationCommand(Guid UserId) : ICommand;

public sealed class RequestEmailVerificationCommandHandler(
    IUserRepository users,
    IOneTimeTokenRepository oneTimeTokens,
    ITokenService tokenService,
    IEmailSender emailSender,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<RequestEmailVerificationCommand>
{
    public async ValueTask<Unit> Handle(RequestEmailVerificationCommand command, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(command.UserId, cancellationToken)
            ?? throw IdentityAppErrors.AccountNotFound();
        if (user.IsEmailVerified)
        {
            throw IdentityErrors.EmailAlreadyVerified();
        }

        var now = timeProvider.GetUtcNow();
        foreach (var previous in await oneTimeTokens.ListUnusedAsync(user.Id, OneTimeTokenPurpose.EmailVerification, cancellationToken))
        {
            previous.Invalidate(now);
        }

        var verification = tokenService.GenerateOpaqueToken();
        oneTimeTokens.Add(OneTimeToken.Issue(user.Id, OneTimeTokenPurpose.EmailVerification, verification.Hash, now));
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await emailSender.SendEmailVerificationAsync(user.Email, verification.Value, cancellationToken);
        return Unit.Value;
    }
}

public sealed record ConfirmEmailVerificationCommand(string Token) : ICommand;

public sealed class ConfirmEmailVerificationCommandValidator : AbstractValidator<ConfirmEmailVerificationCommand>
{
    public ConfirmEmailVerificationCommandValidator()
    {
        RuleFor(x => x.Token).NotEmpty().MaximumLength(512);
    }
}

public sealed class ConfirmEmailVerificationCommandHandler(
    IUserRepository users,
    IOneTimeTokenRepository oneTimeTokens,
    ITokenService tokenService,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<ConfirmEmailVerificationCommand>
{
    public async ValueTask<Unit> Handle(ConfirmEmailVerificationCommand command, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var token = await oneTimeTokens.GetByHashAsync(tokenService.HashOpaqueToken(command.Token), cancellationToken);
        if (token is null || !token.CanBeUsed(OneTimeTokenPurpose.EmailVerification, now))
        {
            throw IdentityAppErrors.InvalidToken();
        }

        var user = await users.GetByIdAsync(token.UserId, cancellationToken)
            ?? throw IdentityAppErrors.InvalidToken();

        token.Use(OneTimeTokenPurpose.EmailVerification, now);
        user.VerifyEmail(now);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
