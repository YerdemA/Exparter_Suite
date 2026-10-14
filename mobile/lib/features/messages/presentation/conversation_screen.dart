import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/api/error_messages.dart';
import '../../../core/api/generated/export.dart';
import '../../../core/router/app_routes.dart';
import '../../../core/theme/app_tokens.dart';
import '../../../l10n/app_localizations.dart';
import '../../matches/presentation/match_labels.dart';
import '../../moderation/presentation/moderation_menu.dart';
import 'message_controller.dart';

/// One conversation: its messages, newest at the bottom, and the box to write in.
class ConversationScreen extends ConsumerStatefulWidget {
  const ConversationScreen({required this.conversationId, super.key});

  final String conversationId;

  @override
  ConsumerState<ConversationScreen> createState() => _ConversationScreenState();
}

class _ConversationScreenState extends ConsumerState<ConversationScreen> {
  /// How close to the oldest loaded message the next page starts loading.
  static const _loadMoreDistance = 400.0;

  final _input = TextEditingController();
  final _scroll = ScrollController();

  @override
  void initState() {
    super.initState();
    _scroll.addListener(() {
      // The list is reversed: its far end is the oldest message.
      if (_scroll.position.extentAfter < _loadMoreDistance) {
        ref.read(threadProvider(widget.conversationId).notifier).loadMore();
      }
    });
  }

  @override
  void dispose() {
    _input.dispose();
    _scroll.dispose();
    super.dispose();
  }

  void _send() {
    final text = _input.text;
    if (text.trim().isEmpty) {
      return;
    }

    _input.clear();
    ref.read(threadProvider(widget.conversationId).notifier).send(text);
  }

