using Exparter.Application.Common;
using Exparter.Domain.Identity;
using Exparter.Domain.Profiles;
using Exparter.Domain.Skills;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Exparter.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IUnitOfWork
{
    public DbSet<User> Users => Set<User>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<OneTimeToken> OneTimeTokens => Set<OneTimeToken>();

    public DbSet<Profile> Profiles => Set<Profile>();

    public DbSet<SkillCategory> SkillCategories => Set<SkillCategory>();

    public DbSet<Skill> Skills => Set<Skill>();

    public DbSet<UserSkill> UserSkills => Set<UserSkill>();

    async Task IUnitOfWork.SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw AppException.Conflict("concurrent_update", "The resource was changed by another request.");
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation
        } postgresException)
        {
            // Handlers check uniqueness first; this only catches two requests racing past that check.
            var conflict = UniqueConstraints.ToAppException(postgresException.ConstraintName);
            if (conflict is null)
            {
                throw;
            }

            throw conflict;
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
}
