using Exparter.Domain.Identity;

namespace Exparter.Application.Identity;

/// <summary>Issues the access/refresh token pair shared by registration, sign-in and refresh.</summary>
public sealed class TokenIssuer(ITokenService tokenService, IRefreshTokenRepository refreshTokens, TimeProvider timeProvider)
{
    public TokenPair IssueForSignIn(User user)
    {
        var opaque = tokenService.GenerateOpaqueToken();
        refreshTokens.Add(RefreshToken.IssueForSignIn(user.Id, opaque.Hash, timeProvider.GetUtcNow()));
        return Pair(user, opaque);
    }

    public TokenPair Rotate(User user, RefreshToken current)
    {
        var opaque = tokenService.GenerateOpaqueToken();
        refreshTokens.Add(current.Rotate(opaque.Hash, timeProvider.GetUtcNow()));
        return Pair(user, opaque);
    }

    private TokenPair Pair(User user, OpaqueToken refreshToken)
    {
        var accessToken = tokenService.CreateAccessToken(user);
        return new TokenPair(accessToken.Value, refreshToken.Value, accessToken.ExpiresInSeconds);
    }
}
