import 'package:exparter/core/api/api_providers.dart';
import 'package:exparter/core/auth/session_controller.dart';
import 'package:exparter/core/auth/session_store.dart';
import 'package:exparter/core/realtime/realtime_client.dart';
import 'package:exparter/core/router/app_router.dart';
import 'package:exparter/core/router/app_routes.dart';
import 'package:exparter/features/auth/data/api_auth_repository.dart';
import 'package:exparter/features/auth/presentation/auth_controller.dart';
import 'package:exparter/features/messages/data/api_message_repository.dart';
import 'package:exparter/features/notifications/data/api_notification_repository.dart';
import 'package:exparter/features/notifications/data/firebase_push_client.dart';
import 'package:exparter/features/notifications/domain/push_client.dart';
import 'package:exparter/features/notifications/presentation/push_controller.dart';
import 'package:exparter/features/shell/presentation/app_shell.dart';
import 'package:exparter/l10n/app_localizations.dart';
import 'package:exparter/l10n/app_localizations_tr.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_riverpod/misc.dart' show Override;
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:mocktail/mocktail.dart';

import '../../helpers.dart';

class FakePushPromptStore implements PushPromptStore {
  bool value = false;

  @override
  Future<bool> shown() async => value;

  @override
  Future<void> markShown() async => value = true;
}

class MockGoRouter extends Mock implements GoRouter {}

