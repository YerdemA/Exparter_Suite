import 'package:riverpod_annotation/riverpod_annotation.dart';

import '../../../core/auth/session_controller.dart';
import '../data/api_auth_repository.dart';

part 'recovery_controller.g.dart';

/// Riverpod retries a failed provider by default. Confirming a token must run exactly
/// once: a rejected token stays rejected, and retrying would only hammer the API.
Duration? _noRetry(int retryCount, Object error) => null;

/// Confirms the email address for [token] as soon as it is first watched.
///
/// If the user is signed in, their cached account is refreshed so the rest of the app
/// sees the verified state immediately.
@Riverpod(retry: _noRetry)
Future<void> emailVerification(Ref ref, String token) async {
  final repository = ref.watch(authRepositoryProvider);
  final session = ref.read(sessionControllerProvider.notifier);
  final signedIn = (await ref.read(sessionControllerProvider.future)) != null;

  await repository.confirmEmailVerification(token: token);

  if (signedIn) {
    try {
      await session.updateAccount(await repository.getAccount());
    } on Object {
      // The address is verified either way; a stale banner corrects itself on the
      // next sign-in.
    }
  }
}

/// Runs the email and password recovery actions. Its state is the progress of the
/// action in flight; each method reports whether it succeeded.
@riverpod
class RecoveryController extends _$RecoveryController {
  @override
  FutureOr<void> build() {}

  Future<bool> resendVerificationEmail() =>
      _run(() => ref.read(authRepositoryProvider).requestEmailVerification());

  Future<bool> requestPasswordReset({required String email}) =>
      _run(() => ref.read(authRepositoryProvider).requestPasswordReset(email: email.trim()));

  Future<bool> confirmPasswordReset({required String token, required String newPassword}) => _run(
        () => ref.read(authRepositoryProvider).confirmPasswordReset(
              token: token,
              newPassword: newPassword,
            ),
      );

  Future<bool> _run(Future<void> Function() action) async {
    state = const AsyncLoading();
    try {
      await action();
      if (ref.mounted) {
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
