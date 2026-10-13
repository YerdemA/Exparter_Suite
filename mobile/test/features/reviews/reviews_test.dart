import 'package:exparter/core/api/api_exception.dart';
import 'package:exparter/core/api/api_providers.dart';
import 'package:exparter/core/api/generated/export.dart';
import 'package:exparter/core/auth/interaction_requirements.dart';
import 'package:exparter/core/auth/session_store.dart';
import 'package:exparter/features/reviews/data/api_review_repository.dart';
import 'package:exparter/features/reviews/presentation/review_controller.dart';
import 'package:exparter/features/reviews/presentation/user_reviews_section.dart';
import 'package:exparter/features/reviews/presentation/write_review_screen.dart';
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
  const rated = RatingSummary(reviewCount: 3, averageRating: 8.5);
  const unrated = RatingSummary(reviewCount: 0);

  late MockReviewRepository repository;
  late InMemorySessionPersistence persistence;
  var canInteract = true;

  Review review({String id = 'review-1', bool mine = false, int rating = 9, String? comment = 'Çok sabırlı.'}) => Review(
        id: id,
        reviewer: UserSummary(
          userId: mine ? myId : 'user-can',
          isDeleted: false,
          username: mine ? 'ayse' : 'can',
          displayName: mine ? null : 'Can Demir',
        ),
        revieweeId: otherId,
        sessionId: 'session-1',
        rating: rating,
        createdAt: DateTime.utc(2026, 10, 13, 9),
        comment: comment,
      );

  void stubReviews(List<Review> Function() reviews) =>
      when(() => repository.listReceived(any())).thenAnswer((_) async => reviews());

  void stubEligibility({required bool canReview}) => when(() => repository.getEligibility(any())).thenAnswer(
        (_) async => ReviewEligibility(
          canReview: canReview,
          reason: canReview ? null : ReviewBlockReason.alreadyReviewed,
        ),
      );

  void stubCreate(Future<Review> Function() answer) => when(
        () => repository.create(any(), rating: any(named: 'rating'), comment: any(named: 'comment')),
      ).thenAnswer((_) => answer());

  setUp(() async {
    repository = MockReviewRepository();
    persistence = InMemorySessionPersistence();
    canInteract = true;
    await SessionStore(persistence).save(testSession());
    stubReviews(() => []);
    stubEligibility(canReview: false);
  });

  List<Override> overrides() => [
        reviewRepositoryProvider.overrideWithValue(repository),
        sessionStoreProvider.overrideWithValue(SessionStore(persistence)),
        interactionRequirementsProvider.overrideWith(
          (ref) async => InteractionRequirements(
            emailVerified: canInteract,
            profileCompleted: true,
            hasOfferedSkill: true,
            canInteract: canInteract,
          ),
        ),
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

  group('ReviewActions', () {
    late ProviderContainer container;

    setUp(() {
      container = ProviderContainer.test(overrides: overrides());
      container.listen(userReviewsProvider(otherId), (_, _) {});
      container.listen(reviewActionsProvider, (_, _) {});
    });

    test('a review is sent with a trimmed comment and reloads the list', () async {
      await container.read(userReviewsProvider(otherId).future);
      stubCreate(() async => review(mine: true));
      stubReviews(() => [review(mine: true)]);

      expect(
        await container.read(reviewActionsProvider.notifier).create(otherId, rating: 9, comment: '  Çok sabırlı.  '),
        isTrue,
      );

      verify(() => repository.create(otherId, rating: 9, comment: 'Çok sabırlı.')).called(1);
      expect(await container.read(userReviewsProvider(otherId).future), hasLength(1));
    });

    test('a blank comment is sent as no comment', () async {
      stubCreate(() async => review(mine: true, comment: null));

      await container.read(reviewActionsProvider.notifier).create(otherId, rating: 4, comment: '   ');

      verify(() => repository.create(otherId, rating: 4, comment: null)).called(1);
    });

    test('a refused action reports failure and keeps the error', () async {
      when(() => repository.delete('review-1')).thenThrow(const ApiException(statusCode: 404, code: 'not_found'));

      expect(await container.read(reviewActionsProvider.notifier).delete('review-1'), isFalse);
      expect((container.read(reviewActionsProvider).error! as ApiException).code, 'not_found');
    });
  });

  group('RatingLabel', () {
    testWidgets('shows the average with one decimal and the number of reviews', (tester) async {
      await tester.pumpWidget(localized(const Scaffold(body: RatingLabel(rating: rated))));

      expect(find.text('8,5 · 3 değerlendirme'), findsOneWidget);
    });

    testWidgets('says when there are no reviews yet', (tester) async {
      await tester.pumpWidget(localized(const Scaffold(body: RatingLabel(rating: unrated))));

      expect(find.text(l10n.ratingNone), findsOneWidget);
    });
  });

  group('UserReviewsSection', () {
    testWidgets('lists the reviews with their author, score and comment', (tester) async {
      stubReviews(() => [review(), review(id: 'review-2', rating: 6, comment: null)]);
      await pump(tester, const UserReviewsSection(userId: otherId, rating: rated));

      expect(find.text(l10n.reviewsTitle), findsOneWidget);
      expect(find.text('8,5 · 3 değerlendirme'), findsOneWidget);
      expect(find.text('Can Demir'), findsNWidgets(2));
      expect(find.text('9/10'), findsOneWidget);
      expect(find.text('6/10'), findsOneWidget);
      expect(find.text('Çok sabırlı.'), findsOneWidget);
      // Someone else's review cannot be deleted, and this user cannot be reviewed again.
      expect(find.text(l10n.reviewDelete), findsNothing);
      expect(find.text(l10n.reviewWrite), findsNothing);
    });

    testWidgets('offers the review form to a user who can review', (tester) async {
      stubEligibility(canReview: true);
      await pump(tester, const UserReviewsSection(userId: otherId, rating: unrated));

      expect(find.text(l10n.ratingNone), findsOneWidget);
      await tapText(tester, l10n.reviewWrite);

      expect(find.text('route:/users/$otherId/review'), findsOneWidget);
    });

    testWidgets('a user who cannot interact yet is told why instead', (tester) async {
      stubEligibility(canReview: true);
      canInteract = false;
      await pump(tester, const UserReviewsSection(userId: otherId, rating: unrated));

      await tapText(tester, l10n.reviewWrite);

      expect(find.text(l10n.interactionRequirements), findsOneWidget);
    });

    testWidgets('nobody is offered a review of themselves', (tester) async {
      stubEligibility(canReview: true);
      await pump(tester, UserReviewsSection(userId: myId, rating: unrated));

      expect(find.text(l10n.reviewWrite), findsNothing);
    });

    testWidgets('the author deletes a review after confirming', (tester) async {
      var reviews = [review(mine: true)];
      stubReviews(() => reviews);
      when(() => repository.delete('review-1')).thenAnswer((_) async => reviews = []);
      await pump(tester, const UserReviewsSection(userId: otherId, rating: rated));

      await tapText(tester, l10n.reviewDelete);
      expect(find.text(l10n.reviewDeleteBody), findsOneWidget);
      verifyNever(() => repository.delete(any()));

      await tapText(tester, l10n.reviewDeleteConfirm);

      verify(() => repository.delete('review-1')).called(1);
      expect(find.text(l10n.reviewDeleted), findsOneWidget);
      expect(find.text('9/10'), findsNothing);
    });

    testWidgets('a failed load can be retried', (tester) async {
      when(() => repository.listReceived(any())).thenThrow(const ApiException(statusCode: 500, code: 'internal_error'));
      await pump(tester, const UserReviewsSection(userId: otherId, rating: rated));

      expect(find.text(l10n.reviewsUnavailable), findsOneWidget);
      stubReviews(() => [review()]);
      await tapText(tester, l10n.errorRetry);

      expect(find.text('9/10'), findsOneWidget);
    });
  });

  group('WriteReviewScreen', () {
    testWidgets('needs a score, then sends the review and returns to the profile', (tester) async {
      stubCreate(() async => review(mine: true));
      await pump(tester, const WriteReviewScreen(userId: otherId), scroll: false);

      expect(tester.widget<FilledButton>(find.widgetWithText(FilledButton, l10n.reviewSubmit)).onPressed, isNull);
      expect(find.byType(ChoiceChip), findsNWidgets(10));

      await tapText(tester, '7');
      await tester.enterText(find.byType(TextField), ' İyi anlatıyor. ');
      await tapText(tester, l10n.reviewSubmit);

      verify(() => repository.create(otherId, rating: 7, comment: 'İyi anlatıyor.')).called(1);
      expect(find.text('route:/users/$otherId'), findsOneWidget);
    });

    testWidgets('explains a refusal and stays on the form', (tester) async {
      stubCreate(() async => throw const ApiException(statusCode: 409, code: 'review_already_exists'));
      await pump(tester, const WriteReviewScreen(userId: otherId), scroll: false);

      await tapText(tester, '10');
      await tapText(tester, l10n.reviewSubmit);

      expect(find.text(l10n.errorReviewAlreadyExists), findsOneWidget);
      expect(find.text(l10n.reviewRatingLabel), findsOneWidget);
    });
  });
}
