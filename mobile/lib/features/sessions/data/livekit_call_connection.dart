import 'dart:async';
import 'dart:convert';
import 'dart:math';

import 'package:flutter/foundation.dart';
import 'package:flutter/services.dart';
import 'package:flutter_foreground_task/flutter_foreground_task.dart';
import 'package:livekit_client/livekit_client.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';
import 'package:wakelock_plus/wakelock_plus.dart';

import '../domain/call.dart';
import '../domain/call_encryption.dart';

part 'livekit_call_connection.g.dart';

/// A new connection for each call.
@riverpod
CallConnection Function() callConnectionFactory(Ref ref) => LiveKitCallConnection.new;

@riverpod
CallKeepAlive callKeepAlive(Ref ref) => ForegroundCallKeepAlive();

class LiveKitCallConnection implements CallConnection {
  LiveKitCallConnection();

  /// Fires when the system ended the screen share by itself (MainActivity.kt).
  static const _captureStops = EventChannel('com.exparter.app/screen_capture');

  /// Made in [connect], since setting up encryption takes a moment.
  Room? _roomOrNull;
  BaseKeyProvider? _keyProvider;
  final _changes = StreamController<void>.broadcast();
  final _keyMessages = StreamController<List<int>>.broadcast();

  /// The other user's key attribute last passed on, so that an unrelated attribute
  /// change does not pass it on again.
  String? _otherKey;
  StreamSubscription<void>? _captureStopsSubscription;
  EventsListener<RoomEvent>? _listener;
  bool _reconnecting = false;
  bool _lost = false;
  bool _leaving = false;

  @override
  Stream<void> get changes => _changes.stream;

  @override
  Stream<List<int>> get keyMessages => _keyMessages.stream;

  Room get _room => _roomOrNull!;

  @override
  bool get otherPresent => _roomOrNull?.remoteParticipants.isNotEmpty ?? false;

  @override
  VideoTrack? get localVideo => _videoOf(_roomOrNull?.localParticipant?.videoTrackPublications, TrackSource.camera);

  @override
  VideoTrack? get remoteVideo => _videoOf(_remoteVideoPublications, TrackSource.camera);

  @override
  VideoTrack? get remoteScreen => _videoOf(_remoteVideoPublications, TrackSource.screenShareVideo);

  @override
  bool get sharingScreen => _roomOrNull?.localParticipant?.isScreenShareEnabled() ?? false;

  List<TrackPublication>? get _remoteVideoPublications =>
      _roomOrNull?.remoteParticipants.values.firstOrNull?.videoTrackPublications;

  @override
  bool get reconnecting => _reconnecting;

  @override
  bool get lost => _lost;

  static VideoTrack? _videoOf(List<TrackPublication>? publications, TrackSource source) => publications
      ?.where((publication) => publication.source == source && !publication.muted)
      .map((publication) => publication.track)
      .whereType<VideoTrack>()
      .firstOrNull;

  @override
  Future<void> connect(String url, String token) async {
    try {
      final keyProvider = _keyProvider = await BaseKeyProvider.create(
        // Without a key the cryptor would pass frames on as they are.
        discardFrameWhenCryptorNotReady: true,
      );
      final random = Random.secure();
      await keyProvider.setRawKey(Uint8List.fromList(List.generate(32, (_) => random.nextInt(256))));
      _roomOrNull = Room(
        roomOptions: RoomOptions(
          // Sends and receives only the quality the other screen needs.
          adaptiveStream: true,
          dynacast: true,
          defaultCameraCaptureOptions: const CameraCaptureOptions(cameraPosition: CameraPosition.front),
          encryption: E2EEOptions(keyProvider: keyProvider),
        ),
      );
    } on Object {
      throw const CallEncryptionException();
    }

    _room.addListener(_notify);
    _listener = _room.createListener()
      ..on<RoomReconnectingEvent>((_) => _setReconnecting(true))
      ..on<RoomReconnectedEvent>((_) => _setReconnecting(false))
      ..on<RoomDisconnectedEvent>((_) {
        _lost = !_leaving;
        _notify();
      })
      ..on<TrackSubscribedEvent>((_) => _notify())
      ..on<TrackUnsubscribedEvent>((_) => _notify())
      ..on<TrackMutedEvent>((_) => _notify())
      ..on<TrackUnmutedEvent>((_) => _notify())
      ..on<LocalTrackPublishedEvent>((_) => _notify())
      ..on<LocalTrackUnpublishedEvent>((_) => _notify())
      ..on<ParticipantConnectedEvent>((_) => _passOnOtherKey())
      ..on<ParticipantAttributesChanged>((_) => _passOnOtherKey())
      ..on<ParticipantDisconnectedEvent>((_) => _otherKey = null);

    // A video call is held at arm's length, not at the ear; a headset still takes over.
    await AudioManager.instance.setSpeakerOutputPreferred(true);
    await _room.connect(url, token);
    // Someone already in the room set their key before this device came in.
    _passOnOtherKey();
    try {
      await _room.localParticipant!.setMicrophoneEnabled(true);
      await _room.localParticipant!.setCameraEnabled(true);
    } on TrackCreateException {
      throw const CallMediaException();
    }
  }

