import 'package:exparter/features/update/data/play_app_updater.dart';
import 'package:exparter/features/update/domain/app_updater.dart';
import 'package:exparter/features/update/presentation/app_update_controller.dart';
import 'package:exparter/l10n/app_localizations.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

class FakeUpdater implements AppUpdater {
  FakeUpdater(this.offer, {this.downloads = true});

  UpdateOffer offer;
  bool downloads;
  final log = <String>[];

  @override
  Future<UpdateOffer> check() async {
    log.add('check');
    return offer;
  }

  @override
  Future<bool> download() async {
    log.add('download');
    if (downloads) {
      offer = UpdateOffer.downloaded;
    }

    return downloads;
  }

  @override
  Future<void> install() async => log.add('install');
}

void main() {
  late AppLocalizations l10n;

  setUpAll(() async {
    l10n = await AppLocalizations.delegate.load(const Locale('tr'));
  });

  ProviderContainer containerWith(FakeUpdater updater) {
    final container = ProviderContainer(overrides: [appUpdaterProvider.overrideWithValue(updater)]);
    addTearDown(container.dispose);
    return container;
  }

  group('AppUpdateController', () {
    test('downloads an available version after the store prompt and then offers to install it', () async {
      final updater = FakeUpdater(UpdateOffer.available);
      final container = containerWith(updater);

      await container.read(appUpdateControllerProvider.notifier).check();

      expect(updater.log, ['check', 'download']);
      expect(container.read(appUpdateControllerProvider), 1);
    });

    test('asks only once per run when the user declines the store prompt', () async {
      final updater = FakeUpdater(UpdateOffer.available, downloads: false);
      final container = containerWith(updater);
      final controller = container.read(appUpdateControllerProvider.notifier);

      await controller.check();
      await controller.check();

      expect(updater.log, ['check', 'download', 'check']);
      expect(container.read(appUpdateControllerProvider), 0);
    });

    test('offers a version downloaded earlier again each time the app comes back', () async {
      final updater = FakeUpdater(UpdateOffer.downloaded);
      final container = containerWith(updater);
      final controller = container.read(appUpdateControllerProvider.notifier);

      await controller.check();
      await controller.check();

      expect(updater.log, ['check', 'check']);
      expect(container.read(appUpdateControllerProvider), 2);
    });

    test('does nothing when there is no newer version or the store cannot be asked', () async {
      final updater = FakeUpdater(UpdateOffer.none);
      final container = containerWith(updater);

      await container.read(appUpdateControllerProvider.notifier).check();

      expect(updater.log, ['check']);
      expect(container.read(appUpdateControllerProvider), 0);
    });

    test('a check that starts while another runs is skipped', () async {
      final updater = FakeUpdater(UpdateOffer.available);
      final container = containerWith(updater);
      final controller = container.read(appUpdateControllerProvider.notifier);

      await Future.wait([controller.check(), controller.check()]);

      expect(updater.log, ['check', 'download']);
    });
  });

  testWidgets('says when the new version is downloaded and restarts into it', (tester) async {
    final updater = FakeUpdater(UpdateOffer.available);
    await tester.pumpWidget(
      ProviderScope(
        overrides: [appUpdaterProvider.overrideWithValue(updater)],
        child: MaterialApp(
          localizationsDelegates: AppLocalizations.localizationsDelegates,
          supportedLocales: AppLocalizations.supportedLocales,
          home: Consumer(
            builder: (context, ref, _) {
              listenForAppUpdates(context, ref);
              return const Scaffold(body: SizedBox.shrink());
            },
          ),
        ),
      ),
    );
    await tester.pump();
    await tester.pump(const Duration(seconds: 1));

    expect(find.text(l10n.updateReady), findsOneWidget);

    await tester.tap(find.text(l10n.updateRestart));
    await tester.pump();

    expect(updater.log, ['check', 'download', 'install']);
  });
}
