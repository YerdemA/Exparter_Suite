using System.Text.RegularExpressions;

namespace Exparter.Domain.Identity;

public static partial class Username
{
    public const int MinLength = 3;
    public const int MaxLength = 30;

    private static readonly HashSet<string> Reserved =
    [
        "admin", "administrator", "exparter", "support", "destek", "moderator", "root", "system",
    ];

    [GeneratedRegex("^[a-z0-9_][a-z0-9_.]{1,28}[a-z0-9_]$")]
    private static partial Regex Format();

    /// <summary>Lowercases with invariant casing, so that "I" never becomes the Turkish dotless "ı".</summary>
    public static string Normalize(string value) => value.Trim().ToLowerInvariant();

    public static bool IsWellFormed(string normalized) =>
        Format().IsMatch(normalized) && !normalized.Contains("..", StringComparison.Ordinal);

    public static bool IsReserved(string normalized) => Reserved.Contains(normalized);

    public static string Parse(string value)
    {
        var normalized = Normalize(value);
        if (!IsWellFormed(normalized))
        {
            throw IdentityErrors.InvalidUsername();
        }

        if (IsReserved(normalized))
        {
            throw IdentityErrors.UsernameReserved();
        }

        return normalized;
    }
}
