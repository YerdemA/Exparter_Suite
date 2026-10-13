using System.Globalization;
using System.Text;
using Exparter.Domain.Common;

namespace Exparter.Domain.Skills;

public enum SkillStatus
{
    PendingApproval,
    Approved,
    Rejected,
}

public enum UserSkillType
{
    Offered,
    Wanted,
}

/// <summary>Declared in ascending order: comparisons between levels rely on it.</summary>
public enum SkillLevel
{
    Beginner,
    Intermediate,
    Advanced,
    Expert,
}

public static class SkillErrors
{
    public const string InvalidSkillNameCode = "invalid_skill_name";
    public const string InvalidStateCode = "invalid_state";
    public const string SkillNotApprovedCode = "skill_not_approved";
    public const string UserSkillAlreadyExistsCode = "user_skill_already_exists";
    public const string SkillLimitReachedCode = "skill_limit_reached";
    public const string WantedLevelNotHigherCode = "wanted_level_not_higher";

    public static DomainException InvalidSkillName() =>
        new(InvalidSkillNameCode, "The skill name must be 2 to 60 characters and contain a letter or digit.");

    public static DomainException ProposalAlreadyReviewed() =>
        new(InvalidStateCode, "Only a pending proposal can be approved or rejected.");

    public static DomainException SkillNotApproved() =>
        new(SkillNotApprovedCode, "Only approved skills can be added to a profile.");

    public static DomainException UserSkillAlreadyExists() =>
        new(UserSkillAlreadyExistsCode, "The skill is already on the profile with this type.");

    public static DomainException SkillLimitReached() =>
        new(SkillLimitReachedCode, $"A user can have at most {UserSkillRules.MaxPerType} skills of each type.");

    public static DomainException WantedLevelNotHigher() =>
        new(WantedLevelNotHigherCode, "A skill that is both offered and wanted needs a wanted level above the offered level.");
}

public static class Slug
{
    private static readonly Dictionary<char, string> TurkishLetters = new()
    {
        ['ı'] = "i", ['İ'] = "i", ['ğ'] = "g", ['Ğ'] = "g", ['ü'] = "u", ['Ü'] = "u",
        ['ş'] = "s", ['Ş'] = "s", ['ö'] = "o", ['Ö'] = "o", ['ç'] = "c", ['Ç'] = "c",
    };

    /// <summary>
    /// Reduces a name to lowercase ASCII words joined by hyphens, so that "Yapay Zekâ" and
    /// "yapay zeka" are the same skill and a search needs no accents.
    /// </summary>
    public static string From(string text)
    {
        var builder = new StringBuilder(text.Length);
        var pendingHyphen = false;
        foreach (var character in text.Normalize(NormalizationForm.FormD))
        {
            if (TurkishLetters.TryGetValue(character, out var replacement))
            {
                Append(replacement);
            }
            else if (char.IsAsciiLetterOrDigit(character))
            {
                Append(char.ToLowerInvariant(character).ToString());
            }
            else if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                // Anything else separates words; combining accents are simply dropped.
                pendingHyphen = builder.Length > 0;
            }
        }

        return builder.ToString();

        void Append(string value)
        {
            if (pendingHyphen)
            {
                builder.Append('-');
                pendingHyphen = false;
            }

            builder.Append(value);
        }
    }
}

public sealed class SkillCategory
{
    private SkillCategory()
    {
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public string Slug { get; private set; } = null!;
    public int SortOrder { get; private set; }
}

public sealed class Skill
{
    public const int NameMinLength = 2;
    public const int NameMaxLength = 60;

    private Skill()
    {
    }

    public Guid Id { get; private set; }
    public Guid CategoryId { get; private set; }
    public string Name { get; private set; } = null!;

    /// <summary>Unique: two skills whose names differ only in case, accents or spacing are the same skill.</summary>
    public string Slug { get; private set; } = null!;

    public SkillStatus Status { get; private set; }
    public Guid? ProposedByUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ReviewedAt { get; private set; }

    public static Skill Propose(Guid categoryId, string name, Guid proposedByUserId, DateTimeOffset now)
    {
        var trimmed = name.Trim();
        var slug = Skills.Slug.From(trimmed);
        if (trimmed.Length is < NameMinLength or > NameMaxLength || slug.Length == 0)
        {
            throw SkillErrors.InvalidSkillName();
        }

        return new Skill
        {
            Id = Guid.CreateVersion7(),
            CategoryId = categoryId,
            Name = trimmed,
            Slug = slug,
            Status = SkillStatus.PendingApproval,
            ProposedByUserId = proposedByUserId,
            CreatedAt = now,
        };
    }

    public void Approve(DateTimeOffset now) => Review(SkillStatus.Approved, now);

    public void Reject(DateTimeOffset now) => Review(SkillStatus.Rejected, now);

    private void Review(SkillStatus outcome, DateTimeOffset now)
    {
        if (Status != SkillStatus.PendingApproval)
        {
            throw SkillErrors.ProposalAlreadyReviewed();
        }

        Status = outcome;
        ReviewedAt = now;
    }
}
