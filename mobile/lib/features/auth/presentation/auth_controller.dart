import 'package:riverpod_annotation/riverpod_annotation.dart';

import '../../../core/api/api_providers.dart';
import '../../../core/api/generated/export.dart';
import '../../../core/auth/session_controller.dart';
import '../../notifications/presentation/push_controller.dart';
import '../data/api_auth_repository.dart';

part 'auth_controller.g.dart';

/// Runs the sign-in, sign-up and sign-out actions. Its state is the progress of the
/// action in flight: screens show a spinner while loading and a message on error.
@riverpod
class AuthController extends _$AuthController {
  @override
  FutureOr<void> build() {}

  Future<void> login({required String identifier, required String password}) => _run(
        () => ref.read(authRepositoryProvider).login(
              identifier: identifier.trim(),
              password: password,
            ),
      );

  Future<void> register({
    required String email,
    required String username,
    required String password,
    required bool ageConfirmed,
  }) =>
      _run(
        () => ref.read(authRepositoryProvider).register(
              email: email.trim(),
              username: username,
              password: password,
              ageConfirmed: ageConfirmed,
            ),
      );

  Future<void> logout() async {
    state = const AsyncLoading();
    final refreshToken = ref.read(sessionStoreProvider).current?.refreshToken;
    final session = ref.read(sessionControllerProvider.notifier);

    // The phone stops getting the user's pushes; this needs the session still valid.
    await ref.read(pushRegistrationProvider.notifier).unregister();
    if (refreshToken != null) {
      try {
        await ref.read(authRepositoryProvider).logout(refreshToken: refreshToken);
      } on Object {
        // Being offline must not trap the user in a signed-in app. The token simply
        // stays valid on the server until it expires.
      }
    }

    await session.end();
    if (ref.mounted) {
      state = const AsyncData(null);
    }
  }

  /// Asks for the account to be deleted and signs out. Returns whether the server
  /// accepted; a refusal (a wrong password, mostly) is left in the state.
  Future<bool> deleteAccount({required String password}) async {
    state = const AsyncLoading();
    final session = ref.read(sessionControllerProvider.notifier);
    try {
      await ref.read(authRepositoryProvider).requestAccountDeletion(password: password);
    } on Object catch (error, stackTrace) {
      if (ref.mounted) {
        state = AsyncError(error, stackTrace);
      }

      return false;
    }

    // The server has already signed every device out; this only forgets the tokens.
    await session.end();

    // The sign-in screen that comes next may be handed this same controller; left as
    // loading, it would show a spinner where its button should be.
    if (ref.mounted) {
      state = const AsyncData(null);
    }

    return true;
  }

  Future<void> _run(Future<AuthResponse> Function() action) async {
    state = const AsyncLoading();
    final session = ref.read(sessionControllerProvider.notifier);
    final restoredNotice = ref.read(accountRestoredNoticeProvider.notifier);
    try {
      final response = await action();
      if (response.accountRestored) {
        restoredNotice.show();
      }

      // Starting the session redirects away from the form, which disposes this controller.
      await session.start(response);
      if (ref.mounted) {
        state = const AsyncData(null);
      }
    } on Object catch (error, stackTrace) {
      if (ref.mounted) {
        state = AsyncError(error, stackTrace);
      }
    }
  }
}

/// Set by a sign-in that cancelled a pending account deletion, so the home screen can
/// tell the user once.
@Riverpod(keepAlive: true)
class AccountRestoredNotice extends _$AccountRestoredNotice {
  @override
  bool build() => false;

  void show() => state = true;

  void dismiss() => state = false;
}
