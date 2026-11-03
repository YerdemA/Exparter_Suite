import 'dart:async';

import 'package:exparter/core/api/api_exception.dart';
import 'package:exparter/core/api/api_providers.dart';
import 'package:exparter/core/api/generated/export.dart';
import 'package:exparter/core/auth/session_store.dart';
import 'package:exparter/features/reviews/data/api_review_repository.dart';
import 'package:exparter/features/sessions/data/api_session_repository.dart';
import 'package:exparter/features/sessions/data/livekit_call_connection.dart';
import 'package:exparter/features/sessions/domain/call.dart';
import 'package:exparter/features/sessions/presentation/call_controller.dart';
import 'package:exparter/features/sessions/presentation/call_screen.dart';
import 'package:exparter/features/sessions/presentation/match_sessions_section.dart';
import 'package:exparter/l10n/app_localizations_tr.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_riverpod/misc.dart' show Override;
import 'package:flutter_test/flutter_test.dart';
import 'package:livekit_client/livekit_client.dart' show VideoTrack;
import 'package:mocktail/mocktail.dart';

import '../../helpers.dart';

/// A media server connection that tests drive by hand. [log] is shared with the
/// [FakeKeepAlive], to check the order of the steps of screen sharing.
class FakeCallConnection implements CallConnection {
  FakeCallConnection(this.log);

  final List<String> log;
  final _changes = StreamController<void>.broadcast();
  bool captureAllowed = true;
  Object? screenError;

  Object? connectError;
  String? connectedWith;
  bool disconnected = false;
  bool? microphone;
  bool? camera;
  Object? cameraError;
  int switches = 0;

  @override
  bool otherPresent = false;

  @override
  bool reconnecting = false;

  @override
  bool lost = false;

  @override
  VideoTrack? get localVideo => null;

  @override
  VideoTrack? get remoteVideo => null;

  @override
  VideoTrack? get remoteScreen => null;

  @override
  bool sharingScreen = false;

  @override
  Stream<void> get changes => _changes.stream;

  void change({bool? otherPresent, bool? reconnecting, bool? lost, bool? sharingScreen}) {
    this.otherPresent = otherPresent ?? this.otherPresent;
    this.sharingScreen = sharingScreen ?? this.sharingScreen;
    this.reconnecting = reconnecting ?? this.reconnecting;
    this.lost = lost ?? this.lost;
    _changes.add(null);
  }

  @override
  Future<void> connect(String url, String token) async {
    if (connectError case final error?) {
      throw error;
    }

    connectedWith = '$url $token';
    microphone = true;
    camera = true;
  }

  @override
  Future<void> setMicrophone({required bool enabled}) async => microphone = enabled;

  @override
  Future<void> setCamera({required bool enabled}) async {
    if (enabled && cameraError != null) {
      throw cameraError!;
    }

    camera = enabled;
  }

  @override
  Future<void> switchCamera() async => switches++;

  @override
  Future<bool> requestScreenCapture() async {
    log.add('prompt');
    return captureAllowed;
  }

  @override
  Future<void> setScreenShare({required bool enabled}) async {
    log.add('share $enabled');
    if (enabled && screenError != null) {
      throw screenError!;
    }

    change(sharingScreen: enabled);
  }

  @override
  Future<void> disconnect() async => disconnected = true;
}

class FakeKeepAlive implements CallKeepAlive {
  FakeKeepAlive(this.log);

  final List<String> log;
  String? started;
  bool stopped = false;

  @override
  Future<void> allowScreenCapture() async => log.add('capture');

  @override
  Future<void> start({required String title, required String text}) async => started = '$title / $text';

  @override
  Future<void> stop() async => stopped = true;
}

