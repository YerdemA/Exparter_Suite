import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/realtime/realtime_client.dart';
import '../../../l10n/app_localizations.dart';
import '../../messages/presentation/message_controller.dart';
import '../../notifications/presentation/notification_controller.dart';
import '../../notifications/presentation/push_controller.dart';
import '../../update/presentation/app_update_controller.dart';

Future<void> _explainPush(BuildContext context, WidgetRef ref) async {
  await ref.read(pushPromptStoreProvider).markShown();
  ref.invalidate(shouldExplainPushProvider);
  if (!context.mounted) {
    return;
  }

  final l10n = AppLocalizations.of(context);
  final allow = await showDialog<bool>(
    context: context,
    builder: (context) => AlertDialog(
      icon: const Icon(Icons.notifications_outlined),
      title: Text(l10n.pushPromptTitle),
      content: Text(l10n.pushPromptBody),
      actions: [
        TextButton(onPressed: () => Navigator.pop(context, false), child: Text(l10n.pushPromptLater)),
        FilledButton(onPressed: () => Navigator.pop(context, true), child: Text(l10n.pushPromptAllow)),
      ],
    ),
  );
  if (allow ?? false) {
    await ref.read(pushPermissionStatusProvider.notifier).request();
  }
}

/// The frame of the signed-in app: the current tab above a bottom navigation bar.
class AppShell extends ConsumerWidget {
  const AppShell({required this.navigationShell, super.key});

  final StatefulNavigationShell navigationShell;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppLocalizations.of(context);
    final unread = ref.watch(unreadMessageCountProvider);

    // Being here means someone is signed in: keep the real-time connection open.
    ref.watch(realtimeSessionProvider);

    // And keep what is on screen in step with what the server reports.
    ref.watch(notificationSyncProvider);

    // Pushes: register this phone, open what a tapped one is about, and the first
    // time, explain why they help before the system asks.
    ref
      ..watch(pushRegistrationProvider)
      ..watch(pushNavigationProvider)
      ..listen(shouldExplainPushProvider, (previous, next) {
        // Only on the change to true: rebuilds while it stays true must not ask again.
        if ((next.value ?? false) && !(previous?.value ?? false)) {
          unawaited(_explainPush(context, ref));
        }
      });

    // Testers seldom open the store, so newer versions are offered here.
    listenForAppUpdates(context, ref);

    return Scaffold(
      body: navigationShell,
      bottomNavigationBar: NavigationBar(
        selectedIndex: navigationShell.currentIndex,
        onDestinationSelected: (index) => navigationShell.goBranch(
          index,
          // Tapping the tab you are on returns to its first screen.
          initialLocation: index == navigationShell.currentIndex,
        ),
        destinations: [
          NavigationDestination(
            icon: const Icon(Icons.explore_outlined),
            selectedIcon: const Icon(Icons.explore),
            label: l10n.navDiscover,
          ),
          NavigationDestination(
            icon: const Icon(Icons.handshake_outlined),
            selectedIcon: const Icon(Icons.handshake),
            label: l10n.navMatches,
          ),
          NavigationDestination(
            icon: Badge(
              isLabelVisible: unread > 0,
              label: Text('$unread'),
              child: const Icon(Icons.chat_bubble_outline),
            ),
            selectedIcon: Badge(
              isLabelVisible: unread > 0,
              label: Text('$unread'),
              child: const Icon(Icons.chat_bubble),
            ),
            label: l10n.navMessages,
          ),
          NavigationDestination(
            icon: const Icon(Icons.person_outline),
            selectedIcon: const Icon(Icons.person),
            label: l10n.navProfile,
          ),
        ],
      ),
    );
  }
}
