import 'package:exparter/core/api/api_exception.dart';
import 'package:exparter/core/api/api_providers.dart';
import 'package:exparter/core/auth/session_controller.dart';
import 'package:exparter/core/auth/session_store.dart';
import 'package:exparter/features/auth/data/api_auth_repository.dart';
import 'package:exparter/features/auth/domain/auth_validators.dart';
import 'package:exparter/features/auth/presentation/auth_controller.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mocktail/mocktail.dart';

import '../../helpers.dart';

void main() {
  group('AuthValidators', () {
    test('username follows the contract rules', () {
      for (final valid in ['ayse', 'ayse.yilmaz', 'user_01', '_a1']) {
        expect(AuthValidators.username(valid), isNull, reason: valid);
      }

      for (final invalid in ['ab', 'Ayse', '.ayse', 'ayse.', 'ay..se', 'ay se', 'ayşe']) {
        expect(AuthValidators.username(invalid), FieldError.username, reason: invalid);
      }

      expect(AuthValidators.username(''), FieldError.required);
    });

    test('email and password', () {
      expect(AuthValidators.email('ayse@example.com'), isNull);
      expect(AuthValidators.email('ayse@example'), FieldError.email);
      expect(AuthValidators.email(' '), FieldError.required);
      expect(AuthValidators.password('12345678'), isNull);
      expect(AuthValidators.password('1234567'), FieldError.password);
      expect(AuthValidators.password(''), FieldError.required);
    });
  });

  group('AuthController', () {
    late MockAuthRepository repository;
    late InMemorySessionPersistence persistence;
    late ProviderContainer container;

    setUp(() {
      repository = MockAuthRepository();
      persistence = InMemorySessionPersistence();
      container = ProviderContainer.test(
        overrides: [
          authRepositoryProvider.overrideWithValue(repository),
          sessionStoreProvider.overrideWithValue(SessionStore(persistence)),
        ],
      );
    });

    AuthController controller() {
      // Keeps the auto-disposed controller alive for the duration of the test.
      container.listen(authControllerProvider, (_, _) {});
      return container.read(authControllerProvider.notifier);
    }

    test('login starts and persists the session', () async {
      when(() => repository.login(identifier: 'ayse', password: 'Passw0rd!x'))
          .thenAnswer((_) async => testAuthResponse());
      await container.read(sessionControllerProvider.future);

      await controller().login(identifier: ' ayse ', password: 'Passw0rd!x');

      expect(container.read(sessionControllerProvider).value, testAccount());
      expect(persistence.value, contains('refresh-1'));
      expect(container.read(authControllerProvider).hasError, isFalse);
      expect(container.read(accountRestoredNoticeProvider), isFalse);
    });

    test('login failure is exposed as an error and leaves the user signed out', () async {
      when(() => repository.login(identifier: any(named: 'identifier'), password: any(named: 'password')))
          .thenThrow(const ApiException(code: 'invalid_credentials', statusCode: 401));
      await container.read(sessionControllerProvider.future);

      await controller().login(identifier: 'ayse', password: 'wrong');

      final state = container.read(authControllerProvider);
      expect((state.error! as ApiException).code, 'invalid_credentials');
      expect(container.read(sessionControllerProvider).value, isNull);
      expect(persistence.value, isNull);
    });

    test('a sign-in that restores the account raises the notice', () async {
      when(() => repository.login(identifier: any(named: 'identifier'), password: any(named: 'password')))
          .thenAnswer((_) async => testAuthResponse(accountRestored: true));
      await container.read(sessionControllerProvider.future);

      await controller().login(identifier: 'ayse', password: 'Passw0rd!x');

      expect(container.read(accountRestoredNoticeProvider), isTrue);
    });

    test('register starts the session', () async {
      when(
        () => repository.register(
          email: 'ayse@example.com',
          username: 'ayse',
          password: 'Passw0rd!x',
          ageConfirmed: true,
          termsAccepted: true,
        ),
      ).thenAnswer((_) async => testAuthResponse());
      await container.read(sessionControllerProvider.future);

      await controller().register(
        email: ' ayse@example.com ',
        username: 'ayse',
        password: 'Passw0rd!x',
        ageConfirmed: true,
        termsAccepted: true,
      );

      expect(container.read(sessionControllerProvider).value, testAccount());
    });

    test('logout ends the session even when the server cannot be reached', () async {
      when(() => repository.logout(refreshToken: 'refresh-1'))
          .thenThrow(const ApiException(code: ApiException.networkCode));
      await container.read(sessionStoreProvider).save(testSession());
      container.invalidate(sessionControllerProvider);
      await container.read(sessionControllerProvider.future);

      await controller().logout();

      verify(() => repository.logout(refreshToken: 'refresh-1')).called(1);
      expect(container.read(sessionControllerProvider).value, isNull);
      expect(persistence.value, isNull);
    });

    test('a stored session is restored on startup', () async {
      await SessionStore(persistence).save(testSession());

      expect(await container.read(sessionControllerProvider.future), testAccount());
    });
  });
}
