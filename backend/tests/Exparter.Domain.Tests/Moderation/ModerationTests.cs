using Exparter.Domain.Common;
using Exparter.Domain.Identity;
using Exparter.Domain.Moderation;

namespace Exparter.Domain.Tests.Moderation;

public class ModerationDomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Nobody_blocks_or_reports_themselves()
    {
        var user = Guid.NewGuid();

        Assert.Equal("block_self", Assert.Throws<DomainException>(() => Block.Create(user, user, Now)).Code);
        Assert.Equal(
            "report_self",
            Assert.Throws<DomainException>(() => Report.Create(user, ReportTargetType.Message, Guid.NewGuid(), user, "Hi", ReportReason.Spam, null, Now)).Code);
    }

    [Fact]
    public void A_report_is_closed_once()
    {
        var report = Report.Create(Guid.NewGuid(), ReportTargetType.User, Guid.NewGuid(), Guid.NewGuid(), null, ReportReason.Other, "  Rahatsız edici.  ", Now);
        var admin = Guid.NewGuid();

        Assert.Equal("Rahatsız edici.", report.Description);
        Assert.Throws<ArgumentOutOfRangeException>(() => report.Close(ReportStatus.Open, admin, Now));

        report.Close(ReportStatus.Dismissed, admin, Now);

        Assert.Equal((ReportStatus.Dismissed, admin, Now), (report.Status, report.ResolvedByUserId!.Value, report.ResolvedAt!.Value));
        Assert.Equal("report_already_closed", Assert.Throws<DomainException>(() => report.Close(ReportStatus.Resolved, admin, Now)).Code);
    }

    [Fact]
    public void Only_an_active_account_is_suspended_and_only_a_suspended_one_is_restored()
    {
        var user = User.Register("ayse@example.com", "ayse", "hash", ageConfirmed: true, Now);

        user.Suspend();
        Assert.Equal(UserStatus.Suspended, user.Status);
        Assert.Equal("invalid_state", Assert.Throws<DomainException>(user.Suspend).Code);

        user.LiftSuspension();
        Assert.Equal(UserStatus.Active, user.Status);
        Assert.Equal("invalid_state", Assert.Throws<DomainException>(user.LiftSuspension).Code);
    }
}
