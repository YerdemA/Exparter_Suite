import 'dart:typed_data';

import 'package:exparter/core/api/api_exception.dart';
import 'package:exparter/core/api/api_providers.dart';
import 'package:exparter/core/api/generated/export.dart';
import 'package:exparter/core/auth/interaction_requirements.dart';
import 'package:exparter/core/auth/session_controller.dart';
import 'package:exparter/core/auth/session_store.dart';
import 'package:exparter/features/auth/data/api_auth_repository.dart';
import 'package:exparter/features/notifications/data/firebase_push_client.dart';
import 'package:exparter/features/notifications/domain/push_client.dart';
import 'package:exparter/features/profile/data/api_profile_repository.dart';
import 'package:exparter/features/profile/data/avatar_picker.dart';
import 'package:exparter/features/profile/domain/profile_repository.dart';
import 'package:exparter/features/profile/presentation/profile_controller.dart';
import 'package:exparter/features/profile/presentation/profile_form_screen.dart';
import 'package:exparter/features/profile/presentation/profile_screen.dart';
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
  final jpeg = PickedImage(
    bytes: Uint8List.fromList([1, 2, 3]),
    contentType: AvatarUploadUrlRequestContentType.fromJson('image/jpeg'),
  );
  late MockProfileRepository repository;
  late MockSkillRepository skillRepository;
  late MockAuthRepository authRepository;
  late InMemorySessionPersistence persistence;

  setUpAll(() => registerFallbackValue(jpeg));

  // Push is off unless a test is about it.
  late FakePushClient push;

  setUp(() async {
    push = FakePushClient()..current = PushPermission.unavailable;
    repository = MockProfileRepository();
    skillRepository = MockSkillRepository();
    authRepository = MockAuthRepository();
    when(() => skillRepository.listOwnSkills()).thenAnswer((_) async => [testUserSkill('Gitar')]);
    persistence = InMemorySessionPersistence();
    await SessionStore(persistence).save(testSession());
    when(() => repository.listCities()).thenAnswer((_) async => testCities);
  });

  List<Override> overrides({AvatarPicker? picker, InteractionRequirements? requirements}) => [
        profileRepositoryProvider.overrideWithValue(repository),
        skillRepositoryProvider.overrideWithValue(skillRepository),
        sessionRepositoryProvider.overrideWithValue(quietSessionRepository()),
        reviewRepositoryProvider.overrideWithValue(quietReviewRepository()),
        interactionRequirementsProvider.overrideWith((ref) async => requirements ?? testRequirements()),
        authRepositoryProvider.overrideWithValue(authRepository),
        sessionStoreProvider.overrideWithValue(SessionStore(persistence)),
        pushClientProvider.overrideWithValue(push),
        if (picker != null) avatarPickerProvider.overrideWithValue(picker),
      ];

  group('OwnProfile', () {
    test('loads the profile of the signed-in user', () async {
      when(() => repository.getOwnProfile()).thenAnswer((_) async => testProfile());
      final container = ProviderContainer.test(overrides: overrides());

      container.listen(ownProfileProvider, (_, _) {});
      await container.read(sessionControllerProvider.future);

      expect(await container.read(ownProfileProvider.future), testProfile());
    });

    test('is empty when signed out and asks the API nothing', () async {
      await SessionStore(persistence).clear();
      final container = ProviderContainer.test(overrides: overrides());

      expect(await container.read(ownProfileProvider.future), isNull);
      verifyNever(() => repository.getOwnProfile());
    });

    test('is dropped when the session ends', () async {
      when(() => repository.getOwnProfile()).thenAnswer((_) async => testProfile());
      final container = ProviderContainer.test(overrides: overrides());
      container.listen(ownProfileProvider, (_, _) {});
      await container.read(sessionControllerProvider.future);
      expect(await container.read(ownProfileProvider.future), isNotNull);

      await container.read(sessionControllerProvider.notifier).end();

      expect(await container.read(ownProfileProvider.future), isNull);
    });
  });

  group('ProfileActions', () {
    late ProviderContainer container;

    setUp(() async {
      when(() => repository.getOwnProfile()).thenAnswer((_) async => testProfile(complete: false));
      container = ProviderContainer.test(overrides: overrides());
      container.listen(ownProfileProvider, (_, _) {});
      container.listen(profileActionsProvider, (_, _) {});
      await container.read(sessionControllerProvider.future);
      await container.read(ownProfileProvider.future);
    });

    test('save trims text, drops the city of an online-only profile and updates the profile', () async {
      when(
        () => repository.updateOwnProfile(
          displayName: 'Ayşe',
          bio: null,
          city: null,
          acceptsOnline: true,
          acceptsInPerson: false,
        ),
      ).thenAnswer((_) async => testProfile());

      final saved = await container.read(profileActionsProvider.notifier).save(
            displayName: '  Ayşe ',
            bio: '   ',
            city: '34',
            acceptsOnline: true,
            acceptsInPerson: false,
          );

      expect(saved, isTrue);
      expect(container.read(ownProfileProvider).value, testProfile());
    });

    test('a failed save keeps the profile and exposes the error', () async {
      when(
        () => repository.updateOwnProfile(
          displayName: any(named: 'displayName'),
          bio: any(named: 'bio'),
          city: any(named: 'city'),
          acceptsOnline: any(named: 'acceptsOnline'),
          acceptsInPerson: any(named: 'acceptsInPerson'),
        ),
      ).thenThrow(const ApiException(code: ApiException.networkCode));

      final saved = await container.read(profileActionsProvider.notifier).save(
            displayName: 'Ayşe',
            bio: '',
            city: null,
            acceptsOnline: true,
            acceptsInPerson: false,
          );

      expect(saved, isFalse);
      expect(container.read(ownProfileProvider).value, testProfile(complete: false));
      expect((container.read(profileActionsProvider).error! as ApiException).code, ApiException.networkCode);
    });

    test('uploadAvatar and removeAvatar update the profile', () async {
      final withAvatar = testProfile(avatarUrl: 'https://storage.test/a.jpg');
      when(() => repository.uploadAvatar(jpeg)).thenAnswer((_) async => withAvatar);
      when(() => repository.deleteAvatar()).thenAnswer((_) async {});
      final actions = container.read(profileActionsProvider.notifier);

      expect(await actions.uploadAvatar(jpeg), isTrue);
      expect(container.read(ownProfileProvider).value, withAvatar);

      when(() => repository.getOwnProfile()).thenAnswer((_) async => testProfile());
      expect(await actions.removeAvatar(), isTrue);
      expect(container.read(ownProfileProvider).value!.avatarUrl, isNull);
    });
  });

  Future<void> pumpScreen(
    WidgetTester tester,
    Widget screen, {
    AvatarPicker? picker,
    InteractionRequirements? requirements,
  }) async {
    tester.view.physicalSize = const Size(1080, 3200);
    tester.view.devicePixelRatio = 2;
    addTearDown(tester.view.reset);
    await tester.pumpWidget(
      ProviderScope(
        overrides: overrides(picker: picker, requirements: requirements),
        child: routed(screen),
      ),
    );
    await tester.pumpAndSettle();
  }

  Finder field(String label) => find.widgetWithText(TextFormField, label);

  group('ProfileFormScreen', () {
    void stubSave() => when(
          () => repository.updateOwnProfile(
            displayName: any(named: 'displayName'),
            bio: any(named: 'bio'),
            city: any(named: 'city'),
            acceptsOnline: any(named: 'acceptsOnline'),
            acceptsInPerson: any(named: 'acceptsInPerson'),
          ),
        ).thenAnswer((_) async => testProfile());

    testWidgets('a blank profile requires a name and at least one way to swap', (tester) async {
      when(() => repository.getOwnProfile()).thenAnswer((_) async => testProfile(complete: false));
      await pumpScreen(tester, const ProfileFormScreen());

      await tester.tap(find.widgetWithText(SwitchListTile, l10n.profileAcceptsOnline));
      await tester.pump();
      await tester.tap(find.widgetWithText(FilledButton, l10n.profileSave));
      await tester.pump();

      expect(find.text(l10n.validationRequired), findsOneWidget);
      expect(find.text(l10n.validationSwapMode), findsOneWidget);
      verifyNever(
        () => repository.updateOwnProfile(
          displayName: any(named: 'displayName'),
          bio: any(named: 'bio'),
          city: any(named: 'city'),
          acceptsOnline: any(named: 'acceptsOnline'),
          acceptsInPerson: any(named: 'acceptsInPerson'),
        ),
      );
    });

    testWidgets('in-person swaps require a city, chosen from the list', (tester) async {
      when(() => repository.getOwnProfile()).thenAnswer((_) async => testProfile(complete: false));
      stubSave();
      await pumpScreen(tester, const ProfileFormScreen());

      expect(find.text(l10n.profileCityLabel), findsNothing);
      await tester.enterText(field(l10n.profileDisplayNameLabel), 'Ayşe');
      await tester.tap(find.widgetWithText(SwitchListTile, l10n.profileAcceptsInPerson));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(FilledButton, l10n.profileSave));
      await tester.pump();
      expect(find.text(l10n.validationCity), findsOneWidget);

      await tester.tap(find.byType(DropdownButtonFormField<String>));
      await tester.pumpAndSettle();
      await tester.tap(find.text('İstanbul').last);
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(FilledButton, l10n.profileSave));
      await tester.pumpAndSettle();

      verify(
        () => repository.updateOwnProfile(
          displayName: 'Ayşe',
          bio: null,
          city: '34',
          acceptsOnline: true,
          acceptsInPerson: true,
        ),
      ).called(1);
      expect(find.text('route:/profile'), findsOneWidget);
    });

    testWidgets('editing starts from the saved profile', (tester) async {
      when(() => repository.getOwnProfile()).thenAnswer((_) async => testProfile());
      await pumpScreen(tester, const ProfileFormScreen());

      expect(find.text(l10n.profileEditTitle), findsOneWidget);
      expect(find.widgetWithText(TextFormField, 'Ayşe Yılmaz'), findsOneWidget);
      expect(find.widgetWithText(TextFormField, 'Gitar çalıyorum.'), findsOneWidget);
      expect(find.text('İstanbul'), findsOneWidget);
    });

    testWidgets('shows a readable error with a retry when the profile cannot be loaded', (tester) async {
      when(() => repository.getOwnProfile()).thenThrow(const ApiException(code: ApiException.networkCode));
      await pumpScreen(tester, const ProfileFormScreen());

      expect(find.text(l10n.errorNetwork), findsOneWidget);

      when(() => repository.getOwnProfile()).thenAnswer((_) async => testProfile());
      await tester.tap(find.widgetWithText(OutlinedButton, l10n.errorRetry));
      await tester.pumpAndSettle();

      expect(find.widgetWithText(TextFormField, 'Ayşe Yılmaz'), findsOneWidget);
    });
  });

  group('ProfileScreen', () {
    testWidgets('shows the profile, the username and the email status', (tester) async {
      when(() => repository.getOwnProfile()).thenAnswer((_) async => testProfile());
      await pumpScreen(tester, const ProfileScreen(), requirements: testRequirements(emailVerified: false));

      expect(find.text('Ayşe Yılmaz'), findsOneWidget);
      expect(find.text('@ayse'), findsOneWidget);
      expect(find.text('Gitar çalıyorum.'), findsOneWidget);
      expect(find.text(l10n.profileModeOnline), findsOneWidget);
      expect(find.text(l10n.profileModeInPersonIn('İstanbul')), findsOneWidget);
      expect(find.text(l10n.homeEmailNotVerified), findsOneWidget);
      expect(find.text(l10n.homeResendVerification), findsOneWidget);

      // The profile and a skill are there; only the email is still missing.
      expect(find.text(l10n.interactionRequirements), findsOneWidget);
      expect(find.byIcon(Icons.check_circle), findsNWidgets(2));
      expect(find.byIcon(Icons.radio_button_unchecked), findsOneWidget);
      expect(find.textContaining('Gitar ·'), findsOneWidget);
    });

    testWidgets('deleting the account needs the password and signs the user out', (tester) async {
      when(() => repository.getOwnProfile()).thenAnswer((_) async => testProfile());
      when(() => authRepository.requestAccountDeletion(password: any(named: 'password'))).thenAnswer((_) async {});
      await pumpScreen(tester, const ProfileScreen());

      await tester.ensureVisible(find.text(l10n.accountDelete));
      await tester.tap(find.text(l10n.accountDelete));
      await tester.pumpAndSettle();
      expect(find.text(l10n.accountDeleteBody), findsOneWidget);
      final confirm = find.widgetWithText(FilledButton, l10n.accountDelete);
      expect(tester.widget<FilledButton>(confirm).onPressed, isNull);

      await tester.enterText(find.byType(TextField), 'Passw0rd!x');
      await tester.pumpAndSettle();
      await tester.tap(confirm);
      // Signed out, the screen has no profile to show and keeps spinning; in the app the
      // router leaves it at this point.
      await tester.pump();
      await tester.pump(const Duration(milliseconds: 500));

      verify(() => authRepository.requestAccountDeletion(password: 'Passw0rd!x')).called(1);
      expect(find.text(l10n.accountDeleteScheduled), findsOneWidget);
      expect(SessionStore(persistence).current, isNull);
    });

    testWidgets('says whether pushes reach the phone and asks again when they do not', (tester) async {
      when(() => repository.getOwnProfile()).thenAnswer((_) async => testProfile());
      push
        ..current = PushPermission.denied
        ..answer = PushPermission.denied;
      await pumpScreen(tester, const ProfileScreen());

      expect(find.text(l10n.pushDisabled), findsOneWidget);
      await tester.ensureVisible(find.text(l10n.pushEnable));
      await tester.tap(find.text(l10n.pushEnable));
      await tester.pumpAndSettle();

      expect(push.requests, 1);
      expect(find.text(l10n.pushEnableInSettings), findsOneWidget);

      push.answer = PushPermission.granted;
      await tester.tap(find.text(l10n.pushEnable));
      await tester.pumpAndSettle();

      expect(find.text(l10n.pushEnabled), findsOneWidget);
      expect(find.text(l10n.pushEnable), findsNothing);
    });

    testWidgets('a wrong password keeps the account and says so', (tester) async {
      when(() => repository.getOwnProfile()).thenAnswer((_) async => testProfile());
      when(() => authRepository.requestAccountDeletion(password: any(named: 'password')))
          .thenThrow(const ApiException(statusCode: 400, code: 'invalid_password'));
      await pumpScreen(tester, const ProfileScreen());

      await tester.ensureVisible(find.text(l10n.accountDelete));
      await tester.tap(find.text(l10n.accountDelete));
      await tester.pumpAndSettle();
      await tester.enterText(find.byType(TextField), 'wrong');
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(FilledButton, l10n.accountDelete));
      await tester.pumpAndSettle();

      expect(find.text(l10n.errorInvalidPassword), findsOneWidget);
      expect(find.text('@ayse'), findsOneWidget);
    });

    testWidgets('a complete, verified user sees no requirements', (tester) async {
      await SessionStore(persistence).save(
        StoredSession(accessToken: 'a', refreshToken: 'r', account: testAccount(emailVerified: true)),
      );
      when(() => repository.getOwnProfile()).thenAnswer((_) async => testProfile());
      await pumpScreen(tester, const ProfileScreen());

      expect(find.text(l10n.interactionRequirements), findsNothing);
      expect(find.widgetWithText(FilledButton, l10n.profileEdit), findsOneWidget);
    });

    testWidgets('a new user is told what is missing before they can interact', (tester) async {
      when(() => repository.getOwnProfile()).thenAnswer((_) async => testProfile(complete: false));
      when(() => skillRepository.listOwnSkills()).thenAnswer((_) async => []);
      await pumpScreen(
        tester,
        const ProfileScreen(),
        requirements: testRequirements(emailVerified: false, profileCompleted: false, hasOfferedSkill: false),
      );

      expect(find.text(l10n.interactionRequirements), findsOneWidget);
      expect(find.text(l10n.requirementProfile), findsOneWidget);
      expect(find.text(l10n.requirementSkill), findsOneWidget);
      expect(find.text(l10n.requirementEmail), findsOneWidget);
      expect(find.byIcon(Icons.radio_button_unchecked), findsNWidgets(3));
      expect(find.text(l10n.skillsOfferedEmpty), findsOneWidget);
      expect(find.text('@ayse'), findsOneWidget);
      expect(find.widgetWithText(FilledButton, l10n.profileFillIn), findsOneWidget);
    });

    testWidgets('uploads a photo picked from the gallery', (tester) async {
      when(() => repository.getOwnProfile()).thenAnswer((_) async => testProfile());
      when(() => repository.uploadAvatar(jpeg)).thenAnswer((_) async => testProfile());
      AvatarSource? requested;
      await pumpScreen(
        tester,
        const ProfileScreen(),
        picker: (source) async {
          requested = source;
          return jpeg;
        },
      );

      await tester.tap(find.byTooltip(l10n.avatarChange));
      await tester.pumpAndSettle();
      expect(find.text(l10n.avatarRemove), findsNothing);
      await tester.tap(find.text(l10n.avatarFromGallery));
      await tester.pumpAndSettle();

      expect(requested, AvatarSource.gallery);
      verify(() => repository.uploadAvatar(jpeg)).called(1);
    });

    testWidgets('explains a rejected photo', (tester) async {
      when(() => repository.getOwnProfile()).thenAnswer((_) async => testProfile());
      when(() => repository.uploadAvatar(any()))
          .thenThrow(const ApiException(code: 'avatar_invalid', statusCode: 400));
      await pumpScreen(tester, const ProfileScreen(), picker: (_) async => jpeg);

      await tester.tap(find.byTooltip(l10n.avatarChange));
      await tester.pumpAndSettle();
      await tester.tap(find.text(l10n.avatarFromCamera));
      await tester.pumpAndSettle();

      expect(find.text(l10n.errorAvatarInvalid), findsOneWidget);
    });

    testWidgets('backing out of the picker uploads nothing', (tester) async {
      when(() => repository.getOwnProfile()).thenAnswer((_) async => testProfile());
      await pumpScreen(tester, const ProfileScreen(), picker: (_) async => null);

      await tester.tap(find.byTooltip(l10n.avatarChange));
      await tester.pumpAndSettle();
      await tester.tap(find.text(l10n.avatarFromGallery));
      await tester.pumpAndSettle();

      verifyNever(() => repository.uploadAvatar(any()));
    });
  });
}
