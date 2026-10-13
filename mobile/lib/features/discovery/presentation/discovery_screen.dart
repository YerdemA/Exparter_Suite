import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/api/error_messages.dart';
import '../../../core/api/generated/export.dart';
import '../../../core/router/app_routes.dart';
import '../../../core/theme/app_tokens.dart';
import '../../../l10n/app_localizations.dart';
import '../../profile/data/api_profile_repository.dart';
import '../../profile/presentation/widgets/profile_avatar.dart';
import '../../skills/presentation/skill_controller.dart';
import '../../skills/presentation/skill_labels.dart';
import '../domain/discovery_repository.dart';
import 'discovery_controller.dart';
import 'discovery_labels.dart';

/// Other users, best fit first, with search and filters.
class DiscoveryScreen extends ConsumerStatefulWidget {
  const DiscoveryScreen({super.key});

  @override
  ConsumerState<DiscoveryScreen> createState() => _DiscoveryScreenState();
}

class _DiscoveryScreenState extends ConsumerState<DiscoveryScreen> {
  static const _searchDelay = Duration(milliseconds: 350);

  /// How close to the end of the list the next page starts loading.
  static const _loadMoreDistance = 600.0;

  final _search = TextEditingController();
  final _scroll = ScrollController();
  Timer? _debounce;
  DiscoveryFilters _filters = noDiscoveryFilters;

  /// The skill behind `_filters.skillId`, kept for its name.
  Skill? _skill;

  @override
  void initState() {
    super.initState();
    _scroll.addListener(_onScroll);
  }

  @override
  void dispose() {
    _debounce?.cancel();
    _search.dispose();
    _scroll.dispose();
    super.dispose();
  }

  void _onScroll() {
    if (_scroll.position.extentAfter < _loadMoreDistance) {
      ref.read(discoveryFeedProvider(_filters).notifier).loadMore();
    }
  }

  void _onSearchChanged(String value) {
    // Waiting for a pause in typing avoids one request per keystroke.
    _debounce?.cancel();
    _debounce = Timer(_searchDelay, () => _setFilters((query: value.trim(), skillId: _filters.skillId, categoryId: _filters.categoryId, city: _filters.city, mode: _filters.mode)));
  }

  void _setFilters(DiscoveryFilters filters) {
    if (mounted) {
      setState(() => _filters = filters);
    }
  }

  Future<void> _pickCity(List<City> cities) async {
    final l10n = AppLocalizations.of(context);
    // An empty code stands for "all cities"; null means the dialog was dismissed.
    final code = await showDialog<String>(
      context: context,
      builder: (context) => SimpleDialog(
        title: Text(l10n.profileCityLabel),
        children: [
          SimpleDialogOption(onPressed: () => Navigator.pop(context, ''), child: Text(l10n.discoverAllCities)),
          for (final city in cities)
            SimpleDialogOption(onPressed: () => Navigator.pop(context, city.code), child: Text(city.name)),
        ],
      ),
    );
    if (code != null) {
      _setFilters((query: _filters.query, skillId: _filters.skillId, categoryId: _filters.categoryId, city: code.isEmpty ? null : code, mode: _filters.mode));
    }
  }

  Future<void> _pickSkill() async {
    final choice = await showDialog<({Skill? skill})>(
      context: context,
      builder: (context) => const _SkillFilterDialog(),
    );
    if (choice != null) {
      _skill = choice.skill;
      _setFilters(
        (query: _filters.query, skillId: choice.skill?.id, categoryId: _filters.categoryId, city: _filters.city, mode: _filters.mode),
      );
    }
  }

  void _toggleMode(SessionMode mode) => _setFilters(
        (query: _filters.query, skillId: _filters.skillId, categoryId: _filters.categoryId, city: _filters.city, mode: _filters.mode == mode ? null : mode),
      );

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final categories = ref.watch(skillCategoriesProvider).value ?? const <SkillCategory>[];
    final cities = ref.watch(citiesProvider).value ?? const <City>[];
    final feed = ref.watch(discoveryFeedProvider(_filters));
    final selectedCity = cities.where((city) => city.code == _filters.city).firstOrNull;

