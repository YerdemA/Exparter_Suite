import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/api/error_messages.dart';
import '../../../core/api/generated/export.dart';
import '../../../core/router/app_routes.dart';
import '../../../core/theme/app_tokens.dart';
import '../../../l10n/app_localizations.dart';
import 'skill_controller.dart';
import 'skill_labels.dart';
import 'skill_picker_screen.dart';

/// The signed-in user's offered and wanted skills, with the means to change them.
class OwnSkillsSection extends ConsumerWidget {
  const OwnSkillsSection({super.key});

  /// Mirrors the backend limit so the add button can explain itself instead of failing.
  static const maxPerType = 5;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppLocalizations.of(context);
    final skills = ref.watch(ownSkillsProvider);

    return switch (skills) {
      AsyncData(:final value) => Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            _SkillGroup(
              title: l10n.skillsOfferedTitle,
              emptyText: l10n.skillsOfferedEmpty,
              type: UserSkillType.offered,
              skills: [
                for (final skill in value)
                  if (skill.type == UserSkillType.offered) skill,
              ],
            ),
            const SizedBox(height: AppSpacing.lg),
            _SkillGroup(
              title: l10n.skillsWantedTitle,
              emptyText: l10n.skillsWantedEmpty,
              type: UserSkillType.wanted,
              skills: [
                for (final skill in value)
                  if (skill.type == UserSkillType.wanted) skill,
              ],
            ),
          ],
        ),
      AsyncError(:final error) => Column(
          children: [
            Text(errorMessage(l10n, error), textAlign: TextAlign.center),
            const SizedBox(height: AppSpacing.sm),
            OutlinedButton(
              onPressed: () => ref.invalidate(ownSkillsProvider),
              child: Text(l10n.errorRetry),
            ),
          ],
        ),
      _ => const Center(child: CircularProgressIndicator()),
    };
  }
}

class _SkillGroup extends ConsumerWidget {
  const _SkillGroup({
    required this.title,
    required this.emptyText,
    required this.type,
    required this.skills,
  });

  final String title;
  final String emptyText;
  final UserSkillType type;
  final List<UserSkill> skills;

  Future<void> _edit(BuildContext context, WidgetRef ref, UserSkill userSkill) async {
    final l10n = AppLocalizations.of(context);
    final messenger = ScaffoldMessenger.of(context);
    final choice = await showModalBottomSheet<_SkillChoice>(
      context: context,
      showDragHandle: true,
      builder: (context) => SafeArea(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            ListTile(
              leading: const Icon(Icons.tune),
              title: Text(l10n.skillChangeLevel),
              onTap: () => Navigator.pop(context, _SkillChoice.changeLevel),
            ),
            ListTile(
              leading: const Icon(Icons.delete_outline),
              title: Text(l10n.skillRemove),
              onTap: () => Navigator.pop(context, _SkillChoice.remove),
            ),
          ],
        ),
      ),
    );
    if (choice == null || !context.mounted) {
      return;
    }

    final bool succeeded;
    if (choice == _SkillChoice.remove) {
      succeeded = await ref.read(skillActionsProvider.notifier).remove(userSkillId: userSkill.id);
    } else {
      final level = await showSkillLevelSheet(
        context,
        skillName: userSkill.skill.name,
        type: userSkill.type,
        current: userSkill.level,
      );
      if (level == null || level == userSkill.level) {
        return;
      }

      succeeded = await ref
          .read(skillActionsProvider.notifier)
          .changeLevel(userSkillId: userSkill.id, level: level);
    }

    if (!succeeded) {
      messenger.showSnackBar(
        SnackBar(content: Text(errorMessage(l10n, ref.read(skillActionsProvider).error ?? Object()))),
      );
    }
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppLocalizations.of(context);
    final theme = Theme.of(context);
    final full = skills.length >= OwnSkillsSection.maxPerType;

    // Watching keeps the auto-disposed action controller alive while an action runs, so
    // it can refresh the list when the action finishes; it also blocks double taps.
    final busy = ref.watch(skillActionsProvider).isLoading;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          children: [
            Expanded(child: Text(title, style: theme.textTheme.titleMedium)),
            Text(
              l10n.skillsCount(skills.length, OwnSkillsSection.maxPerType),
              style: theme.textTheme.bodySmall,
            ),
          ],
        ),
        const SizedBox(height: AppSpacing.sm),
        if (skills.isEmpty)
          Text(emptyText, style: theme.textTheme.bodyMedium?.copyWith(color: theme.colorScheme.onSurfaceVariant)),
        Wrap(
          spacing: AppSpacing.sm,
          runSpacing: AppSpacing.sm,
          children: [
            for (final userSkill in skills)
              ActionChip(
                label: Text(
                  '${userSkill.skill.name} · ${userSkillLevelLabel(l10n, userSkill.type, userSkill.level)}',
                ),
                onPressed: busy ? null : () => _edit(context, ref, userSkill),
              ),
          ],
        ),
        const SizedBox(height: AppSpacing.sm),
        if (full)
          Text(l10n.skillsLimitReached, style: theme.textTheme.bodySmall)
        else
          TextButton.icon(
            onPressed: () => context.go(AppRoutes.skillPicker(type)),
            icon: const Icon(Icons.add),
            label: Text(type == UserSkillType.offered ? l10n.skillsAddOffered : l10n.skillsAddWanted),
          ),
      ],
    );
  }
}

enum _SkillChoice { changeLevel, remove }
