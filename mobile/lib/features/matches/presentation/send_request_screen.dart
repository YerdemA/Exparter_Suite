import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/api/error_messages.dart';
import '../../../core/api/generated/export.dart';
import '../../../core/auth/interaction_requirements.dart';
import '../../../core/router/app_routes.dart';
import '../../../core/theme/app_tokens.dart';
import '../../../l10n/app_localizations.dart';
import '../../discovery/presentation/discovery_controller.dart';
import '../../skills/presentation/skill_controller.dart';
import '../../skills/presentation/skill_labels.dart';
import 'match_controller.dart';

/// Lets the user propose a swap to another user: what they will teach, what they want
/// to learn, and an optional note.
class SendRequestScreen extends ConsumerStatefulWidget {
  const SendRequestScreen({required this.recipientId, super.key});

  static const messageMaxLength = 300;

  final String recipientId;

  @override
  ConsumerState<SendRequestScreen> createState() => _SendRequestScreenState();
}

class _SendRequestScreenState extends ConsumerState<SendRequestScreen> {
  final _message = TextEditingController();
  String? _offeredSkillId;
  String? _requestedSkillId;

  @override
  void dispose() {
    _message.dispose();
    super.dispose();
  }

  Future<void> _send() async {
    final l10n = AppLocalizations.of(context);
    final messenger = ScaffoldMessenger.of(context);
    final sent = await ref.read(matchActionsProvider.notifier).sendRequest(
          recipientId: widget.recipientId,
          offeredSkillId: _offeredSkillId!,
          requestedSkillId: _requestedSkillId!,
          message: _message.text,
        );
    if (!mounted) {
      return;
    }

    if (sent) {
      messenger.showSnackBar(SnackBar(content: Text(l10n.requestSent)));
      context.go(AppRoutes.userProfile(widget.recipientId));
      return;
    }

    final error = ref.read(matchActionsProvider).error;
    if (isInteractionRequirementError(error)) {
      await showInteractionRequiredDialog(context);
    } else {
      messenger.showSnackBar(SnackBar(content: Text(errorMessage(l10n, error ?? Object()))));
    }
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final busy = ref.watch(matchActionsProvider).isLoading;
    final own = ref.watch(ownSkillsProvider);
    final theirs = ref.watch(userSkillsProvider(widget.recipientId));
    final error = own.error ?? theirs.error;

    List<UserSkill> offeredOf(AsyncValue<List<UserSkill>> skills) =>
        (skills.value ?? const []).where((skill) => skill.type == UserSkillType.offered).toList();

    return Scaffold(
      appBar: AppBar(title: Text(l10n.requestSendTitle)),
      body: SafeArea(
        child: switch ((own.hasValue, theirs.hasValue)) {
          (true, true) when error == null => Align(
              alignment: Alignment.topCenter,
              child: ConstrainedBox(
                constraints: const BoxConstraints(maxWidth: AppSizes.formMaxWidth),
                child: SingleChildScrollView(
                  padding: const EdgeInsets.all(AppSpacing.lg),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      _SkillChoice(
                        title: l10n.requestOfferedLabel,
                        emptyText: l10n.requestOwnSkillsEmpty,
                        skills: offeredOf(own),
                        selectedSkillId: _offeredSkillId,
                        onSelected: (skillId) => setState(() => _offeredSkillId = skillId),
                      ),
                      const SizedBox(height: AppSpacing.lg),
                      _SkillChoice(
                        title: l10n.requestRequestedLabel,
                        emptyText: l10n.requestTheirSkillsEmpty,
                        skills: offeredOf(theirs),
                        selectedSkillId: _requestedSkillId,
                        onSelected: (skillId) => setState(() => _requestedSkillId = skillId),
                      ),
                      const SizedBox(height: AppSpacing.lg),
                      TextField(
                        controller: _message,
                        maxLength: SendRequestScreen.messageMaxLength,
                        minLines: 2,
                        maxLines: 4,
                        decoration: InputDecoration(labelText: l10n.requestMessageLabel),
                      ),
                      const SizedBox(height: AppSpacing.md),
                      if (busy) const LinearProgressIndicator(),
                      FilledButton(
                        onPressed: busy || _offeredSkillId == null || _requestedSkillId == null ? null : _send,
                        child: Text(l10n.requestSubmit),
                      ),
                    ],
                  ),
                ),
              ),
            ),
          _ when error != null => Center(
              child: Padding(
                padding: const EdgeInsets.all(AppSpacing.lg),
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Text(errorMessage(l10n, error), textAlign: TextAlign.center),
                    const SizedBox(height: AppSpacing.md),
                    OutlinedButton(
                      onPressed: () {
                        ref.invalidate(ownSkillsProvider);
                        ref.invalidate(userSkillsProvider(widget.recipientId));
                      },
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

/// A titled group of offered skills to pick one from.
class _SkillChoice extends StatelessWidget {
  const _SkillChoice({
    required this.title,
    required this.emptyText,
    required this.skills,
    required this.selectedSkillId,
    required this.onSelected,
  });

  final String title;
  final String emptyText;
  final List<UserSkill> skills;
  final String? selectedSkillId;
  final ValueChanged<String> onSelected;

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final theme = Theme.of(context);

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(title, style: theme.textTheme.titleMedium),
        const SizedBox(height: AppSpacing.sm),
        if (skills.isEmpty)
          Text(emptyText, style: theme.textTheme.bodyMedium)
        else
          Wrap(
            spacing: AppSpacing.sm,
            runSpacing: AppSpacing.sm,
            children: [
              for (final skill in skills)
                ChoiceChip(
                  label: Text('${skill.skill.name} · ${skillLevelLabel(l10n, skill.level)}'),
                  selected: skill.skill.id == selectedSkillId,
                  onSelected: (_) => onSelected(skill.skill.id),
                ),
            ],
          ),
      ],
    );
  }
}
