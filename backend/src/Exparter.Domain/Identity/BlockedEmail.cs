using System.Security.Cryptography;
using System.Text;

namespace Exparter.Domain.Identity;

/// <summary>
/// Remembers, for a while, that an address belonged to an erased account, so that it
/// cannot be used to register again right away. Only a hash is kept: the address itself
/// is gone with the account.
/// </summary>
public sealed class BlockedEmail
{
    /// <summary>How long after an account is erased its address cannot be registered again.</summary>
    public static readonly TimeSpan Duration = TimeSpan.FromDays(90);

    public const int HashLength = 64;

    private BlockedEmail()
    {
    }

    public Guid Id { get; private set; }
    public string EmailHash { get; private set; } = null!;
    public DateTimeOffset BlockedUntil { get; private set; }

    public static BlockedEmail For(string email, DateTimeOffset now) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            EmailHash = HashOf(email),
            BlockedUntil = now + Duration,
        };

    public static string HashOf(string email) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(User.NormalizeEmail(email))));
}
