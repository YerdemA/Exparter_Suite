using Exparter.Application.Common;
using Exparter.Application.Identity;
using Exparter.Application.Messaging;
using Exparter.Application.Notifications;
using Exparter.Application.Profiles;
using Exparter.Application.Reviews;
using Exparter.Application.Skills;
using Exparter.Application.Tests.Messaging;
using Exparter.Application.Tests.Notifications;
using Exparter.Application.Tests.Profiles;
using Exparter.Application.Tests.Reviews;
using Exparter.Application.Tests.Skills;
using Exparter.Domain.Identity;
using Exparter.Domain.Messaging;
using Exparter.Domain.Notifications;
using Exparter.Domain.Profiles;
using Exparter.Domain.Reviews;
using Exparter.Domain.Skills;

namespace Exparter.Application.Tests.Identity;

public class AccountErasureTests
{
    private readonly IdentityTestContext _context = new();

    private EraseDeletedAccountsCommandHandler Handler => new(
        _context.Users,
        _context.RefreshTokens,
        _context.OneTimeTokens,
        _context.BlockedEmails,
        _context.Publisher,
        _context.UnitOfWork,
        _context.Clock);

    private Task<int> EraseAsync() => Handler.Handle(new EraseDeletedAccountsCommand(), CancellationToken.None).AsTask();

    private RegisterCommandHandler RegisterHandler => new(
        _context.Users,
        _context.OneTimeTokens,
        _context.BlockedEmails,
        _context.Hasher,
        _context.Tokens,
        _context.TokenIssuer,
        _context.Emails,
        _context.Publisher,
        _context.UnitOfWork,
        _context.Clock);

    private Task<AuthResult> RegisterAsync(string email, string username) =>
        RegisterHandler.Handle(new RegisterCommand(email, username, IdentityTestContext.Password, AgeConfirmed: true), CancellationToken.None).AsTask();

    [Fact]
    public async Task An_account_is_erased_only_once_its_deletion_can_no_longer_be_taken_back()
    {
        var leaving = _context.AddUser();
        var staying = _context.AddUser("can@example.com", "can");
        _context.AddRefreshToken(leaving);
        _context.AddOneTimeToken(leaving, OneTimeTokenPurpose.PasswordReset);
        _context.AddRefreshToken(staying);
        leaving.RequestDeletion(_context.Clock.GetUtcNow());

        _context.Clock.Advance(User.DeletionGracePeriod - TimeSpan.FromMinutes(1));
        Assert.Equal(0, await EraseAsync());
        Assert.Equal(UserStatus.PendingDeletion, leaving.Status);

        _context.Clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(1, await EraseAsync());
        Assert.Equal(0, await EraseAsync());

        Assert.Equal(UserStatus.Deleted, leaving.Status);
        Assert.DoesNotContain("ayse", leaving.Email + leaving.Username, StringComparison.Ordinal);
        Assert.Equal(string.Empty, leaving.PasswordHash);
        Assert.Equal(UserStatus.Active, staying.Status);
        Assert.Equal(staying.Id, Assert.Single(_context.RefreshTokens.Items).UserId);
        Assert.Empty(_context.OneTimeTokens.Items);
        Assert.Equal(new AccountErased(leaving.Id), Assert.Single(_context.Publisher.Published));
    }

    [Fact]
    public async Task The_address_of_an_erased_account_cannot_register_again_for_three_months_but_its_name_is_free()
    {
        var leaving = _context.AddUser();
        leaving.RequestDeletion(_context.Clock.GetUtcNow());
        _context.Clock.Advance(User.DeletionGracePeriod);
        await EraseAsync();

        // Only a hash of the address is kept.
        Assert.DoesNotContain("ayse", Assert.Single(_context.BlockedEmails.Items).EmailHash, StringComparison.Ordinal);
        var refused = await Assert.ThrowsAsync<AppException>(() => RegisterAsync(" AYSE@example.com ", "yeni.ayse"));
        Assert.Equal("email_recently_deleted", refused.Code);
        await RegisterAsync("baska@example.com", "ayse");

        _context.Clock.Advance(BlockedEmail.Duration);
        await RegisterAsync("ayse@example.com", "yeni.ayse");

        // The next run forgets the address altogether.
        await EraseAsync();
        Assert.Empty(_context.BlockedEmails.Items);
    }

