import 'package:exparter/core/api/api_exception.dart';
import 'package:exparter/core/api/api_providers.dart';
import 'package:exparter/core/api/generated/export.dart';
import 'package:exparter/core/auth/session_controller.dart';
import 'package:exparter/core/auth/session_store.dart';
import 'package:exparter/core/realtime/realtime_client.dart';
import 'package:exparter/features/matches/data/api_match_repository.dart';
import 'package:exparter/features/matches/presentation/match_detail_screen.dart';
import 'package:exparter/features/messages/data/api_message_repository.dart';
import 'package:exparter/features/messages/presentation/conversation_screen.dart';
import 'package:exparter/features/messages/presentation/conversations_screen.dart';
import 'package:exparter/features/messages/presentation/message_controller.dart';
import 'package:exparter/l10n/app_localizations_tr.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_riverpod/misc.dart' show Override;
import 'package:flutter_test/flutter_test.dart';
import 'package:mocktail/mocktail.dart';

import '../../helpers.dart';

void main() {
  final l10n = AppLocalizationsTr();
  final myId = testAccount().id;
  const deniz = UserSummary(userId: 'user-deniz', isDeleted: false, username: 'deniz', displayName: 'Deniz Kaya');
  final start = DateTime.utc(2026, 10, 13, 9);

  late MockMessageRepository repository;
  late FakeRealtimeClient realtime;
  late InMemorySessionPersistence persistence;

  Message message(String id, String body, {bool mine = false, int minute = 0, String? clientMessageId}) => Message(
        id: id,
        conversationId: 'conversation-1',
        senderId: mine ? myId : deniz.userId,
        body: body,
        sentAt: start.add(Duration(minutes: minute)),
        clientMessageId: clientMessageId,
      );

  Conversation conversation({
    String id = 'conversation-1',
    int unreadCount = 0,
    Message? lastMessage,
    bool isClosed = false,
    bool hasActiveMatch = true,
    DateTime? otherUserLastReadAt,
    UserSummary otherUser = deniz,
  }) =>
      Conversation(
        id: id,
        otherUser: otherUser,
        isClosed: isClosed,
        hasActiveMatch: hasActiveMatch,
        unreadCount: unreadCount,
        createdAt: start,
        lastMessage: lastMessage,
        otherUserLastReadAt: otherUserLastReadAt,
      );

  void stubThread(Conversation value, List<Message> newestFirst, {String? nextCursor}) {
    when(() => repository.getConversation('conversation-1')).thenAnswer((_) async => value);
    when(() => repository.listMessages('conversation-1'))
        .thenAnswer((_) async => MessageCursorPage(items: newestFirst, nextCursor: nextCursor));
  }

  void stubSend(Future<Message> Function(Invocation invocation) answer) => when(
        () => repository.sendMessage(
          'conversation-1',
          body: any(named: 'body'),
          clientMessageId: any(named: 'clientMessageId'),
        ),
      ).thenAnswer(answer);

  setUp(() async {
    repository = MockMessageRepository();
    realtime = FakeRealtimeClient();
    persistence = InMemorySessionPersistence();
    await SessionStore(persistence).save(testSession());
    when(() => repository.listConversations()).thenAnswer((_) async => []);
    when(() => repository.markRead(any())).thenAnswer((_) async {});
  });

  List<Override> overrides() => [
        messageRepositoryProvider.overrideWithValue(repository),
        realtimeClientProvider.overrideWithValue(realtime),
        sessionStoreProvider.overrideWithValue(SessionStore(persistence)),
      ];

  ProviderContainer container() {
    final container = ProviderContainer.test(overrides: overrides());
    // The stream has to be subscribed before events are emitted.
    container.listen(realtimeEventsProvider, (_, _) {});
    return container;
  }

  Future<void> settle() => Future<void>.delayed(Duration.zero);

  group('real-time session', () {
    test('the connection is open exactly while someone is signed in', () async {
      final scope = container();
      await scope.read(sessionControllerProvider.future);
      scope.listen(realtimeSessionProvider, (_, _) {});
      await settle();
      expect(realtime.started, isTrue);

      await scope.read(sessionControllerProvider.notifier).end();
      await settle();
      expect(realtime.started, isFalse);
    });
  });

  group('Conversations', () {
    test('reloads when the server reports a change and totals the unread messages', () async {
      var items = [conversation(unreadCount: 2), conversation(id: 'conversation-2', unreadCount: 1)];
      when(() => repository.listConversations()).thenAnswer((_) async => items);
      final scope = container();
      scope.listen(conversationsProvider, (_, _) {});
      scope.listen(unreadMessageCountProvider, (_, _) {});

      await scope.read(conversationsProvider.future);
      expect(scope.read(unreadMessageCountProvider), 3);

      items = [conversation(unreadCount: 3), conversation(id: 'conversation-2', unreadCount: 1)];
      realtime.emit(MessageReceived(message('m9', 'Merhaba')));
      await settle();
      await scope.read(conversationsProvider.future);

      expect(scope.read(unreadMessageCountProvider), 4);
      verify(() => repository.listConversations()).called(2);
    });
  });

  group('Thread', () {
    late ProviderContainer scope;
    final provider = threadProvider('conversation-1');

    Future<ThreadState> open() async {
      scope = container();
      scope.listen(provider, (_, _) {});
      return scope.read(provider.future);
    }

    ThreadState current() => scope.read(provider).value!;

    test('opening loads the messages and marks an unread conversation as read', () async {
      stubThread(conversation(unreadCount: 2), [message('m2', 'İki', minute: 2), message('m1', 'Bir', minute: 1)]);

      final state = await open();

      expect(state.messages.map((item) => item.body), ['İki', 'Bir']);
      verify(() => repository.markRead('conversation-1')).called(1);
    });

    test('opening a conversation with nothing unread does not mark it', () async {
      stubThread(conversation(), [message('m1', 'Bir')]);

      await open();

      verifyNever(() => repository.markRead(any()));
    });

    test('a sent message shows at once and is replaced by the stored one', () async {
      stubThread(conversation(), []);
      String? sentWith;
      stubSend((invocation) async {
        sentWith = invocation.namedArguments[#clientMessageId] as String;
        expect(current().outgoing.single.body, 'Merhaba');
        return message('m1', 'Merhaba', mine: true, clientMessageId: sentWith);
      });
      await open();

      await scope.read(provider.notifier).send('  Merhaba  ');

      expect(current().outgoing, isEmpty);
      expect(current().messages.single.id, 'm1');
      expect(sentWith, matches(RegExp(r'^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$')));
    });

    test('the real-time copy of a sent message is not shown twice', () async {
      stubThread(conversation(), []);
      stubSend((invocation) async {
        final clientMessageId = invocation.namedArguments[#clientMessageId] as String;
        final stored = message('m1', 'Merhaba', mine: true, clientMessageId: clientMessageId);
        // The event overtakes the HTTP answer.
        realtime.emit(MessageReceived(stored));
        await settle();
        return stored;
      });
      await open();

      await scope.read(provider.notifier).send('Merhaba');

      expect(current().messages, hasLength(1));
      expect(current().outgoing, isEmpty);
    });

    test('a failed send stays marked and a retry reuses its id', () async {
      stubThread(conversation(), []);
      final ids = <String>[];
      var fail = true;
      stubSend((invocation) async {
        ids.add(invocation.namedArguments[#clientMessageId] as String);
        if (fail) {
          throw const ApiException(code: ApiException.networkCode);
        }

        return message('m1', 'Merhaba', mine: true, clientMessageId: ids.last);
      });
      await open();

      await scope.read(provider.notifier).send('Merhaba');
      expect(current().outgoing.single.failed, isTrue);
      expect(current().messages, isEmpty);

      fail = false;
      await scope.read(provider.notifier).retry(ids.single);

      expect(current().outgoing, isEmpty);
      expect(current().messages.single.body, 'Merhaba');
      expect(ids, hasLength(2));
      expect(ids.first, ids.last);
    });

    test('a blank message is not sent', () async {
      stubThread(conversation(), []);
      await open();

      await scope.read(provider.notifier).send('   ');

      expect(current().outgoing, isEmpty);
      verifyNever(
        () => repository.sendMessage(any(), body: any(named: 'body'), clientMessageId: any(named: 'clientMessageId')),
      );
    });

    test('a message from the other user appears and is marked read; other conversations are ignored', () async {
      stubThread(conversation(), [message('m1', 'Bir')]);
      await open();

      realtime.emit(MessageReceived(message('m2', 'İki', minute: 1)));
      realtime.emit(
        MessageReceived(
          Message(id: 'x', conversationId: 'conversation-2', senderId: deniz.userId, body: 'Başka', sentAt: start),
        ),
      );
      await settle();

      expect(current().messages.map((item) => item.body), ['İki', 'Bir']);
      verify(() => repository.markRead('conversation-1')).called(1);
    });

    test('a read receipt from the other user updates when they last read', () async {
      stubThread(conversation(), [message('m1', 'Bir', mine: true)]);
      await open();
      final readAt = start.add(const Duration(minutes: 5));

      realtime.emit(ConversationRead(conversationId: 'conversation-1', userId: myId, readAt: readAt));
      await settle();
      expect(current().conversation.otherUserLastReadAt, isNull);

      realtime.emit(ConversationRead(conversationId: 'conversation-1', userId: deniz.userId, readAt: readAt));
      await settle();
      expect(current().conversation.otherUserLastReadAt, readAt);
    });

    test('older messages are appended page by page', () async {
      stubThread(conversation(), [message('m3', 'Üç', minute: 3), message('m2', 'İki', minute: 2)], nextCursor: 'c1');
      when(() => repository.listMessages('conversation-1', cursor: 'c1')).thenAnswer(
        (_) async => MessageCursorPage(items: [message('m2', 'İki', minute: 2), message('m1', 'Bir', minute: 1)]),
      );
      await open();

      await scope.read(provider.notifier).loadMore();
      await scope.read(provider.notifier).loadMore();

      expect(current().messages.map((item) => item.body), ['Üç', 'İki', 'Bir']);
      expect(current().nextCursor, isNull);
      verify(() => repository.listMessages('conversation-1', cursor: 'c1')).called(1);
    });

    test('closing replaces the conversation, and a refusal reports failure', () async {
      stubThread(conversation(hasActiveMatch: false), []);
      when(() => repository.closeConversation('conversation-1'))
          .thenAnswer((_) async => conversation(isClosed: true, hasActiveMatch: false));
      await open();

      expect(await scope.read(provider.notifier).close(), isTrue);
      expect(current().conversation.isClosed, isTrue);

      when(() => repository.closeConversation('conversation-1'))
          .thenThrow(const ApiException(statusCode: 409, code: 'conversation_has_active_match'));
      expect(await scope.read(provider.notifier).close(), isFalse);
    });
  });

  group('ConversationsScreen', () {
    Future<void> pump(WidgetTester tester) async {
      await tester.pumpWidget(ProviderScope(overrides: overrides(), child: routed(const ConversationsScreen())));
      await tester.pumpAndSettle();
    }

    testWidgets('shows each conversation with its preview and unread count, and opens one', (tester) async {
      when(() => repository.listConversations()).thenAnswer(
        (_) async => [
          conversation(unreadCount: 2, lastMessage: message('m2', 'Yarın olur mu?')),
          conversation(
            id: 'conversation-2',
            otherUser: const UserSummary(userId: 'user-can', isDeleted: false, username: 'can'),
            lastMessage: message('m1', 'Teşekkürler', mine: true),
            isClosed: true,
          ),
          conversation(id: 'conversation-3', otherUser: const UserSummary(userId: 'gone', isDeleted: true)),
        ],
      );

      await pump(tester);

      expect(find.text('Deniz Kaya'), findsOneWidget);
      expect(find.text('Yarın olur mu?'), findsOneWidget);
      expect(find.widgetWithText(Badge, '2'), findsOneWidget);
      expect(find.text('@can'), findsOneWidget);
      expect(find.text(l10n.messagesYouPrefix('Teşekkürler')), findsOneWidget);
      expect(find.byIcon(Icons.lock_outline), findsOneWidget);
      expect(find.text(l10n.deletedUser), findsOneWidget);
      expect(find.text(l10n.conversationNoMessages), findsOneWidget);

      await tester.tap(find.text('Deniz Kaya'));
      await tester.pumpAndSettle();
      expect(find.text('route:/messages/conversation-1'), findsOneWidget);
    });

    testWidgets('says so when there are no conversations', (tester) async {
      await pump(tester);

      expect(find.text(l10n.messagesEmpty), findsOneWidget);
    });

    testWidgets('shows a safe message and recovers on retry when loading fails', (tester) async {
      when(() => repository.listConversations()).thenThrow(const ApiException(code: ApiException.networkCode));
      await pump(tester);
      expect(find.text(l10n.errorNetwork), findsOneWidget);

      when(() => repository.listConversations()).thenAnswer((_) async => [conversation()]);
      await tester.tap(find.text(l10n.errorRetry));
      await tester.pumpAndSettle();

      expect(find.text('Deniz Kaya'), findsOneWidget);
    });
  });

  group('ConversationScreen', () {
    Future<void> pump(WidgetTester tester) async {
      await tester.pumpWidget(
        ProviderScope(overrides: overrides(), child: routed(const ConversationScreen(conversationId: 'conversation-1'))),
      );
      await tester.pumpAndSettle();
    }

    testWidgets('shows the messages and marks my newest one as seen', (tester) async {
      stubThread(
        conversation(otherUserLastReadAt: start.add(const Duration(minutes: 3))),
        [
          message('m3', 'Üç', mine: true, minute: 3),
          message('m2', 'İki', minute: 2),
          message('m1', 'Bir', mine: true, minute: 1),
        ],
      );

      await pump(tester);

      expect(find.text('Deniz Kaya'), findsOneWidget);
      expect(find.text('Bir'), findsOneWidget);
      expect(find.text('İki'), findsOneWidget);
      expect(find.text('Üç'), findsOneWidget);
      expect(find.textContaining(l10n.messageSeen), findsOneWidget);
    });

    testWidgets('does not claim a message was seen before the other user read that far', (tester) async {
      stubThread(
        conversation(otherUserLastReadAt: start.add(const Duration(minutes: 1))),
        [message('m2', 'İki', mine: true, minute: 2), message('m1', 'Bir', mine: true, minute: 1)],
      );

      await pump(tester);

      expect(find.textContaining(l10n.messageSeen), findsNothing);
    });

    testWidgets('typing and sending shows the message and clears the box', (tester) async {
      stubThread(conversation(), []);
      stubSend(
        (invocation) async => message(
          'm1',
          'Merhaba',
          mine: true,
          clientMessageId: invocation.namedArguments[#clientMessageId] as String,
        ),
      );
      await pump(tester);
      expect(find.text(l10n.conversationNoMessages), findsOneWidget);

      await tester.enterText(find.byType(TextField), 'Merhaba');
      await tester.tap(find.byTooltip(l10n.messageSend));
      await tester.pumpAndSettle();

      expect(find.text('Merhaba'), findsOneWidget);
      expect(tester.widget<TextField>(find.byType(TextField)).controller!.text, isEmpty);
      verify(
        () => repository.sendMessage('conversation-1', body: 'Merhaba', clientMessageId: any(named: 'clientMessageId')),
      ).called(1);
    });

    testWidgets('a message that could not be sent offers a retry', (tester) async {
      stubThread(conversation(), []);
      var fail = true;
      stubSend((invocation) async {
        if (fail) {
          throw const ApiException(code: ApiException.networkCode);
        }

        return message('m1', 'Merhaba', mine: true, clientMessageId: invocation.namedArguments[#clientMessageId] as String);
      });
      await pump(tester);

      await tester.enterText(find.byType(TextField), 'Merhaba');
      await tester.tap(find.byTooltip(l10n.messageSend));
      await tester.pumpAndSettle();
      expect(find.text(l10n.messageFailed), findsOneWidget);

      fail = false;
      await tester.tap(find.text('Merhaba'));
      await tester.pumpAndSettle();

      expect(find.text(l10n.messageFailed), findsNothing);
      expect(find.text('Merhaba'), findsOneWidget);
    });

    testWidgets('a message arriving in real time appears without reloading', (tester) async {
      stubThread(conversation(), [message('m1', 'Bir')]);
      await pump(tester);

      realtime.emit(MessageReceived(message('m2', 'Şimdi geldi', minute: 1)));
      await tester.pumpAndSettle();

      expect(find.text('Şimdi geldi'), findsOneWidget);
      verify(() => repository.listMessages('conversation-1')).called(1);
    });

    testWidgets('a closed conversation is read-only', (tester) async {
      stubThread(conversation(isClosed: true, hasActiveMatch: false), [message('m1', 'Bir')]);

      await pump(tester);

      expect(find.text(l10n.conversationClosedBanner), findsOneWidget);
      expect(find.byType(TextField), findsNothing);
      expect(find.byType(PopupMenuButton<void>), findsNothing);
    });

    testWidgets('closing is not offered while the two users are matched', (tester) async {
      stubThread(conversation(), [message('m1', 'Bir')]);

      await pump(tester);

      expect(find.byType(PopupMenuButton<void>), findsNothing);
    });

    testWidgets('a conversation without a match can be closed after confirming', (tester) async {
      stubThread(conversation(hasActiveMatch: false), [message('m1', 'Bir')]);
      when(() => repository.closeConversation('conversation-1'))
          .thenAnswer((_) async => conversation(isClosed: true, hasActiveMatch: false));
      await pump(tester);

      await tester.tap(find.byType(PopupMenuButton<void>));
      await tester.pumpAndSettle();
      await tester.tap(find.text(l10n.conversationClose));
      await tester.pumpAndSettle();
      expect(find.text(l10n.conversationCloseConfirmBody), findsOneWidget);
      await tester.tap(find.widgetWithText(FilledButton, l10n.conversationClose));
      await tester.pumpAndSettle();

      verify(() => repository.closeConversation('conversation-1')).called(1);
      expect(find.text(l10n.conversationClosedBanner), findsOneWidget);
    });

    testWidgets('shows a safe message when the conversation cannot be loaded', (tester) async {
      when(() => repository.getConversation('conversation-1'))
          .thenThrow(const ApiException(statusCode: 404, code: 'not_found'));

      await pump(tester);

      expect(find.text(l10n.errorNotFound), findsOneWidget);
      expect(find.text(l10n.errorRetry), findsOneWidget);
    });
  });

  group('match detail', () {
    testWidgets('leads to the conversation of the match', (tester) async {
      final matches = MockMatchRepository();
      when(() => matches.getMatch('match-1')).thenAnswer(
        (_) async => Match(
          id: 'match-1',
          swapRequestId: 'request-1',
          userA: deniz,
          userB: UserSummary(userId: myId, isDeleted: false, username: 'ayse'),
          skillTaughtByA: const SkillRef(id: 's1', name: 'İspanyolca'),
          skillTaughtByB: const SkillRef(id: 's2', name: 'Gitar'),
          status: MatchStatus.active,
          createdAt: start,
          conversationId: 'conversation-1',
        ),
      );
      await tester.pumpWidget(
        ProviderScope(
          overrides: [...overrides(), matchRepositoryProvider.overrideWithValue(matches)],
          child: routed(const MatchDetailScreen(matchId: 'match-1')),
        ),
      );
      await tester.pumpAndSettle();

      await tester.tap(find.text(l10n.matchSendMessage));
      await tester.pumpAndSettle();

      expect(find.text('route:/messages/conversation-1'), findsOneWidget);
    });
  });
}
