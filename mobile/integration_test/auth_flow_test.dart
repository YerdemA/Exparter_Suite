import 'dart:convert';

import 'package:dio/dio.dart';
import 'package:exparter/app.dart';
import 'package:exparter/core/api/generated/export.dart';
import 'package:exparter/core/router/app_routes.dart';
import 'package:exparter/features/profile/data/avatar_picker.dart';
import 'package:exparter/features/profile/domain/profile_repository.dart';
import 'package:exparter/l10n/app_localizations_tr.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:integration_test/integration_test.dart';

/// Runs on a device against the real backend, Mailpit and object storage (see
/// docs/roadmap.md for how to start them).
///
///     flutter test integration_test -d <device>
void main() {
  IntegrationTestWidgetsFlutterBinding.ensureInitialized();
  final l10n = AppLocalizationsTr();

  // Mailpit on the host machine, as seen from the Android emulator.
  final mailpit = Dio(BaseOptions(baseUrl: 'http://10.0.2.2:8025'));

  // The system photo picker cannot be driven from a test, so the picker hands over
  // this 1x1 PNG. Everything after the pick (upload, storage, display) is real.
  final photo = PickedImage(
    bytes: base64Decode(
      'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==',
    ),
    contentType: AvatarUploadUrlRequestContentType.fromJson('image/png'),
  );

  Finder field(String label) => find.widgetWithText(TextFormField, label);

  Future<void> pumpUntilFound(WidgetTester tester, Finder finder) async {
    for (var i = 0; i < 100 && finder.evaluate().isEmpty; i++) {
      await tester.pump(const Duration(milliseconds: 200));
    }

    expect(finder, findsWidgets);
  }

  /// A new ProviderScope has no in-memory state, like a freshly launched process:
  /// whatever it knows must come from secure storage.
  Future<void> launchApp(WidgetTester tester) async {
    await tester.pumpWidget(
      ProviderScope(
        key: UniqueKey(),
        overrides: [avatarPickerProvider.overrideWithValue((_) async => photo)],
        child: const ExparterApp(),
      ),
    );
  }

  Future<void> tapVisible(WidgetTester tester, Finder finder) async {
    await tester.ensureVisible(finder);
    await tester.pumpAndSettle();
    await tester.tap(finder);
    await tester.pump();
  }

  Future<void> openProfileTab(WidgetTester tester) async {
    await pumpUntilFound(tester, find.byType(NavigationBar));
    await tester.tap(find.descendant(of: find.byType(NavigationBar), matching: find.text(l10n.navProfile)));
    await pumpUntilFound(tester, find.text(l10n.profileEdit));
  }

  /// Reads the one-time token out of the newest email to [email] with [subject].
  Future<String> tokenFromEmail(String email, String subject) async {
    for (var attempt = 0; attempt < 30; attempt++) {
      final search = await mailpit.get<Map<String, dynamic>>(
        '/api/v1/search',
        queryParameters: {'query': 'to:$email subject:"$subject"'},
      );
      final messages = search.data!['messages'] as List<dynamic>;
      if (messages.isNotEmpty) {
        final id = (messages.first as Map<String, dynamic>)['ID'] as String;
        final message = await mailpit.get<Map<String, dynamic>>('/api/v1/message/$id');
        final match = RegExp(r'token=([A-Za-z0-9_%\-]+)').firstMatch(message.data!['Text'] as String);
        return Uri.decodeComponent(match!.group(1)!);
      }

      await Future<void>.delayed(const Duration(milliseconds: 300));
    }

    fail('No "$subject" email arrived for $email.');
  }

  /// Navigates as an opened email link would: the router receives the link's path and query.
  void openLink(WidgetTester tester, String path, String token) {
    final uri = Uri(path: path, queryParameters: {AppRoutes.tokenParameter: token});
    GoRouter.of(tester.element(find.byType(Scaffold).first)).go(uri.toString());
  }

  testWidgets('account and profile lifecycle against the real backend', (tester) async {
    final id = DateTime.now().millisecondsSinceEpoch.toRadixString(36);
    final username = 'e2e_$id';
    final email = '$username@example.com';
    const password = 'Passw0rd!x';
    const newPassword = 'N3w-Passw0rd';

    await launchApp(tester);
    await pumpUntilFound(tester, find.text(l10n.loginTitle));

    // Register.
    await tapVisible(tester, find.widgetWithText(TextButton, l10n.loginGoToRegister));
    await pumpUntilFound(tester, find.text(l10n.registerTitle));
    await tester.enterText(field(l10n.registerEmailLabel), email);
    await tester.enterText(field(l10n.registerUsernameLabel), username);
    await tester.enterText(field(l10n.passwordLabel), password);
    await tapVisible(tester, find.byType(Checkbox));
    await tapVisible(tester, find.widgetWithText(FilledButton, l10n.registerSubmit));

    // A new account goes straight into the app and can browse it. Its profile tab
    // says what is missing before it can interact with others.
    await pumpUntilFound(tester, find.byType(NavigationBar));
    await tester.tap(find.descendant(of: find.byType(NavigationBar), matching: find.text(l10n.navProfile)));
    await pumpUntilFound(tester, find.text(l10n.interactionRequirements));
    expect(find.text(l10n.requirementProfile), findsOneWidget);

    await tapVisible(tester, find.widgetWithText(FilledButton, l10n.profileFillIn));
    await pumpUntilFound(tester, find.text(l10n.profileEditTitle));
    await tester.enterText(field(l10n.profileDisplayNameLabel), 'Uçtan Uca');
    await tapVisible(tester, find.widgetWithText(FilledButton, l10n.profileSave));
    // The name is also in the form's text field, so wait for the profile screen itself.
    await pumpUntilFound(tester, find.widgetWithText(FilledButton, l10n.profileEdit));
    expect(find.text('Uçtan Uca'), findsOneWidget);
    expect(find.text('@$username'), findsOneWidget);
    expect(find.text(l10n.homeEmailNotVerified), findsOneWidget);

    // The session and the saved profile survive a restart.
    await launchApp(tester);
    await openProfileTab(tester);
    expect(find.text('Uçtan Uca'), findsOneWidget);

    // Edit the profile: add a bio and in-person swaps in a city.
    await tapVisible(tester, find.widgetWithText(FilledButton, l10n.profileEdit));
    await pumpUntilFound(tester, find.text(l10n.profileEditTitle));
    await tester.enterText(field(l10n.profileBioLabel), 'Gitar çalıyorum.');
    await tapVisible(tester, find.widgetWithText(SwitchListTile, l10n.profileAcceptsInPerson));
    await pumpUntilFound(tester, find.byType(DropdownButtonFormField<String>));
    await tapVisible(tester, find.byType(DropdownButtonFormField<String>));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Ankara').last);
    await tester.pumpAndSettle();
    await tapVisible(tester, find.widgetWithText(FilledButton, l10n.profileSave));
    await pumpUntilFound(tester, find.text(l10n.profileModeInPersonIn('Ankara')));
    expect(find.text('Gitar çalıyorum.'), findsOneWidget);

    // Upload a photo to object storage, then remove it.
    await tapVisible(tester, find.byTooltip(l10n.avatarChange));
    await tester.pumpAndSettle();
    expect(find.text(l10n.avatarRemove), findsNothing);
    await tester.tap(find.text(l10n.avatarFromGallery));
    await pumpUntilFound(tester, find.byType(Image));
    await tester.pumpAndSettle();
    await tapVisible(tester, find.byTooltip(l10n.avatarChange));
    await tester.pumpAndSettle();
    await tester.tap(find.text(l10n.avatarRemove));
    await tester.pumpAndSettle();
    for (var i = 0; i < 50 && find.byType(Image).evaluate().isNotEmpty; i++) {
      await tester.pump(const Duration(milliseconds: 200));
    }

    expect(find.byType(Image), findsNothing);

    // Verify the email address with the link from the email.
    openLink(tester, AppRoutes.verifyEmail, await tokenFromEmail(email, 'doğrula'));
    await pumpUntilFound(tester, find.text(l10n.verifyEmailSuccess));
    await tapVisible(tester, find.widgetWithText(FilledButton, l10n.actionContinue));
    await openProfileTab(tester);
    expect(find.text(l10n.homeEmailVerified), findsOneWidget);
    expect(find.text(l10n.homeResendVerification), findsNothing);
    expect(find.text(l10n.interactionRequirements), findsNothing);

    // Sign out, and stay signed out after another restart.
    await tapVisible(tester, find.widgetWithText(OutlinedButton, l10n.homeLogout));
    await pumpUntilFound(tester, find.text(l10n.loginTitle));
    await launchApp(tester);
    await pumpUntilFound(tester, find.text(l10n.loginTitle));

    // A wrong password is explained.
    await tester.enterText(field(l10n.loginIdentifierLabel), username);
    await tester.enterText(field(l10n.passwordLabel), 'wrong-password');
    await tapVisible(tester, find.widgetWithText(FilledButton, l10n.loginSubmit));
    await pumpUntilFound(tester, find.text(l10n.errorInvalidCredentials));

    // Reset the password with the link from the email.
    await tapVisible(tester, find.widgetWithText(TextButton, l10n.loginForgotPassword));
    await pumpUntilFound(tester, find.text(l10n.forgotPasswordTitle));
    await tester.enterText(field(l10n.registerEmailLabel), email);
    await tapVisible(tester, find.widgetWithText(FilledButton, l10n.forgotPasswordSubmit));
    await pumpUntilFound(tester, find.text(l10n.forgotPasswordSent));

    openLink(tester, AppRoutes.resetPassword, await tokenFromEmail(email, 'parola'));
    await pumpUntilFound(tester, find.text(l10n.resetPasswordTitle));
    await tester.enterText(field(l10n.passwordLabel), newPassword);
    await tapVisible(tester, find.widgetWithText(FilledButton, l10n.resetPasswordSubmit));
    await pumpUntilFound(tester, find.text(l10n.resetPasswordSuccess));
    await tapVisible(tester, find.widgetWithText(FilledButton, l10n.actionBackToLogin));
    await pumpUntilFound(tester, find.text(l10n.loginTitle));

    // The old password no longer works; the new one signs in by username.
    await tester.enterText(field(l10n.loginIdentifierLabel), username);
    await tester.enterText(field(l10n.passwordLabel), password);
    await tapVisible(tester, find.widgetWithText(FilledButton, l10n.loginSubmit));
    await pumpUntilFound(tester, find.text(l10n.errorInvalidCredentials));

    await tester.enterText(field(l10n.passwordLabel), newPassword);
    await tapVisible(tester, find.widgetWithText(FilledButton, l10n.loginSubmit));
    await openProfileTab(tester);
    expect(find.text('@$username'), findsOneWidget);
  });
}
