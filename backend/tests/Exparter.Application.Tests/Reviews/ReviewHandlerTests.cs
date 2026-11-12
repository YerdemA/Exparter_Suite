using Exparter.Application.Moderation;
using Exparter.Application.Common;
using Exparter.Application.Identity;
using Exparter.Application.Reviews;
using Exparter.Application.Sessions;
using Exparter.Application.Tests.Identity;
using Exparter.Application.Tests.Matching;
using Exparter.Application.Tests.Profiles;
using Exparter.Application.Tests.Sessions;
using Exparter.Domain.Common;
using Exparter.Domain.Notifications;
using Exparter.Domain.Reviews;
using Exparter.Domain.Sessions;

namespace Exparter.Application.Tests.Reviews;

internal sealed class InMemoryReviews : IReviewRepository
{
    public List<Review> Items { get; } = [];

    public Task<Review?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(x => x.Id == id && !x.IsDeleted));

    public Task<bool> ExistsAsync(Guid reviewerId, Guid revieweeId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.Any(x => x.ReviewerId == reviewerId && x.RevieweeId == revieweeId));

    public Task<PagedList<Review>> ListReceivedAsync(Guid revieweeId, PageRequest page, CancellationToken cancellationToken) =>
        Task.FromResult(Paged(Items.Where(x => x.RevieweeId == revieweeId), page));

    public Task<PagedList<Review>> ListGivenAsync(Guid reviewerId, PageRequest page, CancellationToken cancellationToken) =>
        Task.FromResult(Paged(Items.Where(x => x.ReviewerId == reviewerId), page));

    public Task<IReadOnlyDictionary<Guid, RatingSummaryDto>> GetRatingsAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<Guid, RatingSummaryDto>>(Items
            .Where(x => !x.IsDeleted && userIds.Contains(x.RevieweeId))
            .GroupBy(x => x.RevieweeId)
            .ToDictionary(group => group.Key, group => new RatingSummaryDto(group.Count(), group.Average(x => x.Rating))));

    public Task DeleteReceivedAsync(Guid revieweeId, CancellationToken cancellationToken)
    {
        Items.RemoveAll(x => x.RevieweeId == revieweeId);
        return Task.CompletedTask;
    }

    public void Add(Review review) => Items.Add(review);

    private static PagedList<Review> Paged(IEnumerable<Review> source, PageRequest page)
    {
        var all = source.Where(x => !x.IsDeleted).OrderByDescending(x => x.CreatedAt).ToList();
        return new PagedList<Review>(all, page.Page, page.PageSize, all.Count);
    }
}

public class ReviewHandlerTests
{
    private readonly Guid _ali = Guid.NewGuid();
    private readonly Guid _ayse = Guid.NewGuid();
    private readonly Guid _can = Guid.NewGuid();
    private readonly InMemoryReviews _reviews = new();
    private readonly InMemorySessions _sessions = new();
    private readonly FakeUserDirectory _users = new();
    private readonly FakeInteractionRequirements _requirements = new();
    private readonly RecordingNotifier _notifier = new();
    private readonly CountingUnitOfWork _unitOfWork = new();
    private readonly TestClock _clock = new();

    public ReviewHandlerTests()
    {
        foreach (var (id, name) in new[] { (_ali, "ali"), (_ayse, "ayse"), (_can, "can") })
        {
            _users.Users[id] = new UserInfo(id, name, IsActive: true, IsEmailVerified: true);
        }
    }

    private ISessionDirectory SessionDirectory => new SessionDirectory(_sessions, _clock);

    internal Moderation.InMemoryBlocks Blocks { get; } = new();

    private ReviewDtoFactory DtoFactory => new(new FakeUserSummaries(_users));

    /// <summary>A session the two users held and both called completed.</summary>
    private SwapSession AddCompletedSession(Guid first, Guid second)
    {
        var session = HeldSessions.MarkedCompleted(Guid.NewGuid(), first, second, _clock.GetUtcNow());
        session.ConfirmCompleted(second, _clock.GetUtcNow());
        _sessions.Items.Add(session);
        return session;
    }

    private Task<ReviewDto> CreateAsync(Guid reviewer, Guid reviewee, int rating = 8, string? comment = null) =>
        new CreateReviewCommandHandler(_requirements, _users, new BlockDirectory(Blocks), SessionDirectory, _reviews, DtoFactory, _notifier, _unitOfWork, _clock)
            .Handle(new CreateReviewCommand(reviewer, reviewee, rating, comment), CancellationToken.None).AsTask();

