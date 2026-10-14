import 'package:dio/dio.dart';
import 'package:exparter/core/api/api_exception.dart';
import 'package:exparter/core/api/error_messages.dart';
import 'package:exparter/core/auth/session_store.dart';
import 'package:exparter/l10n/app_localizations_tr.dart';
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
    });

    test('never exposes raw error text', () {
      expect(errorMessage(l10n, const ApiException(code: 'some_new_backend_code')), l10n.errorUnknown);
      expect(errorMessage(l10n, StateError('secret internals')), l10n.errorUnknown);
    });
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
