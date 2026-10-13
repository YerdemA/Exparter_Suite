import '../../../core/api/generated/export.dart';

/// Conversations and their messages. Failures are thrown as `ApiException`.
abstract interface class MessageRepository {
  /// The signed-in user's conversations, most recently active first.
  Future<List<Conversation>> listConversations();

  Future<Conversation> getConversation(String conversationId);

  /// One page of messages, newest first; pass the previous page's cursor for older ones.
  Future<MessageCursorPage> listMessages(String conversationId, {String? cursor});

  /// Sending again with the same [clientMessageId] returns the message already stored.
  Future<Message> sendMessage(String conversationId, {required String body, required String clientMessageId});

  Future<void> markRead(String conversationId);

  Future<Conversation> closeConversation(String conversationId);
}
