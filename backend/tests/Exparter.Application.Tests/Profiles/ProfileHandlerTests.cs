using Exparter.Application.Moderation;
using Exparter.Application.Common;
using Exparter.Application.Identity;
using Exparter.Application.Profiles;
using Exparter.Application.Tests.Identity;
using Exparter.Domain.Profiles;

namespace Exparter.Application.Tests.Profiles;

internal sealed class InMemoryProfiles : IProfileRepository
{
    public List<Profile> Items { get; } = [];

    public Task<Profile?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(x => x.UserId == userId));

    public Task<IReadOnlyList<Profile>> ListByUserIdsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Profile>>(Items.Where(x => userIds.Contains(x.UserId)).ToList());

    public void Add(Profile profile) => Items.Add(profile);

    public void Remove(Profile profile) => Items.Remove(profile);
}

internal sealed class FakeObjectStorage : IObjectStorage
{
    public Dictionary<string, StoredObjectInfo> Objects { get; } = [];

    public PresignedUpload CreateUploadUrl(string key, string contentType, TimeSpan lifetime) =>
        new(new Uri($"https://storage.test/upload/{key}"), DateTimeOffset.UnixEpoch + lifetime);

    public Uri CreateDownloadUrl(string key, TimeSpan lifetime) => new($"https://storage.test/download/{key}");

    public Task<StoredObjectInfo?> GetInfoAsync(string key, CancellationToken cancellationToken) =>
        Task.FromResult(Objects.GetValueOrDefault(key));

    public Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        Objects.Remove(key);
        return Task.CompletedTask;
    }
}

internal sealed class FakeUserDirectory : IUserDirectory
{
    public Dictionary<Guid, UserInfo> Users { get; } = [];

    public Task<UserInfo?> FindAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult(Users.GetValueOrDefault(userId));

    public Task<IReadOnlyList<UserInfo>> FindManyAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<UserInfo>>(Users.Values.Where(user => userIds.Contains(user.Id)).ToList());
}

internal sealed class FakeReviewDirectory : Exparter.Application.Reviews.IReviewDirectory
{
    public Dictionary<Guid, RatingSummaryDto> Ratings { get; } = [];

    public Dictionary<Guid, Exparter.Application.Reviews.ReviewInfo> Reviews { get; } = [];

    public Task<RatingSummaryDto> GetRatingAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult(Ratings.GetValueOrDefault(userId, RatingSummaryDto.None));

    public Task<Exparter.Application.Reviews.ReviewInfo?> FindAsync(Guid reviewId, CancellationToken cancellationToken) =>
        Task.FromResult(Reviews.GetValueOrDefault(reviewId));
}

public class ProfileHandlerTests
{
    private readonly Guid _viewerId = Guid.NewGuid();
    private readonly Moderation.InMemoryBlocks _blocks = new();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly InMemoryProfiles _profiles = new();
    private readonly FakeObjectStorage _storage = new();
    private readonly FakeUserDirectory _users = new();
    private readonly FakeReviewDirectory _reviews = new();
    private readonly CountingUnitOfWork _unitOfWork = new();
    private readonly TestClock _clock = new();

    private Profile AddProfile(bool complete = false)
    {
        var profile = Profile.CreateEmpty(_userId, _clock.GetUtcNow());
        if (complete)
        {
            profile.Update("Ayşe", "Gitar", "34", acceptsOnline: true, acceptsInPerson: true, _clock.GetUtcNow());
        }

        _profiles.Add(profile);
        return profile;
    }

    private string Upload(long length = 1024, string? contentType = "image/jpeg")
    {
        var key = $"avatars/{_userId:N}/{Guid.NewGuid():N}.jpg";
        _storage.Objects[key] = new StoredObjectInfo(length, contentType);
        return key;
    }

    private UpdateOwnProfileCommand UpdateCommand(string? city = "34", bool acceptsInPerson = true) =>
        new(_userId, "Ayşe Yılmaz", "Gitar çalıyorum.", city, AcceptsOnline: true, acceptsInPerson);

    [Fact]
    public async Task Registration_event_creates_a_blank_profile()
    {
        await new CreateProfileOnUserRegistered(_profiles, _clock).Handle(new UserRegistered(_userId), CancellationToken.None);

        var profile = Assert.Single(_profiles.Items);
        Assert.Equal(_userId, profile.UserId);
        Assert.False(profile.IsComplete);
    }

