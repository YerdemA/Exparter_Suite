import 'dart:typed_data';

import 'package:exparter/core/api/api_exception.dart';
import 'package:exparter/core/api/api_providers.dart';
import 'package:exparter/core/api/generated/export.dart';
import 'package:exparter/core/auth/session_controller.dart';
import 'package:exparter/core/auth/session_store.dart';
import 'package:exparter/features/auth/data/api_auth_repository.dart';
import 'package:exparter/features/profile/data/api_profile_repository.dart';
import 'package:exparter/features/profile/data/avatar_picker.dart';
import 'package:exparter/features/profile/domain/profile_repository.dart';
import 'package:exparter/features/profile/presentation/profile_controller.dart';
import 'package:exparter/features/profile/presentation/profile_form_screen.dart';
import 'package:exparter/features/profile/presentation/profile_screen.dart';
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
  late InMemorySessionPersistence persistence;

  setUpAll(() => registerFallbackValue(jpeg));

  setUp(() async {
    repository = MockProfileRepository();
    persistence = InMemorySessionPersistence();
    await SessionStore(persistence).save(testSession());
    when(() => repository.listCities()).thenAnswer((_) async => testCities);
  });

  List<Override> overrides({AvatarPicker? picker}) => [
        profileRepositoryProvider.overrideWithValue(repository),
        authRepositoryProvider.overrideWithValue(MockAuthRepository()),
        sessionStoreProvider.overrideWithValue(SessionStore(persistence)),
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

  Future<void> pumpScreen(WidgetTester tester, Widget screen, {AvatarPicker? picker}) async {
    tester.view.physicalSize = const Size(1080, 3200);
    tester.view.devicePixelRatio = 2;
    addTearDown(tester.view.reset);
    await tester.pumpWidget(ProviderScope(overrides: overrides(picker: picker), child: localized(screen)));
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

    testWidgets('setup requires a name and at least one way to swap', (tester) async {
      when(() => repository.getOwnProfile()).thenAnswer((_) async => testProfile(complete: false));
      await pumpScreen(tester, const ProfileFormScreen(isSetup: true));

      expect(find.text(l10n.profileSetupTitle), findsOneWidget);
      await tester.tap(find.widgetWithText(SwitchListTile, l10n.profileAcceptsOnline));
      await tester.pump();
      await tester.tap(find.widgetWithText(FilledButton, l10n.profileSetupSubmit));
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
      await pumpScreen(tester, const ProfileFormScreen(isSetup: true));

      expect(find.text(l10n.profileCityLabel), findsNothing);
      await tester.enterText(field(l10n.profileDisplayNameLabel), 'Ayşe');
      await tester.tap(find.widgetWithText(SwitchListTile, l10n.profileAcceptsInPerson));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(FilledButton, l10n.profileSetupSubmit));
      await tester.pump();
      expect(find.text(l10n.validationCity), findsOneWidget);

      await tester.tap(find.byType(DropdownButtonFormField<String>));
      await tester.pumpAndSettle();
      await tester.tap(find.text('İstanbul').last);
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(FilledButton, l10n.profileSetupSubmit));
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
    });

    testWidgets('editing starts from the saved profile', (tester) async {
      when(() => repository.getOwnProfile()).thenAnswer((_) async => testProfile());
      await pumpScreen(tester, const ProfileFormScreen(isSetup: false));

      expect(find.text(l10n.profileEditTitle), findsOneWidget);
      expect(find.widgetWithText(TextFormField, 'Ayşe Yılmaz'), findsOneWidget);
      expect(find.widgetWithText(TextFormField, 'Gitar çalıyorum.'), findsOneWidget);
      expect(find.text('İstanbul'), findsOneWidget);
      expect(find.text(l10n.homeLogout), findsNothing);
    });

    testWidgets('shows a readable error with a retry when the profile cannot be loaded', (tester) async {
      when(() => repository.getOwnProfile()).thenThrow(const ApiException(code: ApiException.networkCode));
      await pumpScreen(tester, const ProfileFormScreen(isSetup: false));

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
      await pumpScreen(tester, const ProfileScreen());

      expect(find.text('Ayşe Yılmaz'), findsOneWidget);
      expect(find.text('@ayse'), findsOneWidget);
      expect(find.text('Gitar çalıyorum.'), findsOneWidget);
      expect(find.text(l10n.profileModeOnline), findsOneWidget);
      expect(find.text(l10n.profileModeInPersonIn('İstanbul')), findsOneWidget);
      expect(find.text(l10n.homeEmailNotVerified), findsOneWidget);
      expect(find.text(l10n.homeResendVerification), findsOneWidget);
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
