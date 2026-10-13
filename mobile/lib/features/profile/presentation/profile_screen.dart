import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/api/error_messages.dart';
import '../../../core/api/generated/export.dart';
import '../../../core/auth/interaction_requirements.dart';
import '../../../core/auth/session_controller.dart';
import '../../../core/router/app_routes.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/theme/app_tokens.dart';
import '../../../l10n/app_localizations.dart';
import '../../auth/presentation/auth_controller.dart';
import '../../auth/presentation/recovery_controller.dart';
import '../../reviews/presentation/user_reviews_section.dart';
import '../../sessions/presentation/user_stats_card.dart';
import '../../skills/presentation/own_skills_section.dart';
import '../data/api_profile_repository.dart';
import '../data/avatar_picker.dart';
import 'profile_controller.dart';
import 'widgets/profile_avatar.dart';

/// The signed-in user's own profile and account.
class ProfileScreen extends ConsumerWidget {
  const ProfileScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppLocalizations.of(context);
    final profile = ref.watch(ownProfileProvider);

    return Scaffold(
      appBar: AppBar(title: Text(l10n.navProfile)),
      body: SafeArea(
        child: switch (profile) {
          AsyncData(:final value?) => _ProfileBody(profile: value),
          AsyncError(:final error) => Center(
              child: Padding(
                padding: const EdgeInsets.all(AppSpacing.lg),
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Text(errorMessage(l10n, error), textAlign: TextAlign.center),
                    const SizedBox(height: AppSpacing.md),
                    OutlinedButton(
                      onPressed: () => ref.invalidate(ownProfileProvider),
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

class _ProfileBody extends ConsumerWidget {
  const _ProfileBody({required this.profile});

  static const _avatarSize = 112.0;

  final Profile profile;

  Future<void> _changeAvatar(BuildContext context, WidgetRef ref) async {
    final l10n = AppLocalizations.of(context);
    final messenger = ScaffoldMessenger.of(context);
    final choice = await showModalBottomSheet<_AvatarChoice>(
      context: context,
      showDragHandle: true,
      builder: (context) => SafeArea(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            ListTile(
              leading: const Icon(Icons.photo_library_outlined),
              title: Text(l10n.avatarFromGallery),
              onTap: () => Navigator.pop(context, _AvatarChoice.gallery),
            ),
            ListTile(
              leading: const Icon(Icons.photo_camera_outlined),
              title: Text(l10n.avatarFromCamera),
              onTap: () => Navigator.pop(context, _AvatarChoice.camera),
            ),
            if (profile.avatarUrl != null)
              ListTile(
                leading: const Icon(Icons.delete_outline),
                title: Text(l10n.avatarRemove),
                onTap: () => Navigator.pop(context, _AvatarChoice.remove),
              ),
          ],
        ),
      ),
    );
    if (choice == null) {
      return;
    }

    final actions = ref.read(profileActionsProvider.notifier);
    final bool succeeded;
    if (choice == _AvatarChoice.remove) {
      succeeded = await actions.removeAvatar();
    } else {
      final image = await ref.read(avatarPickerProvider)(
        choice == _AvatarChoice.camera ? AvatarSource.camera : AvatarSource.gallery,
      );
      if (image == null) {
        return;
      }

      succeeded = await actions.uploadAvatar(image);
    }

    if (!succeeded) {
      final error = ref.read(profileActionsProvider).error;
      messenger.showSnackBar(SnackBar(content: Text(errorMessage(l10n, error ?? Object()))));
    }
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppLocalizations.of(context);
    final theme = Theme.of(context);
    final brand = context.brand;
    final account = ref.watch(sessionControllerProvider).value;
    final busy = ref.watch(profileActionsProvider).isLoading;
    final loggingOut = ref.watch(authControllerProvider).isLoading;
    final restored = ref.watch(accountRestoredNoticeProvider);
    final requirements = ref.watch(interactionRequirementsProvider).value;
    final cityName = profile.city == null
        ? null
        : ref.watch(citiesProvider).value?.where((city) => city.code == profile.city).firstOrNull?.name;

    return Center(
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: AppSizes.formMaxWidth),
        // A plain column rather than a lazy list: the content is short, and every part
        // of it (name, skills, account) stays built while the user scrolls.
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(AppSpacing.lg),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Center(
                child: Stack(
                  alignment: Alignment.bottomRight,
                  children: [
                    ProfileAvatar(name: profile.displayName, imageUrl: profile.avatarUrl, size: _avatarSize),
                    if (busy)
                      const Positioned.fill(child: Center(child: CircularProgressIndicator()))
                    else
                      IconButton.filled(
                        tooltip: l10n.avatarChange,
                        onPressed: () => _changeAvatar(context, ref),
                        icon: const Icon(Icons.photo_camera_outlined),
                      ),
                  ],
                ),
              ),
              const SizedBox(height: AppSpacing.md),
              if (profile.displayName != null)
                Text(
                  profile.displayName!,
                  textAlign: TextAlign.center,
                  style: theme.textTheme.headlineMedium,
                ),
              if (account != null)
                Text(
                  '@${account.username}',
                  textAlign: TextAlign.center,
                  style: theme.textTheme.bodyLarge?.copyWith(color: theme.colorScheme.onSurfaceVariant),
                ),
              const SizedBox(height: AppSpacing.md),
              Wrap(
                alignment: WrapAlignment.center,
                spacing: AppSpacing.sm,
                runSpacing: AppSpacing.sm,
                children: [
                  if (profile.acceptsOnline)
                    Chip(avatar: const Icon(Icons.videocam_outlined), label: Text(l10n.profileModeOnline)),
                  if (profile.acceptsInPerson)
                    Chip(
                      avatar: const Icon(Icons.place_outlined),
                      label: Text(cityName == null ? l10n.profileModeInPerson : l10n.profileModeInPersonIn(cityName)),
                    ),
                ],
              ),
              if (profile.bio != null) ...[
                const SizedBox(height: AppSpacing.md),
                Text(profile.bio!, style: theme.textTheme.bodyLarge),
              ],
              const SizedBox(height: AppSpacing.lg),
              if (requirements != null && !requirements.canInteract) ...[
                _RequirementsCard(requirements: requirements),
                const SizedBox(height: AppSpacing.md),
              ],
              FilledButton.icon(
                onPressed: () => context.go(AppRoutes.profileEdit),
                icon: const Icon(Icons.edit_outlined),
                label: Text(profile.isComplete ? l10n.profileEdit : l10n.profileFillIn),
              ),
              const SizedBox(height: AppSpacing.xl),
              if (account != null) ...[
                UserStatsCard(userId: account.id),
                const SizedBox(height: AppSpacing.lg),
              ],
              const OwnSkillsSection(),
              const SizedBox(height: AppSpacing.xl),
              UserReviewsSection(userId: profile.userId, rating: profile.rating),
              const SizedBox(height: AppSpacing.xl),
              if (restored) ...[
                _StatusTile(icon: Icons.restore, color: brand.success, text: l10n.homeAccountRestored),
                const SizedBox(height: AppSpacing.sm),
              ],
              if (account != null) ...[
                _StatusTile(
                  icon: account.emailVerified ? Icons.verified_outlined : Icons.mark_email_unread_outlined,
                  color: account.emailVerified ? brand.success : brand.warning,
                  text: account.emailVerified ? l10n.homeEmailVerified : l10n.homeEmailNotVerified,
                ),
                if (!account.emailVerified) const _ResendVerificationButton(),
              ],
              const SizedBox(height: AppSpacing.lg),
              OutlinedButton.icon(
                onPressed: loggingOut
                    ? null
                    : () {
                        ref.read(accountRestoredNoticeProvider.notifier).dismiss();
                        ref.read(authControllerProvider.notifier).logout();
                      },
                icon: const Icon(Icons.logout),
                label: Text(l10n.homeLogout),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

enum _AvatarChoice { gallery, camera, remove }

/// Tells a user who cannot interact with others yet what is still missing.
class _RequirementsCard extends StatelessWidget {
  const _RequirementsCard({required this.requirements});

  final InteractionRequirements requirements;

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final theme = Theme.of(context);
    final brand = context.brand;

    Widget row(bool done, String text) => Padding(
          padding: const EdgeInsets.only(top: AppSpacing.sm),
          child: Row(
            children: [
              Icon(
                done ? Icons.check_circle : Icons.radio_button_unchecked,
                color: done ? brand.success : theme.colorScheme.onSurfaceVariant,
                semanticLabel: done ? l10n.requirementDone : l10n.requirementMissing,
              ),
              const SizedBox(width: AppSpacing.sm),
              Expanded(child: Text(text)),
            ],
          ),
        );

    return Container(
      padding: const EdgeInsets.all(AppSpacing.md),
      decoration: BoxDecoration(
        color: brand.warning.withValues(alpha: 0.12),
        borderRadius: BorderRadius.circular(AppRadius.md),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(l10n.interactionRequirements, style: theme.textTheme.titleMedium),
          row(requirements.profileCompleted, l10n.requirementProfile),
          row(requirements.hasOfferedSkill, l10n.requirementSkill),
          row(requirements.emailVerified, l10n.requirementEmail),
        ],
      ),
    );
  }
}

class _ResendVerificationButton extends ConsumerWidget {
  const _ResendVerificationButton();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppLocalizations.of(context);
    final action = ref.watch(recoveryControllerProvider);

    return Align(
      alignment: Alignment.centerLeft,
      child: TextButton.icon(
        onPressed: action.isLoading
            ? null
            : () async {
                final messenger = ScaffoldMessenger.of(context);
                final sent = await ref.read(recoveryControllerProvider.notifier).resendVerificationEmail();
                final error = ref.read(recoveryControllerProvider).error;
                messenger.showSnackBar(
                  SnackBar(
                    content: Text(sent ? l10n.homeVerificationSent : errorMessage(l10n, error ?? Object())),
                  ),
                );
              },
        icon: const Icon(Icons.send_outlined),
        label: Text(l10n.homeResendVerification),
      ),
    );
  }
}

class _StatusTile extends StatelessWidget {
  const _StatusTile({required this.icon, required this.color, required this.text});

  final IconData icon;
  final Color color;
  final String text;

  @override
  Widget build(BuildContext context) => Container(
        padding: const EdgeInsets.all(AppSpacing.md),
        decoration: BoxDecoration(
          color: color.withValues(alpha: 0.12),
          borderRadius: BorderRadius.circular(AppRadius.md),
        ),
        child: Row(
          children: [
            Icon(icon, color: color),
            const SizedBox(width: AppSpacing.md),
            Expanded(child: Text(text)),
          ],
        ),
      );
}
