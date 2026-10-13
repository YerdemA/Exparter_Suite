import 'package:exparter/core/api/api_exception.dart';
import 'package:exparter/core/api/api_providers.dart';
import 'package:exparter/core/api/generated/export.dart';
import 'package:exparter/core/auth/session_store.dart';
import 'package:exparter/features/reviews/data/api_review_repository.dart';
import 'package:exparter/features/sessions/data/api_session_repository.dart';
import 'package:exparter/features/sessions/presentation/match_sessions_section.dart';
import 'package:exparter/features/sessions/presentation/propose_session_screen.dart';
import 'package:exparter/features/sessions/presentation/session_controller.dart';
import 'package:exparter/features/sessions/presentation/user_stats_card.dart';
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
  const otherId = 'user-deniz';
  const deniz = UserSummary(userId: otherId, isDeleted: false, username: 'deniz', displayName: 'Deniz Kaya');

  late MockSessionRepository repository;
  late InMemorySessionPersistence persistence;

  Match match({MatchStatus status = MatchStatus.active}) => Match(
        id: 'match-1',
        swapRequestId: 'request-1',
        userA: deniz,
        userB: UserSummary(userId: myId, isDeleted: false, username: 'ayse'),
        skillTaughtByA: const SkillRef(id: 's1', name: 'İspanyolca'),
        skillTaughtByB: const SkillRef(id: 's2', name: 'Gitar'),
        status: status,
        createdAt: DateTime.utc(2026, 10, 13),
      );

  SwapSession session({
    String id = 'session-1',
    SessionMode mode = SessionMode.online,
    SwapSessionStatus status = SwapSessionStatus.proposed,
    bool proposedByMe = false,
    bool? markedByMe,
    Duration startsIn = const Duration(days: 2),
    String? note,
    String? cancelReason,
    String? cancelledBy,
    bool completedAutomatically = false,
  }) =>
      SwapSession(
        id: id,
        matchId: 'match-1',
        proposedByUserId: proposedByMe ? myId : otherId,
        mode: mode,
        status: status,
        completedAutomatically: completedAutomatically,
        createdAt: DateTime.now(),
        scheduledAt: mode == SessionMode.online ? DateTime.now().add(startsIn) : null,
        durationMinutes: mode == SessionMode.online ? 60 : null,
        note: note,
        completionMarkedByUserId: markedByMe == null ? null : (markedByMe ? myId : otherId),
        completionConfirmDeadline:
            status == SwapSessionStatus.completionPending ? DateTime.now().add(const Duration(days: 2, hours: 1)) : null,
        cancelReason: cancelReason,
        cancelledByUserId: cancelledBy,
      );

  void stubSessions(List<SwapSession> Function() sessions) =>
      when(() => repository.listMatchSessions('match-1')).thenAnswer((_) async => sessions());

  setUp(() async {
    repository = MockSessionRepository();
    persistence = InMemorySessionPersistence();
    await SessionStore(persistence).save(testSession());
    stubSessions(() => []);
    when(() => repository.getUserStats(any())).thenAnswer((_) async => testStats);
  });

  List<Override> overrides() => [
        sessionRepositoryProvider.overrideWithValue(repository),
        reviewRepositoryProvider.overrideWithValue(quietReviewRepository()),
        sessionStoreProvider.overrideWithValue(SessionStore(persistence)),
      ];

  Future<void> pump(WidgetTester tester, Widget child, {bool scroll = true}) async {
    await tester.pumpWidget(
      ProviderScope(
        overrides: overrides(),
        child: routed(Scaffold(body: scroll ? SingleChildScrollView(child: child) : child)),
      ),
    );
    await tester.pumpAndSettle();
  }

  Future<void> tapText(WidgetTester tester, String text) async {
    await tester.ensureVisible(find.text(text));
    await tester.tap(find.text(text));
    await tester.pumpAndSettle();
  }

  group('SessionActions', () {
    late ProviderContainer container;

    setUp(() {
      container = ProviderContainer.test(overrides: overrides());
      container.listen(matchSessionsProvider('match-1'), (_, _) {});
      container.listen(sessionActionsProvider, (_, _) {});
    });

    test('a successful action reloads the list', () async {
      when(() => repository.accept('session-1')).thenAnswer((_) async => session(status: SwapSessionStatus.scheduled));
      await container.read(matchSessionsProvider('match-1').future);

      expect(await container.read(sessionActionsProvider.notifier).accept('session-1'), isTrue);
      await container.read(matchSessionsProvider('match-1').future);

      verify(() => repository.listMatchSessions('match-1')).called(2);
    });

    test('a refused action reports failure and keeps the error', () async {
      when(() => repository.markCompleted('session-1'))
          .thenThrow(const ApiException(statusCode: 409, code: 'session_not_started'));

      expect(await container.read(sessionActionsProvider.notifier).markCompleted('session-1'), isFalse);
      expect((container.read(sessionActionsProvider).error! as ApiException).code, 'session_not_started');
    });

    test('a blank note is sent as no note and a reason is trimmed', () async {
      final at = DateTime(2026, 10, 20, 19);
      when(
        () => repository.propose(
          any(),
          scheduledAt: any(named: 'scheduledAt'),
          durationMinutes: any(named: 'durationMinutes'),
          note: any(named: 'note'),
        ),
      ).thenAnswer((_) async => session());
      when(() => repository.cancel(any(), reason: any(named: 'reason'))).thenAnswer((_) async => session());

      await container.read(sessionActionsProvider.notifier).propose('match-1', scheduledAt: at, durationMinutes: 45, note: '  ');
      await container.read(sessionActionsProvider.notifier).cancel('session-1', reason: '  Uygun değilim  ');

      verify(() => repository.propose('match-1', scheduledAt: at, durationMinutes: 45, note: null)).called(1);
      verify(() => repository.cancel('session-1', reason: 'Uygun değilim')).called(1);
    });
  });

  group('MatchSessionsSection', () {
    testWidgets('an active match offers both ways to add a session and says when there is none', (tester) async {
      await pump(tester, MatchSessionsSection(match: match()));

      expect(find.text(l10n.sessionsEmpty), findsOneWidget);
      expect(find.text(l10n.sessionInPersonHint), findsOneWidget);

      await tapText(tester, l10n.sessionProposeOnline);
      expect(find.text('route:/matches/match-1/sessions/new'), findsOneWidget);
    });

    testWidgets('a match that is ending or over takes no new sessions', (tester) async {
      await pump(tester, MatchSessionsSection(match: match(status: MatchStatus.endRequested)));

      expect(find.text(l10n.sessionProposeOnline), findsNothing);
      expect(find.text(l10n.sessionInPersonReport), findsNothing);
      expect(find.text(l10n.sessionsEmpty), findsOneWidget);
    });

    testWidgets('an in-person meeting is reported after confirming', (tester) async {
      var sessions = <SwapSession>[];
      stubSessions(() => sessions);
      when(() => repository.reportInPerson('match-1')).thenAnswer((_) async {
        sessions = [session(mode: SessionMode.inPerson, status: SwapSessionStatus.completionPending, markedByMe: true)];
        return sessions.single;
      });
      await pump(tester, MatchSessionsSection(match: match()));

      await tapText(tester, l10n.sessionInPersonReport);
      expect(find.text(l10n.sessionInPersonConfirmBody), findsOneWidget);
      verifyNever(() => repository.reportInPerson(any()));

      await tester.tap(find.widgetWithText(FilledButton, l10n.sessionInPersonReport));
      await tester.pumpAndSettle();

      verify(() => repository.reportInPerson('match-1')).called(1);
      expect(find.text(l10n.sessionInPersonTitle), findsOneWidget);
      expect(find.textContaining(l10n.sessionWaitingConfirmation), findsOneWidget);
      expect(find.textContaining(l10n.remainingDays(2)), findsOneWidget);
    });

    testWidgets('a pair that has met in person cannot report another meeting', (tester) async {
      stubSessions(() => [session(mode: SessionMode.inPerson, status: SwapSessionStatus.completed)]);
      await pump(tester, MatchSessionsSection(match: match()));

      expect(find.text(l10n.sessionProposeOnline), findsOneWidget);
      expect(find.text(l10n.sessionInPersonReport), findsNothing);
    });

    testWidgets('a proposal from the other user can be accepted or declined', (tester) async {
      stubSessions(() => [session(note: 'https://meet.example/abc')]);
      when(() => repository.accept('session-1')).thenAnswer((_) async => session(status: SwapSessionStatus.scheduled));
      when(() => repository.decline('session-1')).thenAnswer((_) async => session(status: SwapSessionStatus.declined));
      await pump(tester, MatchSessionsSection(match: match()));

      expect(find.textContaining('60 dk'), findsOneWidget);
      expect(find.text(l10n.sessionStatusProposed), findsOneWidget);
      expect(find.text('https://meet.example/abc'), findsOneWidget);
      expect(find.text(l10n.sessionCancel), findsNothing);

      await tapText(tester, l10n.requestDecline);
      await tapText(tester, l10n.requestAccept);

      verify(() => repository.decline('session-1')).called(1);
      verify(() => repository.accept('session-1')).called(1);
    });

    testWidgets('my own proposal can only be cancelled, and cancelling needs a reason', (tester) async {
      stubSessions(() => [session(proposedByMe: true)]);
      when(() => repository.cancel('session-1', reason: any(named: 'reason')))
          .thenAnswer((_) async => session(status: SwapSessionStatus.cancelled));
      await pump(tester, MatchSessionsSection(match: match()));
      expect(find.text(l10n.requestAccept), findsNothing);

      await tapText(tester, l10n.sessionCancel);
      final submit = find.widgetWithText(FilledButton, l10n.sessionReasonSubmit);
      await tester.enterText(find.byType(TextField), 'yok');
      await tester.pump();
      expect(tester.widget<FilledButton>(submit).onPressed, isNull);

      await tester.enterText(find.byType(TextField), 'O gün şehir dışındayım.');
      await tester.pump();
      await tester.tap(submit);
      await tester.pumpAndSettle();

      verify(() => repository.cancel('session-1', reason: 'O gün şehir dışındayım.')).called(1);
    });

    testWidgets('a scheduled session can be marked completed only once it has started', (tester) async {
      stubSessions(
        () => [
          session(status: SwapSessionStatus.scheduled),
          session(id: 'session-2', status: SwapSessionStatus.scheduled, startsIn: const Duration(hours: -1)),
        ],
      );
      when(() => repository.markCompleted('session-2'))
          .thenAnswer((_) async => session(status: SwapSessionStatus.completionPending, markedByMe: true));
      await pump(tester, MatchSessionsSection(match: match()));

      expect(find.text(l10n.sessionMarkCompleted), findsOneWidget);
      expect(find.text(l10n.sessionCancel), findsNWidgets(2));

      await tapText(tester, l10n.sessionMarkCompleted);

      verify(() => repository.markCompleted('session-2')).called(1);
    });

    testWidgets('the other user’s completion mark can be confirmed or disputed with a reason', (tester) async {
      stubSessions(
        () => [session(mode: SessionMode.inPerson, status: SwapSessionStatus.completionPending, markedByMe: false)],
      );
      when(() => repository.confirmCompleted('session-1'))
          .thenAnswer((_) async => session(status: SwapSessionStatus.completed));
      when(() => repository.cancel('session-1', reason: any(named: 'reason')))
          .thenAnswer((_) async => session(status: SwapSessionStatus.cancelled));
      await pump(tester, MatchSessionsSection(match: match()));

      expect(find.text(l10n.sessionConfirmQuestionInPerson), findsOneWidget);
      expect(find.text(l10n.remainingDays(2)), findsOneWidget);

      await tapText(tester, l10n.sessionDispute);
      expect(find.text(l10n.sessionDisputeTitle), findsOneWidget);
      await tester.enterText(find.byType(TextField), 'Böyle bir buluşma olmadı.');
      await tester.pump();
      await tester.tap(find.widgetWithText(FilledButton, l10n.sessionReasonSubmit));
      await tester.pumpAndSettle();
      await tapText(tester, l10n.sessionConfirm);

      verify(() => repository.cancel('session-1', reason: 'Böyle bir buluşma olmadı.')).called(1);
      verify(() => repository.confirmCompleted('session-1')).called(1);
    });

    testWidgets('finished sessions say how they ended and offer no actions', (tester) async {
      stubSessions(
        () => [
          session(status: SwapSessionStatus.completed, completedAutomatically: true),
          session(
            id: 'session-2',
            status: SwapSessionStatus.cancelled,
            cancelReason: 'Hastalandım',
            cancelledBy: otherId,
          ),
          session(id: 'session-3', status: SwapSessionStatus.cancelled),
          session(id: 'session-4', status: SwapSessionStatus.declined),
        ],
      );

      await pump(tester, MatchSessionsSection(match: match()));

      expect(find.text(l10n.sessionCompletedAutomatically), findsOneWidget);
      expect(find.text(l10n.sessionCancelReason('Hastalandım')), findsOneWidget);
      expect(find.text(l10n.sessionCancelledMatchEnded), findsOneWidget);
      expect(find.text(l10n.sessionStatusDeclined), findsOneWidget);
      expect(find.text(l10n.sessionCancel), findsNothing);
      expect(find.text(l10n.sessionConfirm), findsNothing);
    });

    testWidgets('a refused action is explained', (tester) async {
      stubSessions(() => [session()]);
      when(() => repository.accept('session-1')).thenThrow(const ApiException(statusCode: 409, code: 'invalid_state'));
      await pump(tester, MatchSessionsSection(match: match()));

      await tapText(tester, l10n.requestAccept);

      expect(find.text(l10n.errorInvalidState), findsOneWidget);
    });

    testWidgets('shows a safe message and recovers on retry when the list fails to load', (tester) async {
      when(() => repository.listMatchSessions('match-1')).thenThrow(const ApiException(code: ApiException.networkCode));
      await pump(tester, MatchSessionsSection(match: match()));
      expect(find.text(l10n.errorNetwork), findsOneWidget);

      stubSessions(() => [session()]);
      await tapText(tester, l10n.errorRetry);

      expect(find.text(l10n.sessionStatusProposed), findsOneWidget);
    });
  });

  group('ProposeSessionScreen', () {
    void stubPropose(Future<SwapSession> Function() answer) => when(
          () => repository.propose(
            any(),
            scheduledAt: any(named: 'scheduledAt'),
            durationMinutes: any(named: 'durationMinutes'),
            note: any(named: 'note'),
          ),
        ).thenAnswer((_) => answer());

    testWidgets('proposes tomorrow evening by default with the chosen length and note', (tester) async {
      stubPropose(() async => session(proposedByMe: true));
      await pump(tester, const ProposeSessionScreen(matchId: 'match-1'), scroll: false);

      await tester.tap(find.text(l10n.sessionDurationMinutes(90)));
      await tester.enterText(find.byType(TextField), '  https://meet.example/abc  ');
      await tester.pumpAndSettle();
      await tapText(tester, l10n.sessionProposeSubmit);

      final tomorrow = DateTime.now().add(const Duration(days: 1));
      verify(
        () => repository.propose(
          'match-1',
          scheduledAt: DateTime(tomorrow.year, tomorrow.month, tomorrow.day, 19),
          durationMinutes: 90,
          note: 'https://meet.example/abc',
        ),
      ).called(1);
      expect(find.text('route:/matches/match-1'), findsOneWidget);
    });

    testWidgets('a date and a time picked from the dialogs are used', (tester) async {
      stubPropose(() async => session(proposedByMe: true));
      await pump(tester, const ProposeSessionScreen(matchId: 'match-1'), scroll: false);
      final nextWeek = DateUtils.dateOnly(DateTime.now().add(const Duration(days: 7)));

      // The pickers themselves are Flutter's; what matters here is that their answers are used.
      await tester.tap(find.text(l10n.sessionDateLabel));
      await tester.pumpAndSettle();
      Navigator.of(tester.element(find.byType(DatePickerDialog))).pop(nextWeek);
      await tester.pumpAndSettle();
      await tester.tap(find.text(l10n.sessionTimeLabel));
      await tester.pumpAndSettle();
      Navigator.of(tester.element(find.byType(TimePickerDialog))).pop(const TimeOfDay(hour: 21, minute: 30));
      await tester.pumpAndSettle();
      await tapText(tester, l10n.sessionProposeSubmit);

      verify(
        () => repository.propose(
          'match-1',
          scheduledAt: DateTime(nextWeek.year, nextWeek.month, nextWeek.day, 21, 30),
          durationMinutes: 60,
          note: null,
        ),
      ).called(1);
    });

    testWidgets('explains a refusal and stays on the form', (tester) async {
      stubPropose(() async => throw const ApiException(statusCode: 409, code: 'match_end_pending'));
      await pump(tester, const ProposeSessionScreen(matchId: 'match-1'), scroll: false);

      await tapText(tester, l10n.sessionProposeSubmit);

      expect(find.text(l10n.errorMatchEndPending), findsOneWidget);
      expect(find.text(l10n.sessionProposeTitle), findsOneWidget);
    });
  });

  group('UserStatsCard', () {
    testWidgets('shows total, completed and cancelled matches and the completed sessions', (tester) async {
      await pump(tester, const UserStatsCard(userId: 'user-deniz'));

      expect(find.text('5'), findsOneWidget);
      expect(find.text(l10n.statsTotalMatches), findsOneWidget);
      expect(find.text('3'), findsOneWidget);
      expect(find.text(l10n.statsCompletedMatches), findsOneWidget);
      expect(find.text('1'), findsOneWidget);
      expect(find.text(l10n.statsCancelledMatches), findsOneWidget);
      expect(find.text(l10n.statsCompletedSessions(9)), findsOneWidget);
      verify(() => repository.getUserStats('user-deniz')).called(1);
    });

    testWidgets('says so when the statistics cannot be loaded and can retry', (tester) async {
      when(() => repository.getUserStats(any())).thenThrow(const ApiException(code: ApiException.networkCode));
      await pump(tester, const UserStatsCard(userId: 'user-deniz'));
      expect(find.text(l10n.statsUnavailable), findsOneWidget);

      when(() => repository.getUserStats(any())).thenAnswer((_) async => testStats);
      await tapText(tester, l10n.errorRetry);

      expect(find.text(l10n.statsTotalMatches), findsOneWidget);
    });
  });
}