  /// The public key goes as a participant attribute, not as a data message: with
  /// encryption on, data messages are encrypted with the very key that is not agreed yet.
  /// Attributes travel with the signalling, and a user who comes in later finds the key
  /// already there.
  @override
  Future<void> sendKeyMessage(List<int> message) async =>
      _roomOrNull?.localParticipant?.setAttributes({callKeyTopic: utf8.decode(message)});

  void _passOnOtherKey() {
    final key = _roomOrNull?.remoteParticipants.values.firstOrNull?.attributes[callKeyTopic];
    if (key != null && key != _otherKey && !_keyMessages.isClosed) {
      _otherKey = key;
      _keyMessages.add(utf8.encode(key));
    }
  }

  @override
  Future<void> setEncryptionKey(Uint8List key) async => _keyProvider?.setRawKey(key);

  @override
  Future<void> setMicrophone({required bool enabled}) async {
    await _room.localParticipant?.setMicrophoneEnabled(enabled);
    _notify();
  }

  @override
  Future<void> setCamera({required bool enabled}) async {
    try {
      await _room.localParticipant?.setCameraEnabled(enabled);
    } on TrackCreateException {
      throw const CallMediaException();
    } finally {
      _notify();
    }
  }

  @override
  Future<void> switchCamera() async {
    final track = _room.localParticipant?.videoTrackPublications.firstOrNull?.track;
    final options = track?.currentOptions;
    if (track is LocalVideoTrack && options is CameraCaptureOptions) {
      await track.setCameraPosition(options.cameraPosition.switched());
      _notify();
    }
  }

  // The SDK's own way to ask for screen capture on Android, marked experimental by it.
  @override
  // ignore: experimental_member_use
  Future<bool> requestScreenCapture() => Hardware.instance.requestCapturePermission();

  @override
  Future<void> setScreenShare({required bool enabled}) async {
    try {
      await _room.localParticipant?.setScreenShareEnabled(enabled);
      await _captureStopsSubscription?.cancel();
      _captureStopsSubscription = null;
      if (enabled && defaultTargetPlatform == TargetPlatform.android) {
        _captureStopsSubscription =
            _captureStops.receiveBroadcastStream().listen((_) => unawaited(setScreenShare(enabled: false)));
      }
    } on TrackCreateException {
      throw const CallMediaException();
    } finally {
      _notify();
    }
  }

  @override
  Future<void> disconnect() async {
    _leaving = true;
    await _captureStopsSubscription?.cancel();
    await _listener?.dispose();
    final room = _roomOrNull;
    if (room != null) {
      room.removeListener(_notify);
      await room.disconnect();
      await room.dispose();
    }

    await _changes.close();
    await _keyMessages.close();
  }

  void _setReconnecting(bool value) {
    _reconnecting = value;
    _notify();
  }

  void _notify() {
    if (!_changes.isClosed) {
      _changes.add(null);
    }
  }
}

class ForegroundCallKeepAlive implements CallKeepAlive {
  ForegroundCallKeepAlive();

  String _title = '';
  String _text = '';

  @override
  Future<void> start({required String title, required String text}) async {
    _title = title;
    _text = text;
    await WakelockPlus.enable();
    FlutterForegroundTask.init(
      androidNotificationOptions: AndroidNotificationOptions(
        channelId: 'call',
        channelName: title,
        onlyAlertOnce: true,
      ),
      iosNotificationOptions: const IOSNotificationOptions(showNotification: false),
      // The service only holds the microphone open; it has no work of its own.
      foregroundTaskOptions: ForegroundTaskOptions(eventAction: ForegroundTaskEventAction.nothing()),
    );
    if (!await FlutterForegroundTask.isRunningService) {
      await FlutterForegroundTask.startService(
        serviceTypes: [ForegroundServiceTypes.microphone],
        notificationTitle: title,
        notificationText: text,
      );
    }
  }

  /// A running service cannot add to what it declares, so it is started again while the
  /// app is in front; the notification blinks once, and the call is not affected.
  @override
  Future<void> allowScreenCapture() async {
    if (!await FlutterForegroundTask.isRunningService) {
      return;
    }

    await FlutterForegroundTask.stopService();
    await FlutterForegroundTask.startService(
      serviceTypes: [ForegroundServiceTypes.microphone, ForegroundServiceTypes.mediaProjection],
      notificationTitle: _title,
      notificationText: _text,
    );
  }

  @override
  Future<void> stop() async {
    await WakelockPlus.disable();
    await FlutterForegroundTask.stopService();
  }
}
