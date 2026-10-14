using Exparter.Application.Common;
using Exparter.Application.Identity;
using Exparter.Application.Matching;
using Exparter.Application.Messaging;
using Exparter.Application.Moderation;
using Exparter.Application.Tests.Identity;
using Exparter.Application.Tests.Matching;
using Exparter.Application.Tests.Messaging;
using Exparter.Application.Tests.Profiles;
using Exparter.Domain.Common;
using Exparter.Domain.Identity;
using Exparter.Domain.Matching;
using Exparter.Domain.Messaging;
using Exparter.Domain.Moderation;
using Exparter.Domain.Notifications;

namespace Exparter.Application.Tests.Moderation;

internal sealed class InMemoryBlocks : IBlockRepository
{
    public List<Block> Items { get; } = [];

    public Task<Block?> FindAsync(Guid blockerId, Guid blockedId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(x => x.BlockerId == blockerId && x.BlockedId == blockedId));

    public Task<IReadOnlyList<Block>> ListBetweenAsync(Guid firstUserId, Guid secondUserId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Block>>(Items
            .Where(x => (x.BlockerId == firstUserId && x.BlockedId == secondUserId) || (x.BlockerId == secondUserId && x.BlockedId == firstUserId))
            .ToList());

    public Task<PagedList<Block>> ListByBlockerAsync(Guid blockerId, PageRequest page, CancellationToken cancellationToken)
    {
        var all = Items.Where(x => x.BlockerId == blockerId).OrderByDescending(x => x.CreatedAt).ToList();
        return Task.FromResult(new PagedList<Block>(all, page.Page, page.PageSize, all.Count));
    }

    public void Add(Block block) => Items.Add(block);

    public void Remove(Block block) => Items.Remove(block);
}

internal sealed class InMemoryReports : IReportRepository
{
    public List<Report> Items { get; } = [];

    public Task<Report?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(x => x.Id == id));

    public Task<bool> HasOpenAsync(Guid reporterId, ReportTargetType targetType, Guid targetId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.Any(x =>
            x.ReporterId == reporterId && x.TargetType == targetType && x.TargetId == targetId && x.Status == ReportStatus.Open));

    public Task<PagedList<Report>> ListAsync(ReportStatus? status, PageRequest page, CancellationToken cancellationToken)
    {
        var all = Items.Where(x => status is null || x.Status == status).OrderBy(x => x.CreatedAt).ToList();
        return Task.FromResult(new PagedList<Report>(all, page.Page, page.PageSize, all.Count));
    }

    public void Add(Report report) => Items.Add(report);
}

internal sealed class RecordingConnections : IRealtimeConnections
{
    public List<Guid> Closed { get; } = [];

    public void CloseAll(Guid userId) => Closed.Add(userId);
}

public class ModerationHandlerTests
{
    private readonly Guid _ali = Guid.NewGuid();
    private readonly Guid _ayse = Guid.NewGuid();
    private readonly FakeUserDirectory _users = new();
    private readonly InMemoryBlocks _blocks = new();
    private readonly InMemoryReports _reports = new();
    private readonly InMemoryConversations _conversations = new();
    private readonly FakeReviewDirectory _reviews = new();
    private readonly RecordingNotifier _notifier = new();
    private readonly Identity.RecordingPublisher _publisher = new();
    private readonly CountingUnitOfWork _unitOfWork = new();
    private readonly TestClock _clock = new();

    public ModerationHandlerTests()
    {
        _users.Users[_ali] = new UserInfo(_ali, "ali", IsActive: true, IsEmailVerified: true);
        _users.Users[_ayse] = new UserInfo(_ayse, "ayse", IsActive: true, IsEmailVerified: true);
    }

    private static async Task<string> CodeOfAsync(Func<Task> action) => (await Assert.ThrowsAsync<AppException>(action)).Code;

