import 'package:flutter/foundation.dart';
import 'package:in_app_update/in_app_update.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';

import '../domain/app_updater.dart';

part 'play_app_updater.g.dart';

@Riverpod(keepAlive: true)
AppUpdater appUpdater(Ref ref) => const PlayAppUpdater();

/// Google Play's in-app updates. Only Android has them; elsewhere nothing is offered.
class PlayAppUpdater implements AppUpdater {
  const PlayAppUpdater();

  bool get _supported => defaultTargetPlatform == TargetPlatform.android;

  @override
  Future<UpdateOffer> check() async {
    if (!_supported) {
      return UpdateOffer.none;
    }

    try {
      final info = await InAppUpdate.checkForUpdate();
      if (info.installStatus == InstallStatus.downloaded) {
        return UpdateOffer.downloaded;
      }

      return info.updateAvailability == UpdateAvailability.updateAvailable && info.flexibleUpdateAllowed
          ? UpdateOffer.available
          : UpdateOffer.none;
    } on Object {
      // Play is missing, signed out, or the app came from elsewhere (a debug build).
      return UpdateOffer.none;
    }
  }

  @override
  Future<bool> download() async {
    try {
      return await InAppUpdate.startFlexibleUpdate() == AppUpdateResult.success;
    } on Object {
      return false;
    }
  }

  @override
  Future<void> install() => InAppUpdate.completeFlexibleUpdate();
}
