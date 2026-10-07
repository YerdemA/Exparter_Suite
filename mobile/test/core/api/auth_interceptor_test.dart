import 'package:dio/dio.dart';
import 'package:exparter/core/api/auth_interceptor.dart';
import 'package:exparter/core/auth/session_store.dart';
import 'package:flutter_test/flutter_test.dart';

import '../../helpers.dart';

const _me = '/api/v1/me';
const _refresh = '/api/v1/auth/refresh';
const _login = '/api/v1/auth/login';

void main() {
  late SessionStore store;
  late Dio dio;
  late FakeHttpAdapter apiAdapter;
  late FakeHttpAdapter plainAdapter;
  late int sessionExpiredCalls;

  /// What the fake server does with a refresh request.
  late ResponseBody Function() refreshResult;

  String? bearerOf(RequestOptions options) => options.headers['Authorization'] as String?;

  // Both adapters stand for the same server: it accepts only the token "access-2".
  ResponseBody server(RequestOptions options) {
    if (options.path == _refresh) {
      return refreshResult();
    }

    if (options.path == _login) {
      return problemResponse(401, 'invalid_credentials');
    }

    return bearerOf(options) == 'Bearer access-2'
        ? jsonResponse(200, {'ok': true})
        : problemResponse(401, 'token_expired');
  }

  setUp(() async {
    store = SessionStore(InMemorySessionPersistence());
    await store.save(testSession());
    sessionExpiredCalls = 0;
    refreshResult = () => jsonResponse(200, {
          'accessToken': 'access-2',
          'refreshToken': 'refresh-2',
          'expiresIn': 900,
        });

    apiAdapter = FakeHttpAdapter(server);
    plainAdapter = FakeHttpAdapter(server);
    final plainDio = Dio()..httpClientAdapter = plainAdapter;
    dio = Dio()
      ..httpClientAdapter = apiAdapter
      ..interceptors.add(
        AuthInterceptor(
          store: store,
          plainDio: plainDio,
          onSessionExpired: () async {
            sessionExpiredCalls++;
            await store.clear();
          },
        ),
      );
  });

  test('adds the access token to protected requests only', () async {
    await store.save(testSession(accessToken: 'access-2'));

    await dio.get<dynamic>(_me);
    await expectLater(dio.post<dynamic>(_login), throwsA(isA<DioException>()));

    expect(bearerOf(apiAdapter.requests[0]), 'Bearer access-2');
    expect(bearerOf(apiAdapter.requests[1]), isNull);
  });

  test('refreshes an expired token and retries the request', () async {
    final response = await dio.get<dynamic>(_me);

    expect(response.statusCode, 200);
    expect(plainAdapter.paths, [_refresh, _me]);
    expect(bearerOf(plainAdapter.requests.last), 'Bearer access-2');
    expect(store.current!.accessToken, 'access-2');
    expect(store.current!.refreshToken, 'refresh-2');
    expect(sessionExpiredCalls, 0);
  });

  test('refreshes only once when several requests expire together', () async {
    final responses = await Future.wait([
      dio.get<dynamic>(_me),
      dio.get<dynamic>(_me),
      dio.get<dynamic>(_me),
    ]);

    expect(responses.map((response) => response.statusCode), everyElement(200));
    expect(plainAdapter.paths.where((path) => path == _refresh), hasLength(1));
  });

  test('ends the session when the refresh token is rejected', () async {
    refreshResult = () => problemResponse(401, 'refresh_token_invalid');

    await expectLater(dio.get<dynamic>(_me), throwsA(isA<DioException>()));

    expect(sessionExpiredCalls, 1);
    expect(store.current, isNull);
  });

  test('keeps the session when the refresh fails for another reason', () async {
    refreshResult = () => problemResponse(500, 'internal_error');

    await expectLater(dio.get<dynamic>(_me), throwsA(isA<DioException>()));

    expect(sessionExpiredCalls, 0);
    expect(store.current!.refreshToken, 'refresh-1');
  });

  test('does not refresh after a 401 from a public endpoint', () async {
    await expectLater(dio.post<dynamic>(_login), throwsA(isA<DioException>()));

    expect(plainAdapter.requests, isEmpty);
    expect(sessionExpiredCalls, 0);
  });
}
