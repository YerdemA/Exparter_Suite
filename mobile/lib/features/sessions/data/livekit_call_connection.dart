import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:flutter/services.dart';
import 'package:flutter_foreground_task/flutter_foreground_task.dart';
import 'package:livekit_client/livekit_client.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';
import 'package:wakelock_plus/wakelock_plus.dart';

import '../domain/call.dart';

part 'livekit_call_connection.g.dart';

/// A new connection for each call.
@riverpod
CallConnection Function() callConnectionFactory(Ref ref) => LiveKitCallConnection.new;

@riverpod
CallKeepAlive callKeepAlive(Ref ref) => ForegroundCallKeepAlive();

class LiveKitCallConnection implements CallConnection {
  LiveKitCallConnection()
      : _room = Room(
          roomOptions: const RoomOptions(
            // Sends and receives only the quality the other screen needs.
            adaptiveStream: true,
            dynacast: true,
            defaultCameraCaptureOptions: CameraCaptureOptions(cameraPosition: CameraPosition.front),
          ),
        );

  /// Fires when the system ended the screen share by itself (MainActivity.kt).
  static const _captureStops = EventChannel('com.exparter.app/screen_capture');

  final Room _room;
  final _changes = StreamController<void>.broadcast();
  StreamSubscription<void>? _captureStopsSubscription;
  EventsListener<RoomEvent>? _listener;
  bool _reconnecting = false;
  bool _lost = false;
  bool _leaving = false;

  @override
  Stream<void> get changes => _changes.stream;

  @override
  bool get otherPresent => _room.remoteParticipants.isNotEmpty;

  @override
  VideoTrack? get localVideo => _videoOf(_room.localParticipant?.videoTrackPublications, TrackSource.camera);

  @override
  VideoTrack? get remoteVideo => _videoOf(_remoteVideoPublications, TrackSource.camera);

  @override
  VideoTrack? get remoteScreen => _videoOf(_remoteVideoPublications, TrackSource.screenShareVideo);

  @override
  bool get sharingScreen => _room.localParticipant?.isScreenShareEnabled() ?? false;

  List<TrackPublication>? get _remoteVideoPublications =>
      _room.remoteParticipants.values.firstOrNull?.videoTrackPublications;

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
      ..on<LocalTrackUnpublishedEvent>((_) => _notify());

    // A video call is held at arm's length, not at the ear; a headset still takes over.
    await AudioManager.instance.setSpeakerOutputPreferred(true);
    await _room.connect(url, token);
    try {
      await _room.localParticipant!.setMicrophoneEnabled(true);
      await _room.localParticipant!.setCameraEnabled(true);
    } on TrackCreateException {
      throw const CallMediaException();
    }
  }

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
    _room.removeListener(_notify);
    await _listener?.dispose();
    await _room.disconnect();
    await _room.dispose();
    await _changes.close();
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
