import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';

import '../../../core/api/generated/export.dart';
import '../../../core/auth/session_controller.dart';
import '../../../core/realtime/realtime_client.dart';
import '../../../core/riverpod.dart';
import '../../matches/presentation/match_controller.dart';
import '../../profile/presentation/profile_controller.dart';
import '../../reviews/presentation/review_controller.dart';
import '../../sessions/presentation/session_controller.dart';
import '../data/api_notification_repository.dart';

part 'notification_controller.g.dart';

/// The signed-in user's notifications, newest first; empty when signed out. Reloads
/// when a new one arrives and after a gap in the connection.
@Riverpod(keepAlive: true, retry: noRetry)
class Notifications extends _$Notifications {
  @override
  Future<List<Notification>> build() async {
    final accountId = ref.watch(sessionControllerProvider.select((session) => session.value?.id));
    if (accountId == null) {
      return const [];
    }

    ref.listen(realtimeEventsProvider, (_, event) {
      if (event.value case NotificationReceived() || RealtimeReconnected()) {
        ref.invalidateSelf();
      }
    });
    return ref.watch(notificationRepositoryProvider).list();
  }

  /// Marks one as read at once, without waiting for the server; a failure is put right
  /// by the next reload.
  Future<void> markRead(String notificationId) async {
    final current = state.value;
    if (current == null || !current.any((item) => item.id == notificationId && item.readAt == null)) {
      return;
    }

    state = AsyncData([
      for (final item in current) item.id == notificationId ? item.copyWith(readAt: DateTime.now()) : item,
    ]);
    try {
      await ref.read(notificationRepositoryProvider).markRead(notificationId);
    } on Object {
      if (ref.mounted) {
        ref.invalidateSelf();
      }
    }
  }

  /// Returns whether it succeeded.
  Future<bool> markAllRead() async {
    try {
      await ref.read(notificationRepositoryProvider).markAllRead();
      return true;
    } on Object {
      return false;
    } finally {
      if (ref.mounted) {
        ref.invalidateSelf();
      }
    }
  }
}

/// Unread notifications, for the badge on the bell.
@Riverpod(keepAlive: true)
int unreadNotificationCount(Ref ref) =>
    (ref.watch(notificationsProvider).value ?? const <Notification>[]).where((item) => item.readAt == null).length;

/// A notification means something the user may be looking at has changed: a request
/// was answered, a session was proposed, a review arrived. While this is watched, what
/// is on screen is reloaded as notifications come in.
@Riverpod(keepAlive: true)
void notificationSync(Ref ref) {
  ref.listen(realtimeEventsProvider, (_, event) {
    if (event.value is! NotificationReceived) {
      return;
    }

    ref
      ..invalidate(pendingRequestsProvider)
      ..invalidate(matchesProvider)
      ..invalidate(matchProvider)
      ..invalidate(matchSessionsProvider)
      ..invalidate(userStatsProvider)
      ..invalidate(userReviewsProvider)
      ..invalidate(reviewEligibilityProvider)
      ..invalidate(ownProfileProvider);
  });
}
