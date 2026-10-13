import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/api/error_messages.dart';
import '../../../core/router/app_routes.dart';
import '../../../core/theme/app_tokens.dart';
import '../../../l10n/app_localizations.dart';
import 'review_controller.dart';

/// Lets the user review another user: a score from 1 to 10 and an optional comment.
class WriteReviewScreen extends ConsumerStatefulWidget {
  const WriteReviewScreen({required this.userId, super.key});

  final String userId;

  @override
  ConsumerState<WriteReviewScreen> createState() => _WriteReviewScreenState();
}

class _WriteReviewScreenState extends ConsumerState<WriteReviewScreen> {
  final _comment = TextEditingController();
  int? _rating;

  @override
  void dispose() {
    _comment.dispose();
    super.dispose();
  }

  Future<void> _submit(int rating) async {
    final l10n = AppLocalizations.of(context);
    final messenger = ScaffoldMessenger.of(context);
    final sent = await ref
        .read(reviewActionsProvider.notifier)
        .create(widget.userId, rating: rating, comment: _comment.text);
    if (!mounted) {
      return;
    }

    if (sent) {
      messenger.showSnackBar(SnackBar(content: Text(l10n.reviewSent)));
      context.go(AppRoutes.userProfile(widget.userId));
    } else {
      messenger.showSnackBar(
        SnackBar(content: Text(errorMessage(l10n, ref.read(reviewActionsProvider).error ?? Object()))),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final theme = Theme.of(context);
    final busy = ref.watch(reviewActionsProvider).isLoading;
    final rating = _rating;

    return Scaffold(
      appBar: AppBar(title: Text(l10n.reviewWriteTitle)),
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
                  Text(l10n.reviewRatingLabel, style: theme.textTheme.titleMedium),
                  const SizedBox(height: AppSpacing.sm),
                  Wrap(
                    spacing: AppSpacing.sm,
                    runSpacing: AppSpacing.sm,
                    children: [
                      for (var value = reviewMinRating; value <= reviewMaxRating; value++)
                        ChoiceChip(
                          label: Text('$value'),
                          selected: value == rating,
                          onSelected: (_) => setState(() => _rating = value),
                        ),
                    ],
                  ),
                  const SizedBox(height: AppSpacing.lg),
                  TextField(
                    controller: _comment,
                    maxLength: reviewCommentMaxLength,
                    minLines: 3,
                    maxLines: 6,
                    decoration: InputDecoration(labelText: l10n.reviewCommentLabel),
                  ),
                  const SizedBox(height: AppSpacing.sm),
                  Text(l10n.reviewNotice, style: theme.textTheme.bodySmall),
                  const SizedBox(height: AppSpacing.md),
                  if (busy) const LinearProgressIndicator(),
                  FilledButton(
                    onPressed: busy || rating == null ? null : () => _submit(rating),
                    child: Text(l10n.reviewSubmit),
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
