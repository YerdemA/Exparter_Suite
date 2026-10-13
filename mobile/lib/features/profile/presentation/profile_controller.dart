import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';

import '../../../core/api/generated/export.dart';
import '../../../core/auth/interaction_requirements.dart';
import '../../../core/auth/session_controller.dart';
import '../../../core/riverpod.dart';
import '../data/api_profile_repository.dart';
import '../domain/profile_repository.dart';

part 'profile_controller.g.dart';

/// The signed-in user's profile; null when signed out.
///
/// Reloads whenever a different account signs in, so one user's profile can never be
/// shown to the next.
@Riverpod(keepAlive: true, retry: noRetry)
class OwnProfile extends _$OwnProfile {
  @override
  Future<Profile?> build() async {
    final accountId = ref.watch(sessionControllerProvider.select((session) => session.value?.id));
    return accountId == null ? null : ref.watch(profileRepositoryProvider).getOwnProfile();
  }

  void set(Profile profile) => state = AsyncData(profile);
}

/// Runs the profile's write actions. Its state is the progress of the action in
/// flight; each method reports whether it succeeded.
@riverpod
class ProfileActions extends _$ProfileActions {
  @override
  FutureOr<void> build() {}

  Future<bool> save({
    required String displayName,
    required String bio,
    required String? city,
    required bool acceptsOnline,
    required bool acceptsInPerson,
  }) =>
      _run(
        (repository) => repository.updateOwnProfile(
          displayName: displayName.trim(),
          bio: bio.trim().isEmpty ? null : bio.trim(),
          // A city left over from before in-person was switched off would be saved
          // silently; the profile should say only what the form shows.
          city: acceptsInPerson ? city : null,
          acceptsOnline: acceptsOnline,
          acceptsInPerson: acceptsInPerson,
        ),
      );

  Future<bool> uploadAvatar(PickedImage image) => _run((repository) => repository.uploadAvatar(image));

  Future<bool> removeAvatar() => _run((repository) async {
        await repository.deleteAvatar();
        return repository.getOwnProfile();
      });

  Future<bool> _run(Future<Profile> Function(ProfileRepository repository) action) async {
    state = const AsyncLoading();
    final ownProfile = ref.read(ownProfileProvider.notifier);
    try {
      ownProfile.set(await action(ref.read(profileRepositoryProvider)));
      if (ref.mounted) {
        // Filling in the profile is one of the interaction requirements.
        ref.invalidate(interactionRequirementsProvider);
        state = const AsyncData(null);
      }

      return true;
    } on Object catch (error, stackTrace) {
      if (ref.mounted) {
        state = AsyncError(error, stackTrace);
      }

      return false;
    }
  }
}
