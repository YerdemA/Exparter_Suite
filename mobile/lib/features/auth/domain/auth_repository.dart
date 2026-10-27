import '../../../core/api/generated/export.dart';

/// Authentication operations. Failures are thrown as `ApiException`.
abstract interface class AuthRepository {
  Future<AuthResponse> register({
    required String email,
    required String username,
    required String password,
    required bool ageConfirmed,
    required bool termsAccepted,
  });

  /// [identifier] is the account's email address or username.
  Future<AuthResponse> login({required String identifier, required String password});

  /// Revokes the refresh token on the server. Signing out locally must not depend on
  /// this succeeding.
  Future<void> logout({required String refreshToken});

  Future<Account> getAccount();

  /// Schedules the signed-in account for deletion; [password] proves it is the owner
  /// asking. Signing in again within 30 days takes it back.
  Future<void> requestAccountDeletion({required String password});

  /// Sends a new verification email to the signed-in user.
  Future<void> requestEmailVerification();

  Future<void> confirmEmailVerification({required String token});

  /// Succeeds whether or not [email] belongs to an account, so the caller cannot tell.
  Future<void> requestPasswordReset({required String email});

  Future<void> confirmPasswordReset({required String token, required String newPassword});
}
