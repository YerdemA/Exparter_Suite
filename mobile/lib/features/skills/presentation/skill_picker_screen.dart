import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/api/error_messages.dart';
import '../../../core/api/generated/export.dart';
import '../../../core/auth/interaction_requirements.dart';
import '../../../core/router/app_routes.dart';
import '../../../core/theme/app_tokens.dart';
import '../../../l10n/app_localizations.dart';
import 'skill_controller.dart';
import 'skill_labels.dart';

/// Lets the user pick a level. Returns null if they back out.
Future<SkillLevel?> showSkillLevelSheet(
  BuildContext context, {
  required String skillName,
  required UserSkillType type,
  SkillLevel? current,
}) =>
    showModalBottomSheet<SkillLevel>(
      context: context,
      showDragHandle: true,
      builder: (sheetContext) {
        final l10n = AppLocalizations.of(sheetContext);
        final theme = Theme.of(sheetContext);
        return SafeArea(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Padding(
                padding: const EdgeInsets.symmetric(horizontal: AppSpacing.lg),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(skillName, style: theme.textTheme.titleLarge),
                    const SizedBox(height: AppSpacing.xs),
                    Text(
                      type == UserSkillType.offered ? l10n.skillLevelQuestionOffered : l10n.skillLevelQuestionWanted,
                      style: theme.textTheme.bodyMedium,
                    ),
                  ],
                ),
              ),
              const SizedBox(height: AppSpacing.sm),
              for (final level in selectableSkillLevels)
                ListTile(
                  title: Text(skillLevelLabel(l10n, level)),
                  trailing: level == current ? const Icon(Icons.check) : null,
                  onTap: () => Navigator.pop(sheetContext, level),
                ),
            ],
          ),
        );
      },
    );

/// Catalog browser for adding an offered or a wanted skill.
class SkillPickerScreen extends ConsumerStatefulWidget {
  const SkillPickerScreen({required this.type, super.key});

  final UserSkillType type;

  @override
  ConsumerState<SkillPickerScreen> createState() => _SkillPickerScreenState();
}

class _SkillPickerScreenState extends ConsumerState<SkillPickerScreen> {
  static const _searchDelay = Duration(milliseconds: 350);

  final _search = TextEditingController();
  Timer? _debounce;
  String _query = '';
  String? _categoryId;

  @override
  void dispose() {
    _debounce?.cancel();
    _search.dispose();
    super.dispose();
  }

  void _onSearchChanged(String value) {
    // Waiting for a pause in typing avoids one request per keystroke.
    _debounce?.cancel();
    _debounce = Timer(_searchDelay, () => setState(() => _query = value.trim()));
  }

  Future<void> _add(Skill skill) async {
    final l10n = AppLocalizations.of(context);
    final messenger = ScaffoldMessenger.of(context);
    final level = await showSkillLevelSheet(context, skillName: skill.name, type: widget.type);
    if (level == null) {
      return;
    }

    final added = await ref
        .read(skillActionsProvider.notifier)
        .add(skillId: skill.id, type: widget.type, level: level);
    if (!mounted) {
      return;
    }

    if (added) {
      context.go(AppRoutes.profile);
    } else {
      messenger.showSnackBar(
        SnackBar(content: Text(errorMessage(l10n, ref.read(skillActionsProvider).error ?? Object()))),
      );
    }
  }