    [Fact]
    public async Task Own_profile_is_returned_even_when_blank()
    {
        AddProfile();

        var dto = await new GetOwnProfileQueryHandler(_profiles, _storage, _reviews)
            .Handle(new GetOwnProfileQuery(_userId), CancellationToken.None);

        Assert.Null(dto.DisplayName);
        Assert.False(dto.IsComplete);
        Assert.Null(dto.AvatarUrl);
        Assert.Equal(RatingSummaryDto.None, dto.Rating);
    }

    [Fact]
    public async Task Update_saves_the_profile_and_completes_it()
    {
        AddProfile();

        var dto = await new UpdateOwnProfileCommandHandler(_profiles, _storage, _reviews, _unitOfWork, _clock)
            .Handle(UpdateCommand(), CancellationToken.None);

        Assert.Equal("Ayşe Yılmaz", dto.DisplayName);
        Assert.Equal("34", dto.City);
        Assert.True(dto.IsComplete);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Theory]
    [InlineData("A", "34", true, true, nameof(UpdateOwnProfileCommand.DisplayName))]
    [InlineData("Ayşe", "99", true, false, nameof(UpdateOwnProfileCommand.City))]
    [InlineData("Ayşe", null, true, true, nameof(UpdateOwnProfileCommand.City))]
    [InlineData("Ayşe", null, false, false, nameof(UpdateOwnProfileCommand.AcceptsOnline))]
    public void Update_validator_rejects_invalid_fields(
        string displayName,
        string? city,
        bool acceptsOnline,
        bool acceptsInPerson,
        string field)
    {
        var result = new UpdateOwnProfileCommandValidator()
            .Validate(new UpdateOwnProfileCommand(_userId, displayName, null, city, acceptsOnline, acceptsInPerson));

        Assert.Contains(result.Errors, error => error.PropertyName == field);
    }

    [Fact]
    public void Update_validator_accepts_an_online_only_profile_without_a_city() =>
        Assert.True(new UpdateOwnProfileCommandValidator().Validate(UpdateCommand(city: null, acceptsInPerson: false)).IsValid);

    [Theory]
    [InlineData(true, "Ayşe")]
    [InlineData(false, null)]
    public async Task Public_profile_is_visible_whether_or_not_the_user_can_interact(bool complete, string? displayName)
    {
        AddProfile(complete);
        _users.Users[_userId] = new UserInfo(_userId, "ayse", IsActive: true, IsEmailVerified: false);

        var dto = await new GetUserProfileQueryHandler(_profiles, _users, _storage, _reviews, new BlockDirectory(_blocks))
            .Handle(new GetUserProfileQuery(_viewerId, _userId), CancellationToken.None);

        Assert.Equal("ayse", dto.Username);
        Assert.Equal(displayName, dto.DisplayName);
        Assert.Equal(RatingSummaryDto.None, dto.Rating);

        _reviews.Ratings[_userId] = new RatingSummaryDto(3, 8.5);
        var rated = await new GetUserProfileQueryHandler(_profiles, _users, _storage, _reviews, new BlockDirectory(_blocks))
            .Handle(new GetUserProfileQuery(_viewerId, _userId), CancellationToken.None);
        Assert.Equal(new RatingSummaryDto(3, 8.5), rated.Rating);
    }

    [Fact]
    public async Task Public_profile_of_an_inactive_account_is_not_found()
    {
        AddProfile(complete: true);
        _users.Users[_userId] = new UserInfo(_userId, "ayse", IsActive: false, IsEmailVerified: true);

        var exception = await Assert.ThrowsAsync<AppException>(
            async () => await new GetUserProfileQueryHandler(_profiles, _users, _storage, _reviews, new BlockDirectory(_blocks))
                .Handle(new GetUserProfileQuery(_viewerId, _userId), CancellationToken.None));

        Assert.Equal(AppErrorKind.NotFound, exception.Kind);
    }

    [Fact]
    public async Task Public_profile_of_an_unknown_user_is_not_found()
    {
        var exception = await Assert.ThrowsAsync<AppException>(
            async () => await new GetUserProfileQueryHandler(_profiles, _users, _storage, _reviews, new BlockDirectory(_blocks))
                .Handle(new GetUserProfileQuery(_viewerId, Guid.NewGuid()), CancellationToken.None));

        Assert.Equal(AppErrorKind.NotFound, exception.Kind);
    }

