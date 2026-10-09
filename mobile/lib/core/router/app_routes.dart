abstract final class AppRoutes {
  static const splash = '/splash';
  static const login = '/login';
  static const register = '/register';
  static const home = '/';

  // Reached from links in emails, so these paths are part of the email contract
  // (see Email:EmailVerificationUrl and Email:PasswordResetUrl on the backend).
  static const verifyEmail = '/verify-email';
  static const resetPassword = '/reset-password';

  static const forgotPassword = '/forgot-password';

  /// Query parameter carrying the one-time token in email links.
  static const tokenParameter = 'token';
}
