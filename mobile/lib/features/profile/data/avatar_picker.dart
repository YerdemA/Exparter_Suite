import 'package:image_picker/image_picker.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';

import '../../../core/api/generated/export.dart';
import '../domain/profile_repository.dart';

part 'avatar_picker.g.dart';

enum AvatarSource { gallery, camera }

/// Lets the user choose a photo. Returns null if they back out.
typedef AvatarPicker = Future<PickedImage?> Function(AvatarSource source);

@Riverpod(keepAlive: true)
AvatarPicker avatarPicker(Ref ref) => (source) async {
      final file = await ImagePicker().pickImage(
        source: source == AvatarSource.camera ? ImageSource.camera : ImageSource.gallery,
        // An avatar is shown small; shrinking on the device keeps uploads fast and far
        // below the 5 MB limit.
        maxWidth: 1024,
        maxHeight: 1024,
        imageQuality: 85,
      );
      if (file == null) {
        return null;
      }

      // The generator cannot derive readable names for these enum values, so they are
      // looked up by their wire value.
      final name = file.name.toLowerCase();
      final mimeType = name.endsWith('.png')
          ? 'image/png'
          : name.endsWith('.webp')
              ? 'image/webp'
              : 'image/jpeg';
      return PickedImage(
        bytes: await file.readAsBytes(),
        contentType: AvatarUploadUrlRequestContentType.fromJson(mimeType),
      );
    };
