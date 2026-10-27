import 'dart:async';

import 'package:exparter/core/api/api_exception.dart';
import 'package:exparter/core/api/api_providers.dart';
import 'package:exparter/core/api/generated/export.dart';
import 'package:exparter/core/auth/session_store.dart';
import 'package:exparter/features/auth/data/api_auth_repository.dart';
import 'package:exparter/features/auth/presentation/login_screen.dart';
import 'package:exparter/features/auth/presentation/register_screen.dart';
import 'package:exparter/l10n/app_localizations_tr.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mocktail/mocktail.dart';

import '../../helpers.dart';

void main() {
  final l10n = AppLocalizationsTr();
  late MockAuthRepository repository;

  setUp(() => repository = MockAuthRepository());

  Future<void> pumpScreen(WidgetTester tester, Widget screen) async {
    // A tall surface so the whole form is laid out and tappable without scrolling.
    tester.view.physicalSize = const Size(1080, 2600);
    tester.view.devicePixelRatio = 2;
    addTearDown(tester.view.reset);

    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          authRepositoryProvider.overrideWithValue(repository),
          sessionStoreProvider.overrideWithValue(SessionStore(InMemorySessionPersistence())),
        ],
        child: localized(screen),
      ),
    );
    await tester.pumpAndSettle();
  }

  Finder field(String label) => find.widgetWithText(TextFormField, label);

  group('LoginScreen', () {
    testWidgets('requires both fields before calling the API', (tester) async {
      await pumpScreen(tester, const LoginScreen());

      await tester.tap(find.widgetWithText(FilledButton, l10n.loginSubmit));
      await tester.pump();

      expect(find.text(l10n.validationRequired), findsNWidgets(2));
      verifyNever(() => repository.login(identifier: any(named: 'identifier'), password: any(named: 'password')));
    });

    testWidgets('shows a spinner while signing in, then a readable error', (tester) async {
      final pending = Completer<AuthResponse>();
      when(() => repository.login(identifier: 'ayse', password: 'wrong-password'))
          .thenAnswer((_) => pending.future);
      await pumpScreen(tester, const LoginScreen());

      await tester.enterText(field(l10n.loginIdentifierLabel), 'ayse');
      await tester.enterText(field(l10n.passwordLabel), 'wrong-password');
      await tester.tap(find.widgetWithText(FilledButton, l10n.loginSubmit));
      await tester.pump();

      expect(find.byType(CircularProgressIndicator), findsOneWidget);
      expect(tester.widget<FilledButton>(find.byType(FilledButton)).onPressed, isNull);

      pending.completeError(const ApiException(code: 'invalid_credentials', statusCode: 401));
      await tester.pumpAndSettle();

      expect(find.byType(CircularProgressIndicator), findsNothing);
      expect(find.text(l10n.errorInvalidCredentials), findsOneWidget);
      expect(find.textContaining('ApiException'), findsNothing);
    });

    testWidgets('shows a connection message when the server is unreachable', (tester) async {
      when(() => repository.login(identifier: any(named: 'identifier'), password: any(named: 'password')))
          .thenThrow(const ApiException(code: ApiException.networkCode));
      await pumpScreen(tester, const LoginScreen());

      await tester.enterText(field(l10n.loginIdentifierLabel), 'ayse');
      await tester.enterText(field(l10n.passwordLabel), 'Passw0rd!x');
      await tester.tap(find.widgetWithText(FilledButton, l10n.loginSubmit));
      await tester.pumpAndSettle();

      expect(find.text(l10n.errorNetwork), findsOneWidget);
    });

    testWidgets('password visibility can be toggled', (tester) async {
      await pumpScreen(tester, const LoginScreen());
      EditableText password() => tester.widget<EditableText>(
            find.descendant(of: field(l10n.passwordLabel), matching: find.byType(EditableText)),
          );

      expect(password().obscureText, isTrue);
      await tester.tap(find.byTooltip(l10n.passwordShow));
      await tester.pump();

      expect(password().obscureText, isFalse);
      expect(find.byTooltip(l10n.passwordHide), findsOneWidget);
    });
  });

  group('RegisterScreen', () {
    Future<void> fill(WidgetTester tester, {String username = 'ayse', bool confirmAge = true}) async {
      await tester.enterText(field(l10n.registerEmailLabel), 'ayse@example.com');
      await tester.enterText(field(l10n.registerUsernameLabel), username);
      await tester.enterText(field(l10n.passwordLabel), 'Passw0rd!x');
      if (confirmAge) {
        await tester.tap(find.byType(Checkbox));
      }

      await tester.pump();
    }

    testWidgets('validates every field before calling the API', (tester) async {
      await pumpScreen(tester, const RegisterScreen());

      await tester.enterText(field(l10n.registerEmailLabel), 'not-an-email');
      await tester.enterText(field(l10n.registerUsernameLabel), 'Ayse');
      await tester.enterText(field(l10n.passwordLabel), 'short');
      await tester.tap(find.widgetWithText(FilledButton, l10n.registerSubmit));
      await tester.pump();

      expect(find.text(l10n.validationEmail), findsOneWidget);
      expect(find.text(l10n.validationUsername), findsOneWidget);
      expect(find.text(l10n.validationPassword), findsOneWidget);
      expect(find.text(l10n.validationAgeConfirmation), findsOneWidget);
      verifyNever(
        () => repository.register(
          email: any(named: 'email'),
          username: any(named: 'username'),
          password: any(named: 'password'),
          ageConfirmed: any(named: 'ageConfirmed'),
          termsAccepted: any(named: 'termsAccepted'),
        ),
      );
    });

    testWidgets('submits a valid form', (tester) async {
      when(
        () => repository.register(
          email: 'ayse@example.com',
          username: 'ayse',
          password: 'Passw0rd!x',
          ageConfirmed: true,
          termsAccepted: true,
        ),
      ).thenAnswer((_) async => testAuthResponse());
      await pumpScreen(tester, const RegisterScreen());

      await fill(tester);
      await tester.tap(find.widgetWithText(FilledButton, l10n.registerSubmit));
      await tester.pumpAndSettle();

      verify(
        () => repository.register(
          email: 'ayse@example.com',
          username: 'ayse',
          password: 'Passw0rd!x',
          ageConfirmed: true,
          termsAccepted: true,
        ),
      ).called(1);
    });

    testWidgets('explains a taken username', (tester) async {
      when(
        () => repository.register(
          email: any(named: 'email'),
          username: any(named: 'username'),
          password: any(named: 'password'),
          ageConfirmed: any(named: 'ageConfirmed'),
          termsAccepted: any(named: 'termsAccepted'),
        ),
      ).thenThrow(const ApiException(code: 'username_already_taken', statusCode: 409));
      await pumpScreen(tester, const RegisterScreen());

      await fill(tester);
      await tester.tap(find.widgetWithText(FilledButton, l10n.registerSubmit));
      await tester.pumpAndSettle();

      expect(find.text(l10n.errorUsernameTaken), findsOneWidget);
    });
  });
}
