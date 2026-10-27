using Exparter.Application.Common;
using Exparter.Application.Identity;
using Exparter.Application.Messaging;
using Exparter.Application.Notifications;
using Exparter.Application.Reviews;
using Exparter.Domain.Moderation;
using Exparter.Domain.Notifications;
using FluentValidation;
using Mediator;

namespace Exparter.Application.Moderation;

public interface IBlockRepository
{
    Task<Block?> FindAsync(Guid blockerId, Guid blockedId, CancellationToken cancellationToken);

    /// <summary>The blocks between the two users, in either direction.</summary>
    Task<IReadOnlyList<Block>> ListBetweenAsync(Guid firstUserId, Guid secondUserId, CancellationToken cancellationToken);

    /// <summary>Newest first.</summary>
    Task<PagedList<Block>> ListByBlockerAsync(Guid blockerId, PageRequest page, CancellationToken cancellationToken);

    void Add(Block block);

    void Remove(Block block);
}

public interface IReportRepository
{
    Task<Report?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> HasOpenAsync(Guid reporterId, ReportTargetType targetType, Guid targetId, CancellationToken cancellationToken);

    /// <summary>Oldest first: the administrators work through them in order.</summary>
    Task<PagedList<Report>> ListAsync(ReportStatus? status, PageRequest page, CancellationToken cancellationToken);

    /// <returns>How many reports were removed.</returns>
    Task<int> DeleteClosedBeforeAsync(DateTimeOffset moment, CancellationToken cancellationToken);

    void Add(Report report);
}

/// <summary>How a user stands towards another as far as blocks go.</summary>
public enum BlockRelation
{
    None,

    /// <summary>The user blocked the other.</summary>
    Blocking,

    /// <summary>The other user blocked the user.</summary>
    BlockedBy,
}

/// <summary>What other modules may know about blocks.</summary>
public interface IBlockDirectory
{
    Task<BlockRelation> GetRelationAsync(Guid userId, Guid otherUserId, CancellationToken cancellationToken);
}

internal sealed class BlockDirectory(IBlockRepository blocks) : IBlockDirectory
{
    public async Task<BlockRelation> GetRelationAsync(Guid userId, Guid otherUserId, CancellationToken cancellationToken)
    {
        var between = await blocks.ListBetweenAsync(userId, otherUserId, cancellationToken);
        return between.Any(block => block.BlockerId == userId) ? BlockRelation.Blocking
            : between.Count > 0 ? BlockRelation.BlockedBy
            : BlockRelation.None;
    }
}

public static class BlockDirectoryExtensions
{
    /// <summary>
    /// Stops <paramref name="userId"/> from reaching <paramref name="otherUserId"/> across a
    /// block. The blocker is told why; to the blocked user the other simply does not exist.
    /// </summary>
    public static async Task EnsureNotBlockedAsync(
        this IBlockDirectory blocks,
        Guid userId,
        Guid otherUserId,
        CancellationToken cancellationToken)
    {
        switch (await blocks.GetRelationAsync(userId, otherUserId, cancellationToken))
        {
            case BlockRelation.Blocking:
                throw AppException.Conflict("user_blocked", "The user is blocked; unblock them first.");
            case BlockRelation.BlockedBy:
                throw AppException.NotFound("The user was not found.");
        }
    }
}

/// <summary>
/// Published in the transaction of a block, so that other modules undo what the two
/// users share together with it.
/// </summary>
public sealed record UserBlocked(Guid BlockerId, Guid BlockedId) : INotification;

public sealed record BlockDto(UserSummaryDto User, DateTimeOffset CreatedAt);

public sealed record BlockUserCommand(Guid UserId, Guid BlockedUserId) : ICommand<BlockDto>;

public sealed class BlockUserCommandValidator : AbstractValidator<BlockUserCommand>
{
    public BlockUserCommandValidator() => RuleFor(x => x.BlockedUserId).NotEmpty();
}

