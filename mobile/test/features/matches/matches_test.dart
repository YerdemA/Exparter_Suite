import 'package:exparter/core/api/api_exception.dart';
import 'package:exparter/core/api/api_providers.dart';
import 'package:exparter/core/api/generated/export.dart';
import 'package:exparter/core/auth/interaction_requirements.dart';
import 'package:exparter/core/auth/session_store.dart';
import 'package:exparter/features/discovery/data/api_discovery_repository.dart';
import 'package:exparter/features/discovery/presentation/user_profile_screen.dart';
import 'package:exparter/features/matches/data/api_match_repository.dart';
import 'package:exparter/features/matches/presentation/match_controller.dart';
import 'package:exparter/features/matches/presentation/match_detail_screen.dart';
import 'package:exparter/features/matches/presentation/match_labels.dart';
import 'package:exparter/features/matches/presentation/matches_screen.dart';
import 'package:exparter/features/matches/presentation/send_request_screen.dart';
import 'package:exparter/features/profile/data/api_profile_repository.dart';
import 'package:exparter/features/reviews/data/api_review_repository.dart';
import 'package:exparter/features/sessions/data/api_session_repository.dart';
import 'package:exparter/features/skills/data/api_skill_repository.dart';
import 'package:exparter/l10n/app_localizations_tr.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_riverpod/misc.dart' show Override;
import 'package:flutter_test/flutter_test.dart';
import 'package:mocktail/mocktail.dart';

import '../../helpers.dart';

