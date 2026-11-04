import 'dart:typed_data';

import 'package:livekit_client/livekit_client.dart' show VideoTrack;

import '../../../core/api/generated/export.dart';

/// Where a session's video call stands. The server decides; this only says which button
/// to show, so a clock that is a little off costs at most a refused tap.
enum CallWindow {
  /// In-person meetings, sessions that were never accepted or were cancelled, and
  /// sessions of a match that ended.
  none,

  /// Accepted, but the call opens later (10 minutes before the start).
  upcoming,
  open,

  /// The call closed (15 minutes after the planned end).
  closed,
}

/// A session marked completed keeps its call, but not once its match has ended: the
/// server then refuses to let anyone in.
CallWindow callWindowAt(SwapSession session, DateTime now, {bool matchEnded = false}) {
  final opensAt = session.callOpensAt;
  final closesAt = session.callClosesAt;
  final accepted = switch (session.status) {
    SwapSessionStatus.scheduled || SwapSessionStatus.completionPending || SwapSessionStatus.completed => true,
    _ => false,
  };
  if (matchEnded || session.mode != SessionMode.online || !accepted || opensAt == null || closesAt == null) {
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

  /// The other user's shared screen; null while they are not sharing.
  VideoTrack? get remoteScreen;

  /// Whether this user's screen is being sent. Becomes false by itself when the system
  /// stops the capture, for instance from its own "stop sharing" control.
  bool get sharingScreen;

  bool get reconnecting;

  /// The connection ended without [disconnect], for instance because the server closed
  /// the room when the call window ended or the session was cancelled.
  bool get lost;

  /// Joins with the microphone and camera on and the media encrypted. Until
  /// [setEncryptionKey], the key is a random one nobody else has, so nothing leaves the
  /// device unencrypted. Throws [CallMediaException] if the microphone or camera cannot
  /// be opened, and CallEncryptionException if encryption cannot be set up.
  Future<void> connect(String url, String token);

  /// The messages the other user sends with their public key (ADR 0016).
  Stream<List<int>> get keyMessages;

  Future<void> sendKeyMessage(List<int> message);

  /// The key both users agreed on; media is encrypted and decrypted with it from now on.
  Future<void> setEncryptionKey(Uint8List key);

  Future<void> setMicrophone({required bool enabled});

  Future<void> setCamera({required bool enabled});

  /// Front to back and back to front.
  Future<void> switchCamera();

  /// Asks the system for permission to capture the screen; false if the user declined.
  /// Needed before each [setScreenShare] that turns sharing on.
  Future<bool> requestScreenCapture();

  /// Sends the screen without the device's sound, or stops sending it.
  Future<void> setScreenShare({required bool enabled});

  Future<void> disconnect();
}

/// Keeps a call going while the app is in the background: on Android a foreground
/// service with a "call in progress" notification keeps the microphone open, and the
/// screen is kept on while the app is in front.
abstract interface class CallKeepAlive {
  Future<void> start({required String title, required String text});

  /// Android only lets the screen be captured while the service declares it. Called
  /// after the user allowed the capture and before it starts. The declaration then stays
  /// until [stop]: taking it back means starting the service again, which Android does
  /// not allow while the app is in the background, where sharing often ends.
  Future<void> allowScreenCapture();

  Future<void> stop();
}
