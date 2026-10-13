using Exparter.Application.Common;
using Exparter.Application.Notifications;
using Exparter.Domain.Notifications;
using Exparter.Domain.Sessions;
using Exparter.Infrastructure.Persistence.Skills;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Exparter.Infrastructure.Persistence.Notifications;

internal static class NotificationSchema
{
    public const string Name = "notifications";
}

internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications", NotificationSchema.Name);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Type).HasConversion<string>().HasMaxLength(48);
        builder.Property(x => x.Reason).HasMaxLength(SwapSession.CancelReasonMaxLength);
        builder.Ignore(x => x.Data);

        // The user and the resources are plain values: tables of different modules are
        // not linked by foreign keys (see ADR 0006).
        builder.HasIndex(x => new { x.UserId, x.CreatedAt });
        builder.HasIndex(x => x.CreatedAt);
    }
}

internal sealed class DeviceConfiguration : IEntityTypeConfiguration<Device>
{
    public void Configure(EntityTypeBuilder<Device> builder)
    {
        builder.ToTable("devices", NotificationSchema.Name);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.FcmToken).HasMaxLength(Device.TokenMaxLength);
        builder.Property(x => x.Platform).HasConversion<string>().HasMaxLength(16);

        builder.HasIndex(x => x.FcmToken).IsUnique().HasDatabaseName(UniqueConstraints.DevicePerToken);
        builder.HasIndex(x => x.UserId);
    }
}

internal sealed class NotificationRepository(AppDbContext dbContext) : INotificationRepository
{
    public Task<PagedList<Notification>> ListAsync(Guid userId, bool unreadOnly, PageRequest page, CancellationToken cancellationToken) =>
        dbContext.Notifications.AsNoTracking()
            .Where(x => x.UserId == userId && (!unreadOnly || x.ReadAt == null))
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .ToPageAsync(page, cancellationToken);

    public Task<int> CountUnreadAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.Notifications.CountAsync(x => x.UserId == userId && x.ReadAt == null, cancellationToken);

    public Task<Notification?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Notifications.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task MarkAllReadAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken) =>
        dbContext.Notifications
            .Where(x => x.UserId == userId && x.ReadAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.ReadAt, now), cancellationToken);

    public Task<int> DeleteCreatedBeforeAsync(DateTimeOffset moment, CancellationToken cancellationToken) =>
        dbContext.Notifications.Where(x => x.CreatedAt < moment).ExecuteDeleteAsync(cancellationToken);

    public void Add(Notification notification) => dbContext.Notifications.Add(notification);
}

internal sealed class DeviceRepository(AppDbContext dbContext) : IDeviceRepository
{
    public Task<Device?> FindByTokenAsync(string fcmToken, CancellationToken cancellationToken) =>
        dbContext.Devices.FirstOrDefaultAsync(x => x.FcmToken == fcmToken, cancellationToken);

    public Task<Device?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Devices.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public void Add(Device device) => dbContext.Devices.Add(device);

    public void Remove(Device device) => dbContext.Devices.Remove(device);
}
