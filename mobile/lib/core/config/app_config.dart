abstract final class AppConfig {
  /// Override with `--dart-define=API_BASE_URL=...`.
  ///
  /// The default targets the Android emulator, where 10.0.2.2 is the host
  /// machine's localhost.
  static const apiBaseUrl = String.fromEnvironment(
    'API_BASE_URL',
    defaultValue: 'http://10.0.2.2:5244',
  );

  /// Where crashes are reported (ADR 0013); set with `--dart-define=SENTRY_DSN=...`.
  /// Empty, nothing is reported, as in development and tests.
  static const sentryDsn = String.fromEnvironment('SENTRY_DSN');
}
