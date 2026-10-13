namespace Exparter.Domain.Skills;

public sealed class UserSkill
{
    private UserSkill()
    {
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid SkillId { get; private set; }
    public UserSkillType Type { get; private set; }

    /// <summary>
    /// For an offered skill, the user's own level. For a wanted skill, the minimum level
    /// of the teacher the user is looking for.
    /// </summary>
    public SkillLevel Level { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Adds a skill to a user's profile, given the skills already on it.</summary>
    public static UserSkill Add(
        Guid userId,
        Skill skill,
        UserSkillType type,
        SkillLevel level,
        IReadOnlyCollection<UserSkill> existing,
        DateTimeOffset now)
    {
        if (skill.Status != SkillStatus.Approved)
        {
            throw SkillErrors.SkillNotApproved();
        }

        if (existing.Any(other => other.SkillId == skill.Id && other.Type == type))
        {
            throw SkillErrors.UserSkillAlreadyExists();
        }

        if (existing.Count(other => other.Type == type) >= UserSkillRules.MaxPerType)
        {
            throw SkillErrors.SkillLimitReached();
        }

        UserSkillRules.EnsureLevelsAreConsistent(skill.Id, type, level, existing);

        return new UserSkill
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            SkillId = skill.Id,
            Type = type,
            Level = level,
            CreatedAt = now,
        };
    }

    /// <param name="existing">All skills on the same profile, this one included.</param>
    public void ChangeLevel(SkillLevel level, IReadOnlyCollection<UserSkill> existing)
    {
        UserSkillRules.EnsureLevelsAreConsistent(SkillId, Type, level, existing);
        Level = level;
    }
}

public static class UserSkillRules
{
    /// <summary>The same for everyone for now; meant to depend on the user's plan later.</summary>
    public const int MaxPerType = 5;

    /// <summary>
    /// A user may both offer and want the same skill, to keep learning it from someone
    /// better. Wanting a teacher at or below one's own level would be meaningless.
    /// </summary>
    public static void EnsureLevelsAreConsistent(
        Guid skillId,
        UserSkillType type,
        SkillLevel level,
        IReadOnlyCollection<UserSkill> existing)
    {
        var counterpartType = type == UserSkillType.Offered ? UserSkillType.Wanted : UserSkillType.Offered;
        var counterpart = existing.FirstOrDefault(other => other.SkillId == skillId && other.Type == counterpartType);
        if (counterpart is null)
        {
            return;
        }

        var (offered, wanted) = type == UserSkillType.Offered ? (level, counterpart.Level) : (counterpart.Level, level);
        if (wanted <= offered)
        {
            throw SkillErrors.WantedLevelNotHigher();
        }
    }
}
