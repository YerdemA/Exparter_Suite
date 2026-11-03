import 'package:livekit_client/livekit_client.dart' show VideoTrack;

import '../../../core/api/generated/export.dart';

/// Where a session's video call stands. The server decides; this only says which button
/// to show, so a clock that is a little off costs at most a refused tap.
enum CallWindow {
  /// In-person meetings, and sessions that were never accepted or were cancelled.
  none,

  /// Accepted, but the call opens later (10 minutes before the start).
  upcoming,
  open,

  /// The call closed (15 minutes after the planned end).
  closed,
}

CallWindow callWindowAt(SwapSession session, DateTime now) {
  final opensAt = session.callOpensAt;
  final closesAt = session.callClosesAt;
  final accepted = switch (session.status) {
    SwapSessionStatus.scheduled || SwapSessionStatus.completionPending || SwapSessionStatus.completed => true,
    _ => false,
  };
  if (session.mode != SessionMode.online || !accepted || opensAt == null || closesAt == null) {
    return CallWindow.none;
  }

  if (now.isBefore(opensAt)) {
    return CallWindow.upcoming;
  }

  return now.isBefore(closesAt) ? CallWindow.open : CallWindow.closed;
}

/// The camera or the microphone could not be opened, nearly always because the
/// permission was refused.
class CallMediaException implements Exception {
  const CallMediaException();
}

/// A connection to the media server for one call. Everything it exposes can change at
/// any time; [changes] fires after each change.
abstract interface class CallConnection {
  Stream<void> get changes;

  /// Whether the other user is in the call.
  bool get otherPresent;

  /// Null while the camera is off.
  VideoTrack? get localVideo;

  /// Null while the other user is not there or their camera is off.
  VideoTrack? get remoteVideo;

  bool get reconnecting;

  /// The connection ended without [disconnect], for instance because the server closed
  /// the room when the call window ended or the session was cancelled.
  bool get lost;

  /// Joins with the microphone and camera on. Throws [CallMediaException] if they
  /// cannot be opened.
  Future<void> connect(String url, String token);

  Future<void> setMicrophone({required bool enabled});

  Future<void> setCamera({required bool enabled});

  /// Front to back and back to front.
  Future<void> switchCamera();

  Future<void> disconnect();
}

/// Keeps a call going while the app is in the background: on Android a foreground
/// service with a "call in progress" notification keeps the microphone open, and the
/// screen is kept on while the app is in front.
abstract interface class CallKeepAlive {
  Future<void> start({required String title, required String text});

  Future<void> stop();
}
