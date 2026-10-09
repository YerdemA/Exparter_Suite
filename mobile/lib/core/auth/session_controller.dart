import 'package:riverpod_annotation/riverpod_annotation.dart';

import '../api/api_providers.dart';
import '../api/generated/export.dart';
import 'session_store.dart';

part 'session_controller.g.dart';

/// The signed-in account, or null when signed out. Loading while the stored session
/// is being read at startup.
@Riverpod(keepAlive: true)
class SessionController extends _$SessionController {
  @override
  Future<Account?> build() async => (await ref.watch(sessionStoreProvider).load())?.account;

  /// True right after the session ended because the server rejected the refresh token.
  /// The sign-in screen reads and clears it to explain why the user is back there.
  bool expired = false;

  Future<void> start(AuthResponse response) async {
    expired = false;
    await ref.read(sessionStoreProvider).save(
          StoredSession(
            accessToken: response.accessToken,
            refreshToken: response.refreshToken,
            account: response.account,
          ),
        );
    state = AsyncData(response.account);
  }

  /// Replaces the cached account after the server-side account changed, for example
  /// when the email was verified. Does nothing when signed out.
  Future<void> updateAccount(Account account) async {
    final store = ref.read(sessionStoreProvider);
    final session = store.current;
    if (session == null) {
      return;
    }

    await store.save(session.copyWith(account: account));
    state = AsyncData(account);
  }

  Future<void> end() async {
    await ref.read(sessionStoreProvider).clear();
    state = const AsyncData(null);
  }

  Future<void> expire() async {
    expired = true;
    await end();
  }
}
