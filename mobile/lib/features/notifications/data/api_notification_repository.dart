import 'package:riverpod_annotation/riverpod_annotation.dart';

import '../../../core/api/api_exception.dart';
import '../../../core/api/api_providers.dart';
import '../../../core/api/generated/export.dart';
import '../domain/notification_repository.dart';

part 'api_notification_repository.g.dart';

@Riverpod(keepAlive: true)
NotificationRepository notificationRepository(Ref ref) => ApiNotificationRepository(ref.watch(exparterApiProvider));

class ApiNotificationRepository implements NotificationRepository {
  const ApiNotificationRepository(this._api);

  /// The largest page the API serves; the screen shows the first page only.
  static const _pageSize = 50;

  final ExparterApi _api;

  @override
  Future<List<Notification>> list() => guardApiCall(
        () async => (await _api.notifications.listNotifications(pageSize: _pageSize)).items,
      );

  @override
  Future<void> markRead(String notificationId) =>
      guardApiCall(() => _api.notifications.markNotificationRead(notificationId: notificationId));

  @override
  Future<void> markAllRead() => guardApiCall(_api.notifications.markAllNotificationsRead);
}
