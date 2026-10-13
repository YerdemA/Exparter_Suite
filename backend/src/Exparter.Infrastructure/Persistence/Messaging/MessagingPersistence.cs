using Exparter.Application.Common;
using Exparter.Application.Messaging;
using Exparter.Domain.Messaging;
using Exparter.Infrastructure.Persistence.Skills;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Exparter.Infrastructure.Persistence.Messaging;

internal static class MessagingSchema
{
    public const string Name = "messaging";
}

internal sealed class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        builder.ToTable("conversations", MessagingSchema.Name);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Ignore(x => x.IsClosed);

        // One conversation per pair of users; the pair is stored in a fixed order.
        builder.HasIndex(x => new { x.UserLowId, x.UserHighId }).IsUnique().HasDatabaseName(UniqueConstraints.ConversationPerPair);
        builder.HasIndex(x => new { x.UserLowId, x.LastActivityAt });
        builder.HasIndex(x => new { x.UserHighId, x.LastActivityAt });
    }
}

internal sealed class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.ToTable("messages", MessagingSchema.Name);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Body).HasMaxLength(Message.BodyMaxLength);

        builder.HasIndex(x => new { x.ConversationId, x.SentAt, x.Id });

        // Stops a retried send from being stored twice even if two copies race.
        builder.HasIndex(x => new { x.ConversationId, x.SenderId, x.ClientMessageId })
            .IsUnique()
            .HasDatabaseName(UniqueConstraints.MessagePerClientId);
        builder.HasOne<Conversation>().WithMany().HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ConversationRepository(AppDbContext dbContext) : IConversationRepository
{
    public Task<Conversation?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Conversations.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<Conversation?> FindByUsersAsync(Guid firstUserId, Guid secondUserId, CancellationToken cancellationToken)
    {
        var (low, high) = Conversation.Order(firstUserId, secondUserId);
        return dbContext.Conversations.FirstOrDefaultAsync(x => x.UserLowId == low && x.UserHighId == high, cancellationToken);
    }

    public Task<PagedList<Conversation>> ListAsync(Guid userId, PageRequest page, CancellationToken cancellationToken) =>
        dbContext.Conversations.AsNoTracking()
            .Where(x => x.UserLowId == userId || x.UserHighId == userId)
            .OrderByDescending(x => x.LastActivityAt)
            .ThenByDescending(x => x.Id)
            .ToPageAsync(page, cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, Guid>> FindIdsByOtherUserAsync(
        Guid userId,
        IReadOnlyCollection<Guid> otherUserIds,
        CancellationToken cancellationToken)
    {
        var found = await dbContext.Conversations.AsNoTracking()
            .Where(x => (x.UserLowId == userId && otherUserIds.Contains(x.UserHighId))
                || (x.UserHighId == userId && otherUserIds.Contains(x.UserLowId)))
            .Select(x => new { x.Id, x.UserLowId, x.UserHighId })
            .ToListAsync(cancellationToken);
        return found.ToDictionary(x => x.UserLowId == userId ? x.UserHighId : x.UserLowId, x => x.Id);
    }

    public async Task<IReadOnlyDictionary<Guid, int>> CountUnreadAsync(
        Guid userId,
        IReadOnlyCollection<Guid> conversationIds,
        CancellationToken cancellationToken)
    {
        var counts = await dbContext.Messages.AsNoTracking()
            .Where(message => conversationIds.Contains(message.ConversationId) && message.SenderId != userId)
            .Join(
                dbContext.Conversations.AsNoTracking(),
                message => message.ConversationId,
                conversation => conversation.Id,
                (message, conversation) => new
                {
                    message.ConversationId,
                    message.SentAt,
                    LastReadAt = conversation.UserLowId == userId ? conversation.UserLowLastReadAt : conversation.UserHighLastReadAt,
                })
            .Where(x => x.LastReadAt == null || x.SentAt > x.LastReadAt)
            .GroupBy(x => x.ConversationId)
            .Select(group => new { ConversationId = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);
        return counts.ToDictionary(x => x.ConversationId, x => x.Count);
    }

    public async Task<IReadOnlyList<Message>> ListMessagesByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        await dbContext.Messages.AsNoTracking().Where(x => ids.Contains(x.Id)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Message>> ListMessagesAsync(
        Guid conversationId,
        MessagePosition? before,
        int limit,
        CancellationToken cancellationToken)
    {
        var messages = dbContext.Messages.AsNoTracking().Where(x => x.ConversationId == conversationId);
        if (before is not null)
        {
            // Two messages can share a timestamp; the id breaks the tie the same way the ordering does.
            messages = messages.Where(x => x.SentAt < before.SentAt || (x.SentAt == before.SentAt && x.Id.CompareTo(before.Id) < 0));
        }

        return await messages.OrderByDescending(x => x.SentAt).ThenByDescending(x => x.Id).Take(limit).ToListAsync(cancellationToken);
    }

    public Task<Message?> FindMessageByClientIdAsync(
        Guid conversationId,
        Guid senderId,
        Guid clientMessageId,
        CancellationToken cancellationToken) =>
        dbContext.Messages.AsNoTracking().FirstOrDefaultAsync(
            x => x.ConversationId == conversationId && x.SenderId == senderId && x.ClientMessageId == clientMessageId,
            cancellationToken);

    public void Add(Conversation conversation) => dbContext.Conversations.Add(conversation);

    public void Add(Message message) => dbContext.Messages.Add(message);
}