  Future<void> _propose() async {
    final l10n = AppLocalizations.of(context);
    final messenger = ScaffoldMessenger.of(context);
    final proposed = await showDialog<bool>(
      context: context,
      builder: (context) => _ProposeSkillDialog(initialName: _search.text.trim(), initialCategoryId: _categoryId),
    );
    if (proposed == null || !mounted) {
      return;
    }

    if (proposed) {
      messenger.showSnackBar(SnackBar(content: Text(l10n.skillProposalSent)));
    } else {
      await showInteractionRequiredDialog(context);
    }
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final theme = Theme.of(context);
    final categories = ref.watch(skillCategoriesProvider).value ?? const <SkillCategory>[];
    final results = ref.watch(skillSearchProvider(categoryId: _categoryId, query: _query));
    final busy = ref.watch(skillActionsProvider).isLoading;
    final alreadyAdded = {
      for (final userSkill in ref.watch(ownSkillsProvider).value ?? const <UserSkill>[])
        if (userSkill.type == widget.type) userSkill.skill.id,
    };

    return Scaffold(
      appBar: AppBar(
        title: Text(widget.type == UserSkillType.offered ? l10n.skillPickerTitleOffered : l10n.skillPickerTitleWanted),
      ),
      body: SafeArea(
        child: Column(
          children: [
            Padding(
              padding: const EdgeInsets.fromLTRB(AppSpacing.md, AppSpacing.sm, AppSpacing.md, AppSpacing.sm),
              child: TextField(
                controller: _search,
                onChanged: _onSearchChanged,
                textInputAction: TextInputAction.search,
                decoration: InputDecoration(
                  labelText: l10n.skillSearchLabel,
                  prefixIcon: const Icon(Icons.search),
                ),
              ),
            ),
            if (categories.isNotEmpty)
              SizedBox(
                height: AppSizes.control,
                child: ListView.separated(
                  scrollDirection: Axis.horizontal,
                  padding: const EdgeInsets.symmetric(horizontal: AppSpacing.md),
                  itemCount: categories.length,
                  separatorBuilder: (context, index) => const SizedBox(width: AppSpacing.sm),
                  itemBuilder: (context, index) {
                    final category = categories[index];
                    return ChoiceChip(
                      label: Text(category.name),
                      selected: category.id == _categoryId,
                      onSelected: (selected) => setState(() => _categoryId = selected ? category.id : null),
                    );
                  },
                ),
              ),
            if (busy) const LinearProgressIndicator(),
            Expanded(
              child: switch (results) {
                AsyncData(:final value) => ListView(
                    children: [
                      for (final skill in value.items)
                        ListTile(
                          title: Text(skill.name),
                          trailing: alreadyAdded.contains(skill.id)
                              ? Icon(Icons.check, semanticLabel: l10n.skillAlreadyAdded)
                              : const Icon(Icons.add),
                          enabled: !busy && !alreadyAdded.contains(skill.id),
                          onTap: () => _add(skill),
                        ),
                      if (value.items.isEmpty)
                        Padding(
                          padding: const EdgeInsets.all(AppSpacing.lg),
                          child: Text(l10n.skillSearchEmpty, textAlign: TextAlign.center),
                        ),
                      if (value.totalCount > value.items.length)
                        Padding(
                          padding: const EdgeInsets.all(AppSpacing.md),
                          child: Text(
                            l10n.skillSearchNarrow,
                            textAlign: TextAlign.center,
                            style: theme.textTheme.bodySmall,
                          ),
                        ),
                      const Divider(),
                      ListTile(
                        leading: const Icon(Icons.lightbulb_outline),
                        title: Text(l10n.skillProposeAction),
                        subtitle: Text(l10n.skillProposeHint),
                        onTap: busy ? null : _propose,
                      ),
                      ListTile(
                        leading: const Icon(Icons.list_alt),
                        title: Text(l10n.skillProposalsTitle),
                        onTap: () => context.go(AppRoutes.skillProposals),
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
                            onPressed: () => ref.invalidate(skillSearchProvider),
                            child: Text(l10n.errorRetry),
                          ),
                        ],
                      ),
                    ),
                  ),
                _ => const Center(child: CircularProgressIndicator()),
              },
            ),
          ],
        ),
      ),
    );
  }
}

/// Collects a name and category for a skill missing from the catalog.
///
/// Pops with true when the proposal was sent, false when the backend refused it
/// because the user's email is not verified, and null when cancelled.
class _ProposeSkillDialog extends ConsumerStatefulWidget {
  const _ProposeSkillDialog({required this.initialName, required this.initialCategoryId});

