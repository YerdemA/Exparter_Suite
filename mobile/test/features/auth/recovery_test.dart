import 'dart:async';

import 'package:exparter/core/api/api_exception.dart';
import 'package:exparter/core/api/api_providers.dart';
import 'package:exparter/core/auth/session_controller.dart';
import 'package:exparter/core/auth/session_store.dart';
import 'package:exparter/features/auth/data/api_auth_repository.dart';
import 'package:exparter/features/auth/presentation/forgot_password_screen.dart';
import 'package:exparter/features/auth/presentation/recovery_controller.dart';
import 'package:exparter/features/auth/presentation/reset_password_screen.dart';
import 'package:exparter/features/auth/presentation/verify_email_screen.dart';
import 'package:exparter/l10n/app_localizations_tr.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_riverpod/misc.dart' show Override;
import 'package:flutter_test/flutter_test.dart';
import 'package:mocktail/mocktail.dart';

import '../../helpers.dart';

void main() {
  final l10n = AppLocalizationsTr();
  late MockAuthRepository repository;
  late InMemorySessionPersistence persistence;

  setUp(() {
    repository = MockAuthRepository();
    persistence = InMemorySessionPersistence();
  });

  List<Override> overrides() => [
        authRepositoryProvider.overrideWithValue(repository),
        sessionStoreProvider.overrideWithValue(SessionStore(persistence)),
      ];

  group('emailVerificationProvider', () {
    test('confirms the token and refreshes the signed-in account', () async {
      await SessionStore(persistence).save(testSession());
      when(() => repository.confirmEmailVerification(token: 'token-1')).thenAnswer((_) async {});
      when(() => repository.getAccount()).thenAnswer((_) async => testAccount(emailVerified: true));
      final container = ProviderContainer.test(overrides: overrides());

      await container.read(emailVerificationProvider('token-1').future);

      expect(container.read(sessionControllerProvider).value!.emailVerified, isTrue);
      expect(persistence.value, contains('"emailVerified":true'));
    });

    test('works signed out without asking for the account', () async {
      when(() => repository.confirmEmailVerification(token: 'token-1')).thenAnswer((_) async {});
      final container = ProviderContainer.test(overrides: overrides());

      await container.read(emailVerificationProvider('token-1').future);

      verifyNever(() => repository.getAccount());
    });

    test('a rejected token fails once and is not retried', () async {
      when(() => repository.confirmEmailVerification(token: 'bad'))
          .thenThrow(const ApiException(code: 'invalid_token', statusCode: 400));
      final container = ProviderContainer.test(overrides: overrides());
      container.listen(emailVerificationProvider('bad'), (_, _) {});

      await expectLater(
        container.read(emailVerificationProvider('bad').future),
        throwsA(isA<ApiException>()),
      );
      await Future<void>.delayed(const Duration(milliseconds: 500));

      verify(() => repository.confirmEmailVerification(token: 'bad')).called(1);
    });
  });

  group('RecoveryController', () {
    test('reports success and failure of an action', () async {
      when(() => repository.requestPasswordReset(email: 'ayse@example.com')).thenAnswer((_) async {});
      when(() => repository.requestEmailVerification())
          .thenThrow(const ApiException(code: 'email_already_verified', statusCode: 409));
      final container = ProviderContainer.test(overrides: overrides());
      container.listen(recoveryControllerProvider, (_, _) {});
      final controller = container.read(recoveryControllerProvider.notifier);

      expect(await controller.requestPasswordReset(email: ' ayse@example.com '), isTrue);
      expect(container.read(recoveryControllerProvider).hasError, isFalse);

      expect(await controller.resendVerificationEmail(), isFalse);
      expect(
        (container.read(recoveryControllerProvider).error! as ApiException).code,
        'email_already_verified',
      );
    });
  });

  Future<void> pumpScreen(WidgetTester tester, Widget screen) async {
    tester.view.physicalSize = const Size(1080, 2600);
    tester.view.devicePixelRatio = 2;
    addTearDown(tester.view.reset);
    await tester.pumpWidget(ProviderScope(overrides: overrides(), child: localized(screen)));
    await tester.pump();
  }

  group('VerifyEmailScreen', () {
    testWidgets('shows progress, then success', (tester) async {
      final pending = Completer<void>();
      when(() => repository.confirmEmailVerification(token: 'token-1')).thenAnswer((_) => pending.future);
      await pumpScreen(tester, const VerifyEmailScreen(token: 'token-1'));
      await tester.pump();

      expect(find.text(l10n.verifyEmailInProgress), findsOneWidget);

      pending.complete();
      await tester.pumpAndSettle();

      expect(find.text(l10n.verifyEmailSuccess), findsOneWidget);
    });

    testWidgets('explains an expired link', (tester) async {
      when(() => repository.confirmEmailVerification(token: 'old'))
          .thenThrow(const ApiException(code: 'invalid_token', statusCode: 400));
      await pumpScreen(tester, const VerifyEmailScreen(token: 'old'));
      await tester.pumpAndSettle();

      expect(find.text(l10n.errorInvalidLink), findsOneWidget);
    });

    testWidgets('a link without a token is invalid and calls nothing', (tester) async {
      await pumpScreen(tester, const VerifyEmailScreen(token: null));

      expect(find.text(l10n.errorInvalidLink), findsOneWidget);
      verifyNever(() => repository.confirmEmailVerification(token: any(named: 'token')));
    });
  });

  group('ForgotPasswordScreen', () {
    testWidgets('validates the email, then confirms the request was sent', (tester) async {
      when(() => repository.requestPasswordReset(email: 'ayse@example.com')).thenAnswer((_) async {});
      await pumpScreen(tester, const ForgotPasswordScreen());

      await tester.tap(find.widgetWithText(FilledButton, l10n.forgotPasswordSubmit));
      await tester.pump();
      expect(find.text(l10n.validationRequired), findsOneWidget);

      await tester.enterText(find.byType(TextFormField), 'ayse@example.com');
      await tester.tap(find.widgetWithText(FilledButton, l10n.forgotPasswordSubmit));
      await tester.pumpAndSettle();

      expect(find.text(l10n.forgotPasswordSent), findsOneWidget);
      expect(find.byType(TextFormField), findsNothing);
    });

    testWidgets('shows a connection error and keeps the form', (tester) async {
      when(() => repository.requestPasswordReset(email: any(named: 'email')))
          .thenThrow(const ApiException(code: ApiException.networkCode));
      await pumpScreen(tester, const ForgotPasswordScreen());

      await tester.enterText(find.byType(TextFormField), 'ayse@example.com');
      await tester.tap(find.widgetWithText(FilledButton, l10n.forgotPasswordSubmit));
      await tester.pumpAndSettle();

      expect(find.text(l10n.errorNetwork), findsOneWidget);
      expect(find.byType(TextFormField), findsOneWidget);
    });
  });

  group('ResetPasswordScreen', () {
    testWidgets('changes the password and ends the local session', (tester) async {
      await SessionStore(persistence).save(testSession());
      when(() => repository.confirmPasswordReset(token: 'token-1', newPassword: 'N3w-Passw0rd'))
          .thenAnswer((_) async {});
      when(() => repository.logout(refreshToken: any(named: 'refreshToken'))).thenAnswer((_) async {});
      await pumpScreen(tester, const ResetPasswordScreen(token: 'token-1'));

      await tester.enterText(find.byType(TextFormField), 'short');
      await tester.tap(find.widgetWithText(FilledButton, l10n.resetPasswordSubmit));
      await tester.pump();
      expect(find.text(l10n.validationPassword), findsOneWidget);

      await tester.enterText(find.byType(TextFormField), 'N3w-Passw0rd');
      await tester.tap(find.widgetWithText(FilledButton, l10n.resetPasswordSubmit));
      await tester.pumpAndSettle();

      expect(find.text(l10n.resetPasswordSuccess), findsOneWidget);
      expect(persistence.value, isNull);
    });

    testWidgets('explains an expired link', (tester) async {
      when(() => repository.confirmPasswordReset(token: any(named: 'token'), newPassword: any(named: 'newPassword')))
          .thenThrow(const ApiException(code: 'invalid_token', statusCode: 400));
      await pumpScreen(tester, const ResetPasswordScreen(token: 'old'));

      await tester.enterText(find.byType(TextFormField), 'N3w-Passw0rd');
      await tester.tap(find.widgetWithText(FilledButton, l10n.resetPasswordSubmit));
      await tester.pumpAndSettle();

      expect(find.text(l10n.errorInvalidLink), findsOneWidget);
    });

    testWidgets('a link without a token shows no form', (tester) async {
      await pumpScreen(tester, const ResetPasswordScreen(token: null));

      expect(find.text(l10n.errorInvalidLink), findsOneWidget);
      expect(find.byType(TextFormField), findsNothing);
    });
  });
}
