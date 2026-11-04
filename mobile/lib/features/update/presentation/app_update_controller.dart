import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';

import '../../../l10n/app_localizations.dart';
import '../data/play_app_updater.dart';
import '../domain/app_updater.dart';

part 'app_update_controller.g.dart';

/// Offers newer versions from the store. The state counts how many times a downloaded
/// version was found waiting to be installed, so that each time is shown again.
@Riverpod(keepAlive: true)
class AppUpdateController extends _$AppUpdateController {
  /// The store's prompt is shown once per run of the app: someone who said no is not
  /// asked again each time they come back to it.
  bool _asked = false;
  bool _checking = false;

  @override
  int build() => 0;

  Future<void> check() async {
    if (_checking) {
      return;
    }

    _checking = true;
    try {
      final updater = ref.read(appUpdaterProvider);
      switch (await updater.check()) {
        case UpdateOffer.downloaded:
          state++;
        case UpdateOffer.available when !_asked:
          _asked = true;
          if (await updater.download() && ref.mounted) {
            state++;
          }
        case UpdateOffer.available || UpdateOffer.none:
          break;
      }
    } finally {
      _checking = false;
    }
  }

  Future<void> install() => ref.read(appUpdaterProvider).install();
}

/// Looks for a newer version when the signed-in app opens and whenever it comes back.
@riverpod
void appUpdateSession(Ref ref) {
  final controller = ref.watch(appUpdateControllerProvider.notifier);
  unawaited(controller.check());
  final lifecycle = AppLifecycleListener(onResume: () => unawaited(controller.check()));
  ref.onDispose(lifecycle.dispose);
}

/// Looks for a newer version while the calling widget is shown, says when one is
/// downloaded and lets the user restart into it. Called from the signed-in app's frame.
void listenForAppUpdates(BuildContext context, WidgetRef ref) {
  ref
    ..watch(appUpdateSessionProvider)
    ..listen(appUpdateControllerProvider, (previous, next) {
      if (next <= (previous ?? 0)) {
        return;
      }

      final l10n = AppLocalizations.of(context);
      ScaffoldMessenger.of(context)
        ..hideCurrentSnackBar()
        ..showSnackBar(
          SnackBar(
            content: Text(l10n.updateReady),
            // Stays until it is used or dismissed; the next return to the app shows it again.
            duration: const Duration(days: 1),
            showCloseIcon: true,
            action: SnackBarAction(
              label: l10n.updateRestart,
              onPressed: () => unawaited(ref.read(appUpdateControllerProvider.notifier).install()),
            ),
          ),
        );
    });
}
