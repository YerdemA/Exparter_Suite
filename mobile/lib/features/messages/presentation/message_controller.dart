import 'dart:math';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';

import '../../../core/api/generated/export.dart';
import '../../../core/auth/session_controller.dart';
import '../../../core/realtime/realtime_client.dart';
import '../../../core/riverpod.dart';
import '../data/api_message_repository.dart';

part 'message_controller.g.dart';

/// The signed-in user's conversations; empty when signed out. Reloads whenever the
/// server reports a change to any of them.
@Riverpod(keepAlive: true, retry: noRetry)
class Conversations extends _$Conversations {
  @override
  Future<List<Conversation>> build() async {
    final accountId = ref.watch(sessionControllerProvider.select((session) => session.value?.id));
    if (accountId == null) {
      return const [];
    }

    // Any event can change the order, a preview or an unread count; reloading the one
    // page is simpler and safer than patching it.
    ref.listen(realtimeEventsProvider, (_, event) {
      if (event.hasValue) {
        ref.invalidateSelf();
      }
    });
    return ref.watch(messageRepositoryProvider).listConversations();
  }
}

/// Unread messages across all conversations, for the badge on the Messages tab.
@Riverpod(keepAlive: true)
int unreadMessageCount(Ref ref) =>
    (ref.watch(conversationsProvider).value ?? const <Conversation>[])
        .fold(0, (total, conversation) => total + conversation.unreadCount);

/// A message the user sent that the server has not confirmed yet.
class OutgoingMessage {
  const OutgoingMessage({required this.clientMessageId, required this.body, this.failed = false});

  final String clientMessageId;
  final String body;

  /// The send was refused or did not get through; the user can try again.
  final bool failed;
}

/// One open conversation: its messages so far and what is still on its way out.
class ThreadState {
  const ThreadState({
    required this.conversation,
    required this.messages,
    required this.nextCursor,
    this.outgoing = const [],
    this.loadingMore = false,
  });

  final Conversation conversation;

  /// Newest first.
  final List<Message> messages;

  /// Null once the oldest message has been loaded.
  final String? nextCursor;

  /// Oldest first.
  final List<OutgoingMessage> outgoing;
  final bool loadingMore;

  ThreadState copyWith({
    Conversation? conversation,
    List<Message>? messages,
    List<OutgoingMessage>? outgoing,
    bool? loadingMore,
  }) =>
      ThreadState(
        conversation: conversation ?? this.conversation,
        messages: messages ?? this.messages,
        nextCursor: nextCursor,
        outgoing: outgoing ?? this.outgoing,
        loadingMore: loadingMore ?? this.loadingMore,
      );
}

@Riverpod(retry: noRetry)
class Thread extends _$Thread {
  @override
  Future<ThreadState> build(String conversationId) async {
    final repository = ref.watch(messageRepositoryProvider);
    ref.listen(realtimeEventsProvider, (_, event) {
      if (event.value case final value?) {
        _onEvent(value);
      }
    });

    final conversation = await repository.getConversation(conversationId);
    final page = await repository.listMessages(conversationId);
    if (conversation.unreadCount > 0) {
      _markRead();
    }

    return ThreadState(conversation: conversation, messages: page.items, nextCursor: page.nextCursor);
  }

  /// Loads the next older page. Does nothing if there is none or one is already loading.
  Future<void> loadMore() async {
    final current = state.value;
    if (current == null || current.nextCursor == null || current.loadingMore) {
      return;
    }

    state = AsyncData(current.copyWith(loadingMore: true));
    try {
      final page = await ref.read(messageRepositoryProvider).listMessages(conversationId, cursor: current.nextCursor);
      final latest = state.value;
      if (!ref.mounted || latest == null) {
        return;
      }

      final known = {for (final message in latest.messages) message.id};
      state = AsyncData(
        ThreadState(
          conversation: latest.conversation,
          messages: [...latest.messages, ...page.items.where((message) => !known.contains(message.id))],
          nextCursor: page.nextCursor,
          outgoing: latest.outgoing,
        ),
      );
    } on Object {
      // Scrolling further tries again; what is loaded stays.
      if (ref.mounted && state.value != null) {
        state = AsyncData(state.value!.copyWith(loadingMore: false));
      }
    }
  }

