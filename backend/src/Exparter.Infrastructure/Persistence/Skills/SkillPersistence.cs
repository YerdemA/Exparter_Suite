using Exparter.Application.Common;
using Exparter.Application.Skills;
using Exparter.Domain.Skills;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Exparter.Infrastructure.Persistence.Skills;

internal static class SkillsSchema
{
    public const string Name = "skills";
    public const int SlugMaxLength = 80;
}

internal sealed class SkillCategoryConfiguration : IEntityTypeConfiguration<SkillCategory>
{
    public void Configure(EntityTypeBuilder<SkillCategory> builder)
    {
        builder.ToTable("skill_categories", SkillsSchema.Name);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Name).HasMaxLength(60);
        builder.Property(x => x.Slug).HasMaxLength(SkillsSchema.SlugMaxLength);
        builder.HasIndex(x => x.Slug).IsUnique();
        builder.HasData(SkillCatalogSeed.Categories());
    }
}

internal sealed class SkillConfiguration : IEntityTypeConfiguration<Skill>
{
    public void Configure(EntityTypeBuilder<Skill> builder)
    {
        builder.ToTable("skills", SkillsSchema.Name);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Name).HasMaxLength(Skill.NameMaxLength);
        builder.Property(x => x.Slug).HasMaxLength(SkillsSchema.SlugMaxLength);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        builder.HasIndex(x => x.Slug).IsUnique().HasDatabaseName(UniqueConstraints.SkillSlug);
        builder.HasIndex(x => new { x.Status, x.CategoryId });
        builder.HasOne<SkillCategory>().WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasData(SkillCatalogSeed.Skills());
    }
}

internal sealed class UserSkillConfiguration : IEntityTypeConfiguration<UserSkill>
{
    public void Configure(EntityTypeBuilder<UserSkill> builder)
    {
        builder.ToTable("user_skills", SkillsSchema.Name);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Type).HasConversion<string>().HasMaxLength(16);

        // Stored as a number on purpose: discovery will compare levels in SQL.
        builder.Property(x => x.Level).HasConversion<int>();

        builder.HasIndex(x => new { x.UserId, x.SkillId, x.Type }).IsUnique().HasDatabaseName(UniqueConstraints.UserSkillPerType);
        builder.HasIndex(x => new { x.SkillId, x.Type });
        builder.HasOne<Skill>().WithMany().HasForeignKey(x => x.SkillId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class SkillRepository(AppDbContext dbContext) : ISkillRepository
{
    public async Task<IReadOnlyList<SkillCategory>> ListCategoriesAsync(CancellationToken cancellationToken) =>
        await dbContext.SkillCategories.AsNoTracking().OrderBy(x => x.SortOrder).ToListAsync(cancellationToken);

    public Task<bool> CategoryExistsAsync(Guid categoryId, CancellationToken cancellationToken) =>
        dbContext.SkillCategories.AnyAsync(x => x.Id == categoryId, cancellationToken);

    public Task<PagedList<Skill>> SearchApprovedAsync(
        Guid? categoryId,
        string? slugFragment,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Skills.AsNoTracking().Where(x => x.Status == SkillStatus.Approved);
        if (categoryId is not null)
        {
            query = query.Where(x => x.CategoryId == categoryId);
        }

        if (slugFragment is not null)
        {
            // A slug holds only [a-z0-9-], so the fragment cannot contain LIKE wildcards.
            query = query.Where(x => x.Slug.Contains(slugFragment));
        }

        return query.OrderBy(x => x.Slug).ToPageAsync(page, cancellationToken);
    }

    public Task<Skill?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Skills.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken) =>
        dbContext.Skills.AnyAsync(x => x.Slug == slug, cancellationToken);

    public Task<PagedList<Skill>> ListProposedByAsync(Guid userId, PageRequest page, CancellationToken cancellationToken) =>
        dbContext.Skills.AsNoTracking()
            .Where(x => x.ProposedByUserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .ToPageAsync(page, cancellationToken);

    public Task<PagedList<Skill>> ListProposalsAsync(SkillStatus? status, PageRequest page, CancellationToken cancellationToken)
    {
        var query = dbContext.Skills.AsNoTracking().Where(x => x.ProposedByUserId != null);
        if (status is not null)
        {
            query = query.Where(x => x.Status == status);
        }

        return query.OrderBy(x => x.CreatedAt).ToPageAsync(page, cancellationToken);
    }

    public void Add(Skill skill) => dbContext.Skills.Add(skill);
}

internal sealed class UserSkillRepository(AppDbContext dbContext) : IUserSkillRepository
{
    public async Task<IReadOnlyList<UserSkill>> ListByUserAsync(Guid userId, CancellationToken cancellationToken) =>
        await dbContext.UserSkills.Where(x => x.UserId == userId).ToListAsync(cancellationToken);

    public Task<PagedList<UserSkillWithSkill>> ListWithSkillsAsync(
        Guid userId,
        UserSkillType? type,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        var userSkills = dbContext.UserSkills.AsNoTracking().Where(x => x.UserId == userId);
        if (type is not null)
        {
            userSkills = userSkills.Where(x => x.Type == type);
        }

        return userSkills
            .Join(dbContext.Skills.AsNoTracking(), userSkill => userSkill.SkillId, skill => skill.Id, (userSkill, skill) => new { userSkill, skill })
            .OrderBy(x => x.userSkill.Type)
            .ThenBy(x => x.userSkill.CreatedAt)
            .Select(x => new UserSkillWithSkill(x.userSkill, x.skill))
            .ToPageAsync(page, cancellationToken);
    }

    public Task<bool> HasOfferedSkillAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.UserSkills.AnyAsync(x => x.UserId == userId && x.Type == UserSkillType.Offered, cancellationToken);

    public void Add(UserSkill userSkill) => dbContext.UserSkills.Add(userSkill);

    public void Remove(UserSkill userSkill) => dbContext.UserSkills.Remove(userSkill);
}

internal static class PagingExtensions
{
    public static async Task<PagedList<T>> ToPageAsync<T>(this IQueryable<T> query, PageRequest page, CancellationToken cancellationToken)
    {
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query.Skip(page.Skip).Take(page.PageSize).ToListAsync(cancellationToken);
        return new PagedList<T>(items, page.Page, page.PageSize, totalCount);
    }
}
