import 'package:dio/dio.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';

import '../auth/session_controller.dart';
import '../auth/session_store.dart';
import '../config/app_config.dart';
import 'auth_interceptor.dart';
import 'generated/export.dart';

part 'api_providers.g.dart';

BaseOptions _baseOptions() => BaseOptions(
      baseUrl: AppConfig.apiBaseUrl,
      connectTimeout: const Duration(seconds: 10),
      receiveTimeout: const Duration(seconds: 20),
      contentType: Headers.jsonContentType,
    );

@Riverpod(keepAlive: true)
SessionStore sessionStore(Ref ref) => SessionStore(const SecureSessionPersistence());

@Riverpod(keepAlive: true)
Dio dio(Ref ref) {
  final dio = Dio(_baseOptions());
  dio.interceptors.add(
    AuthInterceptor(
      store: ref.watch(sessionStoreProvider),
      plainDio: Dio(_baseOptions()),
      onSessionExpired: () => ref.read(sessionControllerProvider.notifier).expire(),
    ),
  );
  ref.onDispose(dio.close);
  return dio;
}

@Riverpod(keepAlive: true)
ExparterApi exparterApi(Ref ref) => ExparterApi(ref.watch(dioProvider));