    private Task<BlockDto> BlockAsync(Guid userId, Guid blockedUserId) =>
        new BlockUserCommandHandler(_users, new FakeUserSummaries(_users), _blocks, _publisher, _unitOfWork, _clock)
            .Handle(new BlockUserCommand(userId, blockedUserId), CancellationToken.None).AsTask();

    private Task<ReportDto> ReportAsync(
        ReportTargetType type,
        Guid targetId,
        ReportReason reason = ReportReason.Spam,
        string? description = null,
        Guid? reporter = null) =>
        new CreateReportCommandHandler(_users, new ConversationDirectory(_conversations), _reviews, _reports, _unitOfWork, _clock)
            .Handle(new CreateReportCommand(reporter ?? _ali, type, targetId, reason, description), CancellationToken.None).AsTask();

    [Fact]
    public async Task Blocking_is_recorded_once_and_announced_to_the_other_modules()
    {
        var block = await BlockAsync(_ali, _ayse);

        Assert.Equal("ayse", block.User.Username);
        Assert.Equal(new UserBlocked(_ali, _ayse), Assert.Single(_publisher.Published));
        Assert.Equal(1, _unitOfWork.SaveCount);
        Assert.Equal("already_blocked", await CodeOfAsync(() => BlockAsync(_ali, _ayse)));
        Assert.Equal("not_found", await CodeOfAsync(() => BlockAsync(_ali, Guid.NewGuid())));
        await Assert.ThrowsAsync<DomainException>(() => BlockAsync(_ali, _ali));

        // The other direction is a block of its own.
        await BlockAsync(_ayse, _ali);
        Assert.Equal(2, _blocks.Items.Count);
    }

    [Fact]
    public async Task Blocks_are_listed_and_lifted_by_the_blocker_only()
    {
        await BlockAsync(_ali, _ayse);
        var list = await new ListBlocksQueryHandler(_blocks, new FakeUserSummaries(_users))
            .Handle(new ListBlocksQuery(_ali, null, null), CancellationToken.None);
        Assert.Equal(_ayse, Assert.Single(list.Items).User.UserId);

        var unblock = new UnblockUserCommandHandler(_blocks, _unitOfWork);
        Assert.Equal("not_found", await CodeOfAsync(() => unblock.Handle(new UnblockUserCommand(_ayse, _ali), CancellationToken.None).AsTask()));
        await unblock.Handle(new UnblockUserCommand(_ali, _ayse), CancellationToken.None);
        Assert.Empty(_blocks.Items);
    }

    [Fact]
    public async Task Across_a_block_the_blocker_is_told_why_and_the_blocked_user_finds_nobody()
    {
        await BlockAsync(_ali, _ayse);
        var directory = new BlockDirectory(_blocks);

        Assert.Equal(BlockRelation.Blocking, await directory.GetRelationAsync(_ali, _ayse, CancellationToken.None));
        Assert.Equal(BlockRelation.BlockedBy, await directory.GetRelationAsync(_ayse, _ali, CancellationToken.None));
        Assert.Equal("user_blocked", await CodeOfAsync(() => directory.EnsureNotBlockedAsync(_ali, _ayse, CancellationToken.None)));
        Assert.Equal("not_found", await CodeOfAsync(() => directory.EnsureNotBlockedAsync(_ayse, _ali, CancellationToken.None)));
        await directory.EnsureNotBlockedAsync(_ali, Guid.NewGuid(), CancellationToken.None);
    }

