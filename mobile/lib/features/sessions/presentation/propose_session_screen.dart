import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/api/error_messages.dart';
import '../../../core/router/app_routes.dart';
import '../../../core/theme/app_tokens.dart';
import '../../../l10n/app_localizations.dart';
import 'session_controller.dart';

/// Lets the user propose an online session: when, for how long, and an optional note
/// such as a meeting link.
class ProposeSessionScreen extends ConsumerStatefulWidget {
  const ProposeSessionScreen({required this.matchId, super.key});

  static const noteMaxLength = 500;

  /// How far ahead a session can be planned.
  static const _horizon = Duration(days: 365);

  final String matchId;

  @override
  ConsumerState<ProposeSessionScreen> createState() => _ProposeSessionScreenState();
}

class _ProposeSessionScreenState extends ConsumerState<ProposeSessionScreen> {
  final _note = TextEditingController();
  late DateTime _date;
  TimeOfDay _time = const TimeOfDay(hour: 19, minute: 0);
  int _duration = defaultSessionDuration;

  @override
  void initState() {
    super.initState();
    // Tomorrow evening: a sensible start that is certainly in the future.
    final tomorrow = DateTime.now().add(const Duration(days: 1));
    _date = DateTime(tomorrow.year, tomorrow.month, tomorrow.day);
  }

  @override
  void dispose() {
    _note.dispose();
    super.dispose();
  }

  DateTime get _scheduledAt => DateTime(_date.year, _date.month, _date.day, _time.hour, _time.minute);

  Future<void> _pickDate() async {
    final today = DateTime.now();
    final picked = await showDatePicker(
      context: context,
      initialDate: _date,
      firstDate: DateTime(today.year, today.month, today.day),
      lastDate: today.add(ProposeSessionScreen._horizon),
    );
    if (picked != null) {
      setState(() => _date = picked);
    }
  }

  Future<void> _pickTime() async {
    final picked = await showTimePicker(context: context, initialTime: _time);
    if (picked != null) {
      setState(() => _time = picked);
    }
  }

  Future<void> _submit() async {
    final l10n = AppLocalizations.of(context);
    final messenger = ScaffoldMessenger.of(context);
    final proposed = await ref.read(sessionActionsProvider.notifier).propose(
          widget.matchId,
          scheduledAt: _scheduledAt,
          durationMinutes: _duration,
          note: _note.text,
        );
    if (!mounted) {
      return;
    }

    if (proposed) {
      messenger.showSnackBar(SnackBar(content: Text(l10n.sessionProposed)));
      context.go(AppRoutes.matchDetail(widget.matchId));
    } else {
      messenger.showSnackBar(
        SnackBar(content: Text(errorMessage(l10n, ref.read(sessionActionsProvider).error ?? Object()))),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final theme = Theme.of(context);
    final busy = ref.watch(sessionActionsProvider).isLoading;
    final inPast = !_scheduledAt.isAfter(DateTime.now());

    return Scaffold(
      appBar: AppBar(title: Text(l10n.sessionProposeTitle)),
      body: SafeArea(
        child: Align(
          alignment: Alignment.topCenter,
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: AppSizes.formMaxWidth),
            child: SingleChildScrollView(
              padding: const EdgeInsets.all(AppSpacing.lg),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  ListTile(
                    contentPadding: EdgeInsets.zero,
                    leading: const Icon(Icons.calendar_today_outlined),
                    title: Text(l10n.sessionDateLabel),
                    subtitle: Text(MaterialLocalizations.of(context).formatFullDate(_date)),
                    onTap: _pickDate,
                  ),
                  ListTile(
                    contentPadding: EdgeInsets.zero,
                    leading: const Icon(Icons.schedule),
                    title: Text(l10n.sessionTimeLabel),
                    subtitle: Text(_time.format(context)),
                    onTap: _pickTime,
                  ),
                  if (inPast)
                    Text(
                      l10n.errorSessionInPast,
                      style: theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.error),
                    ),
                  const SizedBox(height: AppSpacing.md),
                  Text(l10n.sessionDurationLabel, style: theme.textTheme.titleMedium),
                  const SizedBox(height: AppSpacing.sm),
                  Wrap(
                    spacing: AppSpacing.sm,
                    runSpacing: AppSpacing.sm,
                    children: [
                      for (final minutes in sessionDurations)
                        ChoiceChip(
                          label: Text(l10n.sessionDurationMinutes(minutes)),
                          selected: minutes == _duration,
                          onSelected: (_) => setState(() => _duration = minutes),
                        ),
                    ],
                  ),
                  const SizedBox(height: AppSpacing.lg),
                  TextField(
                    controller: _note,
                    maxLength: ProposeSessionScreen.noteMaxLength,
                    minLines: 2,
                    maxLines: 4,
                    decoration: InputDecoration(
                      labelText: l10n.sessionNoteLabel,
                      helperText: l10n.sessionNoteHelper,
                    ),
                  ),
                  const SizedBox(height: AppSpacing.md),
                  if (busy) const LinearProgressIndicator(),
                  FilledButton(
                    onPressed: busy || inPast ? null : _submit,
                    child: Text(l10n.sessionProposeSubmit),
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}
