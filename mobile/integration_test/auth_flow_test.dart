import 'package:exparter/app.dart';
import 'package:exparter/l10n/app_localizations_tr.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:integration_test/integration_test.dart';

/// Runs on a device against the real backend (see docs/roadmap.md for how to start it).
///
///     flutter test integration_test -d <device>
void main() {
  IntegrationTestWidgetsFlutterBinding.ensureInitialized();
  final l10n = AppLocalizationsTr();

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
    await tester.pumpWidget(ProviderScope(key: UniqueKey(), child: const ExparterApp()));
  }

  Future<void> tapVisible(WidgetTester tester, Finder finder) async {
    await tester.ensureVisible(finder);
    await tester.pumpAndSettle();
    await tester.tap(finder);
    await tester.pump();
  }

  testWidgets('register, restart, sign out and sign in with the username', (tester) async {
    final id = DateTime.now().millisecondsSinceEpoch.toRadixString(36);
    final username = 'e2e_$id';
    const password = 'Passw0rd!x';

    await launchApp(tester);
    await pumpUntilFound(tester, find.text(l10n.loginTitle));

    // Register.
    await tapVisible(tester, find.widgetWithText(TextButton, l10n.loginGoToRegister));
    await pumpUntilFound(tester, find.text(l10n.registerTitle));
    await tester.enterText(field(l10n.registerEmailLabel), '$username@example.com');
    await tester.enterText(field(l10n.registerUsernameLabel), username);
    await tester.enterText(field(l10n.passwordLabel), password);
    await tapVisible(tester, find.byType(Checkbox));
    await tapVisible(tester, find.widgetWithText(FilledButton, l10n.registerSubmit));
    await pumpUntilFound(tester, find.text(l10n.homeGreeting(username)));
    expect(find.text(l10n.homeEmailNotVerified), findsOneWidget);

    // The session survives a restart.
    await launchApp(tester);
    await pumpUntilFound(tester, find.text(l10n.homeGreeting(username)));

    // Sign out, and stay signed out after another restart.
    await tapVisible(tester, find.widgetWithText(OutlinedButton, l10n.homeLogout));
    await pumpUntilFound(tester, find.text(l10n.loginTitle));
    await launchApp(tester);
    await pumpUntilFound(tester, find.text(l10n.loginTitle));

    // A wrong password is explained, then the username signs in.
    await tester.enterText(field(l10n.loginIdentifierLabel), username);
    await tester.enterText(field(l10n.passwordLabel), 'wrong-password');
    await tapVisible(tester, find.widgetWithText(FilledButton, l10n.loginSubmit));
    await pumpUntilFound(tester, find.text(l10n.errorInvalidCredentials));

    await tester.enterText(field(l10n.passwordLabel), password);
    await tapVisible(tester, find.widgetWithText(FilledButton, l10n.loginSubmit));
    await pumpUntilFound(tester, find.text(l10n.homeGreeting(username)));
  });
}
