import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/api/error_messages.dart';
import '../../../core/api/generated/export.dart';
import '../../../core/auth/session_controller.dart';
import '../../../core/router/app_routes.dart';
import '../../../core/theme/app_tokens.dart';
import '../../../l10n/app_localizations.dart';
import '../../matches/presentation/match_labels.dart';
import 'session_controller.dart';

/// The sessions of a match, with the ways to add one while the match is running.
class MatchSessionsSection extends ConsumerWidget {
  const MatchSessionsSection({required this.match, super.key});

  final Match match;

  Future<void> _reportInPerson(BuildContext context, WidgetRef ref) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) {
        final l10n = AppLocalizations.of(dialogContext);
        return AlertDialog(
          title: Text(l10n.sessionInPersonConfirmTitle),
          content: Text(l10n.sessionInPersonConfirmBody),
          actions: [
            TextButton(onPressed: () => Navigator.pop(dialogContext, false), child: Text(l10n.actionCancel)),
            FilledButton(onPressed: () => Navigator.pop(dialogContext, true), child: Text(l10n.sessionInPersonReport)),
          ],
        );
      },
    );
    if (confirmed == true && context.mounted) {
      await runSessionAction(context, ref, (actions) => actions.reportInPerson(match.id));
    }
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppLocalizations.of(context);
    final theme = Theme.of(context);
    final sessions = ref.watch(matchSessionsProvider(match.id));
    final busy = ref.watch(sessionActionsProvider).isLoading;
    final ownUserId = ref.watch(sessionControllerProvider).value?.id;
    // A pair reports an in-person meeting only once. An earlier match of the same two
    // users is not visible here; the server refuses the report in that case.
    final metInPerson = sessions.value?.any(
          (session) => session.mode == SessionMode.inPerson && session.status != SwapSessionStatus.cancelled,
        ) ??
        false;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Text(l10n.sessionsTitle, style: theme.textTheme.titleMedium),
        const SizedBox(height: AppSpacing.sm),
        // A match that is ending or over takes no new sessions.
        if (match.status == MatchStatus.active) ...[
          OutlinedButton.icon(
            onPressed: busy ? null : () => context.go(AppRoutes.proposeSession(match.id)),
            icon: const Icon(Icons.videocam_outlined),
            label: Text(l10n.sessionProposeOnline),
          ),
          if (!metInPerson) ...[
            const SizedBox(height: AppSpacing.sm),
            OutlinedButton.icon(
              onPressed: busy ? null : () => _reportInPerson(context, ref),
              icon: const Icon(Icons.handshake_outlined),
              label: Text(l10n.sessionInPersonReport),
            ),
            const SizedBox(height: AppSpacing.sm),
            Text(l10n.sessionInPersonHint, style: theme.textTheme.bodySmall),
          ],
          const SizedBox(height: AppSpacing.md),
        ],
        if (busy) const LinearProgressIndicator(),
        switch (sessions) {
          AsyncValue(:final value?) when value.isEmpty => Text(l10n.sessionsEmpty, style: theme.textTheme.bodyMedium),
          AsyncValue(:final value?) => Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                for (final session in value) _SessionCard(session: session, ownUserId: ownUserId, busy: busy),
              ],
            ),
          AsyncError(:final error) => Column(
              children: [
                Text(errorMessage(l10n, error), textAlign: TextAlign.center),
                TextButton(
                  onPressed: () => ref.invalidate(matchSessionsProvider(match.id)),
                  child: Text(l10n.errorRetry),
                ),
              ],
            ),
          _ => const Center(child: CircularProgressIndicator()),
        },
      ],
    );
  }
}

/// Runs a session action and, if it is refused, says why.
Future<void> runSessionAction(
  BuildContext context,
  WidgetRef ref,
  Future<bool> Function(SessionActions actions) action,
) async {
  final l10n = AppLocalizations.of(context);
  final messenger = ScaffoldMessenger.of(context);
  if (!await action(ref.read(sessionActionsProvider.notifier))) {
    messenger.showSnackBar(
      SnackBar(content: Text(errorMessage(l10n, ref.read(sessionActionsProvider).error ?? Object()))),
    );
  }
}

String sessionStatusLabel(AppLocalizations l10n, SwapSessionStatus status) => switch (status) {
      SwapSessionStatus.proposed => l10n.sessionStatusProposed,
      SwapSessionStatus.scheduled => l10n.sessionStatusScheduled,
      SwapSessionStatus.completionPending => l10n.sessionStatusCompletionPending,
      SwapSessionStatus.completed => l10n.sessionStatusCompleted,
      SwapSessionStatus.declined => l10n.sessionStatusDeclined,
      SwapSessionStatus.cancelled => l10n.sessionStatusCancelled,
      SwapSessionStatus.$unknown => l10n.skillLevelUnknown,
    };

class _SessionCard extends ConsumerWidget {
  const _SessionCard({required this.session, required this.ownUserId, required this.busy});

  final SwapSession session;
  final String? ownUserId;
  final bool busy;

  /// Asks for the reason a cancellation needs. Returns null if the user backs out.
  Future<void> _cancel(BuildContext context, WidgetRef ref, {required String title}) async {
    final reason = await showDialog<String>(context: context, builder: (context) => _CancelDialog(title: title));
    if (reason != null && context.mounted) {
      await runSessionAction(context, ref, (actions) => actions.cancel(session.id, reason: reason));
    }
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppLocalizations.of(context);
    final theme = Theme.of(context);
    final muted = theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.onSurfaceVariant);
    final online = session.mode == SessionMode.online;
    final scheduledAt = session.scheduledAt?.toLocal();
    final proposedByMe = session.proposedByUserId == ownUserId;
    final markedByMe = session.completionMarkedByUserId == ownUserId;
    final deadline = session.completionConfirmDeadline;

