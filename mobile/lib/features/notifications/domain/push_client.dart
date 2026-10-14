/// Whether the phone may show this app's notifications.
enum PushPermission {
  granted,
  denied,

  /// Not asked yet.
  notDetermined,

  /// This build has no push service (no Firebase project); nothing to ask for.
  unavailable,
}

/// The phone's push service: the permission, the address of this installation and the
/// notifications the user taps. Tapped notifications arrive as their data payload
/// (docs/realtime.md).
abstract interface class PushClient {
  Future<PushPermission> permission();

  /// Shows the system dialog when it may still be shown; returns the outcome.
  Future<PushPermission> requestPermission();

  /// The token the server sends this installation's pushes to; null if there is none.
  Future<String?> token();

  /// New tokens, when the push service replaces the old one.
  Stream<String> get tokenChanges;

  /// The notification whose tap launched the app, if one did.
  Future<Map<String, String>?> launchTap();

  /// Notifications tapped while the app was running in the background.
  Stream<Map<String, String>> get taps;
}
