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
    'username_already_taken' => l10n.errorUsernameTaken,
    'username_reserved' => l10n.errorUsernameReserved,
    _ => l10n.errorUnknown,
  };
}
