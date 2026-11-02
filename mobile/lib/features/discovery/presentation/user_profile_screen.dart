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
import '../../matches/presentation/match_controller.dart';
import '../../moderation/presentation/moderation_menu.dart';
import '../../profile/data/api_profile_repository.dart';
import '../../profile/presentation/widgets/profile_avatar.dart';
import '../../profile/presentation/widgets/swap_mode_chips.dart';
import '../../reviews/presentation/user_reviews_section.dart';
import '../../sessions/presentation/user_stats_card.dart';
import '../../skills/presentation/skill_labels.dart';
import 'discovery_controller.dart';

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

    final loaded = profile.value;

    return Scaffold(
      appBar: AppBar(
        title: Text(l10n.navProfile),
        actions: [
          if (loaded != null)
            UserModerationMenu(
              userId: userId,
              name: loaded.displayName ?? '@${loaded.username}',
              // Their profile is gone for the blocker too.
              onBlocked: () => context.canPop() ? context.pop() : context.go(AppRoutes.home),
            ),
        ],
      ),
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
              if (profile.acceptsOnline || profile.acceptsInPerson) ...[
                const SizedBox(height: AppSpacing.md),
                SwapModeChips(
                  acceptsOnline: profile.acceptsOnline,
                  acceptsInPerson: profile.acceptsInPerson,
                  cityName: cityName,
                ),
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
                _SwapAction(userId: profile.userId, hasOfferedSkills: offered.isNotEmpty),
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

/// What can be done about a swap with this user: a new request, or a way to what
/// the two already have going, since the server refuses a second one.
class _SwapAction extends ConsumerWidget {
  const _SwapAction({required this.userId, required this.hasOfferedSkills});

  final String userId;
  final bool hasOfferedSkills;

  Future<void> _sendRequest(BuildContext context, WidgetRef ref) async {
    final requirements = await ref.read(interactionRequirementsProvider.future);
    if (!context.mounted) {
      return;
    }

    if (requirements?.canInteract ?? false) {
      context.go(AppRoutes.sendRequest(userId));
    } else {
      await showInteractionRequiredDialog(context);
    }
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppLocalizations.of(context);
    final standing = ref.watch(swapStandingProvider(userId));

    return switch (standing) {
      AsyncData(value: OpenMatch(:final matchId)) => FilledButton.icon(
          onPressed: () => context.go(AppRoutes.matchDetail(matchId)),
          icon: const Icon(Icons.handshake_outlined),
          label: Text(l10n.matchView),
        ),
      AsyncData(value: RequestSent()) => FilledButton.icon(
          onPressed: null,
          icon: const Icon(Icons.schedule_send_outlined),
          label: Text(l10n.requestAlreadySent),
        ),
      AsyncData(value: RequestReceived()) => FilledButton.icon(
          onPressed: () => context.go(AppRoutes.incomingRequests),
          icon: const Icon(Icons.move_to_inbox_outlined),
          label: Text(l10n.requestViewIncoming),
        ),
      // If the answer did not arrive, a request stays possible; the server has the last word.
      _ => FilledButton.icon(
          // A user with nothing to teach cannot be asked for a swap.
          onPressed: standing.isLoading || !hasOfferedSkills ? null : () => _sendRequest(context, ref),
          icon: const Icon(Icons.swap_horiz),
          label: Text(l10n.requestSend),
        ),
    };
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