    private Task DeleteAsync(Guid userId, Guid reviewId) =>
        new DeleteReviewCommandHandler(_reviews, _unitOfWork, _clock)
            .Handle(new DeleteReviewCommand(userId, reviewId), CancellationToken.None).AsTask();

    private Task<ReviewEligibilityDto> EligibilityAsync(Guid userId, Guid otherUserId) =>
        new GetReviewEligibilityQueryHandler(_users, new BlockDirectory(Blocks), SessionDirectory, _reviews)
            .Handle(new GetReviewEligibilityQuery(userId, otherUserId), CancellationToken.None).AsTask();

    private async Task<PagedList<ReviewDto>> ReceivedAsync(Guid userId) =>
        await new ListUserReviewsQueryHandler(_users, _reviews, DtoFactory)
            .Handle(new ListUserReviewsQuery(userId, null, null), CancellationToken.None);

    private Task<RatingSummaryDto> RatingAsync(Guid userId) =>
        new ReviewDirectory(_reviews).GetRatingAsync(userId, CancellationToken.None);

    private static async Task<string> AppCodeOfAsync(Func<Task> action) => (await Assert.ThrowsAsync<AppException>(action)).Code;

    private static async Task<string> DomainCodeOfAsync(Func<Task> action) => (await Assert.ThrowsAsync<DomainException>(action)).Code;

