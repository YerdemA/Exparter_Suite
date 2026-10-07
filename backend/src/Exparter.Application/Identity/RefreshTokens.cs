using Exparter.Application.Common;
using Exparter.Domain.Identity;
using FluentValidation;
using Mediator;

namespace Exparter.Application.Identity;

public sealed record RefreshTokenCommand(string RefreshToken) : ICommand<TokenPair>;

public sealed class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty().MaximumLength(512);
    }
}

public sealed class RefreshTokenCommandHandler(
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    ITokenService tokenService,
    TokenIssuer tokenIssuer,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<RefreshTokenCommand, TokenPair>
{
    public async ValueTask<TokenPair> Handle(RefreshTokenCommand command, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var current = await refreshTokens.GetByHashAsync(tokenService.HashOpaqueToken(command.RefreshToken), cancellationToken)
            ?? throw IdentityAppErrors.RefreshTokenInvalid();

        if (current.IsRevoked)
        {
            // A revoked token coming back means it was copied: either the thief or the
            // owner is replaying it. Revoking the whole family signs both of them out.
            foreach (var token in await refreshTokens.ListByFamilyAsync(current.FamilyId, cancellationToken))
            {
                token.Revoke(now);
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
            throw IdentityAppErrors.RefreshTokenInvalid();
        }

        var user = await users.GetByIdAsync(current.UserId, cancellationToken);
        if (current.IsExpired(now) || user is null || user.Status != UserStatus.Active)
        {
            throw IdentityAppErrors.RefreshTokenInvalid();
        }

        var tokens = tokenIssuer.Rotate(user, current);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return tokens;
    }
}

public sealed record LogoutCommand(string RefreshToken) : ICommand;

public sealed class LogoutCommandValidator : AbstractValidator<LogoutCommand>
{
    public LogoutCommandValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty().MaximumLength(512);
    }
}

public sealed class LogoutCommandHandler(
    IRefreshTokenRepository refreshTokens,
    ITokenService tokenService,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<LogoutCommand>
{
    public async ValueTask<Unit> Handle(LogoutCommand command, CancellationToken cancellationToken)
    {
        var token = await refreshTokens.GetByHashAsync(tokenService.HashOpaqueToken(command.RefreshToken), cancellationToken);
        if (token is not null)
        {
            token.Revoke(timeProvider.GetUtcNow());
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Unit.Value;
    }
}
