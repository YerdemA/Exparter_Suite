import 'dart:async';

import 'package:riverpod_annotation/riverpod_annotation.dart';

import '../../../core/api/api_exception.dart';
import '../../../core/auth/interaction_requirements.dart';
import '../../../core/auth/session_controller.dart';
import '../../../core/riverpod.dart';
import '../data/api_auth_repository.dart';
import 'account_refresh.dart';

part 'recovery_controller.g.dart';

const _emailAlreadyVerifiedCode = 'email_already_verified';

/// Confirms the email address for [token] as soon as it is first watched.
///
/// If the user is signed in, their cached account is refreshed so the rest of the app
/// sees the verified state immediately.
@Riverpod(retry: noRetry)
Future<void> emailVerification(Ref ref, String token) async {
  final repository = ref.watch(authRepositoryProvider);
  final session = ref.read(sessionControllerProvider.notifier);
  final signedIn = (await ref.read(sessionControllerProvider.future)) != null;

  await repository.confirmEmailVerification(token: token);
  ref.invalidate(interactionRequirementsProvider);

  if (signedIn) {
    try {
      await session.updateAccount(await repository.getAccount());
    } on Object {
      // The address is verified either way; a stale banner corrects itself the next
      // time the account is refreshed.
    }
  }
}

/// Runs the email and password recovery actions. Its state is the progress of the
/// action in flight; each method reports whether it succeeded.
@riverpod
class RecoveryController extends _$RecoveryController {
  @override
  FutureOr<void> build() {}

  Future<bool> resendVerificationEmail() async {
    // Read now: refreshing removes the resend button, and this provider along with it.
    final refresher = ref.read(accountRefresherProvider);
    final sent = await _run(() => ref.read(authRepositoryProvider).requestEmailVerification());
    if (!sent && ref.mounted) {
      final error = state.error;
      if (error is ApiException && error.code == _emailAlreadyVerifiedCode) {
        // Not awaited: the caller shows the error while the button is still there.
        unawaited(refresher.refresh());
      }
    }

    return sent;
  }

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
