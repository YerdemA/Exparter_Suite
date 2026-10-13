using Exparter.Application.Common;
using Exparter.Application.Matching;
using Exparter.Application.Tests.Profiles;
using Exparter.Domain.Skills;

namespace Exparter.Application.Tests.Matching;

internal sealed class FakeDiscoveryReadModel : IDiscoveryReadModel
{
    /// <summary>Already in ranked order, as the database would return them.</summary>
    public List<DiscoveredUser> Ranked { get; } = [];

    public DiscoveryCriteria? LastCriteria { get; private set; }

    public Task<IReadOnlyList<DiscoveredUser>> FindAsync(DiscoveryCriteria criteria, CancellationToken cancellationToken)
    {
        LastCriteria = criteria;
        return Task.FromResult<IReadOnlyList<DiscoveredUser>>(Ranked.Skip(criteria.Offset).Take(criteria.Limit).ToList());
    }
}

public class DiscoveryHandlerTests
{
    private readonly Guid _viewerId = Guid.NewGuid();
    private readonly FakeDiscoveryReadModel _readModel = new();
    private readonly FakeObjectStorage _storage = new();

    private DiscoverUsersQueryHandler Handler => new(_readModel, _storage);

    private DiscoverUsersQuery Query(string? search = null, string? cursor = null, int? limit = null, string? city = null) =>
        new(_viewerId, null, null, city, null, search, cursor, limit);

    private void AddUsers(int count)
    {
        for (var index = 0; index < count; index++)
        {
            _readModel.Ranked.Add(new DiscoveredUser(
                Guid.NewGuid(), $"user{index}", $"User {index}", null, "34", true, false, Compatibility.None, RatingSummaryDto.None, [], []));
        }
    }

    [Fact]
    public async Task Result_carries_the_users_skills_compatibility_and_a_download_url_for_the_avatar()
    {
        var skillId = Guid.NewGuid();
        _readModel.Ranked.Add(new DiscoveredUser(
            Guid.NewGuid(),
            "deniz",
            "Deniz",
            "avatars/x/1.jpg",
            "35",
            AcceptsOnline: true,
            AcceptsInPerson: true,
            Compatibility.Mutual,
            new RatingSummaryDto(4, 9.25),
            [new DiscoveredSkill(skillId, "Gitar", SkillLevel.Advanced)],
            []));

        var page = await Handler.Handle(Query(), CancellationToken.None);

        var item = Assert.Single(page.Items);
        Assert.Equal("deniz", item.User.Username);
        Assert.Equal("https://storage.test/download/avatars/x/1.jpg", item.User.AvatarUrl?.ToString());
        Assert.Equal(Compatibility.Mutual, item.Compatibility);
        Assert.Equal(new RatingSummaryDto(4, 9.25), item.Rating);
        Assert.Equal(skillId, Assert.Single(item.OfferedSkills).Skill.Id);
        Assert.Null(page.NextCursor);
    }

    [Fact]
    public async Task Pages_follow_each_other_through_the_cursor_without_gaps_or_repeats()
    {
        AddUsers(5);

        var first = await Handler.Handle(Query(limit: 2), CancellationToken.None);
        var second = await Handler.Handle(Query(limit: 2, cursor: first.NextCursor), CancellationToken.None);
        var third = await Handler.Handle(Query(limit: 2, cursor: second.NextCursor), CancellationToken.None);

        var usernames = first.Items.Concat(second.Items).Concat(third.Items).Select(item => item.User.Username);
        Assert.Equal(["user0", "user1", "user2", "user3", "user4"], usernames);
        Assert.Null(third.NextCursor);
    }

    [Fact]
    public async Task Last_full_page_has_no_next_cursor()
    {
        AddUsers(2);

        var page = await Handler.Handle(Query(limit: 2), CancellationToken.None);

        Assert.Equal(2, page.Items.Count);
        Assert.Null(page.NextCursor);
    }

    [Theory]
    [InlineData("  IŞIK Çağrı ", "isik cagri")]
    [InlineData("İpek", "ipek")]
    [InlineData("   ", null)]
    [InlineData(null, null)]
    public async Task Search_text_is_folded_before_it_reaches_the_read_model(string? search, string? expected)
    {
        await Handler.Handle(Query(search), CancellationToken.None);

        Assert.Equal(expected, _readModel.LastCriteria!.Search);
    }

    [Fact]
    public void Validator_accepts_a_query_without_filters_and_a_cursor_it_issued() =>
        Assert.True(new DiscoverUsersQueryValidator().Validate(Query(cursor: "bzoyMA", limit: 50, city: "81")).IsValid);

    [Theory]
    [InlineData(0, null, null, "Limit")]
    [InlineData(51, null, null, "Limit")]
    [InlineData(null, "82", null, "City")]
    [InlineData(null, "7", null, "City")]
    [InlineData(null, null, "not-a-cursor", "Cursor")]
    [InlineData(null, null, "bzotMQ", "Cursor")]
    public void Validator_rejects_out_of_range_input(int? limit, string? city, string? cursor, string field)
    {
        var result = new DiscoverUsersQueryValidator().Validate(Query(cursor: cursor, limit: limit, city: city));

        Assert.Contains(result.Errors, error => error.PropertyName == field);
    }

    [Fact]
    public void Validator_rejects_a_search_text_that_is_too_long() =>
        Assert.False(new DiscoverUsersQueryValidator().Validate(Query(search: new string('a', 61))).IsValid);
}
