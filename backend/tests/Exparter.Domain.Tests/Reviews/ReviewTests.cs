using Exparter.Domain.Common;
using Exparter.Domain.Reviews;

namespace Exparter.Domain.Tests.Reviews;

public class ReviewTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Ali = Guid.NewGuid();
    private static readonly Guid Ayse = Guid.NewGuid();
    private static readonly Guid SessionId = Guid.NewGuid();

    private static Review Create(int rating = 8, string? comment = null, Guid? reviewee = null) =>
        Review.Create(Ali, reviewee ?? Ayse, SessionId, rating, comment, Now);

    private static string CodeOf(Action action) => Assert.Throws<DomainException>(action).Code;

    [Fact]
    public void A_review_keeps_who_reviewed_whom_after_which_session()
    {
        var review = Create(9, "  Çok sabırlı bir öğretmen.  ");

        Assert.Equal((Ali, Ayse, SessionId), (review.ReviewerId, review.RevieweeId, review.SwapSessionId));
        Assert.Equal((9, "Çok sabırlı bir öğretmen.", Now), (review.Rating, review.Comment, review.CreatedAt));
        Assert.False(review.IsDeleted);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_comment_is_optional(string? comment) => Assert.Null(Create(comment: comment).Comment);

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    public void The_rating_runs_from_one_to_ten(int rating) => Assert.Equal(rating, Create(rating).Rating);

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    [InlineData(-3)]
    public void A_rating_out_of_range_is_refused(int rating) =>
        Assert.Equal(ReviewErrors.InvalidReviewCode, CodeOf(() => Create(rating)));

    [Fact]
    public void A_comment_can_be_at_most_500_characters()
    {
        Assert.Equal(500, Create(comment: new string('a', 500)).Comment!.Length);
        Assert.Equal(ReviewErrors.InvalidReviewCode, CodeOf(() => Create(comment: new string('a', 501))));
    }

    [Fact]
    public void Nobody_reviews_themselves() =>
        Assert.Equal(ReviewErrors.ReviewOfSelfCode, CodeOf(() => Create(reviewee: Ali)));

    [Fact]
    public void Only_the_author_deletes_a_review_and_the_first_deletion_stands()
    {
        var review = Create();

        Assert.Equal(DomainErrorCodes.NotParticipant, CodeOf(() => review.Delete(Ayse, Now)));
        Assert.False(review.IsDeleted);

        review.Delete(Ali, Now.AddHours(1));
        review.Delete(Ali, Now.AddHours(5));

        Assert.True(review.IsDeleted);
        Assert.Equal(Now.AddHours(1), review.DeletedAt);
    }
}