void main() {
  final l10n = AppLocalizationsTr();

  late FakePushClient push;
  late FakePushPromptStore promptStore;
  late MockNotificationRepository repository;
  late InMemorySessionPersistence persistence;

  setUp(() async {
    push = FakePushClient();
    promptStore = FakePushPromptStore();
    repository = quietNotificationRepository();
    persistence = InMemorySessionPersistence();
    await SessionStore(persistence).save(testSession());
    when(() => repository.registerDevice(any())).thenAnswer((_) async => 'device-1');
    when(() => repository.unregisterDevice(any())).thenAnswer((_) async {});
    when(() => repository.markRead(any())).thenAnswer((_) async {});
  });

  List<Override> overrides() => [
        pushClientProvider.overrideWithValue(push),
        pushPromptStoreProvider.overrideWithValue(promptStore),
        notificationRepositoryProvider.overrideWithValue(repository),
        sessionStoreProvider.overrideWithValue(SessionStore(persistence)),
      ];

  group('pushTarget', () {
    test('opens the conversation of a message and what a notification is about', () {
      expect(pushTarget({'type': 'MessageReceived', 'conversationId': 'c1'}), AppRoutes.conversation('c1'));
      expect(pushTarget({'type': 'SessionProposed', 'matchId': 'm1'}), AppRoutes.matchDetail('m1'));
      expect(pushTarget({'type': 'ReviewReceived'}), AppRoutes.profile);
      expect(pushTarget({'type': 'SkillProposalApproved'}), AppRoutes.skillProposals);
      expect(pushTarget({'type': 'SomethingNew'}), AppRoutes.matches);
      expect(pushTarget({}), AppRoutes.notifications);
    });
  });

  group('PushRegistration', () {
    late ProviderContainer container;

    setUp(() async {
      container = ProviderContainer.test(overrides: overrides());
      await container.read(sessionControllerProvider.future);
    });

    Future<void> settle() => Future<void>.delayed(Duration.zero);

    test('registers the phone while signed in with the permission, and again for a new token', () async {
      container.listen(pushRegistrationProvider, (_, _) {});
      await container.read(pushPermissionStatusProvider.future);
      await settle();

      verify(() => repository.registerDevice('token-1')).called(1);

      push.tokenController.add('token-2');
      await settle();

      verify(() => repository.registerDevice('token-2')).called(1);
    });

    test('does not register without the permission', () async {
      push.current = PushPermission.denied;
      container.listen(pushRegistrationProvider, (_, _) {});
      await container.read(pushPermissionStatusProvider.future);
      await settle();

      verifyNever(() => repository.registerDevice(any()));
    });

    test('signing out removes the registration first', () async {
      final auth = MockAuthRepository();
      when(() => auth.logout(refreshToken: any(named: 'refreshToken'))).thenAnswer((_) async {});
      container = ProviderContainer.test(overrides: [...overrides(), authRepositoryProvider.overrideWithValue(auth)]);
      await container.read(sessionControllerProvider.future);
      container.listen(pushRegistrationProvider, (_, _) {});
      await container.read(pushPermissionStatusProvider.future);
      await settle();

      container.listen(authControllerProvider, (_, _) {});
      await container.read(authControllerProvider.notifier).logout();

      verifyInOrder([
        () => repository.unregisterDevice('device-1'),
        () => auth.logout(refreshToken: 'refresh-1'),
      ]);
    });
  });

  group('pushNavigation', () {
    late MockGoRouter router;

    setUp(() => router = MockGoRouter());

    test('opens what the launching push is about and counts it as read', () async {
      push.launchData = {'type': 'SessionProposed', 'matchId': 'm1', 'notificationId': 'n1'};
      final container = ProviderContainer.test(overrides: [...overrides(), appRouterProvider.overrideWithValue(router)]);

      container.listen(pushNavigationProvider, (_, _) {});
      await Future<void>.delayed(Duration.zero);

      verify(() => router.go(AppRoutes.matchDetail('m1'))).called(1);
      verify(() => repository.markRead('n1')).called(1);

      push.tapController.add({'type': 'MessageReceived', 'conversationId': 'c1'});
      await Future<void>.delayed(Duration.zero);

      verify(() => router.go(AppRoutes.conversation('c1'))).called(1);
    });
  });

  group('AppShell', () {
    Future<void> pumpShell(WidgetTester tester) async {
      final messages = MockMessageRepository();
      when(messages.listConversations).thenAnswer((_) async => []);
      final router = GoRouter(
        routes: [
          StatefulShellRoute.indexedStack(
            builder: (context, state, shell) => AppShell(navigationShell: shell),
            branches: [
              for (final path in ['/', '/matches', '/messages', '/profile'])
                StatefulShellBranch(routes: [GoRoute(path: path, builder: (context, state) => const SizedBox())]),
            ],
          ),
        ],
      );
      await tester.pumpWidget(
        ProviderScope(
          overrides: [
            ...overrides(),
            messageRepositoryProvider.overrideWithValue(messages),
            realtimeClientProvider.overrideWithValue(FakeRealtimeClient()),
          ],
          child: MaterialApp.router(
            localizationsDelegates: AppLocalizations.localizationsDelegates,
            supportedLocales: AppLocalizations.supportedLocales,
            routerConfig: router,
          ),
        ),
      );
      await tester.pumpAndSettle();
    }

    testWidgets('explains notifications once before the system asks', (tester) async {
      push.current = PushPermission.notDetermined;
      await pumpShell(tester);

      expect(find.text(l10n.pushPromptBody), findsOneWidget);
      expect(promptStore.value, isTrue);

      await tester.tap(find.text(l10n.pushPromptAllow));
      await tester.pumpAndSettle();

      expect(push.requests, 1);
      expect(find.text(l10n.pushPromptBody), findsNothing);
    });

    testWidgets('does not explain again once it was shown', (tester) async {
      push.current = PushPermission.notDetermined;
      promptStore.value = true;
      await pumpShell(tester);

      expect(find.text(l10n.pushPromptBody), findsNothing);
    });

    testWidgets('"Not now" leaves the permission unasked', (tester) async {
      push.current = PushPermission.notDetermined;
      await pumpShell(tester);

      await tester.tap(find.text(l10n.pushPromptLater));
      await tester.pumpAndSettle();

      expect(push.requests, 0);
      expect(find.text(l10n.pushPromptBody), findsNothing);
    });
  });
}
