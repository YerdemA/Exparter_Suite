import 'package:dio/dio.dart';

import '../auth/session_store.dart';
import 'generated/export.dart';

/// Adds the access token to requests and transparently refreshes it once it expires.
///
/// [QueuedInterceptor] handles errors one at a time, so when several requests fail
/// with 401 together only the first one refreshes; the rest find a new token already
/// in the store and are simply retried.
class AuthInterceptor extends QueuedInterceptor {
  AuthInterceptor({
    required this.store,
    required this.plainDio,
    required this.onSessionExpired,
  });

  static const _authorization = 'Authorization';

  /// Endpoints that work without a session. A 401 from these means wrong credentials,
  /// not an expired token, so they are never refreshed or retried.
  static const _publicPaths = {
    '/api/v1/auth/register',
    '/api/v1/auth/login',
    '/api/v1/auth/refresh',
    '/api/v1/auth/logout',
    '/api/v1/auth/email-verification/confirm',
    '/api/v1/auth/password-reset/request',
    '/api/v1/auth/password-reset/confirm',
  };

  final SessionStore store;

  /// A client without this interceptor, used for the refresh call and for retries.
  /// Sending those through the intercepted client would deadlock the queue.
  final Dio plainDio;

  final Future<void> Function() onSessionExpired;

  static String _bearer(String accessToken) => 'Bearer $accessToken';

  @override
  void onRequest(RequestOptions options, RequestInterceptorHandler handler) {
    final session = store.current;
    if (session != null && !_publicPaths.contains(options.path)) {
      options.headers[_authorization] = _bearer(session.accessToken);
    }

    handler.next(options);
  }

  @override
  Future<void> onError(DioException err, ErrorInterceptorHandler handler) async {
    final request = err.requestOptions;
    final session = store.current;
    if (err.response?.statusCode != 401 || session == null || _publicPaths.contains(request.path)) {
      return handler.next(err);
    }

    // If the token in the store differs from the one this request carried, a request
    // ahead of it in the queue has already refreshed.
    if (request.headers[_authorization] == _bearer(session.accessToken)) {
      try {
        final tokens = await AuthClient(plainDio).refreshToken(
          body: RefreshTokenRequest(refreshToken: session.refreshToken),
        );
        await store.save(
          session.copyWith(accessToken: tokens.accessToken, refreshToken: tokens.refreshToken),
        );
      } on DioException catch (refreshError) {
        // Only a rejected refresh token ends the session. A network failure must not
        // sign the user out; the original error is reported and they can retry.
        if (refreshError.response?.statusCode == 401) {
          await onSessionExpired();
        }

        return handler.next(err);
      }
    }

    try {
      request.headers[_authorization] = _bearer(store.current!.accessToken);
      handler.resolve(await plainDio.fetch<dynamic>(request));
    } on DioException catch (retryError) {
      handler.next(retryError);
    }
  }
}