    [Fact]
    public async Task An_erased_account_is_described_to_others_as_a_deleted_user()
    {
        var leaving = _context.AddUser();
        var staying = _context.AddUser("can@example.com", "can");
        leaving.RequestDeletion(_context.Clock.GetUtcNow());
        var directory = new UserDirectory(_context.Users);

        Assert.Equal((false, false), await FlagsAsync(leaving.Id));

        _context.Clock.Advance(User.DeletionGracePeriod);
        await EraseAsync();

        Assert.Equal((false, true), await FlagsAsync(leaving.Id));
        var summaries = await new UserSummaries(directory, new ProfileDirectory(new InMemoryProfiles()), new FakeObjectStorage())
            .GetAsync([leaving.Id, staying.Id], CancellationToken.None);
        Assert.Equal((true, null), (summaries[leaving.Id].IsDeleted, summaries[leaving.Id].Username));
        Assert.Equal((false, "can"), (summaries[staying.Id].IsDeleted, summaries[staying.Id].Username));

        async Task<(bool IsActive, bool IsDeleted)> FlagsAsync(Guid userId)
        {
            var info = (await directory.FindAsync(userId, CancellationToken.None))!;
            return (info.IsActive, info.IsDeleted);
        }
    }

    [Fact]
    public async Task A_leaving_user_closes_their_open_conversations_whatever_their_matches()
    {
        var ali = Guid.NewGuid();
        var conversations = new InMemoryConversations();
        var withAyse = Conversation.Open(ali, Guid.NewGuid(), _context.Clock.GetUtcNow());
        var withCan = Conversation.Open(Guid.NewGuid(), ali, _context.Clock.GetUtcNow());
        var others = Conversation.Open(Guid.NewGuid(), Guid.NewGuid(), _context.Clock.GetUtcNow());
        conversations.Items.AddRange([withAyse, withCan, others]);

        await new CloseConversationsOnAccountDeletion(conversations, _context.Clock)
            .Handle(new AccountDeletionRequested(ali), CancellationToken.None);

        Assert.Equal((true, ali), (withAyse.IsClosed, withAyse.ClosedByUserId!.Value));
        Assert.True(withCan.IsClosed);
        Assert.False(others.IsClosed);
    }

    [Fact]
    public async Task Erasing_an_account_removes_what_each_module_holds_about_it_and_nothing_of_anyone_else()
    {
        var now = _context.Clock.GetUtcNow();
        var ali = Guid.NewGuid();
        var ayse = Guid.NewGuid();
        var erased = new AccountErased(ali);

        var profiles = new InMemoryProfiles();
        var storage = new FakeObjectStorage();
        storage.Objects["avatars/ali/1.jpg"] = new StoredObjectInfo(1024, "image/jpeg");
        foreach (var userId in new[] { ali, ayse })
        {
            var profile = Profile.CreateEmpty(userId, now);
            profiles.Add(profile);
        }

        profiles.Items[0].SetAvatar("avatars/ali/1.jpg", now);
        await new RemoveProfileOnAccountErased(profiles, storage).Handle(erased, CancellationToken.None);
        await new RemoveProfileOnAccountErased(profiles, storage).Handle(erased, CancellationToken.None);
        Assert.Equal(ayse, Assert.Single(profiles.Items).UserId);
        Assert.Empty(storage.Objects);

        var skills = new InMemorySkills();
        var userSkills = new InMemoryUserSkills(skills);
        var guitar = skills.AddApproved("Gitar");
        userSkills.Add(UserSkill.Add(ali, guitar, UserSkillType.Offered, SkillLevel.Advanced, [], now));
        userSkills.Add(UserSkill.Add(ayse, guitar, UserSkillType.Offered, SkillLevel.Advanced, [], now));
        await new RemoveUserSkillsOnAccountErased(userSkills).Handle(erased, CancellationToken.None);
        Assert.Equal(ayse, Assert.Single(userSkills.Items).UserId);

        // Reviews of the user go; the ones the user wrote stay.
        var reviews = new InMemoryReviews();
        reviews.Add(Review.Create(ayse, ali, Guid.NewGuid(), 9, null, now));
        reviews.Add(Review.Create(ali, ayse, Guid.NewGuid(), 7, null, now));
        await new RemoveReviewsOnAccountErased(reviews).Handle(erased, CancellationToken.None);
        Assert.Equal(ali, Assert.Single(reviews.Items).ReviewerId);

        var notifications = new InMemoryNotifications();
        var devices = new InMemoryDevices();
        notifications.Add(Notification.Create(ali, NotificationType.MatchEnded, new NotificationData(), now));
        notifications.Add(Notification.Create(ayse, NotificationType.MatchEnded, new NotificationData(), now));
        devices.Add(Device.Register(ali, "token-ali", DevicePlatform.Android, now));
        devices.Add(Device.Register(ayse, "token-ayse", DevicePlatform.Android, now));
        await new RemoveNotificationsOnAccountErased(notifications, devices).Handle(erased, CancellationToken.None);
        Assert.Equal(ayse, Assert.Single(notifications.Items).UserId);
        Assert.Equal("token-ayse", Assert.Single(devices.Items).FcmToken);
    }
}