  Future<void> _close() async {
    final l10n = AppLocalizations.of(context);
    final messenger = ScaffoldMessenger.of(context);
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: Text(l10n.conversationCloseConfirmTitle),
        content: Text(l10n.conversationCloseConfirmBody),
        actions: [
          TextButton(onPressed: () => Navigator.pop(dialogContext, false), child: Text(l10n.actionCancel)),
          FilledButton(onPressed: () => Navigator.pop(dialogContext, true), child: Text(l10n.conversationClose)),
        ],
      ),
    );
    if (confirmed != true) {
      return;
    }

    if (!await ref.read(threadProvider(widget.conversationId).notifier).close()) {
      messenger.showSnackBar(SnackBar(content: Text(l10n.conversationCloseFailed)));
    }
  }

  /// Closing is offered only when it is allowed: no unfinished match, not closed already.
  static bool _canClose(Conversation conversation) => !conversation.isClosed && !conversation.hasActiveMatch;

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final thread = ref.watch(threadProvider(widget.conversationId));
    final conversation = thread.value?.conversation;

    return Scaffold(
      appBar: AppBar(
        title: conversation == null
            ? null
            : InkWell(
                onTap: () => context.go(AppRoutes.userProfile(conversation.otherUser.userId)),
                child: Text(userLabel(l10n, conversation.otherUser)),
              ),
        actions: [
          if (conversation != null && (!conversation.otherUser.isDeleted || _canClose(conversation)))
            PopupMenuButton<void>(
              itemBuilder: (menuContext) => [
                if (_canClose(conversation)) PopupMenuItem(onTap: _close, child: Text(l10n.conversationClose)),
                if (!conversation.otherUser.isDeleted)
                  ...userModerationItems(
                    context,
                    ref,
                    userId: conversation.otherUser.userId,
                    name: userLabel(l10n, conversation.otherUser),
                    onBlocked: () => context.go(AppRoutes.messages),
                  ),
              ],
            ),
        ],
      ),
      body: SafeArea(
        child: switch (thread) {
          AsyncValue(:final value?) => Column(
              children: [
                Expanded(child: _MessageList(state: value, controller: _scroll, conversationId: widget.conversationId)),
                if (value.conversation.isClosed)
                  Padding(
                    padding: const EdgeInsets.all(AppSpacing.md),
                    child: Text(l10n.conversationClosedBanner, textAlign: TextAlign.center),
                  )
                else
                  Padding(
                    padding: const EdgeInsets.fromLTRB(AppSpacing.md, AppSpacing.sm, AppSpacing.sm, AppSpacing.sm),
                    child: Row(
                      crossAxisAlignment: CrossAxisAlignment.end,
                      children: [
                        Expanded(
                          child: TextField(
                            controller: _input,
                            minLines: 1,
                            maxLines: 4,
                            maxLength: 2000,
                            textCapitalization: TextCapitalization.sentences,
                            decoration: InputDecoration(hintText: l10n.messageHint, counterText: ''),
                          ),
                        ),
                        const SizedBox(width: AppSpacing.xs),
                        IconButton.filled(onPressed: _send, tooltip: l10n.messageSend, icon: const Icon(Icons.send)),
                      ],
                    ),
                  ),
              ],
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
                      onPressed: () => ref.invalidate(threadProvider(widget.conversationId)),
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

class _MessageList extends ConsumerWidget {
  const _MessageList({required this.state, required this.controller, required this.conversationId});

  final ThreadState state;
  final ScrollController controller;
  final String conversationId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppLocalizations.of(context);
    final otherId = state.conversation.otherUser.userId;
    final seenAt = state.conversation.otherUserLastReadAt;

    // "Seen" goes under the newest of the user's own messages, and only if the other
    // user has read up to it.
    final newestOwn = state.messages.where((message) => message.senderId != otherId).firstOrNull;
    final seenMessageId =
        newestOwn != null && seenAt != null && !seenAt.isBefore(newestOwn.sentAt) ? newestOwn.id : null;

    if (state.messages.isEmpty && state.outgoing.isEmpty) {
      return Center(
        child: Padding(
          padding: const EdgeInsets.all(AppSpacing.xl),
          child: Text(l10n.conversationNoMessages, textAlign: TextAlign.center),
        ),
      );
    }

    // Reversed, so index 0 is at the bottom: what is still being sent, then stored
    // messages from newest to oldest.
    final pending = state.outgoing.reversed.toList();
    return ListView.builder(
      controller: controller,
      reverse: true,
      padding: const EdgeInsets.all(AppSpacing.md),
      itemCount: pending.length + state.messages.length + (state.loadingMore ? 1 : 0),
      itemBuilder: (context, index) {
        if (index < pending.length) {
          final outgoing = pending[index];
          return _Bubble(
            body: outgoing.body,
            own: true,
            footer: outgoing.failed ? l10n.messageFailed : l10n.messageSending,
            failed: outgoing.failed,
            onTap: outgoing.failed
                ? () => ref.read(threadProvider(conversationId).notifier).retry(outgoing.clientMessageId)
                : null,
          );
        }

        final messageIndex = index - pending.length;
        if (messageIndex >= state.messages.length) {
          return const Padding(
            padding: EdgeInsets.all(AppSpacing.md),
            child: Center(child: CircularProgressIndicator()),
          );
        }

        final message = state.messages[messageIndex];
        final time = TimeOfDay.fromDateTime(message.sentAt.toLocal()).format(context);
        final own = message.senderId != otherId;
        return _Bubble(
          body: message.body,
          own: own,
          footer: message.id == seenMessageId ? '$time · ${l10n.messageSeen}' : time,
          // Only what the other user wrote can be reported.
          onLongPress: own
              ? null
              : () => reportContent(context, ref, targetType: ReportTargetType.message, targetId: message.id),
        );
      },
    );
  }
}

class _Bubble extends StatelessWidget {
  const _Bubble({
    required this.body,
    required this.own,
    required this.footer,
    this.failed = false,
    this.onTap,
    this.onLongPress,
  });

  /// Bubbles stop short of the far edge so it stays clear whose side they are on.
  static const _widthFactor = 0.8;

  final String body;
  final bool own;
  final String footer;
  final bool failed;
  final VoidCallback? onTap;
  final VoidCallback? onLongPress;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colors = theme.colorScheme;
    final background = own ? colors.primaryContainer : colors.surfaceContainerHighest;
    final foreground = own ? colors.onPrimaryContainer : colors.onSurface;

    return Align(
      alignment: own ? Alignment.centerRight : Alignment.centerLeft,
      child: FractionallySizedBox(
        widthFactor: _widthFactor,
        alignment: own ? Alignment.centerRight : Alignment.centerLeft,
        child: Align(
          alignment: own ? Alignment.centerRight : Alignment.centerLeft,
          child: Padding(
            padding: const EdgeInsets.only(bottom: AppSpacing.sm),
            child: Column(
              crossAxisAlignment: own ? CrossAxisAlignment.end : CrossAxisAlignment.start,
              children: [
                Material(
                  color: background,
                  borderRadius: BorderRadius.circular(AppRadius.md),
                  clipBehavior: Clip.antiAlias,
                  child: InkWell(
                    onTap: onTap,
                    onLongPress: onLongPress,
                    child: Padding(
                      padding: const EdgeInsets.symmetric(horizontal: AppSpacing.md, vertical: AppSpacing.sm),
                      child: Text(body, style: theme.textTheme.bodyLarge?.copyWith(color: foreground)),
                    ),
                  ),
                ),
                const SizedBox(height: AppSpacing.xs),
                Text(
                  footer,
                  style: theme.textTheme.bodySmall?.copyWith(color: failed ? colors.error : colors.onSurfaceVariant),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