void main() {
  final l10n = AppLocalizationsTr();
  final me = UserSummary(userId: testAccount().id, isDeleted: false, username: 'ayse', displayName: 'Ayşe');
  const deniz = UserSummary(userId: 'user-deniz', isDeleted: false, username: 'deniz', displayName: 'Deniz Kaya');
  const guitar = SkillRef(id: 'skill-gitar', name: 'Gitar');
  const spanish = SkillRef(id: 'skill-ispanyolca', name: 'İspanyolca');

  late MockMatchRepository repository;
  late MockDiscoveryRepository discoveryRepository;
  late MockSkillRepository skillRepository;
  late InMemorySessionPersistence persistence;
  late bool canInteract;

  /// Deniz offers Spanish and asks me for the guitar.
  SwapRequest incomingRequest({String? message}) => SwapRequest(
        id: 'request-in',
        requester: deniz,
        recipient: me,
        offeredSkill: spanish,
        requestedSkill: guitar,
        status: SwapRequestStatus.pending,
        createdAt: DateTime.now(),
        expiresAt: DateTime.now().add(const Duration(days: 2, hours: 5)),
        message: message,
      );

  SwapRequest outgoingRequest() => SwapRequest(
        id: 'request-out',
        requester: me,
        recipient: deniz,
        offeredSkill: guitar,
        requestedSkill: spanish,
        status: SwapRequestStatus.pending,
        createdAt: DateTime.now(),
        expiresAt: DateTime.now().add(const Duration(hours: 5, minutes: 30)),
      );

  /// A match in which Deniz sent the request: Deniz is user A and teaches Spanish.
  Match testMatch({
    MatchStatus status = MatchStatus.active,
    String? endRequestedBy,
    MatchEndReason? endReason,
  }) =>
      Match(
        id: 'match-1',
        swapRequestId: 'request-in',
        userA: deniz,
        userB: me,
        skillTaughtByA: spanish,
        skillTaughtByB: guitar,
        status: status,
        createdAt: DateTime.utc(2026, 10, 13),
        endRequestedByUserId: endRequestedBy,
        endRequestExpiresAt:
            status == MatchStatus.endRequested ? DateTime.now().add(const Duration(days: 6, hours: 1)) : null,
        endReason: endReason,
      );

  setUpAll(() => registerFallbackValue(Direction.incoming));

  setUp(() async {
    repository = MockMatchRepository();
    discoveryRepository = MockDiscoveryRepository();
    skillRepository = MockSkillRepository();
    persistence = InMemorySessionPersistence();
    canInteract = true;
    await SessionStore(persistence).save(testSession());
    when(() => repository.listMatches()).thenAnswer((_) async => []);
    when(() => repository.listPendingRequests(any())).thenAnswer((_) async => []);
    stubNoOpenSwap(repository);
  });

  List<Override> overrides() => [
        sessionRepositoryProvider.overrideWithValue(quietSessionRepository()),
        reviewRepositoryProvider.overrideWithValue(quietReviewRepository()),
        matchRepositoryProvider.overrideWithValue(repository),
        discoveryRepositoryProvider.overrideWithValue(discoveryRepository),
        skillRepositoryProvider.overrideWithValue(skillRepository),
        sessionStoreProvider.overrideWithValue(SessionStore(persistence)),
        citiesProvider.overrideWith((ref) async => testCities),
        interactionRequirementsProvider.overrideWith(
          (ref) async => testRequirements(emailVerified: canInteract),
        ),
      ];

  Future<void> pump(WidgetTester tester, Widget screen) async {
    await tester.pumpWidget(ProviderScope(overrides: overrides(), child: routed(screen)));
    await tester.pumpAndSettle();
  }

  group('labels', () {
    test('remaining time is rounded down to whole days, then hours', () {
      final now = DateTime.utc(2026, 10, 13, 12);

      expect(remainingLabel(l10n, now.add(const Duration(days: 2, hours: 23)), now), l10n.remainingDays(2));
      expect(remainingLabel(l10n, now.add(const Duration(hours: 23, minutes: 59)), now), l10n.remainingHours(23));
      expect(remainingLabel(l10n, now.add(const Duration(minutes: 59)), now), l10n.remainingSoon);
      expect(remainingLabel(l10n, now.subtract(const Duration(minutes: 5)), now), l10n.remainingSoon);
    });

    test('a user is named by display name, then username, then as deleted', () {
      expect(userLabel(l10n, deniz), 'Deniz Kaya');
      expect(userLabel(l10n, const UserSummary(userId: 'x', isDeleted: false, username: 'can')), '@can');
      expect(userLabel(l10n, const UserSummary(userId: 'x', isDeleted: true)), l10n.deletedUser);
    });

    test('a match is read from the side of whoever looks at it', () {
      final mine = MatchView(testMatch(status: MatchStatus.endRequested, endRequestedBy: deniz.userId), me.userId);
      final theirs = MatchView(testMatch(status: MatchStatus.endRequested, endRequestedBy: deniz.userId), deniz.userId);

      expect((mine.other, mine.taught, mine.learned), (deniz, guitar, spanish));
      expect((theirs.other, theirs.taught, theirs.learned), (me, spanish, guitar));
      expect(mine.endRequestedByOther, isTrue);
      expect(theirs.endRequestedByOther, isFalse);
    });
  });

  group('MatchActions', () {
    late ProviderContainer container;

    setUp(() {
      container = ProviderContainer.test(overrides: overrides());
      container.listen(matchesProvider, (_, _) {});
      container.listen(pendingRequestsProvider(Direction.incoming), (_, _) {});
      container.listen(matchActionsProvider, (_, _) {});
    });

    test('a successful action reloads the lists', () async {
      when(() => repository.acceptRequest('request-in')).thenAnswer((_) async => testMatch());
      await container.read(matchesProvider.future);

      final accepted = await container.read(matchActionsProvider.notifier).acceptRequest('request-in');
      await container.read(matchesProvider.future);
      await container.read(pendingRequestsProvider(Direction.incoming).future);

      expect(accepted, isTrue);
      expect(container.read(matchActionsProvider).hasError, isFalse);
      verify(() => repository.listMatches()).called(2);
      verify(() => repository.listPendingRequests(Direction.incoming)).called(2);
    });

    test('a refused action reports failure, keeps the error and still reloads', () async {
      when(() => repository.declineRequest('request-in'))
          .thenThrow(const ApiException(statusCode: 409, code: 'invalid_state'));
      await container.read(pendingRequestsProvider(Direction.incoming).future);

      final declined = await container.read(matchActionsProvider.notifier).declineRequest('request-in');
      await container.read(pendingRequestsProvider(Direction.incoming).future);

      expect(declined, isFalse);
      expect((container.read(matchActionsProvider).error! as ApiException).code, 'invalid_state');
      verify(() => repository.listPendingRequests(Direction.incoming)).called(2);
    });

    test('a blank note is sent as no note', () async {
      when(
        () => repository.sendRequest(
          recipientId: any(named: 'recipientId'),
          offeredSkillId: any(named: 'offeredSkillId'),
          requestedSkillId: any(named: 'requestedSkillId'),
          message: any(named: 'message'),
        ),
      ).thenAnswer((_) async => outgoingRequest());

      await container.read(matchActionsProvider.notifier).sendRequest(
            recipientId: deniz.userId,
            offeredSkillId: guitar.id,
            requestedSkillId: spanish.id,
            message: '   ',
          );

      verify(
        () => repository.sendRequest(
          recipientId: deniz.userId,
          offeredSkillId: guitar.id,
          requestedSkillId: spanish.id,
          message: null,
        ),
      ).called(1);
    });
  });

  group('MatchesScreen', () {
    testWidgets('shows matches from my side and opens one', (tester) async {
      when(() => repository.listMatches()).thenAnswer((_) async => [testMatch()]);

      await pump(tester, const MatchesScreen());

      expect(find.text('Deniz Kaya'), findsOneWidget);
      expect(find.text(l10n.matchStatusActive), findsOneWidget);
      expect(find.text(l10n.swapYouTeach('Gitar')), findsOneWidget);
      expect(find.text(l10n.swapYouLearn('İspanyolca')), findsOneWidget);

      await tester.tap(find.text('Deniz Kaya'));
      await tester.pumpAndSettle();
      expect(find.text('route:/matches/match-1'), findsOneWidget);
    });

    testWidgets('lists the matches still going on apart from those that ended', (tester) async {
      when(() => repository.listMatches()).thenAnswer(
        (_) async => [
          testMatch(status: MatchStatus.ended),
          testMatch(status: MatchStatus.endRequested, endRequestedBy: 'user-deniz'),
        ],
      );

      await pump(tester, const MatchesScreen());

      final open = tester.getTopLeft(find.text(l10n.matchesOpenTitle(1, 3))).dy;
      final asked = tester.getTopLeft(find.text(l10n.matchStatusEndRequested)).dy;
      final endedTitle = tester.getTopLeft(find.text(l10n.matchesEndedTitle)).dy;
      final ended = tester.getTopLeft(find.text(l10n.matchStatusEnded)).dy;
      expect([open < asked, asked < endedTitle, endedTitle < ended], [true, true, true]);
    });

    testWidgets('says so when no match is going on but some ended', (tester) async {
      when(() => repository.listMatches()).thenAnswer((_) async => [testMatch(status: MatchStatus.ended)]);

      await pump(tester, const MatchesScreen());

      expect(find.text(l10n.matchesOpenTitle(0, 3)), findsOneWidget);
      expect(find.text(l10n.matchesOpenEmpty), findsOneWidget);
      expect(find.text(l10n.matchesEndedTitle), findsOneWidget);
    });

    testWidgets('says so when there are no matches or requests', (tester) async {
      await pump(tester, const MatchesScreen());
      expect(find.text(l10n.matchesEmpty), findsOneWidget);

      await tester.tap(find.text(l10n.matchesTabIncoming));
      await tester.pumpAndSettle();
      expect(find.text(l10n.requestsIncomingEmpty), findsOneWidget);

      await tester.tap(find.text(l10n.matchesTabOutgoing));
      await tester.pumpAndSettle();
      expect(find.text(l10n.requestsOutgoingEmpty), findsOneWidget);
    });

    testWidgets('an incoming request shows its note and time left and can be accepted', (tester) async {
      var pending = [incomingRequest(message: 'Merhaba!')];
      when(() => repository.listPendingRequests(Direction.incoming)).thenAnswer((_) async => pending);
      when(() => repository.acceptRequest('request-in')).thenAnswer((_) async {
        pending = [];
        return testMatch();
      });
      await pump(tester, const MatchesScreen());
      await tester.tap(find.text(l10n.matchesTabIncoming));
      await tester.pumpAndSettle();

      expect(find.text('Deniz Kaya'), findsOneWidget);
      expect(find.text(l10n.remainingDays(2)), findsOneWidget);
      // Deniz offered Spanish, so that is what I would learn.
      expect(find.text(l10n.swapYouLearn('İspanyolca')), findsOneWidget);
      expect(find.text(l10n.swapYouTeach('Gitar')), findsOneWidget);
      expect(find.text('“Merhaba!”'), findsOneWidget);

      await tester.tap(find.text(l10n.requestAccept));
      await tester.pumpAndSettle();

      verify(() => repository.acceptRequest('request-in')).called(1);
      expect(find.text(l10n.requestsIncomingEmpty), findsOneWidget);
    });

    testWidgets('an incoming request can be declined', (tester) async {
      when(() => repository.listPendingRequests(Direction.incoming)).thenAnswer((_) async => [incomingRequest()]);
      when(() => repository.declineRequest('request-in')).thenAnswer((_) async => incomingRequest());
      await pump(tester, const MatchesScreen());
      await tester.tap(find.text(l10n.matchesTabIncoming));
      await tester.pumpAndSettle();

      await tester.tap(find.text(l10n.requestDecline));
      await tester.pumpAndSettle();

      verify(() => repository.declineRequest('request-in')).called(1);
    });

    testWidgets('accepting without meeting the requirements explains what is missing', (tester) async {
      when(() => repository.listPendingRequests(Direction.incoming)).thenAnswer((_) async => [incomingRequest()]);
      when(() => repository.acceptRequest('request-in'))
          .thenThrow(const ApiException(statusCode: 403, code: 'interaction_requirements_not_met'));
      await pump(tester, const MatchesScreen());
      await tester.tap(find.text(l10n.matchesTabIncoming));
      await tester.pumpAndSettle();

      await tester.tap(find.text(l10n.requestAccept));
      await tester.pumpAndSettle();

      expect(find.text(l10n.interactionRequirements), findsOneWidget);
      expect(find.text(l10n.interactionGoToProfile), findsOneWidget);
    });

    testWidgets('an outgoing request can be withdrawn, and a refusal is explained', (tester) async {
      when(() => repository.listPendingRequests(Direction.outgoing)).thenAnswer((_) async => [outgoingRequest()]);
      when(() => repository.cancelRequest('request-out'))
          .thenThrow(const ApiException(statusCode: 409, code: 'invalid_state'));
      await pump(tester, const MatchesScreen());
      await tester.tap(find.text(l10n.matchesTabOutgoing));
      await tester.pumpAndSettle();

      expect(find.text(l10n.remainingHours(5)), findsOneWidget);
      expect(find.text(l10n.swapYouTeach('Gitar')), findsOneWidget);
      expect(find.text(l10n.requestAccept), findsNothing);

      await tester.tap(find.text(l10n.requestCancel));
      await tester.pumpAndSettle();

      verify(() => repository.cancelRequest('request-out')).called(1);
      expect(find.text(l10n.errorInvalidState), findsOneWidget);
    });

    testWidgets('shows a safe message and recovers on retry when loading fails', (tester) async {
      when(() => repository.listMatches()).thenThrow(const ApiException(code: ApiException.networkCode));
      await pump(tester, const MatchesScreen());
      expect(find.text(l10n.errorNetwork), findsOneWidget);

      when(() => repository.listMatches()).thenAnswer((_) async => [testMatch()]);
      await tester.tap(find.text(l10n.errorRetry));
      await tester.pumpAndSettle();

      expect(find.text('Deniz Kaya'), findsOneWidget);
    });
  });

  group('MatchDetailScreen', () {
    testWidgets('an active match can be asked to end after confirming', (tester) async {
      var match = testMatch();
      when(() => repository.getMatch('match-1')).thenAnswer((_) async => match);
      when(() => repository.requestEnd('match-1')).thenAnswer((_) async {
        match = testMatch(status: MatchStatus.endRequested, endRequestedBy: me.userId);
        return match;
      });
      await pump(tester, const MatchDetailScreen(matchId: 'match-1'));

      expect(find.text('Deniz Kaya'), findsOneWidget);
      expect(find.text(l10n.swapYouTeach('Gitar')), findsOneWidget);

      await tester.ensureVisible(find.text(l10n.matchEndRequest));
      await tester.tap(find.text(l10n.matchEndRequest));
      await tester.pumpAndSettle();
      expect(find.text(l10n.matchEndConfirmBody), findsOneWidget);
      verifyNever(() => repository.requestEnd(any()));

      await tester.tap(find.widgetWithText(FilledButton, l10n.matchEndRequest));
      await tester.pumpAndSettle();

      verify(() => repository.requestEnd('match-1')).called(1);
      expect(find.text(l10n.matchEndWaitingOther), findsOneWidget);
      expect(find.text(l10n.remainingDays(6)), findsOneWidget);
      expect(find.text(l10n.matchEndWithdraw), findsOneWidget);
    });

    testWidgets('my own end request can be withdrawn', (tester) async {
      when(() => repository.getMatch('match-1'))
          .thenAnswer((_) async => testMatch(status: MatchStatus.endRequested, endRequestedBy: me.userId));
      when(() => repository.withdrawEndRequest('match-1')).thenAnswer((_) async => testMatch());
      await pump(tester, const MatchDetailScreen(matchId: 'match-1'));

      await tester.ensureVisible(find.text(l10n.matchEndWithdraw));
      await tester.tap(find.text(l10n.matchEndWithdraw));
      await tester.pumpAndSettle();

      verify(() => repository.withdrawEndRequest('match-1')).called(1);
    });

    testWidgets('the other user’s end request can be approved or rejected', (tester) async {
      when(() => repository.getMatch('match-1'))
          .thenAnswer((_) async => testMatch(status: MatchStatus.endRequested, endRequestedBy: deniz.userId));
      when(() => repository.acceptEnd('match-1')).thenAnswer((_) async => testMatch(status: MatchStatus.ended));
      when(() => repository.rejectEnd('match-1')).thenAnswer((_) async => testMatch());
      await pump(tester, const MatchDetailScreen(matchId: 'match-1'));

      expect(find.text(l10n.matchEndAskedByOther('Deniz Kaya')), findsOneWidget);
      expect(find.text(l10n.matchEndWithdraw), findsNothing);

      await tester.ensureVisible(find.text(l10n.matchEndReject));
      await tester.tap(find.text(l10n.matchEndReject));
      await tester.pumpAndSettle();
      await tester.ensureVisible(find.text(l10n.matchEndAccept));
      await tester.tap(find.text(l10n.matchEndAccept));
      await tester.pumpAndSettle();

      verify(() => repository.rejectEnd('match-1')).called(1);
      verify(() => repository.acceptEnd('match-1')).called(1);
    });

    testWidgets('an ended match says how it ended and offers no actions', (tester) async {
      when(() => repository.getMatch('match-1')).thenAnswer(
        (_) async => testMatch(status: MatchStatus.ended, endReason: MatchEndReason.endRequestExpired),
      );

      await pump(tester, const MatchDetailScreen(matchId: 'match-1'));

      expect(find.text(l10n.matchEndedExpired), findsOneWidget);
      expect(find.text(l10n.matchEndRequest), findsNothing);
    });

    testWidgets('shows a safe message when the match cannot be loaded', (tester) async {
      when(() => repository.getMatch('match-1')).thenThrow(const ApiException(statusCode: 404, code: 'not_found'));

      await pump(tester, const MatchDetailScreen(matchId: 'match-1'));

      expect(find.text(l10n.errorNotFound), findsOneWidget);
      expect(find.text(l10n.errorRetry), findsOneWidget);
    });
  });

  group('SendRequestScreen', () {
    void stubSkills({bool theyOffer = true}) {
      when(() => skillRepository.listOwnSkills()).thenAnswer(
        (_) async => [testUserSkill('Gitar'), testUserSkill('Yoga', type: UserSkillType.wanted)],
      );
      when(() => discoveryRepository.listUserSkills('user-deniz')).thenAnswer(
        (_) async => theyOffer ? [testUserSkill('İspanyolca', level: SkillLevel.expert)] : [],
      );
    }

    void stubSend(Future<SwapRequest> Function() answer) => when(
          () => repository.sendRequest(
            recipientId: any(named: 'recipientId'),
            offeredSkillId: any(named: 'offeredSkillId'),
            requestedSkillId: any(named: 'requestedSkillId'),
            message: any(named: 'message'),
          ),
        ).thenAnswer((_) => answer());

    testWidgets('sends the chosen pair with the note and returns to the profile', (tester) async {
      stubSkills();
      stubSend(() async => outgoingRequest());
      await pump(tester, const SendRequestScreen(recipientId: 'user-deniz'));

      // Only offered skills can be taught; the wanted one is not a choice.
      expect(find.textContaining('Yoga'), findsNothing);
      expect(tester.widget<FilledButton>(find.byType(FilledButton)).onPressed, isNull);

      await tester.tap(find.textContaining('Gitar'));
      await tester.tap(find.textContaining('İspanyolca'));
      await tester.enterText(find.byType(TextField), '  Merhaba!  ');
      await tester.pumpAndSettle();
      await tester.tap(find.text(l10n.requestSubmit));
      await tester.pumpAndSettle();

      verify(
        () => repository.sendRequest(
          recipientId: 'user-deniz',
          offeredSkillId: 'skill-gitar',
          requestedSkillId: testSkill('İspanyolca').id,
          message: 'Merhaba!',
        ),
      ).called(1);
      expect(find.text('route:/users/user-deniz'), findsOneWidget);
    });

    testWidgets('explains a refusal and stays on the form', (tester) async {
      stubSkills();
      stubSend(() async => throw const ApiException(statusCode: 409, code: 'swap_request_cooldown'));
      await pump(tester, const SendRequestScreen(recipientId: 'user-deniz'));

      await tester.tap(find.textContaining('Gitar'));
      await tester.tap(find.textContaining('İspanyolca'));
      await tester.pumpAndSettle();
      await tester.tap(find.text(l10n.requestSubmit));
      await tester.pumpAndSettle();

      expect(find.text(l10n.errorRequestCooldown), findsOneWidget);
      expect(find.text(l10n.requestSendTitle), findsOneWidget);
    });

    testWidgets('says so when the other user offers nothing', (tester) async {
      stubSkills(theyOffer: false);

      await pump(tester, const SendRequestScreen(recipientId: 'user-deniz'));

      expect(find.text(l10n.requestTheirSkillsEmpty), findsOneWidget);
    });
  });

  group('request button on a user profile', () {
    const profile = PublicProfile(
      userId: 'user-deniz',
      username: 'deniz',
      displayName: 'Deniz Kaya',
      acceptsOnline: true,
      acceptsInPerson: false,
      rating: RatingSummary(reviewCount: 0),
    );

    void stubProfile({required bool offers}) {
      when(() => discoveryRepository.getUserProfile('user-deniz')).thenAnswer((_) async => profile);
      when(() => discoveryRepository.listUserSkills('user-deniz'))
          .thenAnswer((_) async => offers ? [testUserSkill('İspanyolca')] : []);
    }

    testWidgets('opens the request form for a user who can interact', (tester) async {
      stubProfile(offers: true);
      await pump(tester, const UserProfileScreen(userId: 'user-deniz'));

      await tester.tap(find.text(l10n.requestSend));
      await tester.pumpAndSettle();

      expect(find.text('route:/users/user-deniz/request'), findsOneWidget);
    });

    testWidgets('explains the requirements to a user who cannot interact yet', (tester) async {
      canInteract = false;
      stubProfile(offers: true);
      await pump(tester, const UserProfileScreen(userId: 'user-deniz'));

      await tester.tap(find.text(l10n.requestSend));
      await tester.pumpAndSettle();

      expect(find.text(l10n.interactionRequirements), findsOneWidget);
      expect(find.text('route:/users/user-deniz/request'), findsNothing);
    });

    testWidgets('leads to the open match instead of a new request', (tester) async {
      stubProfile(offers: true);
      final match = testMatch();
      when(() => repository.findOpenMatch('user-deniz')).thenAnswer((_) async => match);
      await pump(tester, const UserProfileScreen(userId: 'user-deniz'));

      expect(find.text(l10n.requestSend), findsNothing);
      await tester.tap(find.text(l10n.matchView));
      await tester.pumpAndSettle();

      expect(find.text('route:/matches/${match.id}'), findsOneWidget);
    });

    testWidgets('says a sent request is waiting and offers nothing to press', (tester) async {
      stubProfile(offers: true);
      when(() => repository.findPendingRequest(Direction.outgoing, 'user-deniz')).thenAnswer((_) async => outgoingRequest());
      await pump(tester, const UserProfileScreen(userId: 'user-deniz'));

      expect(find.text(l10n.requestSend), findsNothing);
      expect(tester.widget<FilledButton>(find.widgetWithText(FilledButton, l10n.requestAlreadySent)).onPressed, isNull);
    });

    testWidgets('leads to a request received from this user', (tester) async {
      stubProfile(offers: true);
      when(() => repository.findPendingRequest(Direction.incoming, 'user-deniz')).thenAnswer((_) async => incomingRequest());
      await pump(tester, const UserProfileScreen(userId: 'user-deniz'));

      await tester.tap(find.text(l10n.requestViewIncoming));
      await tester.pumpAndSettle();

      expect(find.text('route:/matches'), findsOneWidget);
    });

    testWidgets('still offers a request when the standing cannot be loaded', (tester) async {
      stubProfile(offers: true);
      when(() => repository.findOpenMatch('user-deniz')).thenThrow(const ApiException(code: ApiException.networkCode));
      await pump(tester, const UserProfileScreen(userId: 'user-deniz'));

      expect(tester.widget<FilledButton>(find.widgetWithText(FilledButton, l10n.requestSend)).onPressed, isNotNull);
    });

    testWidgets('is disabled when the user offers nothing', (tester) async {
      stubProfile(offers: false);

      await pump(tester, const UserProfileScreen(userId: 'user-deniz'));

      expect(tester.widget<FilledButton>(find.widgetWithText(FilledButton, l10n.requestSend)).onPressed, isNull);
    });
  });
}
