import 'package:exparter/core/api/api_exception.dart';
import 'package:exparter/core/api/api_providers.dart';
import 'package:exparter/core/api/generated/export.dart';
import 'package:exparter/core/auth/interaction_requirements.dart';
import 'package:exparter/core/auth/session_controller.dart';
import 'package:exparter/core/auth/session_store.dart';
import 'package:exparter/features/skills/data/api_skill_repository.dart';
import 'package:exparter/features/skills/presentation/own_skills_section.dart';
import 'package:exparter/features/skills/presentation/skill_controller.dart';
import 'package:exparter/features/skills/presentation/skill_labels.dart';
import 'package:exparter/features/skills/presentation/skill_picker_screen.dart';
import 'package:exparter/l10n/app_localizations_tr.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_riverpod/misc.dart' show Override;
import 'package:flutter_test/flutter_test.dart';
import 'package:mocktail/mocktail.dart';

import '../../helpers.dart';

void main() {
  final l10n = AppLocalizationsTr();
  late MockSkillRepository repository;
  late InMemorySessionPersistence persistence;
  late int requirementsLoads;

  setUpAll(() {
    registerFallbackValue(UserSkillType.offered);
    registerFallbackValue(SkillLevel.beginner);
  });

  setUp(() async {
    repository = MockSkillRepository();
    persistence = InMemorySessionPersistence();
    requirementsLoads = 0;
    await SessionStore(persistence).save(testSession());
    when(() => repository.listCategories()).thenAnswer((_) async => testCategories);
    when(() => repository.listOwnSkills()).thenAnswer((_) async => []);
    when(() => repository.listOwnProposals()).thenAnswer((_) async => []);
    when(() => repository.searchSkills(categoryId: any(named: 'categoryId'), query: any(named: 'query')))
        .thenAnswer((_) async => testSkillPage([testSkill('Gitar'), testSkill('Piyano')]));
  });

  List<Override> overrides() => [
        skillRepositoryProvider.overrideWithValue(repository),
        sessionStoreProvider.overrideWithValue(SessionStore(persistence)),
        interactionRequirementsProvider.overrideWith((ref) async {
          requirementsLoads++;
          return testRequirements();
        }),
      ];

  group('labels', () {
    test('a wanted level reads as a minimum', () {
      expect(userSkillLevelLabel(l10n, UserSkillType.offered, SkillLevel.advanced), l10n.skillLevelAdvanced);
      expect(
        userSkillLevelLabel(l10n, UserSkillType.wanted, SkillLevel.advanced),
        l10n.skillWantedLevel(l10n.skillLevelAdvanced),
      );
    });
  });

  group('SkillActions', () {
    late ProviderContainer container;

    setUp(() async {
      container = ProviderContainer.test(overrides: overrides());
      container.listen(ownSkillsProvider, (_, _) {});
      container.listen(skillActionsProvider, (_, _) {});
      container.listen(interactionRequirementsProvider, (_, _) {});
      await container.read(sessionControllerProvider.future);
      await container.read(ownSkillsProvider.future);
      await container.read(interactionRequirementsProvider.future);
    });

    test('adding a skill reloads the skills and the interaction requirements', () async {
      when(() => repository.addOwnSkill(skillId: 'skill-gitar', type: UserSkillType.offered, level: SkillLevel.advanced))
          .thenAnswer((_) async => testUserSkill('Gitar', level: SkillLevel.advanced));
      when(() => repository.listOwnSkills()).thenAnswer((_) async => [testUserSkill('Gitar')]);
      final loadsBefore = requirementsLoads;

      final added = await container
          .read(skillActionsProvider.notifier)
          .add(skillId: 'skill-gitar', type: UserSkillType.offered, level: SkillLevel.advanced);

      expect(added, isTrue);
      expect(await container.read(ownSkillsProvider.future), hasLength(1));
      await container.read(interactionRequirementsProvider.future);
      expect(requirementsLoads, loadsBefore + 1);
    });

    test('a refused action reports failure and exposes the error', () async {
      when(
        () => repository.addOwnSkill(
          skillId: any(named: 'skillId'),
          type: any(named: 'type'),
          level: any(named: 'level'),
        ),
      ).thenThrow(const ApiException(code: 'skill_limit_reached', statusCode: 409));

      final added = await container
          .read(skillActionsProvider.notifier)
          .add(skillId: 'skill-gitar', type: UserSkillType.offered, level: SkillLevel.beginner);

      expect(added, isFalse);
      expect((container.read(skillActionsProvider).error! as ApiException).code, 'skill_limit_reached');
    });

    test('proposing trims the name and does not reload the skills', () async {
      when(() => repository.proposeSkill(categoryId: 'category-music', name: 'Ukulele'))
          .thenAnswer((_) async => testSkill('Ukulele', status: SkillStatus.pendingApproval));

      final proposed = await container
          .read(skillActionsProvider.notifier)
          .propose(categoryId: 'category-music', name: '  Ukulele ');

      expect(proposed, isTrue);
      verify(() => repository.listOwnSkills()).called(1);
    });
  });

  Future<void> pumpScreen(WidgetTester tester, Widget screen) async {
    tester.view.physicalSize = const Size(1080, 3200);
    tester.view.devicePixelRatio = 2;
    addTearDown(tester.view.reset);
    await tester.pumpWidget(ProviderScope(overrides: overrides(), child: routed(screen)));
    await tester.pumpAndSettle();
  }

  group('OwnSkillsSection', () {
    testWidgets('shows offered and wanted skills with their levels', (tester) async {
      when(() => repository.listOwnSkills()).thenAnswer(
        (_) async => [
          testUserSkill('Gitar', level: SkillLevel.advanced),
          testUserSkill('Piyano', type: UserSkillType.wanted, level: SkillLevel.intermediate),
        ],
      );
      await pumpScreen(tester, const Scaffold(body: OwnSkillsSection()));

      expect(find.text('Gitar · ${l10n.skillLevelAdvanced}'), findsOneWidget);
      expect(find.text('Piyano · ${l10n.skillWantedLevel(l10n.skillLevelIntermediate)}'), findsOneWidget);
      expect(find.text(l10n.skillsCount(1, 5)), findsNWidgets(2));
    });

    testWidgets('explains empty lists and offers to add', (tester) async {
      await pumpScreen(tester, const Scaffold(body: OwnSkillsSection()));

      expect(find.text(l10n.skillsOfferedEmpty), findsOneWidget);
      expect(find.text(l10n.skillsWantedEmpty), findsOneWidget);

      await tester.tap(find.text(l10n.skillsAddOffered));
      await tester.pumpAndSettle();
      expect(find.text('route:/profile/skills/add/offered'), findsOneWidget);
    });

    testWidgets('replaces the add button with an explanation at the limit', (tester) async {
      when(() => repository.listOwnSkills()).thenAnswer(
        (_) async => [for (final name in ['A1', 'B2', 'C3', 'D4', 'E5']) testUserSkill(name)],
      );
      await pumpScreen(tester, const Scaffold(body: OwnSkillsSection()));

      expect(find.text(l10n.skillsLimitReached), findsOneWidget);
      expect(find.text(l10n.skillsAddOffered), findsNothing);
      expect(find.text(l10n.skillsAddWanted), findsOneWidget);
    });

    testWidgets('a skill can be removed from its menu', (tester) async {
      final skill = testUserSkill('Gitar');
      when(() => repository.listOwnSkills()).thenAnswer((_) async => [skill]);
      when(() => repository.removeOwnSkill(userSkillId: skill.id)).thenAnswer((_) async {});
      await pumpScreen(tester, const Scaffold(body: OwnSkillsSection()));

      await tester.tap(find.byType(ActionChip));
      await tester.pumpAndSettle();
      await tester.tap(find.text(l10n.skillRemove));
      await tester.pumpAndSettle();

      verify(() => repository.removeOwnSkill(userSkillId: skill.id)).called(1);
      // Once to show the section, once more to refresh it after the removal.
      verify(() => repository.listOwnSkills()).called(2);
    });

    testWidgets('a level can be changed, and a refused change is explained', (tester) async {
      final skill = testUserSkill('Gitar', level: SkillLevel.beginner);
      when(() => repository.listOwnSkills()).thenAnswer((_) async => [skill]);
      when(() => repository.changeOwnSkillLevel(userSkillId: skill.id, level: SkillLevel.expert))
          .thenThrow(const ApiException(code: 'wanted_level_not_higher', statusCode: 409));
      await pumpScreen(tester, const Scaffold(body: OwnSkillsSection()));

      await tester.tap(find.byType(ActionChip));
      await tester.pumpAndSettle();
      await tester.tap(find.text(l10n.skillChangeLevel));
      await tester.pumpAndSettle();
      expect(find.text(l10n.skillLevelQuestionOffered), findsOneWidget);
      await tester.tap(find.text(l10n.skillLevelExpert));
      await tester.pumpAndSettle();

      verify(() => repository.changeOwnSkillLevel(userSkillId: skill.id, level: SkillLevel.expert)).called(1);
      expect(find.text(l10n.errorWantedLevelNotHigher), findsOneWidget);
    });
  });

  group('SkillPickerScreen', () {
    testWidgets('adds the chosen skill at the chosen level and returns to the profile', (tester) async {
      when(() => repository.addOwnSkill(skillId: 'skill-piyano', type: UserSkillType.wanted, level: SkillLevel.advanced))
          .thenAnswer((_) async => testUserSkill('Piyano', type: UserSkillType.wanted, level: SkillLevel.advanced));
      await pumpScreen(tester, const SkillPickerScreen(type: UserSkillType.wanted));

      expect(find.text(l10n.skillPickerTitleWanted), findsOneWidget);
      await tester.tap(find.text('Piyano'));
      await tester.pumpAndSettle();
      expect(find.text(l10n.skillLevelQuestionWanted), findsOneWidget);
      await tester.tap(find.text(l10n.skillLevelAdvanced));
      await tester.pumpAndSettle();

      verify(() => repository.addOwnSkill(skillId: 'skill-piyano', type: UserSkillType.wanted, level: SkillLevel.advanced)).called(1);
      expect(find.text('route:/profile'), findsOneWidget);
    });

    testWidgets('marks skills the user already has and does not add them again', (tester) async {
      when(() => repository.listOwnSkills()).thenAnswer((_) async => [testUserSkill('Gitar')]);
      await pumpScreen(tester, const SkillPickerScreen(type: UserSkillType.offered));

      await tester.tap(find.text('Gitar'));
      await tester.pumpAndSettle();

      expect(find.text(l10n.skillLevelQuestionOffered), findsNothing);
      verifyNever(
        () => repository.addOwnSkill(
          skillId: any(named: 'skillId'),
          type: any(named: 'type'),
          level: any(named: 'level'),
        ),
      );
    });

    testWidgets('searches after a pause in typing and filters by category', (tester) async {
      await pumpScreen(tester, const SkillPickerScreen(type: UserSkillType.offered));

      await tester.enterText(find.byType(TextField), 'pi');
      await tester.pump(const Duration(milliseconds: 100));
      verifyNever(() => repository.searchSkills(categoryId: null, query: 'pi'));
      await tester.pump(const Duration(milliseconds: 400));
      await tester.pumpAndSettle();
      verify(() => repository.searchSkills(categoryId: null, query: 'pi')).called(1);

      await tester.tap(find.widgetWithText(ChoiceChip, 'Diller'));
      await tester.pumpAndSettle();
      verify(() => repository.searchSkills(categoryId: 'category-languages', query: 'pi')).called(1);
    });

    testWidgets('says when nothing matches and when there are more results', (tester) async {
      when(() => repository.searchSkills(categoryId: any(named: 'categoryId'), query: any(named: 'query')))
          .thenAnswer((_) async => testSkillPage([]));
      await pumpScreen(tester, const SkillPickerScreen(type: UserSkillType.offered));
      expect(find.text(l10n.skillSearchEmpty), findsOneWidget);
      expect(find.text(l10n.skillSearchNarrow), findsNothing);
    });

    testWidgets('a refused add is explained and stays on the picker', (tester) async {
      when(
        () => repository.addOwnSkill(
          skillId: any(named: 'skillId'),
          type: any(named: 'type'),
          level: any(named: 'level'),
        ),
      ).thenThrow(const ApiException(code: 'skill_limit_reached', statusCode: 409));
      await pumpScreen(tester, const SkillPickerScreen(type: UserSkillType.offered));

      await tester.tap(find.text('Gitar'));
      await tester.pumpAndSettle();
      await tester.tap(find.text(l10n.skillLevelBeginner));
      await tester.pumpAndSettle();

      expect(find.text(l10n.skillsLimitReached), findsOneWidget);
      expect(find.text(l10n.skillPickerTitleOffered), findsOneWidget);
    });

    testWidgets('a missing skill can be proposed', (tester) async {
      when(() => repository.proposeSkill(categoryId: 'category-music', name: 'Ukulele'))
          .thenAnswer((_) async => testSkill('Ukulele', status: SkillStatus.pendingApproval));
      await pumpScreen(tester, const SkillPickerScreen(type: UserSkillType.offered));

      await tester.tap(find.text(l10n.skillProposeAction));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(FilledButton, l10n.skillProposeSubmit));
      await tester.pump();
      expect(find.text(l10n.validationSkillName), findsOneWidget);
      expect(find.text(l10n.validationRequired), findsOneWidget);

      await tester.enterText(find.widgetWithText(TextFormField, l10n.skillProposeNameLabel), 'Ukulele');
      await tester.tap(find.byType(DropdownButtonFormField<String>));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Müzik').last);
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(FilledButton, l10n.skillProposeSubmit));
      await tester.pumpAndSettle();

      expect(find.text(l10n.skillProposalSent), findsOneWidget);
    });

    testWidgets('proposing without a verified email shows the interaction notice', (tester) async {
      when(() => repository.proposeSkill(categoryId: any(named: 'categoryId'), name: any(named: 'name')))
          .thenThrow(const ApiException(code: 'email_not_verified', statusCode: 403));
      await pumpScreen(tester, const SkillPickerScreen(type: UserSkillType.offered));

      await tester.tap(find.text(l10n.skillProposeAction));
      await tester.pumpAndSettle();
      await tester.enterText(find.widgetWithText(TextFormField, l10n.skillProposeNameLabel), 'Ukulele');
      await tester.tap(find.byType(DropdownButtonFormField<String>));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Müzik').last);
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(FilledButton, l10n.skillProposeSubmit));
      await tester.pumpAndSettle();

      expect(find.text(l10n.interactionRequirements), findsOneWidget);
      await tester.tap(find.widgetWithText(FilledButton, l10n.interactionGoToProfile));
      await tester.pumpAndSettle();
      expect(find.text('route:/profile'), findsOneWidget);
    });
  });

  group('SkillProposalsScreen', () {
    testWidgets('lists proposals with their status, or says there are none', (tester) async {
      when(() => repository.listOwnProposals()).thenAnswer(
        (_) async => [
          testSkill('Ukulele', status: SkillStatus.pendingApproval),
          testSkill('Kanun', status: SkillStatus.rejected),
        ],
      );
      await pumpScreen(tester, const SkillProposalsScreen());

      expect(find.text('Ukulele'), findsOneWidget);
      expect(find.text(l10n.skillStatusPending), findsOneWidget);
      expect(find.text(l10n.skillStatusRejected), findsOneWidget);
    });

    testWidgets('says when there are no proposals', (tester) async {
      await pumpScreen(tester, const SkillProposalsScreen());

      expect(find.text(l10n.skillProposalsEmpty), findsOneWidget);
    });
  });
}
