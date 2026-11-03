import 'package:dio/dio.dart';
import 'package:exparter/core/api/api_exception.dart';
import 'package:exparter/core/api/error_messages.dart';
import 'package:exparter/core/api/api_providers.dart';
import 'package:exparter/core/auth/session_controller.dart';
import 'package:exparter/core/auth/session_store.dart';
import 'package:exparter/core/error_reporting.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:sentry_flutter/sentry_flutter.dart';
import 'package:exparter/l10n/app_localizations_tr.dart';
import 'package:exparter/features/profile/presentation/widgets/profile_avatar.dart';
import 'package:flutter_test/flutter_test.dart';

import '../helpers.dart';

void main() {
  group('ApiException.fromDio', () {
    DioException dioError({int? statusCode, Object? data}) {
      final request = RequestOptions(path: '/api/v1/me');
      return DioException(
        requestOptions: request,
        response: statusCode == null
            ? null
            : Response<Object?>(requestOptions: request, statusCode: statusCode, data: data),
      );
    }

    test('reads the code and field errors from problem details', () {
      final exception = ApiException.fromDio(
        dioError(
          statusCode: 400,
          data: <String, Object?>{
            'code': 'validation_failed',
            'errors': <String, Object?>{
              'username': ['bad'],
            },
          },
        ),
      );

      expect(exception.code, 'validation_failed');
      expect(exception.statusCode, 400);
      expect(exception.fieldErrors, {
        'username': ['bad'],
      });
    });

    test('treats a missing response as a network error', () {
      expect(ApiException.fromDio(dioError()).code, ApiException.networkCode);
    });

    test('treats a response without a code as unknown', () {
      expect(
        ApiException.fromDio(dioError(statusCode: 502, data: '<html>')).code,
        ApiException.unknownCode,
      );
    });
  });

  group('errorMessage', () {
    final l10n = AppLocalizationsTr();

    test('maps known codes to Turkish messages', () {
      expect(errorMessage(l10n, const ApiException(code: 'invalid_credentials')), l10n.errorInvalidCredentials);
      expect(errorMessage(l10n, const ApiException(code: 'username_already_taken')), l10n.errorUsernameTaken);
      expect(errorMessage(l10n, const ApiException(code: ApiException.networkCode)), l10n.errorNetwork);
      expect(errorMessage(l10n, const ApiException(statusCode: 429, code: 'rate_limited')), l10n.errorRateLimited);
      expect(errorMessage(l10n, const ApiException(code: 'session_time_taken')), l10n.errorSessionTimeTaken);
      expect(errorMessage(l10n, const ApiException(code: 'other_user_busy')), l10n.errorOtherUserBusy);
    });

    test('never exposes raw error text', () {
      expect(errorMessage(l10n, const ApiException(code: 'some_new_backend_code')), l10n.errorUnknown);
      expect(errorMessage(l10n, StateError('secret internals')), l10n.errorUnknown);
    });
  });

  test('an avatar is cached under its address without the signature', () {
    expect(
      avatarCacheKey('http://10.0.2.2:3900/exparter/avatars/u1/a.jpg?X-Amz-Signature=abc&X-Amz-Date=1'),
      'http://10.0.2.2:3900/exparter/avatars/u1/a.jpg',
    );
    expect(avatarCacheKey('https://cdn.example.com/a.png?X-Amz-Signature=def'), avatarCacheKey('https://cdn.example.com/a.png?x=1'));
  });

  test('crash reports carry the id of the signed-in user and nothing else about them', () async {
    // Initialised without anywhere to send to: only the scope is looked at.
    await Sentry.init((options) => options..dsn = 'https://key@example.invalid/1');
    addTearDown(Sentry.close);
    final persistence = InMemorySessionPersistence();
    await SessionStore(persistence).save(testSession());
    final container = ProviderContainer.test(overrides: [sessionStoreProvider.overrideWithValue(SessionStore(persistence))]);
    container.listen(errorReportUserProvider, (_, _) {});
    await container.read(sessionControllerProvider.future);
    container.read(errorReportUserProvider);

    SentryUser? user;
    await Sentry.configureScope((scope) => user = scope.user);
    expect(user?.id, testAccount().id);
    expect((user?.email, user?.username, user?.ipAddress), (null, null, null));

    await container.read(sessionControllerProvider.notifier).end();
    container.read(errorReportUserProvider);
    await Sentry.configureScope((scope) => user = scope.user);
    expect(user, isNull);
  });

  group('SessionStore', () {
    test('restores a saved session from persistence', () async {
      final persistence = InMemorySessionPersistence();
      await SessionStore(persistence).save(testSession());

      final restored = await SessionStore(persistence).load();

      expect(restored!.accessToken, 'access-1');
      expect(restored.refreshToken, 'refresh-1');
      expect(restored.account, testAccount());
    });

    test('discards unreadable data instead of failing', () async {
      final persistence = InMemorySessionPersistence('not json');

      expect(await SessionStore(persistence).load(), isNull);
      expect(persistence.value, isNull);
    });

    test('clear removes the session', () async {
      final persistence = InMemorySessionPersistence();
      final store = SessionStore(persistence);
      await store.save(testSession());

      await store.clear();

      expect(store.current, isNull);
      expect(persistence.value, isNull);
    });
  });
}
