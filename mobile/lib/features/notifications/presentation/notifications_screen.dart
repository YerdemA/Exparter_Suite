import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/api/error_messages.dart';
import '../../../core/api/generated/export.dart' hide Notification;
import '../../../core/api/generated/export.dart' as api show Notification;
import '../../../core/router/app_routes.dart';
import '../../../core/theme/app_tokens.dart';
import '../../../l10n/app_localizations.dart';
import '../../discovery/presentation/discovery_controller.dart';
import 'notification_controller.dart';

/// The bell that opens the notifications, with the number of unread ones.
class NotificationBell extends ConsumerWidget {
  const NotificationBell({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppLocalizations.of(context);
    final unread = ref.watch(unreadNotificationCountProvider);

    return IconButton(
      tooltip: l10n.notificationsTitle,
      onPressed: () => context.go(AppRoutes.notifications),
      icon: Badge(
        isLabelVisible: unread > 0,
        label: Text('$unread'),
        child: const Icon(Icons.notifications_outlined),
      ),
    );
  }
}

/// Where tapping a notification leads: the resource it is about.
String notificationTarget(api.Notification notification) {
  final data = notification.data;
  return switch (notification.type) {
    NotificationType.reviewReceived => AppRoutes.profile,
    NotificationType.skillProposalApproved || NotificationType.skillProposalRejected => AppRoutes.skillProposals,
    _ when data.matchId != null => AppRoutes.matchDetail(data.matchId!),
    _ => AppRoutes.matches,
  };
}

/// The sentence that tells the user what happened. [actorName] is who did it, when
/// someone did.
String notificationText(
  BuildContext context,
  AppLocalizations l10n,
  api.Notification notification,
  String? actorName,
) {
  final name = actorName ?? l10n.notificationSomeone;
  final data = notification.data;
  final byDeadline = data.actorUserId == null;
  final moment = data.deadline?.toLocal();
  final when = moment == null
      ? ''
      : '${MaterialLocalizations.of(context).formatMediumDate(moment)} ${TimeOfDay.fromDateTime(moment).format(context)}';

  return switch (notification.type) {
    NotificationType.swapRequestReceived => l10n.notificationSwapRequestReceived(name),
    NotificationType.swapRequestAccepted => l10n.notificationSwapRequestAccepted(name),
    NotificationType.swapRequestDeclined => l10n.notificationSwapRequestDeclined(name),
    NotificationType.swapRequestCancelled => l10n.notificationSwapRequestCancelled(name),
    NotificationType.swapRequestExpired => l10n.notificationSwapRequestExpired,
    NotificationType.matchEndRequested => l10n.notificationMatchEndRequested(name),
    NotificationType.matchEndRequestRejected => l10n.notificationMatchEndRequestRejected(name),
    NotificationType.matchEndRequestWithdrawn => l10n.notificationMatchEndRequestWithdrawn(name),
    NotificationType.matchEndReminder => l10n.notificationMatchEndReminder(name),
    NotificationType.matchEnded =>
      byDeadline ? l10n.notificationMatchEndedByDeadline : l10n.notificationMatchEnded(name),
    NotificationType.sessionProposed => l10n.notificationSessionProposed(name),
    NotificationType.sessionAccepted => l10n.notificationSessionAccepted(name),
    NotificationType.sessionDeclined =>
      byDeadline ? l10n.notificationSessionProposalLapsed : l10n.notificationSessionDeclined(name),
    NotificationType.sessionCancelled => data.reason == null
        ? l10n.notificationSessionCancelled(name)
        : l10n.notificationSessionCancelledBecause(name, data.reason!),
    NotificationType.sessionStartingSoon => l10n.notificationSessionStartingSoon(when),
    NotificationType.sessionCompletionMarked => l10n.notificationSessionCompletionMarked(name),
    NotificationType.sessionCompletionReminder => l10n.notificationSessionCompletionReminder,
    NotificationType.sessionCompleted =>
      byDeadline ? l10n.notificationSessionCompletedByDeadline : l10n.notificationSessionCompleted(name),
    NotificationType.reviewReceived => l10n.notificationReviewReceived(name),
    NotificationType.skillProposalApproved => l10n.notificationSkillProposalApproved,
    NotificationType.skillProposalRejected => l10n.notificationSkillProposalRejected,
    NotificationType.$unknown => l10n.notificationUnknown,
  };
}

IconData _iconOf(NotificationType type) => switch (type) {
      NotificationType.swapRequestReceived ||
      NotificationType.swapRequestAccepted ||
      NotificationType.swapRequestDeclined ||
      NotificationType.swapRequestCancelled ||
      NotificationType.swapRequestExpired =>
        Icons.swap_horiz,
      NotificationType.matchEndRequested ||
      NotificationType.matchEndRequestRejected ||
      NotificationType.matchEndRequestWithdrawn ||
      NotificationType.matchEndReminder ||
      NotificationType.matchEnded =>
        Icons.handshake_outlined,
      NotificationType.reviewReceived => Icons.star_outline,
      NotificationType.skillProposalApproved || NotificationType.skillProposalRejected => Icons.lightbulb_outline,
      _ => Icons.event_outlined,
    };

/// Everything that happened to the signed-in user lately, newest first.
class NotificationsScreen extends ConsumerWidget {
  const NotificationsScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppLocalizations.of(context);
    final notifications = ref.watch(notificationsProvider);
    final anyUnread = ref.watch(unreadNotificationCountProvider) > 0;

