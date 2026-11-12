using Exparter.Domain.Sessions;

namespace Exparter.Domain.Tests.Sessions;

internal static class HeldSessions
{
    /// <summary>
    /// A session that took place in the hour before <paramref name="now"/> and that
    /// <paramref name="marker"/> marks as completed at <paramref name="now"/>; the other
    /// user has yet to confirm.
    /// </summary>
    public static SwapSession MarkedCompleted(Guid matchId, Guid marker, Guid other, DateTimeOffset now)
    {
        var planned = now.AddDays(-1);
        var session = SwapSession.Propose(matchId, marker, other, now.AddHours(-1), 60, null, planned);
        session.Accept(other, planned);
        session.MarkCompleted(marker, now);
        return session;
    }
}
