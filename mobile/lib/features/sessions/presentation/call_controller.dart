import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:livekit_client/livekit_client.dart' show VideoTrack;
import 'package:riverpod_annotation/riverpod_annotation.dart';

import '../data/api_session_repository.dart';
import '../data/livekit_call_connection.dart';
import '../domain/call.dart';
import '../domain/call_encryption.dart';

part 'call_controller.g.dart';

enum CallPhase {
  connecting,

  /// In the call, alone.
  waiting,
  inCall,
  reconnecting,
  ended,

  /// Could not join; [CallState.error] says why.
  failed,
}

enum CallEnd {
  /// The user hung up.
  left,
  windowClosed,

  /// The connection ended from the other side: the server closed the room (the session
  /// was cancelled, or the window ended) or the network was gone for too long.
  lost,
}

/// Where the call's end-to-end encryption stands with the other user (ADR 0016).
enum CallEncryption {
  /// The other user is not there yet, or their public key has not come.
  pending,

  /// Both devices have the same key; [CallState.safetyCode] is set.
  secured,

  /// The other user is there but sent no public key: their app is too old for encrypted
  /// calls. The call does not go on unencrypted.
  peerOutdated,
}

@immutable
class CallState {
  const CallState({
    this.phase = CallPhase.connecting,
    this.microphoneOn = true,
    this.cameraOn = true,
    this.closingSoon = false,
    this.sharingScreen = false,
    this.encryption = CallEncryption.pending,
    this.safetyCode,
    this.end,
    this.error,
    this.localVideo,
    this.remoteVideo,
    this.remoteScreen,
  });

  /// How long before the call closes the users are told.
  static const closingWarning = Duration(minutes: 5);

  /// How long the other user's public key may take before their app is taken to be too
  /// old for encrypted calls. Key messages arrive within a second.
  static const keyWait = Duration(seconds: 6);

  final CallPhase phase;
  final bool microphoneOn;
  final bool cameraOn;

  /// The call closes within [closingWarning].
  final bool closingSoon;

  /// This user's screen is being sent.
  final bool sharingScreen;

  final CallEncryption encryption;

  /// The code both users can compare aloud; null until [CallEncryption.secured].
  final String? safetyCode;

  final CallEnd? end;
  final Object? error;
  final VideoTrack? localVideo;
  final VideoTrack? remoteVideo;
  final VideoTrack? remoteScreen;
}

/// One video call: joining, the controls, and leaving when the window closes. Keeps the
/// call going in the background with the camera off, and turns the camera back on when
/// the user returns.
@riverpod
class CallController extends _$CallController {
  CallConnection? _connection;
  StreamSubscription<void>? _changes;
  Timer? _warningTimer;
  Timer? _closeTimer;
  // Kept here because a provider cannot be read once this one is being disposed.
  CallKeepAlive? _keepAlive;
  bool _cameraOffForBackground = false;

  /// The keep-alive service declares screen capture; it keeps doing so until the call
  /// ends (see [CallKeepAlive.allowScreenCapture]).
  bool _captureAllowed = false;
  bool _startingShare = false;

  CallKeyAgreement? _agreement;
  StreamSubscription<List<int>>? _keyMessages;
  Timer? _keyTimer;

  /// The other user's public key the current key was agreed with.
  Uint8List? _otherKey;
  bool _otherWasPresent = false;
  CallEncryption _encryption = CallEncryption.pending;
  String? _safetyCode;

  @override
  CallState build(String sessionId) {
    ref.onDispose(() => unawaited(_release()));
    return const CallState();
  }