  final String initialName;
  final String? initialCategoryId;

  @override
  ConsumerState<_ProposeSkillDialog> createState() => _ProposeSkillDialogState();
}

class _ProposeSkillDialogState extends ConsumerState<_ProposeSkillDialog> {
  static const _nameMin = 2;
  static const _nameMax = 60;

  final _formKey = GlobalKey<FormState>();
  late final _name = TextEditingController(text: widget.initialName);
  late String? _categoryId = widget.initialCategoryId;

  @override
  void dispose() {
    _name.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) {
      return;
    }

    final sent = await ref
        .read(skillActionsProvider.notifier)
        .propose(categoryId: _categoryId!, name: _name.text);
    if (!mounted) {
      return;
    }

    if (sent) {
      Navigator.pop(context, true);
    } else if (isInteractionRequirementError(ref.read(skillActionsProvider).error)) {
      Navigator.pop(context, false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final action = ref.watch(skillActionsProvider);
    final categories = ref.watch(skillCategoriesProvider).value ?? const <SkillCategory>[];

    return AlertDialog(
      title: Text(l10n.skillProposeAction),
      content: Form(
        key: _formKey,
        child: SingleChildScrollView(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Text(l10n.skillProposeExplanation),
              const SizedBox(height: AppSpacing.md),
              TextFormField(
                controller: _name,
                maxLength: _nameMax,
                textCapitalization: TextCapitalization.sentences,
                validator: (value) =>
                    (value?.trim().length ?? 0) < _nameMin ? l10n.validationSkillName : null,
                decoration: InputDecoration(labelText: l10n.skillProposeNameLabel),
              ),
              const SizedBox(height: AppSpacing.sm),
              DropdownButtonFormField<String>(
                initialValue: _categoryId,
                isExpanded: true,
                items: [
                  for (final category in categories)
                    DropdownMenuItem(value: category.id, child: Text(category.name)),
                ],
                onChanged: (value) => setState(() => _categoryId = value),
                validator: (value) => value == null ? l10n.validationRequired : null,
                decoration: InputDecoration(labelText: l10n.skillProposeCategoryLabel),
              ),
              if (action.hasError && !action.isLoading && !isInteractionRequirementError(action.error)) ...[
                const SizedBox(height: AppSpacing.md),
                Text(
                  errorMessage(l10n, action.error!),
                  style: TextStyle(color: Theme.of(context).colorScheme.error),
                ),
              ],
            ],
          ),
        ),
      ),
      actions: [
        TextButton(
          onPressed: action.isLoading ? null : () => Navigator.pop(context),
          child: Text(l10n.actionCancel),
        ),
        FilledButton(
          onPressed: action.isLoading ? null : _submit,
          child: Text(l10n.skillProposeSubmit),
        ),
      ],
    );
  }
}

/// The user's own skill proposals and what became of them.
class SkillProposalsScreen extends ConsumerWidget {
  const SkillProposalsScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppLocalizations.of(context);
    final proposals = ref.watch(ownSkillProposalsProvider);

    return Scaffold(
      appBar: AppBar(title: Text(l10n.skillProposalsTitle)),
      body: SafeArea(
        child: switch (proposals) {
          AsyncData(:final value) when value.isEmpty => Center(
              child: Padding(
                padding: const EdgeInsets.all(AppSpacing.lg),
                child: Text(l10n.skillProposalsEmpty, textAlign: TextAlign.center),
              ),
            ),
          AsyncData(:final value) => ListView(
              children: [
                for (final skill in value)
                  ListTile(
                    title: Text(skill.name),
                    trailing: Text(skillStatusLabel(l10n, skill.status)),
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
                      onPressed: () => ref.invalidate(ownSkillProposalsProvider),
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
