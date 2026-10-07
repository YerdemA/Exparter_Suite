using Exparter.Application.Common;
using Exparter.Domain.Identity;
using FluentValidation;
using Mediator;

namespace Exparter.Application.Identity;

public sealed record RequestPasswordResetCommand(string Email) : ICommand;

public sealed class RequestPasswordResetCommandValidator : AbstractValidator<RequestPasswordResetCommand>
{
    public RequestPasswordResetCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().MaximumLength(254).EmailAddress();
    }
}

public sealed class RequestPasswordResetCommandHandler(
    IUserRepository users,
    IOneTimeTokenRepository oneTimeTokens,
    ITokenService tokenService,
    IEmailSender emailSender,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<RequestPasswordResetCommand>
{
    public async ValueTask<Unit> Handle(RequestPasswordResetCommand command, CancellationToken cancellationToken)
    {
        var user = await users.GetByEmailAsync(User.NormalizeEmail(command.Email), cancellationToken);

        // Unknown and suspended accounts get the same silent success as real ones, so the
        // endpoint cannot be used to find out which emails are registered.
        if (user is null || user.Status == UserStatus.Suspended)
        {
            return Unit.Value;
        }

        var now = timeProvider.GetUtcNow();
        foreach (var previous in await oneTimeTokens.ListUnusedAsync(user.Id, OneTimeTokenPurpose.PasswordReset, cancellationToken))
        {
            previous.Invalidate(now);
        }

        var reset = tokenService.GenerateOpaqueToken();
        oneTimeTokens.Add(OneTimeToken.Issue(user.Id, OneTimeTokenPurpose.PasswordReset, reset.Hash, now));
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await emailSender.SendPasswordResetAsync(user.Email, reset.Value, cancellationToken);
        return Unit.Value;
    }
}

public sealed record ConfirmPasswordResetCommand(string Token, string NewPassword) : ICommand;

public sealed class ConfirmPasswordResetCommandValidator : AbstractValidator<ConfirmPasswordResetCommand>
{
    public ConfirmPasswordResetCommandValidator()
    {
        RuleFor(x => x.Token).NotEmpty().MaximumLength(512);
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(8).MaximumLength(128);
    }
}

public sealed class ConfirmPasswordResetCommandHandler(
    IUserRepository users,
    IOneTimeTokenRepository oneTimeTokens,
    IRefreshTokenRepository refreshTokens,
    ITokenService tokenService,
    IPasswordHasher passwordHasher,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<ConfirmPasswordResetCommand>
{
    public async ValueTask<Unit> Handle(ConfirmPasswordResetCommand command, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var token = await oneTimeTokens.GetByHashAsync(tokenService.HashOpaqueToken(command.Token), cancellationToken);
        if (token is null || !token.CanBeUsed(OneTimeTokenPurpose.PasswordReset, now))
        {
            throw IdentityAppErrors.InvalidToken();
        }

        var user = await users.GetByIdAsync(token.UserId, cancellationToken)
            ?? throw IdentityAppErrors.InvalidToken();

        token.Use(OneTimeTokenPurpose.PasswordReset, now);
        user.ChangePassword(passwordHasher.Hash(command.NewPassword));

        // Whoever knew the old password must not stay signed in on any device.
        foreach (var refreshToken in await refreshTokens.ListActiveByUserAsync(user.Id, now, cancellationToken))
        {
            refreshToken.Revoke(now);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
