using Exparter.Domain.Common;

namespace Exparter.Domain.Moderation;

public static class ModerationErrors
{
    public const string BlockSelfCode = "block_self";
    public const string ReportSelfCode = "report_self";
    public const string ReportAlreadyClosedCode = "report_already_closed";

    public static DomainException BlockSelf() => new(BlockSelfCode, "A user cannot block themselves.");

    public static DomainException ReportSelf() => new(ReportSelfCode, "A user cannot report themselves or what they wrote.");

    public static DomainException ReportAlreadyClosed() => new(ReportAlreadyClosedCode, "The report has already been closed.");
}

/// <summary>
/// One user no longer wants anything to do with another. It is recorded one way, but it
/// separates both: neither finds nor contacts the other while it stands.
/// </summary>
public sealed class Block
{
    private Block()
    {
    }

    public Guid BlockerId { get; private set; }
    public Guid BlockedId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static Block Create(Guid blockerId, Guid blockedId, DateTimeOffset now) =>
        blockerId == blockedId
            ? throw ModerationErrors.BlockSelf()
            : new Block { BlockerId = blockerId, BlockedId = blockedId, CreatedAt = now };
}

public enum ReportTargetType
{
    User,
    Message,
    Review,
}

public enum ReportReason
{
    Spam,
    Harassment,
    InappropriateContent,
    FakeProfile,
    Other,
}

public enum ReportStatus
{
    Open,
    Resolved,
    Dismissed,
}

/// <summary>A user tells the administrators that someone, or something someone wrote, breaks the rules.</summary>
public sealed class Report
{
    public const int DescriptionMaxLength = 1000;

    private Report()
    {
    }

    public Guid Id { get; private set; }
    public Guid ReporterId { get; private set; }
    public ReportTargetType TargetType { get; private set; }
    public Guid TargetId { get; private set; }

    /// <summary>The reported user, or the author of the reported message or review.</summary>
    public Guid TargetUserId { get; private set; }

    /// <summary>
    /// The reported text as it was when reported, so that the administrators judge what
    /// the reporter saw. Null for a report about a user.
    /// </summary>
    public string? TargetContent { get; private set; }

    public ReportReason Reason { get; private set; }
    public string? Description { get; private set; }
    public ReportStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ResolvedAt { get; private set; }
    public Guid? ResolvedByUserId { get; private set; }

    public static Report Create(
        Guid reporterId,
        ReportTargetType targetType,
        Guid targetId,
        Guid targetUserId,
        string? targetContent,
        ReportReason reason,
        string? description,
        DateTimeOffset now)
    {
        if (targetUserId == reporterId)
        {
            throw ModerationErrors.ReportSelf();
        }

        var trimmed = description?.Trim();
        return new Report
        {
            Id = Guid.CreateVersion7(),
            ReporterId = reporterId,
            TargetType = targetType,
            TargetId = targetId,
            TargetUserId = targetUserId,
            TargetContent = targetContent,
            Reason = reason,
            Description = string.IsNullOrEmpty(trimmed) ? null : trimmed,
            Status = ReportStatus.Open,
            CreatedAt = now,
        };
    }

    /// <param name="outcome"><see cref="ReportStatus.Resolved"/> if action was taken, otherwise <see cref="ReportStatus.Dismissed"/>.</param>
    public void Close(ReportStatus outcome, Guid adminId, DateTimeOffset now)
    {
        if (outcome == ReportStatus.Open)
        {
            throw new ArgumentOutOfRangeException(nameof(outcome), "A report is closed as resolved or dismissed.");
        }

        if (Status != ReportStatus.Open)
        {
            throw ModerationErrors.ReportAlreadyClosed();
        }

        Status = outcome;
        ResolvedAt = now;
        ResolvedByUserId = adminId;
    }
}
