import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/api/error_messages.dart';
import '../../../core/api/generated/export.dart';
import '../../../core/auth/interaction_requirements.dart';
import '../../../core/auth/session_controller.dart';
import '../../../core/router/app_routes.dart';
import '../../../core/theme/app_tokens.dart';
import '../../../l10n/app_localizations.dart';
import '../../profile/data/api_profile_repository.dart';
import '../../profile/presentation/widgets/profile_avatar.dart';
import '../../reviews/presentation/user_reviews_section.dart';
import '../../sessions/presentation/user_stats_card.dart';
import '../../skills/presentation/skill_labels.dart';
import 'discovery_controller.dart';
import 'discovery_labels.dart';

/// Another user's profile and skills, read-only.
class UserProfileScreen extends ConsumerWidget {
  const UserProfileScreen({required this.userId, super.key});

  static const _avatarSize = 112.0;

  final String userId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppLocalizations.of(context);
    final profile = ref.watch(userProfileProvider(userId));
    final skills = ref.watch(userSkillsProvider(userId));
    final error = profile.error ?? skills.error;

    return Scaffold(
      appBar: AppBar(title: Text(l10n.navProfile)),
      body: SafeArea(
        child: switch ((profile.value, skills.value)) {
          (final PublicProfile loadedProfile, final List<UserSkill> loadedSkills) when error == null =>
            _Content(profile: loadedProfile, skills: loadedSkills),
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
                        ref.invalidate(userProfileProvider(userId));
                        ref.invalidate(userSkillsProvider(userId));
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

class _Content extends ConsumerWidget {
  const _Content({required this.profile, required this.skills});

  final PublicProfile profile;
  final List<UserSkill> skills;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppLocalizations.of(context);
    final theme = Theme.of(context);
    final muted = theme.textTheme.bodyLarge?.copyWith(color: theme.colorScheme.onSurfaceVariant);
    final cityName = profile.city == null
        ? null
        : ref.watch(citiesProvider).value?.where((city) => city.code == profile.city).firstOrNull?.name;
    final modes = swapModesLabel(
      l10n,
      acceptsOnline: profile.acceptsOnline,
      acceptsInPerson: profile.acceptsInPerson,
      cityName: cityName,
    );
    final offered = skills.where((skill) => skill.type == UserSkillType.offered).toList();
    final isOwnProfile = ref.watch(sessionControllerProvider).value?.id == profile.userId;
    final wanted = skills.where((skill) => skill.type == UserSkillType.wanted).toList();

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
                  name: profile.displayName ?? profile.username,
                  imageUrl: profile.avatarUrl,
                  size: UserProfileScreen._avatarSize,
                ),
              ),
              const SizedBox(height: AppSpacing.md),
              if (profile.displayName != null)
                Text(profile.displayName!, textAlign: TextAlign.center, style: theme.textTheme.headlineMedium),
              Text('@${profile.username}', textAlign: TextAlign.center, style: muted),
              if (modes != null) ...[
                const SizedBox(height: AppSpacing.xs),
                Text(modes, textAlign: TextAlign.center, style: muted),
              ],
              if (profile.displayName == null) ...[
                const SizedBox(height: AppSpacing.lg),
                Text(l10n.userProfileIncomplete, textAlign: TextAlign.center),
              ],
              if (profile.bio != null) ...[
                const SizedBox(height: AppSpacing.lg),
                Text(profile.bio!, style: theme.textTheme.bodyLarge),
              ],
              const SizedBox(height: AppSpacing.lg),
              UserStatsCard(userId: profile.userId),
              if (!isOwnProfile) ...[
                const SizedBox(height: AppSpacing.lg),
                FilledButton.icon(
                  // A user with nothing to teach cannot be asked for a swap.
                  onPressed: offered.isEmpty
                      ? null
                      : () async {
                          final requirements = await ref.read(interactionRequirementsProvider.future);
                          if (!context.mounted) {
                            return;
                          }

                          if (requirements?.canInteract ?? false) {
                            context.go(AppRoutes.sendRequest(profile.userId));
                          } else {
                            await showInteractionRequiredDialog(context);
                          }
                        },
                  icon: const Icon(Icons.swap_horiz),
                  label: Text(l10n.requestSend),
                ),
              ],
              const SizedBox(height: AppSpacing.lg),
              _SkillGroup(title: l10n.userOfferedTitle, skills: offered),
              const SizedBox(height: AppSpacing.lg),
              _SkillGroup(title: l10n.userWantedTitle, skills: wanted),
              const SizedBox(height: AppSpacing.lg),
              UserReviewsSection(userId: profile.userId, rating: profile.rating),
            ],
          ),
        ),
      ),
    );
  }
}

class _SkillGroup extends StatelessWidget {
  const _SkillGroup({required this.title, required this.skills});

  final String title;
  final List<UserSkill> skills;

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
          Text(l10n.userSkillsEmpty, style: theme.textTheme.bodyMedium)
        else
          Wrap(
            spacing: AppSpacing.sm,
            runSpacing: AppSpacing.sm,
            children: [
              for (final skill in skills)
                Chip(label: Text('${skill.skill.name} · ${userSkillLevelLabel(l10n, skill.type, skill.level)}')),
            ],
          ),
      ],
    );
  }
}