    [Fact]
    public async Task Upload_url_points_at_a_key_under_the_users_own_prefix()
    {
        var dto = await new CreateAvatarUploadUrlCommandHandler(_storage)
            .Handle(new CreateAvatarUploadUrlCommand(_userId, "image/png", 2048), CancellationToken.None);

        Assert.StartsWith($"avatars/{_userId:N}/", dto.ObjectKey);
        Assert.EndsWith(".png", dto.ObjectKey);
        Assert.Contains(dto.ObjectKey, dto.UploadUrl.ToString());
    }

    [Theory]
    [InlineData("image/gif", 1024)]
    [InlineData("application/pdf", 1024)]
    [InlineData("image/jpeg", 0)]
    [InlineData("image/jpeg", 5 * 1024 * 1024 + 1)]
    public void Upload_url_validator_rejects_wrong_types_and_sizes(string contentType, long contentLength) =>
        Assert.False(new CreateAvatarUploadUrlCommandValidator()
            .Validate(new CreateAvatarUploadUrlCommand(_userId, contentType, contentLength)).IsValid);

    [Fact]
    public async Task SetAvatar_uses_the_uploaded_file_and_deletes_the_previous_one()
    {
        var profile = AddProfile();
        var first = Upload();
        var second = Upload();
        var handler = new SetAvatarCommandHandler(_profiles, _storage, _reviews, _unitOfWork, _clock);

        await handler.Handle(new SetAvatarCommand(_userId, first), CancellationToken.None);
        var dto = await handler.Handle(new SetAvatarCommand(_userId, second), CancellationToken.None);

        Assert.Equal(second, profile.AvatarKey);
        Assert.Contains(second, dto.AvatarUrl!.ToString());
        Assert.False(_storage.Objects.ContainsKey(first));
        Assert.True(_storage.Objects.ContainsKey(second));
    }

    [Fact]
    public async Task SetAvatar_rejects_a_key_outside_the_users_prefix()
    {
        AddProfile();
        var foreignKey = $"avatars/{Guid.NewGuid():N}/photo.jpg";
        _storage.Objects[foreignKey] = new StoredObjectInfo(1024, "image/jpeg");

        var exception = await Assert.ThrowsAsync<AppException>(
            async () => await new SetAvatarCommandHandler(_profiles, _storage, _reviews, _unitOfWork, _clock)
                .Handle(new SetAvatarCommand(_userId, foreignKey), CancellationToken.None));

        Assert.Equal("avatar_not_uploaded", exception.Code);
        Assert.True(_storage.Objects.ContainsKey(foreignKey));
    }

    [Fact]
    public async Task SetAvatar_rejects_a_key_nothing_was_uploaded_to()
    {
        AddProfile();

        var exception = await Assert.ThrowsAsync<AppException>(
            async () => await new SetAvatarCommandHandler(_profiles, _storage, _reviews, _unitOfWork, _clock)
                .Handle(new SetAvatarCommand(_userId, $"avatars/{_userId:N}/missing.jpg"), CancellationToken.None));

        Assert.Equal("avatar_not_uploaded", exception.Code);
    }

    [Theory]
    [InlineData(5 * 1024 * 1024 + 1, "image/jpeg")]
    [InlineData(1024, "text/html")]
    [InlineData(1024, null)]
    public async Task SetAvatar_deletes_an_upload_that_breaks_the_limits(long length, string? contentType)
    {
        var profile = AddProfile();
        var key = Upload(length, contentType);

        var exception = await Assert.ThrowsAsync<AppException>(
            async () => await new SetAvatarCommandHandler(_profiles, _storage, _reviews, _unitOfWork, _clock)
                .Handle(new SetAvatarCommand(_userId, key), CancellationToken.None));

        Assert.Equal("avatar_invalid", exception.Code);
        Assert.Null(profile.AvatarKey);
        Assert.Empty(_storage.Objects);
    }

    [Fact]
    public async Task DeleteAvatar_removes_the_file_and_is_harmless_without_one()
    {
        var profile = AddProfile();
        var handler = new DeleteAvatarCommandHandler(_profiles, _storage, _unitOfWork, _clock);
        await handler.Handle(new DeleteAvatarCommand(_userId), CancellationToken.None);
        Assert.Equal(0, _unitOfWork.SaveCount);

        profile.SetAvatar(Upload(), _clock.GetUtcNow());
        await handler.Handle(new DeleteAvatarCommand(_userId), CancellationToken.None);

        Assert.Null(profile.AvatarKey);
        Assert.Empty(_storage.Objects);
    }
}
