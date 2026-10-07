using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Exparter.Application.Identity;
using Exparter.Domain.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Exparter.Infrastructure.Identity;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; init; } = null!;

    [Required]
    public string Audience { get; init; } = null!;

    /// <summary>HS256 needs at least 256 bits of key material.</summary>
    [Required]
    [MinLength(32)]
    public string SigningKey { get; init; } = null!;

    [Range(1, 60)]
    public int AccessTokenLifetimeMinutes { get; init; } = 15;

    public SymmetricSecurityKey CreateSecurityKey() => new(Encoding.UTF8.GetBytes(SigningKey));
}

public static class JwtClaims
{
    public const string Subject = "sub";
    public const string Role = "role";
    public const string EmailVerified = "email_verified";
}

internal sealed class JwtTokenService(IOptions<JwtOptions> options, TimeProvider timeProvider) : ITokenService
{
    private readonly JwtOptions _options = options.Value;
    private readonly JsonWebTokenHandler _handler = new();

    public AccessToken CreateAccessToken(User user)
    {
        var now = timeProvider.GetUtcNow();
        var lifetime = TimeSpan.FromMinutes(_options.AccessTokenLifetimeMinutes);
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = (now + lifetime).UtcDateTime,
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtClaims.Subject, user.Id.ToString()),
                new Claim(JwtClaims.Role, user.Role.ToString()),
                new Claim(JwtClaims.EmailVerified, user.IsEmailVerified ? "true" : "false", ClaimValueTypes.Boolean),
            ]),
            SigningCredentials = new SigningCredentials(_options.CreateSecurityKey(), SecurityAlgorithms.HmacSha256),
        };

        return new AccessToken(_handler.CreateToken(descriptor), (int)lifetime.TotalSeconds);
    }

    public OpaqueToken GenerateOpaqueToken()
    {
        var value = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
        return new OpaqueToken(value, HashOpaqueToken(value));
    }

    // A fast hash is enough: the input is 256 random bits, so it cannot be guessed offline.
    public string HashOpaqueToken(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
