import 'package:firebase_core/firebase_core.dart';
import 'package:firebase_messaging/firebase_messaging.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';

import '../domain/push_client.dart';

part 'firebase_push_client.g.dart';

@Riverpod(keepAlive: true)
PushClient pushClient(Ref ref) => const FirebasePushClient();

/// Push through Firebase Cloud Messaging. Without an initialized Firebase app (a build
/// without google-services.json, or widget tests) it reports push as unavailable.
class FirebasePushClient implements PushClient {
  const FirebasePushClient();

  static bool get _available => Firebase.apps.isNotEmpty;

  @override
  Future<PushPermission> permission() async => _available
      ? _permissionOf(await FirebaseMessaging.instance.getNotificationSettings())
      : PushPermission.unavailable;

  @override
  Future<PushPermission> requestPermission() async =>
      _available ? _permissionOf(await FirebaseMessaging.instance.requestPermission()) : PushPermission.unavailable;

  @override
  Future<String?> token() async => _available ? FirebaseMessaging.instance.getToken() : null;

  @override
  Stream<String> get tokenChanges => _available ? FirebaseMessaging.instance.onTokenRefresh : const Stream.empty();

  @override
  Future<Map<String, String>?> launchTap() async {
    if (!_available) {
      return null;
    }

    final message = await FirebaseMessaging.instance.getInitialMessage();
    return message == null ? null : _dataOf(message);
  }

  @override
  Stream<Map<String, String>> get taps =>
      _available ? FirebaseMessaging.onMessageOpenedApp.map(_dataOf) : const Stream.empty();

  static PushPermission _permissionOf(NotificationSettings settings) => switch (settings.authorizationStatus) {
        AuthorizationStatus.authorized || AuthorizationStatus.provisional => PushPermission.granted,
        AuthorizationStatus.denied || AuthorizationStatus.deniedPermanently => PushPermission.denied,
        AuthorizationStatus.notDetermined => PushPermission.notDetermined,
      };

  static Map<String, String> _dataOf(RemoteMessage message) =>
      {for (final entry in message.data.entries) entry.key: '${entry.value}'};
}
