using Exparter.Application.Common;
using Exparter.Domain.Skills;

namespace Exparter.Application.Skills;

public interface ISkillRepository
{
    Task<IReadOnlyList<SkillCategory>> ListCategoriesAsync(CancellationToken cancellationToken);

    Task<bool> CategoryExistsAsync(Guid categoryId, CancellationToken cancellationToken);

    /// <summary>Approved skills only, ordered by name.</summary>
    /// <param name="slugFragment">Matches skills whose slug contains it; null for no filter.</param>
    Task<PagedList<Skill>> SearchApprovedAsync(
        Guid? categoryId,
        string? slugFragment,
        PageRequest page,
        CancellationToken cancellationToken);

    Task<Skill?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken);

    /// <summary>Skills proposed by one user, newest first, whatever their status.</summary>
    Task<PagedList<Skill>> ListProposedByAsync(Guid userId, PageRequest page, CancellationToken cancellationToken);

    /// <summary>Skills proposed by users, oldest first.</summary>
    Task<PagedList<Skill>> ListProposalsAsync(SkillStatus? status, PageRequest page, CancellationToken cancellationToken);

    void Add(Skill skill);
}

/// <summary>A user skill together with the catalog skill it refers to.</summary>
public sealed record UserSkillWithSkill(UserSkill UserSkill, Skill Skill);

public interface IUserSkillRepository
{
    Task<IReadOnlyList<UserSkill>> ListByUserAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Offered skills first, then by the order they were added.</summary>
    Task<PagedList<UserSkillWithSkill>> ListWithSkillsAsync(
        Guid userId,
        UserSkillType? type,
        PageRequest page,
        CancellationToken cancellationToken);

    Task<bool> HasOfferedSkillAsync(Guid userId, CancellationToken cancellationToken);

    void Add(UserSkill userSkill);

    void Remove(UserSkill userSkill);
}

/// <summary>What other modules may know about a user's skills.</summary>
public interface IUserSkillDirectory
{
    Task<bool> HasOfferedSkillAsync(Guid userId, CancellationToken cancellationToken);
}

internal sealed class UserSkillDirectory(IUserSkillRepository userSkills) : IUserSkillDirectory
{
    public Task<bool> HasOfferedSkillAsync(Guid userId, CancellationToken cancellationToken) =>
        userSkills.HasOfferedSkillAsync(userId, cancellationToken);
}

public sealed record SkillCategoryDto(Guid Id, string Name, string Slug);

public sealed record SkillDto(Guid Id, Guid CategoryId, string Name, string Slug, SkillStatus Status)
{
    public static SkillDto From(Skill skill) => new(skill.Id, skill.CategoryId, skill.Name, skill.Slug, skill.Status);
}

public sealed record AdminSkillProposalDto(
    Guid Id,
    Guid CategoryId,
    string Name,
    string Slug,
    SkillStatus Status,
    Guid? ProposedByUserId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReviewedAt)
{
    public static AdminSkillProposalDto From(Skill skill) =>
        new(skill.Id, skill.CategoryId, skill.Name, skill.Slug, skill.Status, skill.ProposedByUserId, skill.CreatedAt, skill.ReviewedAt);
}

public sealed record UserSkillDto(Guid Id, SkillDto Skill, UserSkillType Type, SkillLevel Level, DateTimeOffset CreatedAt)
{
    public static UserSkillDto From(UserSkill userSkill, Skill skill) =>
        new(userSkill.Id, SkillDto.From(skill), userSkill.Type, userSkill.Level, userSkill.CreatedAt);

    public static UserSkillDto From(UserSkillWithSkill item) => From(item.UserSkill, item.Skill);
}

internal static class SkillAppErrors
{
    public static AppException SkillNotFound() => AppException.NotFound("The skill does not exist.");

    public static AppException UserSkillNotFound() => AppException.NotFound("The user skill does not exist.");

    public static AppException UserNotFound() => AppException.NotFound("The user does not exist.");

    public static AppException SkillAlreadyExists() =>
        AppException.Conflict("skill_already_exists", "A skill with this name already exists or is awaiting review.");

    public static AppException EmailNotVerified() =>
        AppException.Forbidden("email_not_verified", "A verified email is required.");
}
