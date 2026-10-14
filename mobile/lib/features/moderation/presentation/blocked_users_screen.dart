import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/api/error_messages.dart';
import '../../../core/theme/app_tokens.dart';
import '../../../l10n/app_localizations.dart';
import '../../matches/presentation/match_labels.dart';
import 'moderation_controller.dart';

/// The users the signed-in user blocked, each with a way to lift the block.
class BlockedUsersScreen extends ConsumerWidget {
  const BlockedUsersScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppLocalizations.of(context);
    final blocks = ref.watch(blocksProvider);

    // Watched, so that the action is not disposed while it runs.
    final busy = ref.watch(moderationActionsProvider).isLoading;

    return Scaffold(
      appBar: AppBar(title: Text(l10n.blockedUsersTitle)),
      body: SafeArea(
        child: switch (blocks) {
          AsyncValue(:final value?) when value.isEmpty => Center(
              child: Padding(
                padding: const EdgeInsets.all(AppSpacing.lg),
                child: Text(l10n.blockedUsersEmpty, textAlign: TextAlign.center),
              ),
            ),
          AsyncValue(:final value?) => ListView.separated(
              itemCount: value.length,
              separatorBuilder: (context, index) => const Divider(height: 1),
              itemBuilder: (context, index) {
                final block = value[index];
                return ListTile(
                  leading: const Icon(Icons.block),
                  title: Text(userLabel(l10n, block.user)),
                  subtitle: Text(MaterialLocalizations.of(context).formatMediumDate(block.createdAt.toLocal())),
                  trailing: TextButton(
                    onPressed: busy
                        ? null
                        : () async {
                            final messenger = ScaffoldMessenger.of(context);
                            final lifted = await ref.read(moderationActionsProvider.notifier).unblock(block.user.userId);
                            messenger.showSnackBar(
                              SnackBar(
                                content: Text(
                                  lifted
                                      ? l10n.unblockDone
                                      : errorMessage(l10n, ref.read(moderationActionsProvider).error ?? Object()),
                                ),
                              ),
                            );
                          },
                    child: Text(l10n.moderationUnblock),
                  ),
                );
              },
            ),
          AsyncError(:final error) => Center(
              child: Padding(
                padding: const EdgeInsets.all(AppSpacing.lg),
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Text(errorMessage(l10n, error), textAlign: TextAlign.center),
                    const SizedBox(height: AppSpacing.md),
                    OutlinedButton(onPressed: () => ref.invalidate(blocksProvider), child: Text(l10n.errorRetry)),
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
