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

    /// <summary>
    /// Usernames are lowercase only: uppercase input is rejected, not converted, so the
    /// name a user sees while typing is exactly the name they get.
    /// </summary>
    public static bool IsWellFormed(string value) =>
        Format().IsMatch(value) && !value.Contains("..", StringComparison.Ordinal);

    public static bool IsReserved(string value) => Reserved.Contains(value);

    /// <summary>
    /// Folds what a user typed into a sign-in field to the stored form. Invariant casing,
    /// so that "I" never becomes the Turkish dotless "ı".
    /// </summary>
    public static string NormalizeForLookup(string value) => value.Trim().ToLowerInvariant();

    public static string Parse(string value)
    {
        if (!IsWellFormed(value))
        {
            throw IdentityErrors.InvalidUsername();
        }

        if (IsReserved(value))
        {
            throw IdentityErrors.UsernameReserved();
        }

        return value;
    }
}