    return Scaffold(
      appBar: AppBar(
        title: Text(l10n.notificationsTitle),
        actions: [
          if (anyUnread)
            TextButton(
              onPressed: () async {
                final messenger = ScaffoldMessenger.of(context);
                if (!await ref.read(notificationsProvider.notifier).markAllRead()) {
                  messenger.showSnackBar(SnackBar(content: Text(l10n.errorUnknown)));
                }
              },
              child: Text(l10n.notificationsMarkAllRead),
            ),
        ],
      ),
      body: SafeArea(
        child: switch (notifications) {
          AsyncValue(:final value?) when value.isEmpty => Center(
              child: Padding(
                padding: const EdgeInsets.all(AppSpacing.lg),
                child: Text(l10n.notificationsEmpty, textAlign: TextAlign.center),
              ),
            ),
          AsyncValue(:final value?) => RefreshIndicator(
              onRefresh: () => ref.refresh(notificationsProvider.future),
              child: ListView.separated(
                itemCount: value.length,
                separatorBuilder: (context, index) => const Divider(height: 1),
                itemBuilder: (context, index) => _NotificationTile(notification: value[index]),
              ),
            ),
          AsyncError(:final error) => Center(
              child: Padding(
                padding: const EdgeInsets.all(AppSpacing.lg),
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Text(errorMessage(l10n, error), textAlign: TextAlign.center),
                    const SizedBox(height: AppSpacing.md),
                    OutlinedButton(
                      onPressed: () => ref.invalidate(notificationsProvider),
                      child: Text(l10n.errorRetry),
                    ),
                  ],
                ),
              ),
            ),
          _ => const Center(child: CircularProgressIndicator()),
        },
      ),
    );
  }
}

class _NotificationTile extends ConsumerWidget {
  const _NotificationTile({required this.notification});

  final api.Notification notification;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppLocalizations.of(context);
    final theme = Theme.of(context);
    final unread = notification.readAt == null;
    final actorId = notification.data.actorUserId;

    // The notification names the actor only by id; their name comes from their profile.
    final actor = actorId == null ? null : ref.watch(userProfileProvider(actorId)).value;
    final actorName = actor == null ? null : actor.displayName ?? '@${actor.username}';
    final created = notification.createdAt.toLocal();

    return ListTile(
      leading: Icon(_iconOf(notification.type), color: unread ? theme.colorScheme.primary : null),
      title: Text(
        notificationText(context, l10n, notification, actorName),
        style: unread ? theme.textTheme.bodyLarge?.copyWith(fontWeight: FontWeight.w600) : theme.textTheme.bodyLarge,
      ),
      subtitle: Text(
        '${MaterialLocalizations.of(context).formatMediumDate(created)} ${TimeOfDay.fromDateTime(created).format(context)}',
      ),
      trailing: unread ? Icon(Icons.circle, size: AppSpacing.sm, color: theme.colorScheme.primary) : null,
      onTap: () {
        ref.read(notificationsProvider.notifier).markRead(notification.id);
        context.go(notificationTarget(notification));
      },
    );
  }
}
