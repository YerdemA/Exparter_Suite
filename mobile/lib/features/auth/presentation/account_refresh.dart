import 'dart:async';

import 'package:flutter/widgets.dart' show AppLifecycleListener;
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';

import '../../../core/auth/interaction_requirements.dart';
import '../../../core/auth/session_controller.dart';
import '../data/api_auth_repository.dart';

part 'account_refresh.g.dart';

/// Brings the cached account up to date with the server.
///
/// The account can change away from the app: an email link opened in a browser or on
/// another device verifies the address without the app knowing.
@Riverpod(keepAlive: true)
AccountRefresher accountRefresher(Ref ref) => AccountRefresher(ref);

class AccountRefresher {
  const AccountRefresher(this._ref);

  final Ref _ref;

  Future<void> refresh() async {
    try {
      final account = await _ref.read(authRepositoryProvider).getAccount();
      // The router re-evaluates on every session change, so stay quiet when nothing
      // changed, which is nearly every time.
      final current = await _ref.read(sessionControllerProvider.future);
      if (current == null || current == account) {
        return;
      }

      await _ref.read(sessionControllerProvider.notifier).updateAccount(account);
      _ref.invalidate(interactionRequirementsProvider);
    } on Object {
      // The next return to the app tries again; until then the cached account stands.
    }
  }
}

/// Refreshes the account when someone is signed in, and whenever the app comes back.
@Riverpod(keepAlive: true)
void accountRefreshSession(Ref ref) {
  final signedIn = ref.watch(sessionControllerProvider.select((session) => session.value != null));
  if (!signedIn) {
    return;
  }

  final refresher = ref.watch(accountRefresherProvider);
  unawaited(refresher.refresh());
  final lifecycle = AppLifecycleListener(onResume: () => unawaited(refresher.refresh()));
  ref.onDispose(lifecycle.dispose);
}
