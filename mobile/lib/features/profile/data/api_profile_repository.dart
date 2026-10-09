import 'package:dio/dio.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';

import '../../../core/api/api_exception.dart';
import '../../../core/api/api_providers.dart';
import '../../../core/api/generated/export.dart';
import '../../../core/riverpod.dart';
import '../domain/profile_repository.dart';

part 'api_profile_repository.g.dart';

@Riverpod(keepAlive: true)
ProfileRepository profileRepository(Ref ref) {
  // Uploads go to object storage, not to the API: no base URL and, above all, no
  // interceptor that would attach the user's access token to a third-party request.
  final uploadDio = Dio(
    BaseOptions(
      connectTimeout: const Duration(seconds: 10),
      sendTimeout: const Duration(seconds: 60),
      receiveTimeout: const Duration(seconds: 20),
    ),
  );
  ref.onDispose(uploadDio.close);
  return ApiProfileRepository(ref.watch(exparterApiProvider), uploadDio);
}

/// The fixed list of cities. Kept for the lifetime of the app once loaded.
@Riverpod(keepAlive: true, retry: noRetry)
Future<List<City>> cities(Ref ref) => ref.watch(profileRepositoryProvider).listCities();

class ApiProfileRepository implements ProfileRepository {
  const ApiProfileRepository(this._api, this._uploadDio);

  final ExparterApi _api;
  final Dio _uploadDio;

  @override
  Future<Profile> getOwnProfile() => guardApiCall(_api.profiles.getOwnProfile);

  @override
  Future<Profile> updateOwnProfile({
    required String displayName,
    required String? bio,
    required String? city,
    required bool acceptsOnline,
    required bool acceptsInPerson,
  }) =>
      guardApiCall(
        () => _api.profiles.updateOwnProfile(
          body: UpdateProfileRequest(
            displayName: displayName,
            bio: bio,
            city: city,
            acceptsOnline: acceptsOnline,
            acceptsInPerson: acceptsInPerson,
          ),
        ),
      );

  @override
  Future<Profile> uploadAvatar(PickedImage image) => guardApiCall(() async {
        final target = await _api.profiles.createAvatarUploadUrl(
          body: AvatarUploadUrlRequest(
            contentType: image.contentType,
            contentLength: image.bytes.length,
          ),
        );

        // The URL is signed for exactly this content type; any other is refused.
        await _uploadDio.put<void>(
          target.uploadUrl,
          data: image.bytes,
          options: Options(
            contentType: image.contentType.json,
            headers: {Headers.contentLengthHeader: image.bytes.length},
          ),
        );

        return _api.profiles.setAvatar(body: SetAvatarRequest(objectKey: target.objectKey));
      });

  @override
  Future<void> deleteAvatar() => guardApiCall(_api.profiles.deleteAvatar);

  @override
  Future<List<City>> listCities() => guardApiCall(_api.profiles.listCities);
}
