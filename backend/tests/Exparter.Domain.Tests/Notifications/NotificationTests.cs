using Exparter.Domain.Matching;
using Exparter.Domain.Notifications;
using Exparter.Domain.Sessions;

namespace Exparter.Domain.Tests.Notifications;

public class NotificationTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Ali = Guid.NewGuid();
    private static readonly Guid Ayse = Guid.NewGuid();

    [Fact]
    public void A_notification_keeps_what_it_is_about_and_is_unread_until_read()
    {
        var data = new NotificationData(ActorUserId: Ayse, MatchId: Guid.NewGuid(), Reason: "Uygun değilim", Deadline: Now.AddDays(1));

        var notification = Notification.Create(Ali, NotificationType.SessionCancelled, data, Now);

        Assert.Equal((Ali, NotificationType.SessionCancelled, Now), (notification.UserId, notification.Type, notification.CreatedAt));
        Assert.Equal(data, notification.Data);
        Assert.Null(notification.ReadAt);

        notification.MarkRead(Now.AddMinutes(5));
        notification.MarkRead(Now.AddHours(5));

        Assert.Equal(Now.AddMinutes(5), notification.ReadAt);
    }

    [Fact]
    public void A_device_that_registers_again_moves_to_the_user_who_is_signed_in_now()
    {
        var device = Device.Register(Ali, "token-1", DevicePlatform.Android, Now);

        device.Refresh(Ayse, DevicePlatform.Android, Now.AddDays(2));

        Assert.Equal((Ayse, "token-1", Now, Now.AddDays(2)), (device.UserId, device.FcmToken, device.CreatedAt, device.LastSeenAt));
    }

    [Fact]
    public void An_end_request_is_reminded_of_once_and_only_in_its_last_day()
    {
        var match = SwapRequest.Send(Ali, Ayse, Guid.NewGuid(), Guid.NewGuid(), null, null, Now).Accept(Ayse, Now);
        Assert.False(match.TakeDueEndReminder(Now));

        match.RequestEnd(Ali, Now);
        var deadline = Now + Match.EndRequestLifetime;

        Assert.False(match.TakeDueEndReminder(deadline.AddHours(-25)));
        Assert.True(match.TakeDueEndReminder(deadline.AddHours(-24)));
        Assert.False(match.TakeDueEndReminder(deadline.AddHours(-2)));

        // A new end request gets its own reminder.
        match.RejectEnd(Ayse);
        match.RequestEnd(Ayse, Now);
        Assert.False(match.TakeDueEndReminder(deadline));
        Assert.True(match.TakeDueEndReminder(deadline.AddMinutes(-1)));
    }

    [Fact]
    public void A_scheduled_session_is_reminded_of_a_day_and_an_hour_before_it_starts()
    {
        var start = Now.AddDays(3);
        var session = SwapSession.ProposeOnline(Guid.NewGuid(), Ali, Ayse, start, 60, null, Now);
        Assert.False(session.TakeDueStartReminder(start.AddHours(-2)));

        session.Accept(Ayse, Now);

        Assert.False(session.TakeDueStartReminder(start.AddHours(-25)));
        Assert.True(session.TakeDueStartReminder(start.AddHours(-24)));
        Assert.False(session.TakeDueStartReminder(start.AddHours(-3)));
        Assert.True(session.TakeDueStartReminder(start.AddMinutes(-60)));
        Assert.False(session.TakeDueStartReminder(start.AddMinutes(-5)));
        Assert.False(session.TakeDueStartReminder(start));
    }

    [Theory]
    [InlineData(20, 1)]
    [InlineData(0.5, 0)]
    public void A_session_accepted_shortly_before_its_start_skips_the_reminders_it_is_already_past(double hoursAhead, int reminders)
    {
        var start = Now.AddHours(hoursAhead);
        var session = SwapSession.ProposeOnline(Guid.NewGuid(), Ali, Ayse, start, 60, null, Now.AddDays(-1));
        session.Accept(Ayse, Now);

        var sent = Enumerable.Range(0, 2000).Count(minute => session.TakeDueStartReminder(Now.AddMinutes(minute)));

        Assert.Equal(reminders, sent);
    }

    [Fact]
    public void A_completion_mark_is_reminded_of_once_and_only_in_its_last_day()
    {
        var session = SwapSession.ReportInPerson(Guid.NewGuid(), Ali, Ayse, Now);
        var deadline = Now + SwapSession.ConfirmationPeriod;

        Assert.False(session.TakeDueCompletionReminder(deadline.AddHours(-30)));
        Assert.True(session.TakeDueCompletionReminder(deadline.AddHours(-24)));
        Assert.False(session.TakeDueCompletionReminder(deadline.AddHours(-1)));

        var answered = SwapSession.ReportInPerson(Guid.NewGuid(), Ali, Ayse, Now);
        answered.ConfirmCompleted(Ayse, Now);
        Assert.False(answered.TakeDueCompletionReminder(deadline.AddHours(-1)));
        Assert.False(SwapSession.ReportInPerson(Guid.NewGuid(), Ali, Ayse, Now).TakeDueCompletionReminder(deadline));
    }
}
