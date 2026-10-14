import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/api/error_messages.dart';
import '../../../core/api/generated/export.dart';
import '../../../core/theme/app_tokens.dart';
import '../../../l10n/app_localizations.dart';
import 'moderation_controller.dart';

String reportReasonLabel(AppLocalizations l10n, ReportReason reason) => switch (reason) {
      ReportReason.spam => l10n.reportReasonSpam,
      ReportReason.harassment => l10n.reportReasonHarassment,
      ReportReason.inappropriateContent => l10n.reportReasonInappropriate,
      ReportReason.fakeProfile => l10n.reportReasonFakeProfile,
      ReportReason.other || ReportReason.$unknown => l10n.reportReasonOther,
    };

/// Asks why, then reports a user, a message or a review; the outcome shows in a snackbar.
Future<void> reportContent(
  BuildContext context,
  WidgetRef ref, {
  required ReportTargetType targetType,
  required String targetId,
}) async {
  final l10n = AppLocalizations.of(context);
  final messenger = ScaffoldMessenger.of(context);
  final answer = await showDialog<(ReportReason, String)>(
    context: context,
    builder: (context) => _ReportDialog(targetType: targetType),
  );
  if (answer == null) {
    return;
  }

  final (reason, description) = answer;
  final sent = await ref
      .read(moderationActionsProvider.notifier)
      .report(targetType: targetType, targetId: targetId, reason: reason, description: description);
  messenger.showSnackBar(
    SnackBar(
      content: Text(sent ? l10n.reportSent : errorMessage(l10n, ref.read(moderationActionsProvider).error ?? Object())),
    ),
  );
}

/// Explains what a block does, then blocks. Returns whether the user was blocked.
Future<bool> blockUser(BuildContext context, WidgetRef ref, {required String userId, required String name}) async {
  final l10n = AppLocalizations.of(context);
  final messenger = ScaffoldMessenger.of(context);
  final confirmed = await showDialog<bool>(
    context: context,
    builder: (dialogContext) => AlertDialog(
      title: Text(l10n.blockTitle(name)),
      content: Text(l10n.blockBody(name)),
      actions: [
        TextButton(onPressed: () => Navigator.pop(dialogContext, false), child: Text(l10n.actionCancel)),
        FilledButton(onPressed: () => Navigator.pop(dialogContext, true), child: Text(l10n.moderationBlock)),
      ],
    ),
  );
  if (confirmed != true) {
    return false;
  }

  final blocked = await ref.read(moderationActionsProvider.notifier).block(userId);
  messenger.showSnackBar(
    SnackBar(
      content: Text(blocked ? l10n.blockDone : errorMessage(l10n, ref.read(moderationActionsProvider).error ?? Object())),
    ),
  );
  return blocked;
}

/// "Report" and "Block" for another user, in an app bar.
class UserModerationMenu extends ConsumerWidget {
  const UserModerationMenu({required this.userId, required this.name, required this.onBlocked, super.key});

  final String userId;
  final String name;

  /// What the screen does once the user is blocked; their profile, for one, is gone.
  final VoidCallback onBlocked;

  @override
  Widget build(BuildContext context, WidgetRef ref) => PopupMenuButton<void>(
        itemBuilder: (menuContext) =>
            userModerationItems(context, ref, userId: userId, name: name, onBlocked: onBlocked),
      );
}

/// The items of [UserModerationMenu], for screens whose menu offers more.
List<PopupMenuEntry<void>> userModerationItems(
  BuildContext context,
  WidgetRef ref, {
  required String userId,
  required String name,
  required VoidCallback onBlocked,
}) {
  final l10n = AppLocalizations.of(context);
  return [
    PopupMenuItem(
      onTap: () => reportContent(context, ref, targetType: ReportTargetType.user, targetId: userId),
      child: Text(l10n.moderationReport),
    ),
    PopupMenuItem(
      onTap: () async {
        if (await blockUser(context, ref, userId: userId, name: name)) {
          onBlocked();
        }
      },
      child: Text(l10n.moderationBlock),
    ),
  ];
}

class _ReportDialog extends StatefulWidget {
  const _ReportDialog({required this.targetType});

  final ReportTargetType targetType;

  @override
  State<_ReportDialog> createState() => _ReportDialogState();
}

class _ReportDialogState extends State<_ReportDialog> {
  final _description = TextEditingController();
  ReportReason? _reason;

  @override
  void dispose() {
    _description.dispose();
    super.dispose();
  }

  // "Other" says nothing by itself.
  bool get _complete => _reason != null && (_reason != ReportReason.other || _description.text.trim().isNotEmpty);

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);

    return AlertDialog(
      title: Text(switch (widget.targetType) {
        ReportTargetType.message => l10n.reportMessageTitle,
        ReportTargetType.review => l10n.reportReviewTitle,
        _ => l10n.reportUserTitle,
      }),
      content: SingleChildScrollView(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text(l10n.reportBody),
            const SizedBox(height: AppSpacing.sm),
            RadioGroup<ReportReason>(
              groupValue: _reason,
              onChanged: (reason) => setState(() => _reason = reason),
              child: Column(
                children: [
                  for (final reason in ReportReason.values.where((reason) => reason != ReportReason.$unknown))
                    RadioListTile<ReportReason>(
                      value: reason,
                      contentPadding: EdgeInsets.zero,
                      title: Text(reportReasonLabel(l10n, reason)),
                    ),
                ],
              ),
            ),
            TextField(
              controller: _description,
              maxLength: reportDescriptionMaxLength,
              minLines: 2,
              maxLines: 4,
              onChanged: (_) => setState(() {}),
              decoration: InputDecoration(
                labelText: l10n.reportDescriptionLabel,
                helperText: _reason == ReportReason.other ? l10n.reportDescriptionRequired : l10n.reportDescriptionOptional,
              ),
            ),
          ],
        ),
      ),
      actions: [
        TextButton(onPressed: () => Navigator.pop(context), child: Text(l10n.actionCancel)),
        FilledButton(
          onPressed: _complete ? () => Navigator.pop(context, (_reason!, _description.text)) : null,
          child: Text(l10n.reportSubmit),
        ),
      ],
    );
  }
}