  /// Joins the call. The notification texts are those of the "call in progress"
  /// notification shown while the app is in the background.
  Future<void> start({required String notificationTitle, required String notificationText}) async {
    if (_connection != null) {
      return;
    }

    try {
      final token = await ref.read(sessionRepositoryProvider).createCallToken(sessionId);
      if (!ref.mounted) {
        return;
      }

      _agreement = await CallKeyAgreement.create();
      if (!ref.mounted) {
        return;
      }

      final connection = _connection = ref.read(callConnectionFactoryProvider)();
      _changes = connection.changes.listen((_) => _sync());
      _keyMessages = connection.keyMessages.listen((message) => unawaited(_onKeyMessage(message)));
      await connection.connect(token.url, token.token);
      if (!ref.mounted) {
        return;
      }

      final keepAlive = ref.read(callKeepAliveProvider);
      _keepAlive = keepAlive;
      await keepAlive.start(title: notificationTitle, text: notificationText);
      // In the call first: the warning is only shown to someone in it.
      _sync();
      _scheduleClose(token.expiresAt);
    } on Object catch (error) {
      if (ref.mounted) {
        state = CallState(phase: CallPhase.failed, error: error);
      }

      await _release();
    }
  }

  Future<void> toggleMicrophone() async {
    final enabled = !state.microphoneOn;
    await _connection?.setMicrophone(enabled: enabled);
    _update(microphoneOn: enabled);
  }

  Future<void> toggleCamera() async {
    final enabled = !state.cameraOn;
    try {
      await _connection?.setCamera(enabled: enabled);
      _update(cameraOn: enabled);
    } on CallMediaException {
      _update(cameraOn: false);
    }
  }

  Future<void> switchCamera() async => _connection?.switchCamera();

  /// Starts or stops sending the screen. Declining the system's capture prompt leaves
  /// things as they were.
  Future<void> toggleScreenShare() async {
    final connection = _connection;
    if (connection == null || !_isLive || _startingShare) {
      return;
    }

    if (state.sharingScreen) {
      await connection.setScreenShare(enabled: false);
      return;
    }

    _startingShare = true;
    try {
      if (!await connection.requestScreenCapture()) {
        return;
      }

      if (!_captureAllowed) {
        await _keepAlive?.allowScreenCapture();
        _captureAllowed = true;
      }

      await connection.setScreenShare(enabled: true);
    } on CallMediaException {
      // The capture could not start; nothing to tell beyond the button staying off.
    } finally {
      _startingShare = false;
      _sync();
    }
  }

  Future<void> leave() => _finish(CallEnd.left);

  /// The app went to the background, where Android does not let it use the camera.
  Future<void> appHidden() async {
    if (_isLive && state.cameraOn) {
      _cameraOffForBackground = true;
      await _connection?.setCamera(enabled: false);
    }
  }

  Future<void> appShown() async {
    if (_cameraOffForBackground) {
      _cameraOffForBackground = false;
      try {
        await _connection?.setCamera(enabled: true);
      } on CallMediaException {
        _update(cameraOn: false);
      }
    }
  }

  bool get _isLive => switch (state.phase) {
        CallPhase.waiting || CallPhase.inCall || CallPhase.reconnecting => true,
        _ => false,
      };

  void _scheduleClose(DateTime closesAt) {
    final untilClose = closesAt.difference(DateTime.now());
    final untilWarning = untilClose - CallState.closingWarning;
    if (untilWarning > Duration.zero) {
      _warningTimer = Timer(untilWarning, () => _update(closingSoon: true));
    } else {
      _update(closingSoon: true);
    }

    _closeTimer = Timer(untilClose.isNegative ? Duration.zero : untilClose, () => _finish(CallEnd.windowClosed));
  }

  void _sync() {
    final connection = _connection;
    if (connection == null || !ref.mounted || state.phase == CallPhase.ended || state.phase == CallPhase.failed) {
      return;
    }

    if (connection.lost) {
      unawaited(_finish(CallEnd.lost));
      return;
    }

    _followPresence(connection);
    state = CallState(
      phase: connection.reconnecting
          ? CallPhase.reconnecting
          : (connection.otherPresent ? CallPhase.inCall : CallPhase.waiting),
      microphoneOn: state.microphoneOn,
      cameraOn: state.cameraOn,
      closingSoon: state.closingSoon,
      sharingScreen: connection.sharingScreen,
      encryption: _encryption,
      safetyCode: _safetyCode,
      localVideo: connection.localVideo,
      remoteVideo: connection.remoteVideo,
      remoteScreen: connection.remoteScreen,
    );
  }

