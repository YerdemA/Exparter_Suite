using Exparter.Application.Common;
using Exparter.Application.Reviews;
using Exparter.Domain.Reviews;
using Exparter.Infrastructure.Persistence.Skills;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Exparter.Infrastructure.Persistence.Reviews;

internal sealed class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public const string Schema = "reviews";

    public void Configure(EntityTypeBuilder<Review> builder)
    {
        builder.ToTable("reviews", Schema);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Comment).HasMaxLength(Review.CommentMaxLength);
        builder.Ignore(x => x.IsDeleted);

        // Deleted reviews are part of the index on purpose: a review that was taken back
        // still stands in the way of a second one.
        // The users and the session are plain values: tables of different modules are not
        // linked by foreign keys (see ADR 0006).
        builder.HasIndex(x => new { x.ReviewerId, x.RevieweeId }).IsUnique().HasDatabaseName(UniqueConstraints.ReviewPerPair);
        builder.HasIndex(x => new { x.RevieweeId, x.CreatedAt });
    }
}

internal sealed class ReviewRepository(AppDbContext dbContext) : IReviewRepository
{
    public Task<Review?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Reviews.FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null, cancellationToken);

    public Task<bool> ExistsAsync(Guid reviewerId, Guid revieweeId, CancellationToken cancellationToken) =>
        dbContext.Reviews.AnyAsync(x => x.ReviewerId == reviewerId && x.RevieweeId == revieweeId, cancellationToken);

    public Task<PagedList<Review>> ListReceivedAsync(Guid revieweeId, PageRequest page, CancellationToken cancellationToken) =>
        Newest(Visible.Where(x => x.RevieweeId == revieweeId)).ToPageAsync(page, cancellationToken);

    public Task<PagedList<Review>> ListGivenAsync(Guid reviewerId, PageRequest page, CancellationToken cancellationToken) =>
        Newest(Visible.Where(x => x.ReviewerId == reviewerId)).ToPageAsync(page, cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, RatingSummaryDto>> GetRatingsAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken) =>
        await Visible
            .Where(x => userIds.Contains(x.RevieweeId))
            .GroupBy(x => x.RevieweeId)
            .Select(group => new { UserId = group.Key, Count = group.Count(), Average = group.Average(x => x.Rating) })
            .ToDictionaryAsync(x => x.UserId, x => new RatingSummaryDto(x.Count, x.Average), cancellationToken);

    public Task DeleteReceivedAsync(Guid revieweeId, CancellationToken cancellationToken) =>
        dbContext.Reviews.Where(x => x.RevieweeId == revieweeId).ExecuteDeleteAsync(cancellationToken);

    public void Add(Review review) => dbContext.Reviews.Add(review);

    private IQueryable<Review> Visible => dbContext.Reviews.AsNoTracking().Where(x => x.DeletedAt == null);

    private static IQueryable<Review> Newest(IQueryable<Review> reviews) =>
        reviews.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id);
}
