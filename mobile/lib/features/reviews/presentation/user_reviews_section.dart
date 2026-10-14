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
import '../../matches/presentation/match_labels.dart';
import '../../moderation/presentation/moderation_menu.dart';
import 'review_controller.dart';

/// A user's average rating and how many reviews it rests on, e.g. "8,5 · 3 değerlendirme".
class RatingLabel extends StatelessWidget {
  const RatingLabel({required this.rating, this.alignment = MainAxisAlignment.start, super.key});

  final RatingSummary rating;
  final MainAxisAlignment alignment;

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final theme = Theme.of(context);
    final average = rating.averageRating;
    final style = theme.textTheme.bodyMedium?.copyWith(color: theme.colorScheme.onSurfaceVariant);

    return Row(
      mainAxisAlignment: alignment,
      mainAxisSize: MainAxisSize.min,
      children: [
        Icon(
          average == null ? Icons.star_outline : Icons.star,
          size: AppSpacing.md,
          color: average == null ? theme.colorScheme.onSurfaceVariant : theme.colorScheme.primary,
        ),
        const SizedBox(width: AppSpacing.xs),
        Text(average == null ? l10n.ratingNone : l10n.ratingSummary(average, rating.reviewCount), style: style),
      ],
    );
  }
}

/// Offers the review form for a user, and shows nothing unless the signed-in user can
/// review them: after a completed session, once.
class ReviewPromptButton extends ConsumerWidget {
  const ReviewPromptButton({required this.userId, super.key});

  final String userId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppLocalizations.of(context);
    final canReview = ref.watch(reviewEligibilityProvider(userId)).value?.canReview ?? false;
    if (!canReview) {
      return const SizedBox.shrink();
    }

    return FilledButton.tonalIcon(
      onPressed: () async {
        final requirements = await ref.read(interactionRequirementsProvider.future);
        if (!context.mounted) {
          return;
        }

        if (requirements?.canInteract ?? false) {
          context.go(AppRoutes.writeReview(userId));
        } else {
          await showInteractionRequiredDialog(context);
        }
      },
      icon: const Icon(Icons.rate_review_outlined),
      label: Text(l10n.reviewWrite),
    );
  }
}

/// The reviews a user has received, under their average.
class UserReviewsSection extends ConsumerWidget {
  const UserReviewsSection({required this.userId, required this.rating, super.key});

  final String userId;
  final RatingSummary rating;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppLocalizations.of(context);
    final theme = Theme.of(context);
    final reviews = ref.watch(userReviewsProvider(userId));
    final ownUserId = ref.watch(sessionControllerProvider).value?.id;
    final busy = ref.watch(reviewActionsProvider).isLoading;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Text(l10n.reviewsTitle, style: theme.textTheme.titleMedium),
        const SizedBox(height: AppSpacing.xs),
        RatingLabel(rating: rating),
        if (userId != ownUserId) ...[
          const SizedBox(height: AppSpacing.sm),
          ReviewPromptButton(userId: userId),
        ],
        const SizedBox(height: AppSpacing.sm),
        if (busy) const LinearProgressIndicator(),
        switch (reviews) {
          // The label above already says that there are none.
          AsyncValue(:final value?) when value.isEmpty => const SizedBox.shrink(),
          AsyncValue(:final value?) => Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                for (final review in value)
                  _ReviewCard(review: review, isOwn: review.reviewer.userId == ownUserId, busy: busy),
              ],
            ),
          AsyncError() => Column(
              children: [
                Text(l10n.reviewsUnavailable, textAlign: TextAlign.center),
                TextButton(onPressed: () => ref.invalidate(userReviewsProvider(userId)), child: Text(l10n.errorRetry)),
              ],
            ),
          _ => const Center(child: CircularProgressIndicator()),
        },
      ],
    );
  }
}

class _ReviewCard extends ConsumerWidget {
  const _ReviewCard({required this.review, required this.isOwn, required this.busy});

  final Review review;
  final bool isOwn;
  final bool busy;

  Future<void> _delete(BuildContext context, WidgetRef ref) async {
    final l10n = AppLocalizations.of(context);
    final messenger = ScaffoldMessenger.of(context);
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: Text(l10n.reviewDeleteTitle),
        content: Text(l10n.reviewDeleteBody),
        actions: [
          TextButton(onPressed: () => Navigator.pop(dialogContext, false), child: Text(l10n.actionCancel)),
          FilledButton(onPressed: () => Navigator.pop(dialogContext, true), child: Text(l10n.reviewDeleteConfirm)),
        ],
      ),
    );
    if (confirmed != true) {
      return;
    }

    final deleted = await ref.read(reviewActionsProvider.notifier).delete(review.id);
    messenger.showSnackBar(
      SnackBar(
        content: Text(
          deleted ? l10n.reviewDeleted : errorMessage(l10n, ref.read(reviewActionsProvider).error ?? Object()),
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppLocalizations.of(context);
    final theme = Theme.of(context);
    final muted = theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.onSurfaceVariant);

    return Card(
      margin: const EdgeInsets.only(bottom: AppSpacing.sm),
      child: Padding(
        padding: const EdgeInsets.all(AppSpacing.md),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Row(
              children: [
                Expanded(child: Text(userLabel(l10n, review.reviewer), style: theme.textTheme.titleSmall)),
                Text(l10n.reviewRatingOutOf(review.rating), style: theme.textTheme.titleSmall),
                if (!isOwn)
                  PopupMenuButton<void>(
                    itemBuilder: (menuContext) => [
                      PopupMenuItem(
                        onTap: () => reportContent(context, ref, targetType: ReportTargetType.review, targetId: review.id),
                        child: Text(l10n.moderationReport),
                      ),
                    ],
                  ),
              ],
            ),
            Text(MaterialLocalizations.of(context).formatMediumDate(review.createdAt.toLocal()), style: muted),
            if (review.comment != null) ...[
              const SizedBox(height: AppSpacing.sm),
              Text(review.comment!, style: theme.textTheme.bodyMedium),
            ],
            if (isOwn)
              Align(
                alignment: Alignment.centerRight,
                child: TextButton(
                  onPressed: busy ? null : () => _delete(context, ref),
                  child: Text(l10n.reviewDelete),
                ),
              ),
          ],
        ),
      ),
    );
  }
}