    return Scaffold(
      appBar: AppBar(title: Text(l10n.navDiscover)),
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
                  labelText: l10n.discoverSearchLabel,
                  prefixIcon: const Icon(Icons.search),
                ),
              ),
            ),
            SizedBox(
              height: AppSizes.control,
              child: ListView(
                scrollDirection: Axis.horizontal,
                padding: const EdgeInsets.symmetric(horizontal: AppSpacing.md),
                children: [
                  if (cities.isNotEmpty)
                    ActionChip(
                      avatar: const Icon(Icons.place_outlined),
                      label: Text(selectedCity?.name ?? l10n.discoverAllCities),
                      onPressed: () => _pickCity(cities),
                    ),
                  const SizedBox(width: AppSpacing.sm),
                  ActionChip(
                    avatar: const Icon(Icons.school_outlined),
                    label: Text(_skill?.name ?? l10n.discoverAllSkills),
                    onPressed: _pickSkill,
                  ),
                  const SizedBox(width: AppSpacing.sm),
                  ChoiceChip(
                    label: Text(l10n.profileModeOnline),
                    selected: _filters.mode == SessionMode.online,
                    onSelected: (_) => _toggleMode(SessionMode.online),
                  ),
                  const SizedBox(width: AppSpacing.sm),
                  ChoiceChip(
                    label: Text(l10n.profileModeInPerson),
                    selected: _filters.mode == SessionMode.inPerson,
                    onSelected: (_) => _toggleMode(SessionMode.inPerson),
                  ),
                  for (final category in categories) ...[
                    const SizedBox(width: AppSpacing.sm),
                    ChoiceChip(
                      label: Text(category.name),
                      selected: category.id == _filters.categoryId,
                      onSelected: (selected) => _setFilters(
                        (query: _filters.query, skillId: _filters.skillId, categoryId: selected ? category.id : null, city: _filters.city, mode: _filters.mode),
                      ),
                    ),
                  ],
                ],
              ),
            ),
            Expanded(
              child: switch (feed) {
                AsyncData(:final value) => RefreshIndicator(
                    onRefresh: () => ref.refresh(discoveryFeedProvider(_filters).future),
                    child: ListView.builder(
                      controller: _scroll,
                      // Keeps pull-to-refresh working when the list is short or empty.
                      physics: const AlwaysScrollableScrollPhysics(),
                      padding: const EdgeInsets.all(AppSpacing.md),
                      itemCount: value.users.length + 1,
                      itemBuilder: (context, index) => index < value.users.length
                          ? _UserCard(
                              item: value.users[index],
                              cityName: cities
                                  .where((city) => city.code == value.users[index].city)
                                  .firstOrNull
                                  ?.name,
                            )
                          : _Footer(
                              state: value,
                              searching: _filters.query.isNotEmpty,
                              onRetry: () => ref.read(discoveryFeedProvider(_filters).notifier).loadMore(),
                            ),
                    ),
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
                            onPressed: () => ref.invalidate(discoveryFeedProvider(_filters)),
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

/// Lets the user search the catalog for the skill to filter by. Pops with the chosen
/// skill, or with a null skill for "all skills"; dismissing it pops with nothing.
class _SkillFilterDialog extends ConsumerStatefulWidget {
  const _SkillFilterDialog();

  @override
  ConsumerState<_SkillFilterDialog> createState() => _SkillFilterDialogState();
}

class _SkillFilterDialogState extends ConsumerState<_SkillFilterDialog> {
  static const _searchDelay = Duration(milliseconds: 350);
  static const _listHeight = 320.0;

  Timer? _debounce;
  String _query = '';

  @override
  void dispose() {
    _debounce?.cancel();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final results = ref.watch(skillSearchProvider(categoryId: null, query: _query));

    return AlertDialog(
      title: Text(l10n.discoverSkillFilterTitle),
      contentPadding: const EdgeInsets.symmetric(vertical: AppSpacing.md),
      content: SizedBox(
        width: double.maxFinite,
        height: _listHeight,
        child: Column(
          children: [
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: AppSpacing.lg),
              child: TextField(
                autofocus: true,
                textInputAction: TextInputAction.search,
                decoration: InputDecoration(labelText: l10n.skillSearchLabel, prefixIcon: const Icon(Icons.search)),
                onChanged: (value) {
                  _debounce?.cancel();
                  _debounce = Timer(_searchDelay, () {
                    if (mounted) {
                      setState(() => _query = value.trim());
                    }
                  });
                },
              ),
            ),
            Expanded(
              child: switch (results) {
                AsyncData(:final value) => ListView(
                    children: [
                      ListTile(
                        leading: const Icon(Icons.clear_all),
                        title: Text(l10n.discoverAllSkills),
                        onTap: () => Navigator.pop(context, (skill: null)),
                      ),
                      for (final skill in value.items)
                        ListTile(title: Text(skill.name), onTap: () => Navigator.pop(context, (skill: skill))),
                      if (value.items.isEmpty)
                        Padding(
                          padding: const EdgeInsets.all(AppSpacing.lg),
                          child: Text(l10n.skillSearchEmpty, textAlign: TextAlign.center),
                        ),
                    ],
                  ),
                AsyncError(:final error) => Center(
                    child: Padding(
                      padding: const EdgeInsets.all(AppSpacing.lg),
                      child: Text(errorMessage(l10n, error), textAlign: TextAlign.center),
                    ),
                  ),
                _ => const Center(child: CircularProgressIndicator()),
              },
            ),
          ],
        ),
      ),
      actions: [TextButton(onPressed: () => Navigator.pop(context), child: Text(l10n.actionCancel))],
    );
  }
}

/// What follows the last card: the empty message, a spinner, or a retry.
class _Footer extends StatelessWidget {
  const _Footer({required this.state, required this.searching, required this.onRetry});

  final DiscoveryFeedState state;
  final bool searching;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);

    if (state.users.isEmpty) {
      return Padding(
        padding: const EdgeInsets.all(AppSpacing.lg),
        child: Text(searching ? l10n.discoverSearchEmpty : l10n.discoverEmpty, textAlign: TextAlign.center),
      );
    }

    if (state.loadMoreFailed) {
      return Column(
        children: [
          Text(l10n.discoverLoadMoreFailed, textAlign: TextAlign.center),
          TextButton(onPressed: onRetry, child: Text(l10n.errorRetry)),
        ],
      );
    }

    return state.loadingMore
        ? const Padding(
            padding: EdgeInsets.all(AppSpacing.md),
            child: Center(child: CircularProgressIndicator()),
          )
        : const SizedBox.shrink();
  }
}