  /// Shows the message at once and sends it. A failed send stays on screen, marked,
  /// until [retry] gets it through.
  Future<void> send(String body) async {
    final text = body.trim();
    final current = state.value;
    if (text.isEmpty || current == null) {
      return;
    }

    final outgoing = OutgoingMessage(clientMessageId: _newClientMessageId(), body: text);
    state = AsyncData(current.copyWith(outgoing: [...current.outgoing, outgoing]));
    await _deliver(outgoing);
  }

  Future<void> retry(String clientMessageId) async {
    final current = state.value;
    final failed = current?.outgoing.where((item) => item.clientMessageId == clientMessageId).firstOrNull;
    if (current == null || failed == null) {
      return;
    }

    final again = OutgoingMessage(clientMessageId: failed.clientMessageId, body: failed.body);
    state = AsyncData(
      current.copyWith(
        outgoing: [for (final item in current.outgoing) item.clientMessageId == clientMessageId ? again : item],
      ),
    );
    await _deliver(again);
  }

  /// Returns whether the conversation was closed.
  Future<bool> close() async {
    try {
      final closed = await ref.read(messageRepositoryProvider).closeConversation(conversationId);
      if (ref.mounted && state.value != null) {
        state = AsyncData(state.value!.copyWith(conversation: closed));
        ref.invalidate(conversationsProvider);
      }

      return true;
    } on Object {
      return false;
    }
  }

  Future<void> _deliver(OutgoingMessage outgoing) async {
    try {
      // The same id on every attempt: if an earlier attempt did arrive, the server
      // answers with that message instead of storing a second one.
      final message = await ref
          .read(messageRepositoryProvider)
          .sendMessage(conversationId, body: outgoing.body, clientMessageId: outgoing.clientMessageId);
      if (ref.mounted) {
        _confirm(message);
        ref.invalidate(conversationsProvider);
      }
    } on Object {
      final current = state.value;
      if (ref.mounted && current != null) {
        state = AsyncData(
          current.copyWith(
            outgoing: [
              for (final item in current.outgoing)
                item.clientMessageId == outgoing.clientMessageId
                    ? OutgoingMessage(clientMessageId: item.clientMessageId, body: item.body, failed: true)
                    : item,
            ],
          ),
        );
      }
    }
  }

  /// Adds a stored message, wherever it came from (the send's own answer or a
  /// real-time event), exactly once, and drops the pending copy it replaces.
  void _confirm(Message message) {
    final current = state.value;
    if (current == null || message.conversationId != conversationId) {
      return;
    }

    state = AsyncData(
      current.copyWith(
        messages: current.messages.any((item) => item.id == message.id)
            ? current.messages
            : [message, ...current.messages],
        outgoing: [
          for (final item in current.outgoing)
            if (item.clientMessageId != message.clientMessageId) item,
        ],
      ),
    );
  }

  void _onEvent(RealtimeEvent event) {
    final current = state.value;
    if (current == null) {
      return;
    }

    switch (event) {
      case MessageReceived(:final message) when message.conversationId == conversationId:
        _confirm(message);
        if (message.senderId == current.conversation.otherUser.userId) {
          // The conversation is on screen, so the user is reading as it arrives.
          _markRead();
        }
      case ConversationRead(conversationId: final id, :final userId, :final readAt)
          when id == conversationId && userId == current.conversation.otherUser.userId:
        state = AsyncData(
          current.copyWith(conversation: current.conversation.copyWith(otherUserLastReadAt: readAt)),
        );
      case ConversationUpdated(:final conversation) when conversation.id == conversationId:
        state = AsyncData(current.copyWith(conversation: conversation));
      case RealtimeReconnected():
        ref.invalidateSelf();
      default:
    }
  }

  void _markRead() {
    ref.read(messageRepositoryProvider).markRead(conversationId).then(
      (_) {
        if (ref.mounted) {
          ref.invalidate(conversationsProvider);
        }
      },
      // Not being able to mark as read only leaves the badge up until the next visit.
      onError: (Object _) {},
    );
  }

  static final _random = Random.secure();

  /// A random (version 4) UUID.
  static String _newClientMessageId() {
    final bytes = List<int>.generate(16, (_) => _random.nextInt(256));
    bytes[6] = (bytes[6] & 0x0f) | 0x40;
    bytes[8] = (bytes[8] & 0x3f) | 0x80;
    final hex = bytes.map((byte) => byte.toRadixString(16).padLeft(2, '0')).join();
    return '${hex.substring(0, 8)}-${hex.substring(8, 12)}-${hex.substring(12, 16)}-'
        '${hex.substring(16, 20)}-${hex.substring(20)}';
  }
}
