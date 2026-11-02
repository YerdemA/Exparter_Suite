import 'package:exparter/core/api/api_exception.dart';
import 'package:exparter/core/api/generated/export.dart';
import 'package:exparter/features/matches/data/api_match_repository.dart';
import 'package:exparter/features/discovery/data/api_discovery_repository.dart';
import 'package:exparter/features/discovery/domain/discovery_repository.dart';
import 'package:exparter/features/discovery/presentation/discovery_controller.dart';
import 'package:exparter/features/discovery/presentation/discovery_labels.dart';
import 'package:exparter/features/discovery/presentation/discovery_screen.dart';
import 'package:exparter/features/discovery/presentation/user_profile_screen.dart';
import 'package:exparter/features/profile/data/api_profile_repository.dart';
import 'package:exparter/features/reviews/data/api_review_repository.dart';
import 'package:exparter/features/sessions/data/api_session_repository.dart';
import 'package:exparter/features/skills/data/api_skill_repository.dart';
import 'package:exparter/features/skills/presentation/skill_controller.dart';
import 'package:exparter/l10n/app_localizations_tr.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_riverpod/misc.dart' show Override;
import 'package:flutter_test/flutter_test.dart';
import 'package:mocktail/mocktail.dart';

import '../../helpers.dart';

void main() {
  final l10n = AppLocalizationsTr();
  late MockDiscoveryRepository repository;
  late MockSkillRepository skillRepository;

  setUpAll(() => registerFallbackValue(noDiscoveryFilters));

  setUp(() {
    repository = MockDiscoveryRepository();
    skillRepository = MockSkillRepository();
    when(() => skillRepository.searchSkills(categoryId: any(named: 'categoryId'), query: any(named: 'query')))
        .thenAnswer((_) async => testSkillPage([testSkill('Gitar'), testSkill('Piyano')]));
  });

  List<Override> overrides() => [
        sessionRepositoryProvider.overrideWithValue(quietSessionRepository()),
        reviewRepositoryProvider.overrideWithValue(quietReviewRepository()),
        discoveryRepositoryProvider.overrideWithValue(repository),
        matchRepositoryProvider.overrideWithValue(quietMatchRepository()),
        skillRepositoryProvider.overrideWithValue(skillRepository),
        skillCategoriesProvider.overrideWith((ref) async => testCategories),
        citiesProvider.overrideWith((ref) async => testCities),
      ];

  void stubPage(List<DiscoveryUser> items, {String? nextCursor, String? cursor}) =>
      when(() => repository.discover(filters: any(named: 'filters'), cursor: cursor))
          .thenAnswer((_) async => DiscoveryUserCursorPage(items: items, nextCursor: nextCursor));

  const notFound = ApiException(statusCode: 404, code: 'not_found');

  group('labels', () {
    test('modes name the city only for in-person swaps', () {
      expect(
        swapModesLabel(l10n, acceptsOnline: true, acceptsInPerson: true, cityName: 'İzmir'),
        '${l10n.profileModeOnline} · ${l10n.profileModeInPersonIn('İzmir')}',
      );
      expect(
        swapModesLabel(l10n, acceptsOnline: true, acceptsInPerson: false, cityName: null),
        l10n.profileModeOnline,
      );
      expect(swapModesLabel(l10n, acceptsOnline: false, acceptsInPerson: false, cityName: null), isNull);
    });
  });

  group('DiscoveryFeed', () {
    late ProviderContainer container;
    final provider = discoveryFeedProvider(noDiscoveryFilters);

    setUp(() => container = ProviderContainer.test(overrides: overrides()));

    // Listening is what loads the first page, so tests stub the repository first.
    void listen() => container.listen(provider, (_, _) {});

    test('loads the first page and then appends the next one once', () async {
      stubPage([testDiscoveryUser('ali'), testDiscoveryUser('ayse')], nextCursor: 'c1');
      // The list shifted between pages: "ayse" comes back on the second page.
      stubPage([testDiscoveryUser('ayse'), testDiscoveryUser('can')], cursor: 'c1');
      listen();

      final first = await container.read(provider.future);
      expect(first.users.map((item) => item.user.username), ['ali', 'ayse']);
      expect(first.hasMore, isTrue);

      await container.read(provider.notifier).loadMore();
      final all = container.read(provider).value!;
      expect(all.users.map((item) => item.user.username), ['ali', 'ayse', 'can']);
      expect(all.hasMore, isFalse);

      await container.read(provider.notifier).loadMore();
      verify(() => repository.discover(filters: any(named: 'filters'), cursor: 'c1')).called(1);
    });

    test('keeps what is loaded when the next page fails, and can try again', () async {
      stubPage([testDiscoveryUser('ali')], nextCursor: 'c1');
      when(() => repository.discover(filters: any(named: 'filters'), cursor: 'c1'))
          .thenThrow(const ApiException(code: ApiException.networkCode));
      listen();
      await container.read(provider.future);

      await container.read(provider.notifier).loadMore();
      final failed = container.read(provider).value!;
      expect(failed.users, hasLength(1));
      expect(failed.loadMoreFailed, isTrue);

      stubPage([testDiscoveryUser('can')], cursor: 'c1');
      await container.read(provider.notifier).loadMore();
      final retried = container.read(provider).value!;
      expect(retried.users, hasLength(2));
      expect(retried.loadMoreFailed, isFalse);
    });
  });

  group('DiscoveryScreen', () {
    Future<void> pump(WidgetTester tester) async {
      await tester.pumpWidget(ProviderScope(overrides: overrides(), child: routed(const DiscoveryScreen())));
      await tester.pumpAndSettle();
    }

    testWidgets('shows each user with their fit, skills and where they meet', (tester) async {
      stubPage([
        testDiscoveryUser(
          'deniz',
          displayName: 'Deniz Kaya',
          compatibility: Compatibility.mutual,
          offered: [testDiscoverySkill('Gitar', level: SkillLevel.advanced)],
          wanted: [testDiscoverySkill('Yoga')],
        ),
        testDiscoveryUser('yeni', compatibility: Compatibility.oneWay),
        testDiscoveryUser('can', displayName: 'Can'),
      ]);

      await pump(tester);

      expect(find.text('Deniz Kaya'), findsOneWidget);
      expect(find.text('@deniz'), findsOneWidget);
      expect(find.text(l10n.discoverMutual), findsOneWidget);
      expect(find.text(l10n.discoverOneWay), findsOneWidget);
      expect(
        find.text('${l10n.discoverOffers}: Gitar · ${l10n.skillLevelAdvanced}', findRichText: true),
        findsOneWidget,
      );
      expect(
        find.text(
          '${l10n.discoverWants}: Yoga · ${l10n.skillWantedLevel(l10n.skillLevelIntermediate)}',
          findRichText: true,
        ),
        findsOneWidget,
      );
      expect(find.textContaining('İstanbul'), findsNWidgets(3));
      // A user without a profile is shown by username alone.
      expect(find.text('@yeni'), findsOneWidget);
    });

    testWidgets('opens the profile of the tapped user', (tester) async {
      stubPage([testDiscoveryUser('deniz', displayName: 'Deniz Kaya')]);
      await pump(tester);

      await tester.tap(find.text('Deniz Kaya'));
      await tester.pumpAndSettle();

      expect(find.text('route:/users/user-deniz'), findsOneWidget);
    });

    testWidgets('says so when there is nobody to show', (tester) async {
      stubPage([]);

      await pump(tester);

      expect(find.text(l10n.discoverEmpty), findsOneWidget);
    });

    testWidgets('searches after a pause in typing and says when nothing matches', (tester) async {
      stubPage([testDiscoveryUser('deniz', displayName: 'Deniz Kaya')]);
      await pump(tester);
      when(
        () => repository.discover(
          filters: (query: 'zeynep', skillId: null, categoryId: null, city: null, mode: null),
          cursor: any(named: 'cursor'),
        ),
      ).thenAnswer((_) async => const DiscoveryUserCursorPage(items: []));

      await tester.enterText(find.byType(TextField), ' zeynep ');
      await tester.pump(const Duration(milliseconds: 400));
      await tester.pumpAndSettle();

      expect(find.text(l10n.discoverSearchEmpty), findsOneWidget);
      expect(find.text('Deniz Kaya'), findsNothing);
    });

    testWidgets('filters by mode and category', (tester) async {
      stubPage([]);
      await pump(tester);

      await tester.tap(find.widgetWithText(ChoiceChip, l10n.profileModeOnline));
      await tester.pumpAndSettle();
      // The categories follow the other filters in a row that scrolls sideways.
      await tester.dragUntilVisible(
        find.widgetWithText(ChoiceChip, 'Müzik'),
        find.byType(ListView).first,
        const Offset(-200, 0),
      );
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(ChoiceChip, 'Müzik'));
      await tester.pumpAndSettle();

      verify(
        () => repository.discover(
          filters: (query: '', skillId: null, categoryId: 'category-music', city: null, mode: SessionMode.online),
          cursor: any(named: 'cursor'),
        ),
      ).called(1);
    });

    testWidgets('filters by a skill picked from the catalog, and clears it again', (tester) async {
      stubPage([]);
      await pump(tester);

      await tester.tap(find.text(l10n.discoverAllSkills));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Gitar'));
      await tester.pumpAndSettle();

      expect(find.widgetWithText(ActionChip, 'Gitar'), findsOneWidget);
      verify(
        () => repository.discover(
          filters: (query: '', skillId: 'skill-gitar', categoryId: null, city: null, mode: null),
          cursor: any(named: 'cursor'),
        ),
      ).called(1);

      await tester.tap(find.widgetWithText(ActionChip, 'Gitar'));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(ListTile, l10n.discoverAllSkills));
      await tester.pumpAndSettle();

      expect(find.widgetWithText(ActionChip, l10n.discoverAllSkills), findsOneWidget);
    });

    testWidgets('filters by the chosen city', (tester) async {
      stubPage([]);
      await pump(tester);

      await tester.tap(find.text(l10n.discoverAllCities));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Ankara'));
      await tester.pumpAndSettle();

      expect(find.widgetWithText(ActionChip, 'Ankara'), findsOneWidget);
      verify(
        () => repository.discover(
          filters: (query: '', skillId: null, categoryId: null, city: '06', mode: null),
          cursor: any(named: 'cursor'),
        ),
      ).called(1);
    });

    testWidgets('shows a safe message and recovers on retry when loading fails', (tester) async {
      when(() => repository.discover(filters: any(named: 'filters'), cursor: any(named: 'cursor')))
          .thenThrow(const ApiException(code: ApiException.networkCode));
      await pump(tester);
      expect(find.text(l10n.errorNetwork), findsOneWidget);

      stubPage([testDiscoveryUser('deniz', displayName: 'Deniz Kaya')]);
      await tester.tap(find.text(l10n.errorRetry));
      await tester.pumpAndSettle();

      expect(find.text('Deniz Kaya'), findsOneWidget);
    });
  });

  group('UserProfileScreen', () {
    const profile = PublicProfile(
      userId: 'user-deniz',
      username: 'deniz',
      displayName: 'Deniz Kaya',
      bio: 'Gitar çalıyorum.',
      city: '34',
      acceptsOnline: false,
      acceptsInPerson: true,
      rating: RatingSummary(reviewCount: 0),
    );

    Future<void> pump(WidgetTester tester) async {
      await tester.pumpWidget(
        ProviderScope(overrides: overrides(), child: localized(const UserProfileScreen(userId: 'user-deniz'))),
      );
      await tester.pumpAndSettle();
    }

    testWidgets('shows the profile and both kinds of skills', (tester) async {
      when(() => repository.getUserProfile('user-deniz')).thenAnswer((_) async => profile);
      when(() => repository.listUserSkills('user-deniz')).thenAnswer(
        (_) async => [testUserSkill('Gitar', level: SkillLevel.advanced)],
      );

      await pump(tester);

      expect(find.text('Deniz Kaya'), findsOneWidget);
      expect(find.text('@deniz'), findsOneWidget);
      expect(find.text('Gitar çalıyorum.'), findsOneWidget);
      expect(find.text(l10n.profileModeInPersonIn('İstanbul')), findsOneWidget);
      expect(find.text('Gitar · ${l10n.skillLevelAdvanced}'), findsOneWidget);
      // Nothing wanted yet.
      expect(find.text(l10n.userSkillsEmpty), findsOneWidget);
    });

    testWidgets('says so when the user has not filled in their profile', (tester) async {
      when(() => repository.getUserProfile('user-deniz')).thenAnswer(
        (_) async => const PublicProfile(
          userId: 'user-deniz',
          username: 'deniz',
          acceptsOnline: false,
          acceptsInPerson: false,
          rating: RatingSummary(reviewCount: 0),
        ),
      );
      when(() => repository.listUserSkills('user-deniz')).thenAnswer((_) async => []);

      await pump(tester);

      expect(find.text('@deniz'), findsOneWidget);
      expect(find.text(l10n.userProfileIncomplete), findsOneWidget);
    });

    testWidgets('shows a safe message when the user cannot be found', (tester) async {
      when(() => repository.getUserProfile('user-deniz')).thenThrow(notFound);
      when(() => repository.listUserSkills('user-deniz')).thenThrow(notFound);

      await pump(tester);

      expect(find.text(l10n.errorNotFound), findsOneWidget);
      expect(find.text(l10n.errorRetry), findsOneWidget);
    });
  });
}
