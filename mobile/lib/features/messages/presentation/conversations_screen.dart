import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/api/error_messages.dart';
import '../../../core/api/generated/export.dart';
import '../../../core/router/app_routes.dart';
import '../../../core/theme/app_tokens.dart';
import '../../../l10n/app_localizations.dart';
import '../../matches/presentation/match_labels.dart';
import '../../profile/presentation/widgets/profile_avatar.dart';
import 'message_controller.dart';

/// The user's conversations, most recently active first.
class ConversationsScreen extends ConsumerWidget {
  const ConversationsScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppLocalizations.of(context);
    final conversations = ref.watch(conversationsProvider);

    return Scaffold(
      appBar: AppBar(title: Text(l10n.navMessages)),
      body: SafeArea(
        child: switch (conversations) {
          AsyncValue(:final value?) => RefreshIndicator(
              onRefresh: () => ref.refresh(conversationsProvider.future),
              child: ListView(
                physics: const AlwaysScrollableScrollPhysics(),
                children: [
                  if (value.isEmpty)
                    Padding(
                      padding: const EdgeInsets.all(AppSpacing.xl),
                      child: Text(l10n.messagesEmpty, textAlign: TextAlign.center),
                    ),
                  for (final conversation in value) _ConversationTile(conversation: conversation),
                ],
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
                      onPressed: () => ref.invalidate(conversationsProvider),
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

class _ConversationTile extends StatelessWidget {
  const _ConversationTile({required this.conversation});

  static const _avatarSize = 48.0;

  final Conversation conversation;

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final theme = Theme.of(context);
    final other = conversation.otherUser;
    final last = conversation.lastMessage;
    final unread = conversation.unreadCount;
    final preview = last == null
        ? l10n.conversationNoMessages
        : last.senderId == other.userId
            ? last.body
            : l10n.messagesYouPrefix(last.body);

    return ListTile(
      leading: ProfileAvatar(name: other.displayName ?? other.username, imageUrl: other.avatarUrl, size: _avatarSize),
      title: Text(userLabel(l10n, other), maxLines: 1, overflow: TextOverflow.ellipsis),
      subtitle: Text(
        preview,
        maxLines: 1,
        overflow: TextOverflow.ellipsis,
        style: unread > 0 ? theme.textTheme.bodyMedium?.copyWith(fontWeight: FontWeight.w700) : null,
      ),
      trailing: unread > 0
          ? Badge(label: Text('$unread'))
          : conversation.isClosed
              ? Icon(Icons.lock_outline, semanticLabel: l10n.conversationClosedLabel)
              : null,
      onTap: () => context.go(AppRoutes.conversation(conversation.id)),
    );
  }
}
