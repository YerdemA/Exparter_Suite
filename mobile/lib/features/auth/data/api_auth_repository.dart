import 'package:riverpod_annotation/riverpod_annotation.dart';

import '../../../core/api/api_exception.dart';
import '../../../core/api/api_providers.dart';
import '../../../core/api/generated/export.dart';
import '../domain/auth_repository.dart';

part 'api_auth_repository.g.dart';

@Riverpod(keepAlive: true)
AuthRepository authRepository(Ref ref) => ApiAuthRepository(ref.watch(exparterApiProvider));

class ApiAuthRepository implements AuthRepository {
  const ApiAuthRepository(this._api);

  final ExparterApi _api;

  @override
  Future<AuthResponse> register({
    required String email,
    required String username,
    required String password,
    required bool ageConfirmed,
  }) =>
      guardApiCall(
        () => _api.auth.register(
          body: RegisterRequest(
            email: email,
            username: username,
            password: password,
            ageConfirmed: ageConfirmed,
          ),
        ),
      );

  @override
  Future<AuthResponse> login({required String identifier, required String password}) => guardApiCall(
        () => _api.auth.login(body: LoginRequest(identifier: identifier, password: password)),
      );

  @override
  Future<void> logout({required String refreshToken}) => guardApiCall(
        () => _api.auth.logout(body: RefreshTokenRequest(refreshToken: refreshToken)),
      );
}