void main() {
  final l10n = AppLocalizationsTr();
  final now = DateTime.now();

  late MockSessionRepository repository;
  late FakeCallConnection connection;
  late FakeKeepAlive keepAlive;
  late List<String> log;

  SwapSession session({
    SessionMode mode = SessionMode.online,
    SwapSessionStatus status = SwapSessionStatus.scheduled,
    Duration startsIn = const Duration(minutes: 5),
  }) {
    final start = now.add(startsIn);
    final online = mode == SessionMode.online;
    return SwapSession(
      id: 'session-1',
      matchId: 'match-1',
      proposedByUserId: 'user-deniz',
      mode: mode,
      status: status,
      completedAutomatically: false,
      createdAt: now,
      scheduledAt: online ? start : null,
      durationMinutes: online ? 60 : null,
      callOpensAt: online ? start.subtract(const Duration(minutes: 10)) : null,
      callClosesAt: online ? start.add(const Duration(minutes: 75)) : null,
    );
  }

  void stubToken({Duration closesIn = const Duration(hours: 1)}) =>
      when(() => repository.createCallToken('session-1')).thenAnswer(
        (_) async => CallToken(url: 'wss://rtc.test', token: 'secret', expiresAt: DateTime.now().add(closesIn)),
      );

  List<Override> overrides() => [
        sessionRepositoryProvider.overrideWithValue(repository),
        reviewRepositoryProvider.overrideWithValue(quietReviewRepository()),
        sessionStoreProvider.overrideWithValue(SessionStore(InMemorySessionPersistence())),
        callConnectionFactoryProvider.overrideWithValue(() => connection),
        callKeepAliveProvider.overrideWithValue(keepAlive),
      ];

  setUp(() {
    repository = MockSessionRepository();
    log = [];
    connection = FakeCallConnection(log);
    keepAlive = FakeKeepAlive(log);
    when(() => repository.listMatchSessions(any())).thenAnswer((_) async => []);
    stubToken();
  });

  group('callWindowAt', () {
    test('only an accepted online session that was not cancelled has a call', () {
      expect(callWindowAt(session(mode: SessionMode.inPerson), now), CallWindow.none);
      expect(callWindowAt(session(status: SwapSessionStatus.proposed), now), CallWindow.none);
      expect(callWindowAt(session(status: SwapSessionStatus.cancelled), now), CallWindow.none);
      expect(callWindowAt(session(status: SwapSessionStatus.completionPending), now), CallWindow.open);
      expect(callWindowAt(session(status: SwapSessionStatus.completed), now), CallWindow.open);
    });

    test('the call opens ten minutes before the start and closes fifteen minutes after the end', () {
      final scheduled = session(startsIn: const Duration(hours: 1));
      final start = scheduled.scheduledAt!;

      expect(callWindowAt(scheduled, start.subtract(const Duration(minutes: 11))), CallWindow.upcoming);
      expect(callWindowAt(scheduled, start.subtract(const Duration(minutes: 10))), CallWindow.open);
      expect(callWindowAt(scheduled, start.add(const Duration(minutes: 74))), CallWindow.open);
      expect(callWindowAt(scheduled, start.add(const Duration(minutes: 75))), CallWindow.closed);
    });
  });

  group('CallController', () {
    late ProviderContainer container;

    CallController controller() => container.read(callControllerProvider('session-1').notifier);

    CallState state() => container.read(callControllerProvider('session-1'));

    Future<void> start() => controller().start(notificationTitle: 'title', notificationText: 'text');

    setUp(() {
      container = ProviderContainer.test(overrides: overrides());
      container.listen(callControllerProvider('session-1'), (_, _) {});
    });

    test('joins with the token, keeps the call alive and waits for the other user', () async {
      await start();

      expect(connection.connectedWith, 'wss://rtc.test secret');
      expect(keepAlive.started, 'title / text');
      expect(state().phase, CallPhase.waiting);
      expect((state().microphoneOn, state().cameraOn, state().closingSoon), (true, true, false));

      connection.change(otherPresent: true);
      await pumpEventQueue();
      expect(state().phase, CallPhase.inCall);

      connection.change(reconnecting: true);
      await pumpEventQueue();
      expect(state().phase, CallPhase.reconnecting);
    });

    test('hanging up disconnects and lets the app sleep again', () async {
      await start();

      await controller().leave();

      expect((state().phase, state().end), (CallPhase.ended, CallEnd.left));
      expect((connection.disconnected, keepAlive.stopped), (true, true));
    });

    test('a connection closed by the server ends the call', () async {
      await start();

      connection.change(lost: true);
      await pumpEventQueue();

      expect((state().phase, state().end), (CallPhase.ended, CallEnd.lost));
      expect((connection.disconnected, keepAlive.stopped), (true, true));
    });

    test('a refused token or a refused permission fails the call without keeping it alive', () async {
      when(() => repository.createCallToken('session-1'))
          .thenThrow(const ApiException(statusCode: 409, code: 'call_window_closed'));
      await start();
      expect(state().phase, CallPhase.failed);
      expect((state().error! as ApiException).code, 'call_window_closed');
      expect(connection.connectedWith, isNull);

      stubToken();
      connection.connectError = const CallMediaException();
      container.invalidate(callControllerProvider('session-1'));
      await start();
      expect(state().phase, CallPhase.failed);
      expect(state().error, isA<CallMediaException>());
      expect(connection.disconnected, isTrue);
      expect(keepAlive.started, isNull);
    });

    test('the microphone and the camera are turned off and on', () async {
      await start();

      await controller().toggleMicrophone();
      await controller().toggleCamera();
      expect((state().microphoneOn, state().cameraOn), (false, false));
      expect((connection.microphone, connection.camera), (false, false));

      connection.cameraError = const CallMediaException();
      await controller().toggleCamera();
      expect(state().cameraOn, isFalse);

      await controller().switchCamera();
      expect(connection.switches, 1);
    });

    test('in the background the camera pauses and comes back, unless the user had turned it off', () async {
      await start();

      await controller().appHidden();
      expect((connection.camera, state().cameraOn), (false, true));
      await controller().appShown();
      expect(connection.camera, isTrue);

      await controller().toggleCamera();
      await controller().appHidden();
      await controller().appShown();
      expect(connection.camera, isFalse);
    });

    test('the screen is shared once the system allowed it and the service declares the capture', () async {
      await start();

      await controller().toggleScreenShare();
      expect(state().sharingScreen, isTrue);
      expect(log, ['prompt', 'capture', 'share true']);

      await controller().toggleScreenShare();
      await pumpEventQueue();
      expect(state().sharingScreen, isFalse);

      // The service keeps declaring the capture; sharing again only asks the user again.
      log.clear();
      await controller().toggleScreenShare();
      expect(log, ['prompt', 'share true']);
    });

    test('a declined prompt or a failed capture leaves the call as it was', () async {
      await start();

      connection.captureAllowed = false;
      await controller().toggleScreenShare();
      expect(log, ['prompt']);
      expect(state().sharingScreen, isFalse);

      connection
        ..captureAllowed = true
        ..screenError = const CallMediaException();
      log.clear();
      await controller().toggleScreenShare();
      await pumpEventQueue();
      expect(log, ['prompt', 'capture', 'share true']);
      expect((state().sharingScreen, state().phase), (false, CallPhase.waiting));
    });

    test('sharing that the system stopped by itself shows as stopped', () async {
      await start();
      await controller().toggleScreenShare();

      connection.change(sharingScreen: false);
      await pumpEventQueue();

      expect(state().sharingScreen, isFalse);
    });

    test('the users are warned before the window closes and the call ends when it does', () async {
      stubToken(closesIn: const Duration(milliseconds: 100));
      await start();
      expect(state().closingSoon, isTrue);

      await Future<void>.delayed(const Duration(milliseconds: 200));

      expect((state().phase, state().end), (CallPhase.ended, CallEnd.windowClosed));
      expect(connection.disconnected, isTrue);
    });
  });

  group('screens', () {
    Future<void> pump(WidgetTester tester, Widget child) async {
      await tester.pumpWidget(ProviderScope(overrides: overrides(), child: routed(child)));
      await tester.pumpAndSettle();
    }

    Match match() => Match(
          id: 'match-1',
          swapRequestId: 'request-1',
          userA: const UserSummary(userId: 'user-deniz', isDeleted: false, username: 'deniz'),
          userB: UserSummary(userId: testAccount().id, isDeleted: false, username: 'ayse'),
          skillTaughtByA: const SkillRef(id: 's1', name: 'İspanyolca'),
          skillTaughtByB: const SkillRef(id: 's2', name: 'Gitar'),
          status: MatchStatus.active,
          createdAt: DateTime.utc(2026, 10, 13),
        );

    testWidgets('an open call can be joined from the session card', (tester) async {
      when(() => repository.listMatchSessions('match-1')).thenAnswer((_) async => [session()]);
      await pump(tester, Scaffold(body: SingleChildScrollView(child: MatchSessionsSection(match: match()))));

      expect(find.text(l10n.sessionCallPermissionHint), findsOneWidget);
      await tester.ensureVisible(find.text(l10n.sessionJoinCall));
      await tester.tap(find.text(l10n.sessionJoinCall));
      await tester.pumpAndSettle();

      expect(find.text('route:/calls/session-1'), findsOneWidget);
    });

    testWidgets('a later call says when it opens and cannot be joined yet', (tester) async {
      final later = session(startsIn: const Duration(hours: 3));
      when(() => repository.listMatchSessions('match-1')).thenAnswer((_) async => [later]);
      await pump(tester, Scaffold(body: SingleChildScrollView(child: MatchSessionsSection(match: match()))));

      expect(find.textContaining('itibarıyla katılabilirsiniz'), findsOneWidget);
      expect(find.text(l10n.sessionJoinCall), findsNothing);
    });

    testWidgets('the call screen waits for the other user and ends with a way back', (tester) async {
      await pump(tester, const CallScreen(sessionId: 'session-1'));

      expect(find.text(l10n.callWaiting), findsOneWidget);
      expect(keepAlive.started, '${l10n.callNotificationTitle} / ${l10n.callNotificationText}');

      connection.change(otherPresent: true);
      await tester.pumpAndSettle();
      expect(find.text(l10n.callRemoteCameraOff), findsOneWidget);

      await tester.tap(find.byTooltip(l10n.callShareScreen));
      await tester.pumpAndSettle();
      expect(find.text(l10n.callSharingScreen), findsOneWidget);
      expect(find.byTooltip(l10n.callStopSharing), findsOneWidget);

      await tester.tap(find.byTooltip(l10n.callEnd));
      await tester.pumpAndSettle();
      expect(find.text(l10n.callEndedLeft), findsOneWidget);
      expect(find.text(l10n.callBack), findsOneWidget);
    });

    testWidgets('a refused permission is explained instead of shown raw', (tester) async {
      connection.connectError = const CallMediaException();
      await pump(tester, const CallScreen(sessionId: 'session-1'));

      expect(find.text(l10n.callMediaDenied), findsOneWidget);
    });
  });
}
