import 'package:exparter/core/api/api_exception.dart';
import 'package:exparter/core/api/api_providers.dart';
import 'package:exparter/core/api/generated/export.dart' hide Notification;
import 'package:exparter/core/api/generated/export.dart' as api show Notification;
import 'package:exparter/core/auth/session_store.dart';
import 'package:exparter/core/realtime/realtime_client.dart';
import 'package:exparter/features/discovery/data/api_discovery_repository.dart';
import 'package:exparter/features/matches/data/api_match_repository.dart';
import 'package:exparter/features/matches/presentation/match_controller.dart';
import 'package:exparter/features/notifications/data/api_notification_repository.dart';
import 'package:exparter/features/notifications/presentation/notification_controller.dart';
import 'package:exparter/features/notifications/presentation/notifications_screen.dart';
import 'package:exparter/l10n/app_localizations_tr.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_riverpod/misc.dart' show Override;
import 'package:flutter_test/flutter_test.dart';
import 'package:mocktail/mocktail.dart';

import '../../helpers.dart';

void main() {
  final l10n = AppLocalizationsTr();
  const denizId = 'user-deniz';

  late MockNotificationRepository repository;
  late MockDiscoveryRepository discoveryRepository;
  late MockMatchRepository matchRepository;
  late FakeRealtimeClient realtime;
  late InMemorySessionPersistence persistence;

  api.Notification notification({
    String id = 'n1',
    NotificationType type = NotificationType.swapRequestReceived,
    bool read = false,
    String? actor = denizId,
    String? matchId,
    String? reason,
    DateTime? deadline,
  }) =>
      api.Notification(
        id: id,
        type: type,
        data: NotificationData(actorUserId: actor, matchId: matchId, reason: reason, deadline: deadline),
        createdAt: DateTime.utc(2026, 10, 13, 9),
        readAt: read ? DateTime.utc(2026, 10, 13, 10) : null,
      );

  void stubList(List<api.Notification> Function() items) => when(repository.list).thenAnswer((_) async => items());

  setUp(() async {
    repository = MockNotificationRepository();
    discoveryRepository = MockDiscoveryRepository();
    matchRepository = MockMatchRepository();
    realtime = FakeRealtimeClient();
    persistence = InMemorySessionPersistence();
    await SessionStore(persistence).save(testSession());
    stubList(() => []);
    when(() => repository.markRead(any())).thenAnswer((_) async {});
    when(repository.markAllRead).thenAnswer((_) async {});
    when(() => discoveryRepository.getUserProfile(denizId)).thenAnswer(
      (_) async => const PublicProfile(
        userId: denizId,
        username: 'deniz',
        displayName: 'Deniz Kaya',
        rating: RatingSummary(reviewCount: 0),
      ),
    );
    when(matchRepository.listMatches).thenAnswer((_) async => []);
  });

  List<Override> overrides() => [
        notificationRepositoryProvider.overrideWithValue(repository),
        discoveryRepositoryProvider.overrideWithValue(discoveryRepository),
        matchRepositoryProvider.overrideWithValue(matchRepository),
        realtimeClientProvider.overrideWithValue(realtime),
        sessionStoreProvider.overrideWithValue(SessionStore(persistence)),
      ];

  Future<void> pump(WidgetTester tester, Widget child) async {
    await tester.pumpWidget(ProviderScope(overrides: overrides(), child: routed(child)));
    await tester.pumpAndSettle();
  }

  group('Notifications', () {
    late ProviderContainer container;

    setUp(() {
      container = ProviderContainer.test(overrides: overrides());
      container.listen(notificationsProvider, (_, _) {});
      container.listen(unreadNotificationCountProvider, (_, _) {});
    });

    test('counts the unread ones', () async {
      stubList(() => [notification(), notification(id: 'n2', read: true), notification(id: 'n3')]);

      await container.read(notificationsProvider.future);

      expect(container.read(unreadNotificationCountProvider), 2);
    });

    test('reloads when a notification arrives and after a gap in the connection', () async {
      await container.read(notificationsProvider.future);
      stubList(() => [notification()]);

      realtime.emit(NotificationReceived(notification()));
      await Future<void>.delayed(Duration.zero);
      expect(await container.read(notificationsProvider.future), hasLength(1));

      stubList(() => [notification(), notification(id: 'n2')]);
      realtime.emit(const RealtimeReconnected());
      await Future<void>.delayed(Duration.zero);
      expect(await container.read(notificationsProvider.future), hasLength(2));
    });

    test('marking one as read shows at once and tells the server', () async {
      stubList(() => [notification(), notification(id: 'n2')]);
      await container.read(notificationsProvider.future);

      final pending = container.read(notificationsProvider.notifier).markRead('n1');

      expect(container.read(unreadNotificationCountProvider), 1);
      await pending;
      verify(() => repository.markRead('n1')).called(1);

      // One that is already read is left alone.
      await container.read(notificationsProvider.notifier).markRead('n1');
      verifyNever(() => repository.markRead('n1'));
    });

    test('marking all as read reloads the list and reports a failure', () async {
      stubList(() => [notification()]);
      await container.read(notificationsProvider.future);
      stubList(() => [notification(read: true)]);

      expect(await container.read(notificationsProvider.notifier).markAllRead(), isTrue);
      await container.read(notificationsProvider.future);
      expect(container.read(unreadNotificationCountProvider), 0);

      when(repository.markAllRead).thenThrow(const ApiException(statusCode: 500, code: 'internal_error'));
      expect(await container.read(notificationsProvider.notifier).markAllRead(), isFalse);
    });

    test('a notification reloads the lists it may have changed', () async {
      container
        ..listen(notificationSyncProvider, (_, _) {})
        ..listen(matchesProvider, (_, _) {});
      await container.read(matchesProvider.future);

      realtime.emit(const RealtimeReconnected());
      await Future<void>.delayed(Duration.zero);
      await container.read(matchesProvider.future);
      verify(matchRepository.listMatches).called(1);

      realtime.emit(NotificationReceived(notification(type: NotificationType.swapRequestAccepted)));
      await Future<void>.delayed(Duration.zero);
      await container.read(matchesProvider.future);
      verify(matchRepository.listMatches).called(1);
    });
  });

  group('notificationTarget', () {
    test('leads to the resource the notification is about', () {
      expect(notificationTarget(notification()), '/matches');
      expect(notificationTarget(notification(type: NotificationType.sessionProposed, matchId: 'm1')), '/matches/m1');
      expect(notificationTarget(notification(type: NotificationType.matchEnded, matchId: 'm1')), '/matches/m1');
      expect(notificationTarget(notification(type: NotificationType.reviewReceived)), '/profile');
      expect(notificationTarget(notification(type: NotificationType.skillProposalApproved)), '/profile/skills/proposals');
    });
  });

  group('NotificationsScreen', () {
    testWidgets('says when there is nothing yet', (tester) async {
      await pump(tester, const NotificationsScreen());

      expect(find.text(l10n.notificationsEmpty), findsOneWidget);
      expect(find.text(l10n.notificationsMarkAllRead), findsNothing);
    });

    testWidgets('tells what happened, naming who did it', (tester) async {
      stubList(
        () => [
          notification(),
          notification(id: 'n2', type: NotificationType.sessionCancelled, reason: 'O gün uygun değilim.', read: true),
          notification(id: 'n3', type: NotificationType.matchEnded, actor: null),
          notification(id: 'n4', type: NotificationType.sessionCompleted, actor: null),
          notification(id: 'n5', type: NotificationType.swapRequestExpired, actor: null),
        ],
      );
      await pump(tester, const NotificationsScreen());

      expect(find.text(l10n.notificationSwapRequestReceived('Deniz Kaya')), findsOneWidget);
      expect(find.text(l10n.notificationSessionCancelledBecause('Deniz Kaya', 'O gün uygun değilim.')), findsOneWidget);
      expect(find.text(l10n.notificationMatchEndedByDeadline), findsOneWidget);
      expect(find.text(l10n.notificationSessionCompletedByDeadline), findsOneWidget);
      expect(find.text(l10n.notificationSwapRequestExpired), findsOneWidget);
      // One dot for each unread notification.
      expect(find.byIcon(Icons.circle), findsNWidgets(4));
    });

    testWidgets('every kind of notification has its own sentence', (tester) async {
      stubList(
        () => [
          for (final type in NotificationType.values.where((type) => type != NotificationType.$unknown))
            notification(id: type.name, type: type, deadline: DateTime.utc(2026, 10, 20, 16)),
        ],
      );
      await pump(tester, const NotificationsScreen());
      final context = tester.element(find.byType(NotificationsScreen));

      final sentences = {
        for (final type in NotificationType.values.where((type) => type != NotificationType.$unknown))
          notificationText(context, l10n, notification(type: type, deadline: DateTime.utc(2026, 10, 20, 16)), 'Deniz'),
      };

      expect(sentences, hasLength(NotificationType.values.length - 1));
      expect(sentences, isNot(contains(l10n.notificationUnknown)));
    });

    testWidgets('tapping a notification marks it as read and opens what it is about', (tester) async {
      stubList(() => [notification(type: NotificationType.sessionProposed, matchId: 'match-1')]);
      await pump(tester, const NotificationsScreen());

      await tester.tap(find.text(l10n.notificationSessionProposed('Deniz Kaya')));
      await tester.pumpAndSettle();

      verify(() => repository.markRead('n1')).called(1);
      expect(find.text('route:/matches/match-1'), findsOneWidget);
    });

    testWidgets('all can be marked as read at once', (tester) async {
      var read = false;
      stubList(() => [notification(read: read)]);
      when(repository.markAllRead).thenAnswer((_) async => read = true);
      await pump(tester, const NotificationsScreen());

      await tester.tap(find.text(l10n.notificationsMarkAllRead));
      await tester.pumpAndSettle();

      verify(repository.markAllRead).called(1);
      expect(find.byIcon(Icons.circle), findsNothing);
      expect(find.text(l10n.notificationsMarkAllRead), findsNothing);
    });

    testWidgets('a failed load can be retried', (tester) async {
      when(repository.list).thenThrow(const ApiException(statusCode: 500, code: 'internal_error'));
      await pump(tester, const NotificationsScreen());

      stubList(() => [notification()]);
      await tester.tap(find.text(l10n.errorRetry));
      await tester.pumpAndSettle();

      expect(find.text(l10n.notificationSwapRequestReceived('Deniz Kaya')), findsOneWidget);
    });
  });

  group('NotificationBell', () {
    testWidgets('shows how many are unread and opens the notifications', (tester) async {
      stubList(() => [notification(), notification(id: 'n2')]);
      await pump(tester, const Scaffold(body: NotificationBell()));

      expect(find.text('2'), findsOneWidget);
      await tester.tap(find.byType(NotificationBell));
      await tester.pumpAndSettle();

      expect(find.text('route:/notifications'), findsOneWidget);
    });
  });
}
