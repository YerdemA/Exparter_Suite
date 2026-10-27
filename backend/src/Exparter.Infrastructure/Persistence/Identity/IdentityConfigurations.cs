using Exparter.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Exparter.Infrastructure.Persistence.Identity;

internal static class IdentitySchema
{
    public const string Name = "identity";

    // SHA-256 as lowercase hex.
    public const int TokenHashLength = 64;
}

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users", IdentitySchema.Name);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Email).HasMaxLength(254);
        builder.Property(x => x.Username).HasMaxLength(Username.MaxLength);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.Role).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.TermsVersion).HasMaxLength(32);
        builder.HasIndex(x => x.Email).IsUnique().HasDatabaseName(UniqueConstraints.UserEmail);
        builder.HasIndex(x => x.Username).IsUnique().HasDatabaseName(UniqueConstraints.UserUsername);

        // The erasure job looks for due deletions every minute; only the few accounts
        // waiting to be deleted belong in the index.
        builder.HasIndex(x => x.DeletionRequestedAt).HasFilter("status = 'PendingDeletion'");
    }
}

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens", IdentitySchema.Name);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.TokenHash).HasMaxLength(IdentitySchema.TokenHashLength).IsFixedLength();

        // Two requests rotating the same token at once must not both succeed.
        builder.Property(x => x.RevokedAt).IsConcurrencyToken();

        builder.HasIndex(x => x.TokenHash).IsUnique();
        builder.HasIndex(x => x.FamilyId);
        builder.HasIndex(x => x.UserId);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class OneTimeTokenConfiguration : IEntityTypeConfiguration<OneTimeToken>
{
    public void Configure(EntityTypeBuilder<OneTimeToken> builder)
    {
        builder.ToTable("one_time_tokens", IdentitySchema.Name);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Purpose).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.TokenHash).HasMaxLength(IdentitySchema.TokenHashLength).IsFixedLength();
        builder.HasIndex(x => x.TokenHash).IsUnique();
        builder.HasIndex(x => new { x.UserId, x.Purpose });
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class BlockedEmailConfiguration : IEntityTypeConfiguration<BlockedEmail>
{
    public void Configure(EntityTypeBuilder<BlockedEmail> builder)
    {
        builder.ToTable("blocked_emails", IdentitySchema.Name);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.EmailHash).HasMaxLength(BlockedEmail.HashLength).IsFixedLength();
        builder.HasIndex(x => x.EmailHash);
    }
}
