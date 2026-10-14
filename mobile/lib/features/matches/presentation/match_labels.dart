import '../../../core/api/generated/export.dart';
import '../../../l10n/app_localizations.dart';

/// The name to show for a user mentioned in a request or match.
String userLabel(AppLocalizations l10n, UserSummary user) =>
    user.displayName ?? (user.username == null ? l10n.deletedUser : '@${user.username}');

/// How long until [deadline], rounded down to whole days or hours.
String remainingLabel(AppLocalizations l10n, DateTime deadline, DateTime now) {
  final left = deadline.difference(now);
  if (left.inDays >= 1) {
    return l10n.remainingDays(left.inDays);
  }

  return left.inHours >= 1 ? l10n.remainingHours(left.inHours) : l10n.remainingSoon;
}

String matchStatusLabel(AppLocalizations l10n, MatchStatus status) => switch (status) {
      MatchStatus.active => l10n.matchStatusActive,
      MatchStatus.endRequested => l10n.matchStatusEndRequested,
      MatchStatus.ended => l10n.matchStatusEnded,
      MatchStatus.$unknown => l10n.skillLevelUnknown,
    };

String matchEndReasonLabel(AppLocalizations l10n, MatchEndReason? reason) => switch (reason) {
      MatchEndReason.mutualConsent => l10n.matchEndedMutual,
      MatchEndReason.endRequestExpired => l10n.matchEndedExpired,
      MatchEndReason.blocked => l10n.matchEndedBlocked,
      _ => l10n.matchEndedOther,
    };

/// A match as one of its two users sees it.
class MatchView {
  MatchView(Match match, String? ownUserId)
      : _isUserA = match.userA.userId == ownUserId,
        _match = match;

  final Match _match;
  final bool _isUserA;

  UserSummary get other => _isUserA ? _match.userB : _match.userA;

  /// The skill the viewer teaches.
  SkillRef get taught => _isUserA ? _match.skillTaughtByA : _match.skillTaughtByB;

  /// The skill the viewer learns.
  SkillRef get learned => _isUserA ? _match.skillTaughtByB : _match.skillTaughtByA;

  bool get endRequestedByOther => _match.status == MatchStatus.endRequested && _match.endRequestedByUserId == other.userId;
}
