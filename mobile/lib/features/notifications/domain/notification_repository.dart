import '../../../core/api/generated/export.dart';

/// The signed-in user's notifications. Failures are thrown as `ApiException`.
abstract interface class NotificationRepository {
  /// Newest first.
  Future<List<Notification>> list();

  Future<void> markRead(String notificationId);

  Future<void> markAllRead();
}
