using Exparter.Application.Identity;
using Exparter.Domain.Identity;
using Microsoft.AspNetCore.Identity;

namespace Exparter.Infrastructure.Identity;

/// <summary>PBKDF2 through the framework's hasher (see ADR 0003).</summary>
internal sealed class PasswordHasher : IPasswordHasher
{
    // The framework hasher never reads the user argument; it exists for custom per-user schemes.
    private readonly PasswordHasher<User> _hasher = new();

    public string Hash(string password) => _hasher.HashPassword(null!, password);

    public bool Verify(string passwordHash, string password) =>
        _hasher.VerifyHashedPassword(null!, passwordHash, password) != PasswordVerificationResult.Failed;
}
