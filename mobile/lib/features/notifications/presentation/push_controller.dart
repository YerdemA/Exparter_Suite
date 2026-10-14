import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';

import '../../../core/api/generated/export.dart';
import '../../../core/auth/session_controller.dart';
import '../../../core/router/app_router.dart';
import '../../../core/router/app_routes.dart';
import '../data/api_notification_repository.dart';
import '../data/firebase_push_client.dart';
import '../domain/push_client.dart';
import 'notification_controller.dart';
import 'notifications_screen.dart';

part 'push_controller.g.dart';

/// Whether the phone may show notifications; asking changes it.
@Riverpod(keepAlive: true)
class PushPermissionStatus extends _$PushPermissionStatus {
  @override
  Future<PushPermission> build() => ref.watch(pushClientProvider).permission();

  Future<PushPermission> request() async {
    final result = await ref.read(pushClientProvider).requestPermission();
    state = AsyncData(result);
    return result;
  }
}

/// Remembers that the explanation before the system dialog was shown, so that it is
/// shown once per installation and not on every launch.
abstract interface class PushPromptStore {
  Future<bool> shown();

  Future<void> markShown();
}

class SecurePushPromptStore implements PushPromptStore {
  const SecurePushPromptStore([this._storage = const FlutterSecureStorage()]);

  static const _key = 'push_prompt_shown';

  final FlutterSecureStorage _storage;

  @override
  Future<bool> shown() async => await _storage.read(key: _key) != null;

  @override
  Future<void> markShown() => _storage.write(key: _key, value: '1');
}

@Riverpod(keepAlive: true)
PushPromptStore pushPromptStore(Ref ref) => const SecurePushPromptStore();

/// True when the signed-in user should be told why notifications help, before the
/// system asks for the permission: the first time after signing in.
@riverpod
Future<bool> shouldExplainPush(Ref ref) async {
  if (await ref.watch(pushPermissionStatusProvider.future) != PushPermission.notDetermined) {
    return false;
  }

  return !await ref.watch(pushPromptStoreProvider).shown();
}

/// Tells the server where this phone's pushes go while someone is signed in with the
/// permission granted. The registration is removed when the user signs out.
@Riverpod(keepAlive: true)
class PushRegistration extends _$PushRegistration {
  String? _deviceId;

  @override
  void build() {
    final signedIn = ref.watch(sessionControllerProvider.select((session) => session.value != null));
    final granted = ref.watch(pushPermissionStatusProvider.select((status) => status.value == PushPermission.granted));
    if (!signedIn || !granted) {
      return;
    }

    final client = ref.watch(pushClientProvider);
    unawaited(_register(client.token()));
    final changes = client.tokenChanges.listen((token) => unawaited(_register(Future.value(token))));
    ref.onDispose(changes.cancel);
  }

  Future<void> _register(Future<String?> pending) async {
    try {
      final token = await pending;
      if (token == null || !ref.mounted) {
        return;
      }

      _deviceId = await ref.read(notificationRepositoryProvider).registerDevice(token);
    } on Object {
      // Without a registration the user still sees everything in the app; the next
      // launch tries again.
    }
  }

  /// Called before signing out, while the request can still be authorized.
  Future<void> unregister() async {
    final deviceId = _deviceId;
    _deviceId = null;
    if (deviceId == null) {
      return;
    }

    try {
      await ref.read(notificationRepositoryProvider).unregisterDevice(deviceId);
    } on Object {
      // The server forgets the device when another user signs in on it or FCM drops it.
    }
  }
}

/// Where tapping a push leads. A message opens its conversation; anything else opens
/// what the matching notification in the list would.
String pushTarget(Map<String, String> data) {
  final type = data['type'];
  if (type == 'MessageReceived') {
    final conversationId = data['conversationId'];
    return conversationId == null ? AppRoutes.messages : AppRoutes.conversation(conversationId);
  }

  return type == null ? AppRoutes.notifications : targetOf(NotificationType.fromJson(type), data['matchId']);
}

/// Opens the screen of a tapped push, and counts its notification as read. Watched by
/// the signed-in part of the app, so the launch tap is handled once someone is signed in.
@Riverpod(keepAlive: true)
void pushNavigation(Ref ref) {
  final client = ref.watch(pushClientProvider);

  void open(Map<String, String> data) {
    if (data['notificationId'] case final notificationId?) {
      unawaited(_markRead(ref, notificationId));
    }

    ref.read(appRouterProvider).go(pushTarget(data));
  }

  unawaited(
    client.launchTap().then((data) {
      if (data != null && ref.mounted) {
        open(data);
      }
    }),
  );
  final taps = client.taps.listen(open);
  ref.onDispose(taps.cancel);
}

Future<void> _markRead(Ref ref, String notificationId) async {
  try {
    await ref.read(notificationRepositoryProvider).markRead(notificationId);
  } on Object {
    return;
  }

  if (ref.mounted) {
    ref.invalidate(notificationsProvider);
  }
}