class _UserCard extends StatelessWidget {
  const _UserCard({required this.item, required this.cityName});

  static const _avatarSize = 56.0;

  final DiscoveryUser item;
  final String? cityName;

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final theme = Theme.of(context);
    final muted = theme.textTheme.bodyMedium?.copyWith(color: theme.colorScheme.onSurfaceVariant);
    final user = item.user;
    final username = user.username == null ? null : '@${user.username}';
    final modes = swapModesLabel(
      l10n,
      acceptsOnline: item.acceptsOnline,
      acceptsInPerson: item.acceptsInPerson,
      cityName: cityName,
    );
    final badge = switch (item.compatibility) {
      Compatibility.mutual => l10n.discoverMutual,
      Compatibility.oneWay => l10n.discoverOneWay,
      _ => null,
    };

    return Card(
      margin: const EdgeInsets.only(bottom: AppSpacing.md),
      clipBehavior: Clip.antiAlias,
      child: InkWell(
        onTap: () => context.go(AppRoutes.userProfile(user.userId)),
        child: Padding(
          padding: const EdgeInsets.all(AppSpacing.md),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  ProfileAvatar(name: user.displayName ?? user.username, imageUrl: user.avatarUrl, size: _avatarSize),
                  const SizedBox(width: AppSpacing.md),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(user.displayName ?? username ?? '', style: theme.textTheme.titleMedium),
                        if (user.displayName != null && username != null) Text(username, style: muted),
                        if (modes != null) Text(modes, style: muted),
                      ],
                    ),
                  ),
                ],
              ),
              if (badge != null) ...[
                const SizedBox(height: AppSpacing.sm),
                Chip(
                  avatar: Icon(
                    item.compatibility == Compatibility.mutual ? Icons.swap_horiz : Icons.arrow_forward,
                  ),
                  label: Text(badge),
                  visualDensity: VisualDensity.compact,
                ),
              ],
              if (item.offeredSkills.isNotEmpty) ...[
                const SizedBox(height: AppSpacing.sm),
                _SkillLine(label: l10n.discoverOffers, skills: item.offeredSkills, type: UserSkillType.offered),
              ],
              if (item.wantedSkills.isNotEmpty) ...[
                const SizedBox(height: AppSpacing.xs),
                _SkillLine(label: l10n.discoverWants, skills: item.wantedSkills, type: UserSkillType.wanted),
              ],
            ],
          ),
        ),
      ),
    );
  }
}

class _SkillLine extends StatelessWidget {
  const _SkillLine({required this.label, required this.skills, required this.type});

  final String label;
  final List<DiscoveryUserSkill> skills;
  final UserSkillType type;

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final theme = Theme.of(context);

    return Text.rich(
      TextSpan(
        children: [
          TextSpan(text: '$label: ', style: const TextStyle(fontWeight: FontWeight.w700)),
          TextSpan(
            text: skills
                .map((item) => '${item.skill.name} · ${userSkillLevelLabel(l10n, type, item.level)}')
                .join(', '),
          ),
        ],
      ),
      style: theme.textTheme.bodyMedium,
    );
  }
}