    [Fact]
    public async Task A_block_withdraws_the_pending_request_ends_the_match_and_closes_the_conversation_silently()
    {
        var requests = new InMemorySwapRequests();
        var matches = new InMemoryMatches();
        var now = _clock.GetUtcNow();
        var request = SwapRequest.Send(_ayse, _ali, Guid.NewGuid(), Guid.NewGuid(), null, null, now);
        requests.Add(request);
        var match = SwapRequest.Send(_ali, _ayse, Guid.NewGuid(), Guid.NewGuid(), null, null, now).Accept(_ayse, now);
        matches.Add(match);
        var conversation = Conversation.Open(_ali, _ayse, now);
        _conversations.Add(conversation);
        var publisher = new Matching.RecordingPublisher();

        await new CloseMatchingOnBlock(requests, matches, publisher, _clock).Handle(new UserBlocked(_ali, _ayse), CancellationToken.None);
        await new CloseConversationOnBlock(_conversations, _clock).Handle(new UserBlocked(_ali, _ayse), CancellationToken.None);

        Assert.Equal(SwapRequestStatus.Cancelled, request.Status);
        Assert.Equal((MatchStatus.Ended, MatchEndReason.Blocked, _ali), (match.Status, match.EndReason!.Value, match.EndedByUserId!.Value));
        Assert.Contains(publisher.Published, published => published is MatchEnded ended && ended.MatchId == match.Id);
        Assert.True(conversation.IsClosed);
        Assert.Empty(_notifier.Sent);
    }

    [Fact]
    public async Task Users_messages_and_reviews_are_reported_with_what_they_said()
    {
        var conversation = Conversation.Open(_ali, _ayse, _clock.GetUtcNow());
        _conversations.Add(conversation);
        var message = conversation.Post(_ayse, "Kaba bir mesaj", null, _clock.GetUtcNow());
        _conversations.Add(message);
        var reviewId = Guid.NewGuid();
        _reviews.Reviews[reviewId] = new Application.Reviews.ReviewInfo(reviewId, _ayse, 2, "Berbat biri.");

        var aboutUser = await ReportAsync(ReportTargetType.User, _ayse, ReportReason.FakeProfile);
        await ReportAsync(ReportTargetType.Message, message.Id, ReportReason.Harassment);
        await ReportAsync(ReportTargetType.Review, reviewId, ReportReason.Other, "Hakaret içeriyor.");

        Assert.Equal((ReportStatus.Open, ReportReason.FakeProfile), (aboutUser.Status, aboutUser.Reason));
        Assert.All(_reports.Items, report => Assert.Equal(_ayse, report.TargetUserId));
        Assert.Equal([null, "Kaba bir mesaj", "2/10 Berbat biri."], _reports.Items.Select(report => report.TargetContent));
    }

    [Fact]
    public async Task What_the_reporter_cannot_see_or_wrote_themselves_is_not_reported()
    {
        var conversation = Conversation.Open(_ali, _ayse, _clock.GetUtcNow());
        _conversations.Add(conversation);
        var message = conversation.Post(_ayse, "Merhaba", null, _clock.GetUtcNow());
        _conversations.Add(message);
        var stranger = Guid.NewGuid();
        _users.Users[stranger] = new UserInfo(stranger, "stranger", IsActive: true, IsEmailVerified: true);

        Assert.Equal("not_found", await CodeOfAsync(() => ReportAsync(ReportTargetType.Message, message.Id, reporter: stranger)));
        Assert.Equal("not_found", await CodeOfAsync(() => ReportAsync(ReportTargetType.Review, Guid.NewGuid())));
        Assert.Equal("not_found", await CodeOfAsync(() => ReportAsync(ReportTargetType.User, Guid.NewGuid())));
        await Assert.ThrowsAsync<DomainException>(() => ReportAsync(ReportTargetType.Message, message.Id, reporter: _ayse));
        Assert.Empty(_reports.Items);
    }

    [Fact]
    public async Task One_open_report_per_target_and_a_verified_email_are_required()
    {
        await ReportAsync(ReportTargetType.User, _ayse);
        Assert.Equal("report_already_open", await CodeOfAsync(() => ReportAsync(ReportTargetType.User, _ayse)));

        var unverified = Guid.NewGuid();
        _users.Users[unverified] = new UserInfo(unverified, "new", IsActive: true, IsEmailVerified: false);
        Assert.Equal("email_not_verified", await CodeOfAsync(() => ReportAsync(ReportTargetType.User, _ayse, reporter: unverified)));
    }

