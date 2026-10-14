import '../../l10n/app_localizations.dart';
import 'api_exception.dart';

/// Turns any error into a message that is safe to show: never raw exception text.
String errorMessage(AppLocalizations l10n, Object error) {
  if (error is! ApiException) {
    return l10n.errorUnknown;
  }

  return switch (error.code) {
    ApiException.networkCode => l10n.errorNetwork,
    ApiException.sessionExpiredCode ||
    'refresh_token_invalid' ||
    'token_expired' ||
    'unauthorized' =>
      l10n.errorSessionExpired,
    'validation_failed' => l10n.errorValidation,
    'invalid_credentials' => l10n.errorInvalidCredentials,
    'account_suspended' => l10n.errorAccountSuspended,
    'email_already_registered' => l10n.errorEmailAlreadyRegistered,
    'email_recently_deleted' => l10n.errorEmailRecentlyDeleted,
    'invalid_password' => l10n.errorInvalidPassword,
    'username_already_taken' => l10n.errorUsernameTaken,
    'username_reserved' => l10n.errorUsernameReserved,
    'invalid_token' => l10n.errorInvalidLink,
    'interaction_requirements_not_met' || 'email_not_verified' => l10n.interactionRequirements,
    'skill_limit_reached' => l10n.skillsLimitReached,
    'user_skill_already_exists' => l10n.errorSkillAlreadyAdded,
    'wanted_level_not_higher' => l10n.errorWantedLevelNotHigher,
    'skill_already_exists' => l10n.errorSkillAlreadyExists,
    'skill_not_approved' => l10n.errorSkillNotApproved,
    'avatar_invalid' || 'avatar_not_uploaded' => l10n.errorAvatarInvalid,
    'email_already_verified' => l10n.errorEmailAlreadyVerified,
    'not_found' => l10n.errorNotFound,
    'swap_request_already_pending' => l10n.errorRequestPending,
    'match_already_exists' => l10n.errorMatchExists,
    'swap_request_cooldown' => l10n.errorRequestCooldown,
    'skill_not_offered' => l10n.errorSkillNotOffered,
    'user_skill_in_use' => l10n.errorSkillInUse,
    'invalid_state' || 'concurrent_update' => l10n.errorInvalidState,
    'not_participant' => l10n.errorNotParticipant,
    'conversation_closed' => l10n.errorConversationClosed,
    'session_in_past' => l10n.errorSessionInPast,
    'session_not_started' => l10n.errorSessionNotStarted,
    'match_end_pending' => l10n.errorMatchEndPending,
    'review_already_exists' => l10n.errorReviewAlreadyExists,
    'session_not_completed' => l10n.errorSessionNotCompleted,
    'in_person_already_reported' => l10n.errorInPersonAlreadyReported,
    'conversation_has_active_match' => l10n.errorConversationHasActiveMatch,
    _ => l10n.errorUnknown,
  };
}
