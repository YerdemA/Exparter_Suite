import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';
import 'package:signalr_netcore/signalr_client.dart';

import '../api/api_providers.dart';
import '../api/generated/export.dart';
import '../auth/session_controller.dart';
import '../auth/session_store.dart';
import '../config/app_config.dart';

part 'realtime_client.g.dart';

/// What the server pushes over the real-time connection (docs/realtime.md).
sealed class RealtimeEvent {
  const RealtimeEvent();
}

class MessageReceived extends RealtimeEvent {
  const MessageReceived(this.message);

  final Message message;
}

class ConversationRead extends RealtimeEvent {
  const ConversationRead({required this.conversationId, required this.userId, required this.readAt});

  final String conversationId;
  final String userId;
  final DateTime readAt;
}

class ConversationUpdated extends RealtimeEvent {
  const ConversationUpdated(this.conversation);

  final Conversation conversation;
}

/// The connection came back after a gap. Events may have been missed meanwhile, so
/// whatever is on screen should be reloaded.
class RealtimeReconnected extends RealtimeEvent {
  const RealtimeReconnected();
}

abstract interface class RealtimeClient {
  Stream<RealtimeEvent> get events;

  /// Connects and keeps trying to stay connected until [stop].
  Future<void> start();

  Future<void> stop();
}

@Riverpod(keepAlive: true)
RealtimeClient realtimeClient(Ref ref) {
  final client = SignalRRealtimeClient(ref.watch(sessionStoreProvider));
  ref.onDispose(client.dispose);
  return client;
}

/// A stream of the server's events, for providers to listen to.
@Riverpod(keepAlive: true)
Stream<RealtimeEvent> realtimeEvents(Ref ref) => ref.watch(realtimeClientProvider).events;

/// Keeps the connection open exactly while someone is signed in. The signed-in part of
/// the app watches this to switch it on.
@Riverpod(keepAlive: true)
void realtimeSession(Ref ref) {
  final signedIn = ref.watch(sessionControllerProvider.select((session) => session.value != null));
  final client = ref.watch(realtimeClientProvider);
  if (signedIn) {
    unawaited(client.start());
    ref.onDispose(() => unawaited(client.stop()));
  }
}

class SignalRRealtimeClient implements RealtimeClient {
  SignalRRealtimeClient(this._store);

  static const _retryDelay = Duration(seconds: 5);

  final SessionStore _store;
  final _events = StreamController<RealtimeEvent>.broadcast();
  HubConnection? _connection;
  Timer? _retry;
  bool _wanted = false;

  @override
  Stream<RealtimeEvent> get events => _events.stream;

  @override
  Future<void> start() async {
    _wanted = true;
    await _connect(announce: false);
  }

  @override
  Future<void> stop() async {
    _wanted = false;
    _retry?.cancel();
    final connection = _connection;
    _connection = null;
    await connection?.stop();
  }

  Future<void> dispose() async {
    await stop();
    await _events.close();
  }

  Future<void> _connect({required bool announce}) async {
    if (!_wanted || _connection != null) {
      return;
    }

    final connection = HubConnectionBuilder()
        .withUrl(
          '${AppConfig.apiBaseUrl}/hubs/realtime',
          // Read at every (re)connection, so a token refreshed by the HTTP layer is picked up.
          options: HttpConnectionOptions(accessTokenFactory: () async => _store.current?.accessToken ?? ''),
        )
        .withAutomaticReconnect()
        .build();
    connection
      ..on('MessageReceived', (arguments) => _emit(arguments, (json) => MessageReceived(Message.fromJson(json))))
      ..on(
        'ConversationRead',
        (arguments) => _emit(
          arguments,
          (json) => ConversationRead(
            conversationId: json['conversationId']! as String,
            userId: json['userId']! as String,
            readAt: DateTime.parse(json['readAt']! as String),
          ),
        ),
      )
      ..on(
        'ConversationUpdated',
        (arguments) => _emit(arguments, (json) => ConversationUpdated(Conversation.fromJson(json))),
      )
      ..onreconnected(({connectionId}) => _events.add(const RealtimeReconnected()))
      ..onclose(({error}) {
        // Automatic reconnection gave up (or the server refused an expired token).
        // Start over later; by then the HTTP layer has usually refreshed the token.
        if (identical(_connection, connection)) {
          _connection = null;
          _scheduleRetry();
        }
      });
    _connection = connection;
    try {
      await connection.start();
      if (announce) {
        _events.add(const RealtimeReconnected());
      }
    } on Object {
      if (identical(_connection, connection)) {
        _connection = null;
        _scheduleRetry();
      }
    }
  }

  void _scheduleRetry() {
    _retry?.cancel();
    if (_wanted) {
      _retry = Timer(_retryDelay, () => unawaited(_connect(announce: true)));
    }
  }

  void _emit(List<Object?>? arguments, RealtimeEvent Function(Map<String, Object?> json) parse) {
    final payload = arguments == null || arguments.isEmpty ? null : arguments.first;
    if (payload is! Map) {
      return;
    }

    try {
      _events.add(parse(payload.cast<String, Object?>()));
    } on Object {
      // A payload this version does not understand is skipped; the next reload shows the change.
    }
  }
}
