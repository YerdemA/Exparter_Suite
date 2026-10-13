import 'dart:typed_data';

import '../../../core/api/generated/export.dart';

/// An image chosen by the user, ready to upload.
class PickedImage {
  const PickedImage({required this.bytes, required this.contentType});

  final Uint8List bytes;
  final AvatarUploadUrlRequestContentType contentType;
}

/// Profile operations. Failures are thrown as `ApiException`.
abstract interface class ProfileRepository {
  Future<Profile> getOwnProfile();

  Future<Profile> updateOwnProfile({
    required String displayName,
    required String? bio,
    required String? city,
    required bool acceptsOnline,
    required bool acceptsInPerson,
  });

  /// Uploads [image] straight to object storage and makes it the avatar.
  Future<Profile> uploadAvatar(PickedImage image);

  Future<void> deleteAvatar();

  Future<List<City>> listCities();
}
