import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/theme/app_tokens.dart';
import '../../../l10n/app_localizations.dart';
import 'session_controller.dart';

/// A user's history of swaps in numbers: how many matches have ended, how many of them
/// went well and how many ended without a single session.
class UserStatsCard extends ConsumerWidget {
  const UserStatsCard({required this.userId, super.key});

  final String userId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppLocalizations.of(context);
    final theme = Theme.of(context);
    final stats = ref.watch(userStatsProvider(userId));

    return Card(
      margin: EdgeInsets.zero,
      child: Padding(
        padding: const EdgeInsets.all(AppSpacing.md),
        child: switch (stats) {
          AsyncValue(:final value?) => Column(
              children: [
                Row(
                  children: [
                    _Figure(value: value.totalMatches, label: l10n.statsTotalMatches),
                    _Figure(value: value.completedMatches, label: l10n.statsCompletedMatches),
                    _Figure(value: value.cancelledMatches, label: l10n.statsCancelledMatches),
                  ],
                ),
                const SizedBox(height: AppSpacing.sm),
                Text(
                  l10n.statsCompletedSessions(value.completedSessions),
                  style: theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.onSurfaceVariant),
                ),
              ],
            ),
          AsyncError() => Column(
              children: [
                Text(l10n.statsUnavailable, textAlign: TextAlign.center),
                TextButton(onPressed: () => ref.invalidate(userStatsProvider(userId)), child: Text(l10n.errorRetry)),
              ],
            ),
          _ => const Center(child: CircularProgressIndicator()),
        },
      ),
    );
  }
}

class _Figure extends StatelessWidget {
  const _Figure({required this.value, required this.label});

  final int value;
  final String label;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Expanded(
      child: Column(
        children: [
          Text('$value', style: theme.textTheme.headlineSmall),
          Text(label, textAlign: TextAlign.center, style: theme.textTheme.bodySmall),
        ],
      ),
    );
  }
}