  /// Hands over this device's public key whenever the other user comes into the call,
  /// and forgets the agreed key when they leave: back, they bring a new key pair.
  void _followPresence(CallConnection connection) {
    final present = connection.otherPresent;
    if (present == _otherWasPresent) {
      return;
    }

    _otherWasPresent = present;
    _keyTimer?.cancel();
    if (present) {
      unawaited(_sendKey(connection));
      _keyTimer = Timer(CallState.keyWait, () {
        if (_encryption == CallEncryption.pending) {
          _encryption = CallEncryption.peerOutdated;
          _sync();
        }
      });
    } else {
      _otherKey = null;
      _encryption = CallEncryption.pending;
      _safetyCode = null;
    }
  }

  Future<void> _sendKey(CallConnection connection) async {
    final agreement = _agreement;
    if (agreement == null) {
      return;
    }

    try {
      await connection.sendKeyMessage(agreement.message);
    } on Object {
      // A lost message is made up for: the other user sends their key on arriving, and
      // each new key is answered with this one.
    }
  }

  Future<void> _onKeyMessage(List<int> message) async {
    final agreement = _agreement;
    final connection = _connection;
    final otherKey = CallKeyAgreement.readMessage(message);
    if (agreement == null ||
        connection == null ||
        otherKey == null ||
        listEquals(otherKey, agreement.publicKey) ||
        listEquals(otherKey, _otherKey)) {
      return;
    }

    try {
      final secret = await agreement.agree(otherKey, sessionId);
      await connection.setEncryptionKey(secret.key);
      if (!ref.mounted || _connection != connection) {
        return;
      }

      _otherKey = otherKey;
      _encryption = CallEncryption.secured;
      _safetyCode = secret.safetyCode;
      _keyTimer?.cancel();
      // The other user may have come in after this device's key went out, or lost it.
      await _sendKey(connection);
      _sync();
    } on Object {
      if (ref.mounted) {
        state = const CallState(phase: CallPhase.failed, error: CallEncryptionException());
      }

      await _release();
    }
  }

  void _update({bool? microphoneOn, bool? cameraOn, bool? closingSoon}) {
    if (!ref.mounted || !_isLive) {
      return;
    }

    state = CallState(
      phase: state.phase,
      microphoneOn: microphoneOn ?? state.microphoneOn,
      cameraOn: cameraOn ?? state.cameraOn,
      closingSoon: closingSoon ?? state.closingSoon,
      sharingScreen: state.sharingScreen,
      encryption: state.encryption,
      safetyCode: state.safetyCode,
      localVideo: _connection?.localVideo,
      remoteVideo: _connection?.remoteVideo,
      remoteScreen: _connection?.remoteScreen,
    );
  }

  Future<void> _finish(CallEnd end) async {
    if (ref.mounted && state.phase != CallPhase.failed) {
      state = CallState(phase: CallPhase.ended, end: end);
    }

    await _release();
  }

  /// Safe to call more than once.
  Future<void> _release() async {
    _warningTimer?.cancel();
    _closeTimer?.cancel();
    _keyTimer?.cancel();
    final keyMessages = _keyMessages;
    _keyMessages = null;
    _agreement = null;
    _otherKey = null;
    _otherWasPresent = false;
    await keyMessages?.cancel();
    final changes = _changes;
    final connection = _connection;
    _changes = null;
    _connection = null;
    _captureAllowed = false;
    await changes?.cancel();
    final keepAlive = _keepAlive;
    _keepAlive = null;
    await keepAlive?.stop();

    await connection?.disconnect();
  }
}