public sealed class BlockUserCommandHandler(
    IUserDirectory users,
    IUserSummaries summaries,
    IBlockRepository blocks,
    IPublisher publisher,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<BlockUserCommand, BlockDto>
{
    public async ValueTask<BlockDto> Handle(BlockUserCommand command, CancellationToken cancellationToken)
    {
        // Anyone can block, whatever the state of their own account: it is a way to be left alone.
        if (command.BlockedUserId != command.UserId
            && await users.FindAsync(command.BlockedUserId, cancellationToken) is null or { IsDeleted: true })
        {
            throw AppException.NotFound("The user was not found.");
        }

        if (await blocks.FindAsync(command.UserId, command.BlockedUserId, cancellationToken) is not null)
        {
            throw AppException.Conflict("already_blocked", "The user is already blocked.");
        }

        var block = Block.Create(command.UserId, command.BlockedUserId, timeProvider.GetUtcNow());
        blocks.Add(block);
        await publisher.Publish(new UserBlocked(block.BlockerId, block.BlockedId), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new BlockDto((await summaries.GetAsync([block.BlockedId], cancellationToken))[block.BlockedId], block.CreatedAt);
    }
}

/// <summary>Lifting a block lets the two find each other again; what it closed stays closed.</summary>
public sealed record UnblockUserCommand(Guid UserId, Guid BlockedUserId) : ICommand;

public sealed class UnblockUserCommandHandler(IBlockRepository blocks, IUnitOfWork unitOfWork)
    : ICommandHandler<UnblockUserCommand>
{
    public async ValueTask<Unit> Handle(UnblockUserCommand command, CancellationToken cancellationToken)
    {
        var block = await blocks.FindAsync(command.UserId, command.BlockedUserId, cancellationToken)
            ?? throw AppException.NotFound("The block was not found.");
        blocks.Remove(block);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public sealed record ListBlocksQuery(Guid UserId, int? Page, int? PageSize) : IQuery<PagedList<BlockDto>>;

public sealed class ListBlocksQueryHandler(IBlockRepository blocks, IUserSummaries summaries)
    : IQueryHandler<ListBlocksQuery, PagedList<BlockDto>>
{
    public async ValueTask<PagedList<BlockDto>> Handle(ListBlocksQuery query, CancellationToken cancellationToken)
    {
        var page = await blocks.ListByBlockerAsync(query.UserId, PageRequest.From(query.Page, query.PageSize), cancellationToken);
        var users = await summaries.GetAsync(page.Items.Select(block => block.BlockedId).ToList(), cancellationToken);
        return page.Map(block => new BlockDto(users[block.BlockedId], block.CreatedAt));
    }
}

public sealed record ReportDto(
    Guid Id,
    ReportTargetType TargetType,
    Guid TargetId,
    ReportReason Reason,
    string? Description,
    ReportStatus Status,
    DateTimeOffset CreatedAt)
{
    public static ReportDto From(Report report) =>
        new(report.Id, report.TargetType, report.TargetId, report.Reason, report.Description, report.Status, report.CreatedAt);
}

public sealed record AdminReportDto(
    Guid Id,
    ReportTargetType TargetType,
    Guid TargetId,
    ReportReason Reason,
    string? Description,
    ReportStatus Status,
    DateTimeOffset CreatedAt,
    Guid ReporterId,
    Guid TargetUserId,
    string? TargetContent,
    DateTimeOffset? ResolvedAt,
    Guid? ResolvedByUserId)
{
    public static AdminReportDto From(Report report) =>
        new(
            report.Id,
            report.TargetType,
            report.TargetId,
            report.Reason,
            report.Description,
            report.Status,
            report.CreatedAt,
            report.ReporterId,
            report.TargetUserId,
            report.TargetContent,
            report.ResolvedAt,
            report.ResolvedByUserId);
}

public sealed record CreateReportCommand(
    Guid UserId,
    ReportTargetType TargetType,
    Guid TargetId,
    ReportReason Reason,
    string? Description) : ICommand<ReportDto>;

public sealed class CreateReportCommandValidator : AbstractValidator<CreateReportCommand>
{
    public CreateReportCommandValidator()
    {
        RuleFor(x => x.TargetType).IsInEnum();
        RuleFor(x => x.TargetId).NotEmpty();
        RuleFor(x => x.Reason).IsInEnum();
        RuleFor(x => x.Description).MaximumLength(Report.DescriptionMaxLength);

        // "Other" says nothing by itself.
        RuleFor(x => x.Description)
            .Must(description => !string.IsNullOrWhiteSpace(description))
            .When(x => x.Reason == ReportReason.Other)
            .WithMessage("A report for another reason needs a description.");
    }
}

public sealed class CreateReportCommandHandler(
    IUserDirectory users,
    IConversationDirectory conversations,
    IReviewDirectory reviews,
    IReportRepository reports,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<CreateReportCommand, ReportDto>
{
    public async ValueTask<ReportDto> Handle(CreateReportCommand command, CancellationToken cancellationToken)
    {
        if (await users.FindAsync(command.UserId, cancellationToken) is not { IsEmailVerified: true })
        {
            throw AppException.Forbidden("email_not_verified", "A verified email is required.");
        }

        var (targetUserId, content) = await ResolveTargetAsync(command, cancellationToken)
            ?? throw AppException.NotFound("The reported content was not found.");

        if (await reports.HasOpenAsync(command.UserId, command.TargetType, command.TargetId, cancellationToken))
        {
            throw AppException.Conflict("report_already_open", "This was already reported and is waiting to be reviewed.");
        }

        var report = Report.Create(
            command.UserId,
            command.TargetType,
            command.TargetId,
            targetUserId,
            content,
            command.Reason,
            command.Description,
            timeProvider.GetUtcNow());
        reports.Add(report);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ReportDto.From(report);
    }

    /// <returns>Who the target belongs to and its text; null if the reporter cannot see it.</returns>
    private async Task<(Guid UserId, string? Content)?> ResolveTargetAsync(CreateReportCommand command, CancellationToken cancellationToken)
    {
        switch (command.TargetType)
        {
            case ReportTargetType.User:
                return await users.FindAsync(command.TargetId, cancellationToken) is { IsDeleted: false } user
                    ? (user.Id, null)
                    : null;
            case ReportTargetType.Message:
                // Only the two people in the conversation have seen the message.
                return await conversations.FindMessageAsync(command.TargetId, command.UserId, cancellationToken) is { } message
                    ? (message.SenderId, message.Body)
                    : null;
            default:
                return await reviews.FindAsync(command.TargetId, cancellationToken) is { } review
                    ? (review.ReviewerId, review.Comment is null ? $"{review.Rating}/10" : $"{review.Rating}/10 {review.Comment}")
                    : null;
        }
    }
}

public sealed record AdminListReportsQuery(ReportStatus? Status, int? Page, int? PageSize) : IQuery<PagedList<AdminReportDto>>;

public sealed class AdminListReportsQueryHandler(IReportRepository reports)
    : IQueryHandler<AdminListReportsQuery, PagedList<AdminReportDto>>
{
    public async ValueTask<PagedList<AdminReportDto>> Handle(AdminListReportsQuery query, CancellationToken cancellationToken) =>
        (await reports.ListAsync(query.Status, PageRequest.From(query.Page, query.PageSize), cancellationToken))
        .Map(AdminReportDto.From);
}

/// <param name="Outcome"><see cref="ReportStatus.Resolved"/> or <see cref="ReportStatus.Dismissed"/>.</param>
public sealed record CloseReportCommand(Guid AdminId, Guid ReportId, ReportStatus Outcome) : ICommand<AdminReportDto>;

public sealed class CloseReportCommandValidator : AbstractValidator<CloseReportCommand>
{
    public CloseReportCommandValidator() =>
        RuleFor(x => x.Outcome).Must(outcome => outcome is ReportStatus.Resolved or ReportStatus.Dismissed);
}

public sealed class CloseReportCommandHandler(
    IReportRepository reports,
    IUserNotifier notifier,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<CloseReportCommand, AdminReportDto>
{
    public async ValueTask<AdminReportDto> Handle(CloseReportCommand command, CancellationToken cancellationToken)
    {
        var report = await reports.GetByIdAsync(command.ReportId, cancellationToken)
            ?? throw AppException.NotFound("The report was not found.");
        report.Close(command.Outcome, command.AdminId, timeProvider.GetUtcNow());

        // The reporter learns that someone looked, not what was decided about another user.
        notifier.Notify(report.ReporterId, NotificationType.ReportReviewed, new NotificationData());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return AdminReportDto.From(report);
    }
}

/// <summary>Removes reports closed longer ago than the retention period.</summary>
public sealed record PurgeOldReportsCommand : ICommand<int>;

public sealed class PurgeOldReportsCommandHandler(IReportRepository reports, TimeProvider timeProvider)
    : ICommandHandler<PurgeOldReportsCommand, int>
{
    /// <returns>How many reports were removed.</returns>
    public async ValueTask<int> Handle(PurgeOldReportsCommand command, CancellationToken cancellationToken) =>
        await reports.DeleteClosedBeforeAsync(timeProvider.GetUtcNow() - Report.Retention, cancellationToken);
}
