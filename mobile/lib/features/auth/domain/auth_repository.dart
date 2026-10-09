import '../../../core/api/generated/export.dart';

/// Authentication operations. Failures are thrown as `ApiException`.
abstract interface class AuthRepository {
  Future<AuthResponse> register({
    required String email,
    required String username,
    required String password,
    required bool ageConfirmed,
  });

  /// [identifier] is the account's email address or username.
  Future<AuthResponse> login({required String identifier, required String password});

  /// Revokes the refresh token on the server. Signing out locally must not depend on
  /// this succeeding.
  Future<void> logout({required String refreshToken});
}
