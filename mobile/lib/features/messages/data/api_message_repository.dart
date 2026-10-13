import 'package:riverpod_annotation/riverpod_annotation.dart';

import '../../../core/api/api_exception.dart';
import '../../../core/api/api_providers.dart';
import '../../../core/api/generated/export.dart';
import '../domain/message_repository.dart';

part 'api_message_repository.g.dart';

@Riverpod(keepAlive: true)
MessageRepository messageRepository(Ref ref) => ApiMessageRepository(ref.watch(exparterApiProvider));

class ApiMessageRepository implements MessageRepository {
  const ApiMessageRepository(this._api);

  /// The largest page the API serves; the list shows the first page only.
  static const _conversationPageSize = 50;
  static const _messagePageSize = 30;

  final ExparterApi _api;

  @override
  Future<List<Conversation>> listConversations() => guardApiCall(
        () async => (await _api.conversations.listConversations(pageSize: _conversationPageSize)).items,
      );

  @override
  Future<Conversation> getConversation(String conversationId) =>
      guardApiCall(() => _api.conversations.getConversation(conversationId: conversationId));

  @override
  Future<MessageCursorPage> listMessages(String conversationId, {String? cursor}) => guardApiCall(
        () => _api.conversations.listMessages(
          conversationId: conversationId,
          cursor: cursor,
          limit: _messagePageSize,
        ),
      );

  @override
  Future<Message> sendMessage(String conversationId, {required String body, required String clientMessageId}) =>
      guardApiCall(
        () => _api.conversations.sendMessage(
          conversationId: conversationId,
          body: SendMessageRequest(body: body, clientMessageId: clientMessageId),
        ),
      );

  @override
  Future<void> markRead(String conversationId) =>
      guardApiCall(() => _api.conversations.markConversationRead(conversationId: conversationId));

  @override
  Future<Conversation> closeConversation(String conversationId) =>
      guardApiCall(() => _api.conversations.closeConversation(conversationId: conversationId));
}