    String when() {
      if (scheduledAt == null) {
        return l10n.sessionInPersonTitle;
      }

      final date = MaterialLocalizations.of(context).formatMediumDate(scheduledAt);
      final time = TimeOfDay.fromDateTime(scheduledAt).format(context);
      return l10n.sessionOnlineTitle(date, time, session.durationMinutes ?? 0);
    }

    Widget action(String label, VoidCallback onPressed, {bool primary = false}) => Padding(
          padding: const EdgeInsets.only(top: AppSpacing.sm),
          child: primary
              ? FilledButton(onPressed: busy ? null : onPressed, child: Text(label))
              : OutlinedButton(onPressed: busy ? null : onPressed, child: Text(label)),
        );

    return Card(
      margin: const EdgeInsets.only(bottom: AppSpacing.md),
      child: Padding(
        padding: const EdgeInsets.all(AppSpacing.md),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Row(
              children: [
                Icon(online ? Icons.videocam_outlined : Icons.handshake_outlined),
                const SizedBox(width: AppSpacing.sm),
                Expanded(child: Text(when(), style: theme.textTheme.titleSmall)),
              ],
            ),
            const SizedBox(height: AppSpacing.xs),
            Text(sessionStatusLabel(l10n, session.status), style: muted),
            if (session.note != null) ...[
              const SizedBox(height: AppSpacing.sm),
              Text(session.note!, style: theme.textTheme.bodyMedium),
            ],
            ...switch (session.status) {
              SwapSessionStatus.proposed when proposedByMe => [
                  action(l10n.sessionCancel, () => _cancel(context, ref, title: l10n.sessionCancelTitle)),
                ],
              SwapSessionStatus.proposed => [
                  action(
                    l10n.requestAccept,
                    () => runSessionAction(context, ref, (actions) => actions.accept(session.id)),
                    primary: true,
                  ),
                  action(
                    l10n.requestDecline,
                    () => runSessionAction(context, ref, (actions) => actions.decline(session.id)),
                  ),
                ],
              SwapSessionStatus.scheduled => [
                  // Before its start a session cannot have taken place yet.
                  if (scheduledAt != null && !DateTime.now().isBefore(scheduledAt))
                    action(
                      l10n.sessionMarkCompleted,
                      () => runSessionAction(context, ref, (actions) => actions.markCompleted(session.id)),
                      primary: true,
                    ),
                  action(l10n.sessionCancel, () => _cancel(context, ref, title: l10n.sessionCancelTitle)),
                ],
              SwapSessionStatus.completionPending when markedByMe => [
                  const SizedBox(height: AppSpacing.sm),
                  Text(
                    deadline == null
                        ? l10n.sessionWaitingConfirmation
                        : '${l10n.sessionWaitingConfirmation} ${remainingLabel(l10n, deadline, DateTime.now())}.',
                    style: theme.textTheme.bodyMedium,
                  ),
                  action(l10n.sessionCancel, () => _cancel(context, ref, title: l10n.sessionCancelTitle)),
                ],
              SwapSessionStatus.completionPending => [
                  const SizedBox(height: AppSpacing.sm),
                  Text(
                    online ? l10n.sessionConfirmQuestionOnline : l10n.sessionConfirmQuestionInPerson,
                    style: theme.textTheme.bodyMedium,
                  ),
                  if (deadline != null) Text(remainingLabel(l10n, deadline, DateTime.now()), style: muted),
                  action(
                    l10n.sessionConfirm,
                    () => runSessionAction(context, ref, (actions) => actions.confirmCompleted(session.id)),
                    primary: true,
                  ),
                  action(l10n.sessionDispute, () => _cancel(context, ref, title: l10n.sessionDisputeTitle)),
                ],
              SwapSessionStatus.completed when session.completedAutomatically => [
                  const SizedBox(height: AppSpacing.xs),
                  Text(l10n.sessionCompletedAutomatically, style: muted),
                ],
              SwapSessionStatus.cancelled => [
                  const SizedBox(height: AppSpacing.sm),
                  Text(
                    session.cancelReason == null
                        ? l10n.sessionCancelledMatchEnded
                        : l10n.sessionCancelReason(session.cancelReason!),
                    style: theme.textTheme.bodyMedium,
                  ),
                ],
              _ => const <Widget>[],
            },
          ],
        ),
      ),
    );
  }
}

/// Collects the reason for a cancellation. Pops with the reason, or null if dismissed.
class _CancelDialog extends StatefulWidget {
  const _CancelDialog({required this.title});

  static const _minLength = 5;
  static const _maxLength = 500;

  final String title;

  @override
  State<_CancelDialog> createState() => _CancelDialogState();
}

class _CancelDialogState extends State<_CancelDialog> {
  final _reason = TextEditingController();

  @override
  void dispose() {
    _reason.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final valid = _reason.text.trim().length >= _CancelDialog._minLength;

    return AlertDialog(
      title: Text(widget.title),
      content: TextField(
        controller: _reason,
        autofocus: true,
        minLines: 2,
        maxLines: 4,
        maxLength: _CancelDialog._maxLength,
        onChanged: (_) => setState(() {}),
        decoration: InputDecoration(labelText: l10n.sessionReasonLabel, helperText: l10n.sessionReasonHelper),
      ),
      actions: [
        TextButton(onPressed: () => Navigator.pop(context), child: Text(l10n.actionCancel)),
        FilledButton(
          onPressed: valid ? () => Navigator.pop(context, _reason.text.trim()) : null,
          child: Text(l10n.sessionReasonSubmit),
        ),
      ],
    );
  }
}
