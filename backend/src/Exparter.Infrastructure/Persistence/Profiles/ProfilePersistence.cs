using Exparter.Application.Profiles;
using Exparter.Domain.Profiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Exparter.Infrastructure.Persistence.Profiles;

internal sealed class ProfileConfiguration : IEntityTypeConfiguration<Profile>
{
    public const string Schema = "profiles";

    public void Configure(EntityTypeBuilder<Profile> builder)
    {
        builder.ToTable("profiles", Schema);

        // The user id is a plain value here: tables of different modules are not linked
        // by foreign keys (see ADR 0006).
        builder.HasKey(x => x.UserId);
        builder.Property(x => x.UserId).ValueGeneratedNever();
        builder.Property(x => x.DisplayName).HasMaxLength(Profile.DisplayNameMaxLength);
        builder.Property(x => x.Bio).HasMaxLength(Profile.BioMaxLength);
        builder.Property(x => x.AvatarKey).HasMaxLength(512);
        builder.Property(x => x.City).HasMaxLength(2).IsFixedLength();
    }
}

internal sealed class ProfileRepository(AppDbContext dbContext) : IProfileRepository
{
    public Task<Profile?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.Profiles.FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);

    public async Task<IReadOnlyList<Profile>> ListByUserIdsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken) =>
        await dbContext.Profiles.AsNoTracking().Where(x => userIds.Contains(x.UserId)).ToListAsync(cancellationToken);

    public void Add(Profile profile) => dbContext.Profiles.Add(profile);

    public void Remove(Profile profile) => dbContext.Profiles.Remove(profile);
}
