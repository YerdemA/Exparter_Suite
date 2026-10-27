using Exparter.Application.Common;
using Exparter.Application.Moderation;
using Exparter.Domain.Moderation;
using Exparter.Infrastructure.Persistence.Skills;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Exparter.Infrastructure.Persistence.Moderation;

internal static class ModerationSchema
{
    public const string Name = "moderation";
}

internal sealed class BlockConfiguration : IEntityTypeConfiguration<Block>
{
    public void Configure(EntityTypeBuilder<Block> builder)
    {
        builder.ToTable("blocks", ModerationSchema.Name);

        // The users are plain values: tables of different modules are not linked by
        // foreign keys (see ADR 0006).
        builder.HasKey(x => new { x.BlockerId, x.BlockedId }).HasName(UniqueConstraints.BlockPerPair);
        builder.HasIndex(x => x.BlockedId);
    }
}

internal sealed class ReportConfiguration : IEntityTypeConfiguration<Report>
{
    public void Configure(EntityTypeBuilder<Report> builder)
    {
        builder.ToTable("reports", ModerationSchema.Name);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.TargetType).HasConversion<string>().HasMaxLength(16);
        builder.Property(x => x.Reason).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        builder.Property(x => x.Description).HasMaxLength(Report.DescriptionMaxLength);

        // One open report per reporter and target; once it is closed, a new one may follow.
        builder.HasIndex(x => new { x.ReporterId, x.TargetType, x.TargetId })
            .IsUnique()
            .HasFilter("status = 'Open'")
            .HasDatabaseName(UniqueConstraints.OpenReportPerTarget);
        builder.HasIndex(x => new { x.Status, x.CreatedAt });
    }
}

internal sealed class BlockRepository(AppDbContext dbContext) : IBlockRepository
{
    public Task<Block?> FindAsync(Guid blockerId, Guid blockedId, CancellationToken cancellationToken) =>
        dbContext.Blocks.FirstOrDefaultAsync(x => x.BlockerId == blockerId && x.BlockedId == blockedId, cancellationToken);

    public async Task<IReadOnlyList<Block>> ListBetweenAsync(Guid firstUserId, Guid secondUserId, CancellationToken cancellationToken) =>
        await dbContext.Blocks
            .AsNoTracking()
            .Where(x => (x.BlockerId == firstUserId && x.BlockedId == secondUserId)
                || (x.BlockerId == secondUserId && x.BlockedId == firstUserId))
            .ToListAsync(cancellationToken);

    public Task<PagedList<Block>> ListByBlockerAsync(Guid blockerId, PageRequest page, CancellationToken cancellationToken) =>
        dbContext.Blocks
            .AsNoTracking()
            .Where(x => x.BlockerId == blockerId)
            .OrderByDescending(x => x.CreatedAt)
            .ThenBy(x => x.BlockedId)
            .ToPageAsync(page, cancellationToken);

    public void Add(Block block) => dbContext.Blocks.Add(block);

    public void Remove(Block block) => dbContext.Blocks.Remove(block);
}

internal sealed class ReportRepository(AppDbContext dbContext) : IReportRepository
{
    public Task<Report?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Reports.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<bool> HasOpenAsync(Guid reporterId, ReportTargetType targetType, Guid targetId, CancellationToken cancellationToken) =>
        dbContext.Reports.AnyAsync(
            x => x.ReporterId == reporterId && x.TargetType == targetType && x.TargetId == targetId && x.Status == ReportStatus.Open,
            cancellationToken);

    public Task<PagedList<Report>> ListAsync(ReportStatus? status, PageRequest page, CancellationToken cancellationToken) =>
        dbContext.Reports
            .AsNoTracking()
            .Where(x => status == null || x.Status == status)
            .OrderBy(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .ToPageAsync(page, cancellationToken);

    public Task<int> DeleteClosedBeforeAsync(DateTimeOffset moment, CancellationToken cancellationToken) =>
        dbContext.Reports
            .Where(x => x.Status != ReportStatus.Open && x.ResolvedAt < moment)
            .ExecuteDeleteAsync(cancellationToken);

    public void Add(Report report) => dbContext.Reports.Add(report);
}
