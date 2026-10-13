import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/api/error_messages.dart';
import '../../../core/api/generated/export.dart';
import '../../../core/auth/session_controller.dart';
import '../../../core/router/app_routes.dart';
import '../../../core/theme/app_tokens.dart';
import '../../../l10n/app_localizations.dart';
import '../../profile/presentation/widgets/profile_avatar.dart';
import 'match_controller.dart';
import 'match_labels.dart';

/// One match: who it is with, what is swapped, and how to end it.
class MatchDetailScreen extends ConsumerWidget {
  const MatchDetailScreen({required this.matchId, super.key});

  final String matchId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppLocalizations.of(context);
    final match = ref.watch(matchProvider(matchId));

    return Scaffold(
      appBar: AppBar(title: Text(l10n.matchTitle)),
      body: SafeArea(
        child: switch (match) {
          AsyncData(:final value) => _Content(match: value),
          AsyncError(:final error) => Center(
              child: Padding(
                padding: const EdgeInsets.all(AppSpacing.lg),
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Text(errorMessage(l10n, error), textAlign: TextAlign.center),
                    const SizedBox(height: AppSpacing.md),
                    OutlinedButton(
                      onPressed: () => ref.invalidate(matchProvider(matchId)),
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

class _Content extends ConsumerWidget {
  const _Content({required this.match});

  static const _avatarSize = 88.0;

  final Match match;

  Future<void> _run(BuildContext context, WidgetRef ref, Future<bool> Function(MatchActions actions) action) async {
    final l10n = AppLocalizations.of(context);
    final messenger = ScaffoldMessenger.of(context);
    if (!await action(ref.read(matchActionsProvider.notifier))) {
      messenger.showSnackBar(
        SnackBar(content: Text(errorMessage(l10n, ref.read(matchActionsProvider).error ?? Object()))),
      );
    }
  }

  Future<void> _requestEnd(BuildContext context, WidgetRef ref) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) {
        final l10n = AppLocalizations.of(dialogContext);
        return AlertDialog(
          title: Text(l10n.matchEndConfirmTitle),
          content: Text(l10n.matchEndConfirmBody),
          actions: [
            TextButton(onPressed: () => Navigator.pop(dialogContext, false), child: Text(l10n.actionCancel)),
            FilledButton(onPressed: () => Navigator.pop(dialogContext, true), child: Text(l10n.matchEndRequest)),
          ],
        );
      },
    );
    if (confirmed == true && context.mounted) {
      await _run(context, ref, (actions) => actions.requestEnd(match.id));
    }
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppLocalizations.of(context);
    final theme = Theme.of(context);
    final busy = ref.watch(matchActionsProvider).isLoading;
    final view = MatchView(match, ref.watch(sessionControllerProvider).value?.id);
    final otherName = userLabel(l10n, view.other);
    final deadline = match.endRequestExpiresAt;

    return Align(
      alignment: Alignment.topCenter,
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: AppSizes.formMaxWidth),
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(AppSpacing.lg),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Center(
                child: ProfileAvatar(
                  name: view.other.displayName ?? view.other.username,
                  imageUrl: view.other.avatarUrl,
                  size: _avatarSize,
                ),
              ),
              const SizedBox(height: AppSpacing.md),
              Text(otherName, textAlign: TextAlign.center, style: theme.textTheme.headlineSmall),
              Text(
                matchStatusLabel(l10n, match.status),
                textAlign: TextAlign.center,
                style: theme.textTheme.bodyLarge?.copyWith(color: theme.colorScheme.onSurfaceVariant),
              ),
              const SizedBox(height: AppSpacing.sm),
              Center(
                child: TextButton(
                  onPressed: () => context.go(AppRoutes.userProfile(view.other.userId)),
                  child: Text(l10n.matchViewProfile),
                ),
              ),
              const SizedBox(height: AppSpacing.md),
              ListTile(
                leading: const Icon(Icons.north_east),
                title: Text(l10n.swapYouTeach(view.taught.name)),
              ),
              ListTile(
                leading: const Icon(Icons.south_west),
                title: Text(l10n.swapYouLearn(view.learned.name)),
              ),
              const SizedBox(height: AppSpacing.md),
              if (match.conversationId case final conversationId?) ...[
                FilledButton.icon(
                  onPressed: () => context.go(AppRoutes.conversation(conversationId)),
                  icon: const Icon(Icons.chat_bubble_outline),
                  label: Text(l10n.matchSendMessage),
                ),
                const SizedBox(height: AppSpacing.md),
              ],
              if (busy) const LinearProgressIndicator(),
              ...switch (match.status) {
                MatchStatus.active => [
                    OutlinedButton(
                      onPressed: busy ? null : () => _requestEnd(context, ref),
                      child: Text(l10n.matchEndRequest),
                    ),
                  ],
                MatchStatus.endRequested when view.endRequestedByOther => [
                    Text(l10n.matchEndAskedByOther(otherName), textAlign: TextAlign.center),
                    if (deadline != null)
                      Text(
                        remainingLabel(l10n, deadline, DateTime.now()),
                        textAlign: TextAlign.center,
                        style: theme.textTheme.bodySmall,
                      ),
                    const SizedBox(height: AppSpacing.md),
                    FilledButton(
                      onPressed: busy ? null : () => _run(context, ref, (actions) => actions.acceptEnd(match.id)),
                      child: Text(l10n.matchEndAccept),
                    ),
                    const SizedBox(height: AppSpacing.sm),
                    OutlinedButton(
                      onPressed: busy ? null : () => _run(context, ref, (actions) => actions.rejectEnd(match.id)),
                      child: Text(l10n.matchEndReject),
                    ),
                  ],
                MatchStatus.endRequested => [
                    Text(l10n.matchEndWaitingOther, textAlign: TextAlign.center),
                    if (deadline != null)
                      Text(
                        remainingLabel(l10n, deadline, DateTime.now()),
                        textAlign: TextAlign.center,
                        style: theme.textTheme.bodySmall,
                      ),
                    const SizedBox(height: AppSpacing.md),
                    OutlinedButton(
                      onPressed:
                          busy ? null : () => _run(context, ref, (actions) => actions.withdrawEndRequest(match.id)),
                      child: Text(l10n.matchEndWithdraw),
                    ),
                  ],
                _ => [Text(matchEndReasonLabel(l10n, match.endReason), textAlign: TextAlign.center)],
              },
            ],
          ),
        ),
      ),
    );
  }
}