    [Fact]
    public async Task A_review_is_stored_with_the_session_that_allowed_it_and_names_its_author()
    {
        var session = AddCompletedSession(_ayse, _ali);

        var dto = await CreateAsync(_ali, _ayse, 9, "  Harika anlatıyor.  ");

        Assert.Equal((_ayse, session.Id, 9, "Harika anlatıyor."), (dto.RevieweeId, dto.SessionId, dto.Rating, dto.Comment));
        Assert.Equal((_ali, "ali"), (dto.Reviewer.UserId, dto.Reviewer.Username));
        Assert.Equal(_clock.GetUtcNow(), dto.CreatedAt);
        var (recipient, type, data) = Assert.Single(_notifier.Sent);
        Assert.Equal((_ayse, NotificationType.ReviewReceived), (recipient, type));
        Assert.Equal((_ali, dto.Id), (data.ActorUserId, data.ReviewId));
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Each_side_reviews_the_other_once_while_their_match_may_still_be_running()
    {
        AddCompletedSession(_ali, _ayse);

        await CreateAsync(_ali, _ayse);
        await CreateAsync(_ayse, _ali, 6);

        Assert.Equal("review_already_exists", await AppCodeOfAsync(() => CreateAsync(_ali, _ayse, 3)));
        Assert.Equal(2, _reviews.Items.Count);
    }

    [Fact]
    public async Task A_review_needs_a_completed_session_between_the_two_users()
    {
        // Proposed, pending and cancelled sessions do not count; neither does one with someone else.
        _sessions.Items.Add(SwapSession.Propose(Guid.NewGuid(), _ali, _ayse, _clock.GetUtcNow().AddDays(1), 60, null, _clock.GetUtcNow()));
        var pending = HeldSessions.MarkedCompleted(Guid.NewGuid(), _ali, _ayse, _clock.GetUtcNow());
        _sessions.Items.Add(pending);
        AddCompletedSession(_ali, _can);

        Assert.Equal("session_not_completed", await AppCodeOfAsync(() => CreateAsync(_ali, _ayse)));
        Assert.Equal(ReviewBlockReason.NoCompletedSession, (await EligibilityAsync(_ali, _ayse)).Reason);

        // A mark nobody answered stands once its deadline has passed.
        _clock.Advance(SwapSession.ConfirmationPeriod);
        Assert.True((await EligibilityAsync(_ayse, _ali)).CanReview);
        Assert.Equal(pending.Id, (await CreateAsync(_ayse, _ali)).SessionId);
    }

    [Fact]
    public async Task Reviewing_is_refused_for_oneself_strangers_to_the_app_and_users_who_cannot_interact()
    {
        AddCompletedSession(_ali, _ayse);

        Assert.Equal(ReviewErrors.ReviewOfSelfCode, await DomainCodeOfAsync(() => CreateAsync(_ali, _ali)));
        Assert.Equal("not_found", await AppCodeOfAsync(() => CreateAsync(_ali, Guid.NewGuid())));

        _users.Users[_ayse] = _users.Users[_ayse] with { IsActive = false };
        Assert.Equal("not_found", await AppCodeOfAsync(() => CreateAsync(_ali, _ayse)));
        Assert.Equal("not_found", await AppCodeOfAsync(() => EligibilityAsync(_ali, _ayse)));
        Assert.Equal("not_found", await AppCodeOfAsync(() => ReceivedAsync(_ayse)));

        _requirements.CannotInteract.Add(_ayse);
        Assert.Equal("interaction_requirements_not_met", await AppCodeOfAsync(() => CreateAsync(_ayse, _ali)));
        Assert.Empty(_reviews.Items);
    }

    [Fact]
    public async Task Eligibility_says_why_a_review_cannot_be_written()
    {
        AddCompletedSession(_ali, _ayse);

        Assert.Equal(new ReviewEligibilityDto(true, null), await EligibilityAsync(_ali, _ayse));
        Assert.Equal(new ReviewEligibilityDto(false, ReviewBlockReason.Self), await EligibilityAsync(_ali, _ali));
        Assert.Equal(new ReviewEligibilityDto(false, ReviewBlockReason.NoCompletedSession), await EligibilityAsync(_ali, _can));

        var review = await CreateAsync(_ali, _ayse);
        Assert.Equal(new ReviewEligibilityDto(false, ReviewBlockReason.AlreadyReviewed), await EligibilityAsync(_ali, _ayse));
        Assert.True((await EligibilityAsync(_ayse, _ali)).CanReview);

        await DeleteAsync(_ali, review.Id);
        Assert.Equal(ReviewBlockReason.AlreadyReviewed, (await EligibilityAsync(_ali, _ayse)).Reason);
    }

    [Fact]
    public async Task A_deleted_review_disappears_but_cannot_be_replaced()
    {
        AddCompletedSession(_ali, _ayse);
        var review = await CreateAsync(_ali, _ayse, 2);

        Assert.Equal(DomainErrorCodes.NotParticipant, await DomainCodeOfAsync(() => DeleteAsync(_ayse, review.Id)));
        await DeleteAsync(_ali, review.Id);

        Assert.Empty((await ReceivedAsync(_ayse)).Items);
        Assert.Equal(RatingSummaryDto.None, await RatingAsync(_ayse));
        Assert.Equal("not_found", await AppCodeOfAsync(() => DeleteAsync(_ali, review.Id)));
        Assert.Equal("not_found", await AppCodeOfAsync(() => DeleteAsync(_ali, Guid.NewGuid())));
        Assert.Equal("review_already_exists", await AppCodeOfAsync(() => CreateAsync(_ali, _ayse, 10)));
    }

    [Fact]
    public async Task Reviews_are_listed_newest_first_and_averaged_without_the_deleted_ones()
    {
        AddCompletedSession(_ali, _ayse);
        AddCompletedSession(_can, _ayse);
        var first = await CreateAsync(_ali, _ayse, 10);
        _clock.Advance(TimeSpan.FromHours(1));
        var second = await CreateAsync(_can, _ayse, 7, "İyiydi.");

        var received = await ReceivedAsync(_ayse);
        var given = await new ListGivenReviewsQueryHandler(_reviews, DtoFactory)
            .Handle(new ListGivenReviewsQuery(_can, null, null), CancellationToken.None);

        Assert.Equal([second.Id, first.Id], received.Items.Select(item => item.Id));
        Assert.Equal(2, received.TotalCount);
        Assert.Equal(second.Id, Assert.Single(given.Items).Id);
        Assert.Equal(new RatingSummaryDto(2, 8.5), await RatingAsync(_ayse));
        Assert.Equal(RatingSummaryDto.None, await RatingAsync(_ali));

        await DeleteAsync(_ali, first.Id);
        Assert.Equal(new RatingSummaryDto(1, 7), await RatingAsync(_ayse));
    }

    [Fact]
    public void The_validator_refuses_ratings_and_comments_out_of_range()
    {
        var validator = new CreateReviewCommandValidator();

        Assert.True(validator.Validate(new CreateReviewCommand(_ali, _ayse, 1, null)).IsValid);
        Assert.True(validator.Validate(new CreateReviewCommand(_ali, _ayse, 10, new string('a', 500))).IsValid);
        Assert.False(validator.Validate(new CreateReviewCommand(_ali, _ayse, 0, null)).IsValid);
        Assert.False(validator.Validate(new CreateReviewCommand(_ali, _ayse, 11, null)).IsValid);
        Assert.False(validator.Validate(new CreateReviewCommand(_ali, _ayse, 5, new string('a', 501))).IsValid);
    }
}