    [Fact]
    public void A_report_for_another_reason_needs_a_description()
    {
        var validator = new CreateReportCommandValidator();

        Assert.False(validator.Validate(new CreateReportCommand(_ali, ReportTargetType.User, _ayse, ReportReason.Other, " ")).IsValid);
        Assert.True(validator.Validate(new CreateReportCommand(_ali, ReportTargetType.User, _ayse, ReportReason.Other, "Neden")).IsValid);
        Assert.True(validator.Validate(new CreateReportCommand(_ali, ReportTargetType.User, _ayse, ReportReason.Spam, null)).IsValid);
        Assert.False(validator.Validate(new CreateReportCommand(_ali, ReportTargetType.User, _ayse, ReportReason.Spam, new string('a', 1001))).IsValid);
    }

    [Fact]
    public async Task Closing_a_report_tells_the_reporter_that_it_was_reviewed()
    {
        var report = await ReportAsync(ReportTargetType.User, _ayse);
        var admin = Guid.NewGuid();
        var close = new CloseReportCommandHandler(_reports, _notifier, _unitOfWork, _clock);

        var closed = await close.Handle(new CloseReportCommand(admin, report.Id, ReportStatus.Resolved), CancellationToken.None);

        Assert.Equal((ReportStatus.Resolved, admin, _ayse), (closed.Status, closed.ResolvedByUserId!.Value, closed.TargetUserId));
        Assert.Equal((_ali, NotificationType.ReportReviewed), Assert.Single(_notifier.Take()));
        await Assert.ThrowsAsync<DomainException>(() => close.Handle(new CloseReportCommand(admin, report.Id, ReportStatus.Dismissed), CancellationToken.None).AsTask());
        Assert.Equal("not_found", await CodeOfAsync(() => close.Handle(new CloseReportCommand(admin, Guid.NewGuid(), ReportStatus.Dismissed), CancellationToken.None).AsTask()));

        var open = await new AdminListReportsQueryHandler(_reports).Handle(new AdminListReportsQuery(ReportStatus.Open, null, null), CancellationToken.None);
        Assert.Empty(open.Items);
    }
}

public class SuspensionHandlerTests
{
    private readonly IdentityTestContext _context = new();
    private readonly RecordingConnections _connections = new();

    private SuspendUserCommandHandler Suspend =>
        new(_context.Users, _context.RefreshTokens, _connections, _context.UnitOfWork, _context.Clock);

    [Fact]
    public async Task A_suspended_account_is_signed_out_everywhere_and_can_be_restored()
    {
        var admin = _context.AddUser("yonetici@example.com", "yonetici");
        var user = _context.AddUser();
        _context.AddRefreshToken(user);

        await Suspend.Handle(new SuspendUserCommand(admin.Id, user.Id), CancellationToken.None);

        Assert.Equal(UserStatus.Suspended, user.Status);
        Assert.All(_context.RefreshTokens.Items, token => Assert.True(token.IsRevoked));
        Assert.Equal(user.Id, Assert.Single(_connections.Closed));

        await new LiftSuspensionCommandHandler(_context.Users, _context.UnitOfWork)
            .Handle(new LiftSuspensionCommand(user.Id), CancellationToken.None);
        Assert.Equal(UserStatus.Active, user.Status);
    }

    [Fact]
    public async Task Administrators_do_not_suspend_themselves_or_unknown_accounts()
    {
        var admin = _context.AddUser("yonetici@example.com", "yonetici");

        Assert.Equal("invalid_state", (await Assert.ThrowsAsync<DomainException>(
            () => Suspend.Handle(new SuspendUserCommand(admin.Id, admin.Id), CancellationToken.None).AsTask())).Code);
        await Assert.ThrowsAsync<AppException>(
            () => Suspend.Handle(new SuspendUserCommand(admin.Id, Guid.NewGuid()), CancellationToken.None).AsTask());
        Assert.Equal(UserStatus.Active, admin.Status);
    }
}
