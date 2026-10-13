import 'package:riverpod_annotation/riverpod_annotation.dart';

import '../../../core/api/generated/export.dart';
import '../../../core/riverpod.dart';
import '../../discovery/presentation/discovery_controller.dart';
import '../../profile/presentation/profile_controller.dart';
import '../data/api_review_repository.dart';
import '../domain/review_repository.dart';

part 'review_controller.g.dart';

const reviewMinRating = 1;
const reviewMaxRating = 10;
const reviewCommentMaxLength = 500;

/// The reviews a user has received, newest first.
@Riverpod(retry: noRetry)
Future<List<Review>> userReviews(Ref ref, String userId) => ref.watch(reviewRepositoryProvider).listReceived(userId);

/// Whether the signed-in user can review a user.
@Riverpod(retry: noRetry)
Future<ReviewEligibility> reviewEligibility(Ref ref, String userId) =>
    ref.watch(reviewRepositoryProvider).getEligibility(userId);

/// Writes and deletes reviews. Its state is the progress of the action in flight; each
/// method reports whether it succeeded.
@riverpod
class ReviewActions extends _$ReviewActions {
  @override
  FutureOr<void> build() {}

  Future<bool> create(String revieweeId, {required int rating, required String comment}) => _run(
        (repository) => repository.create(
          revieweeId,
          rating: rating,
          comment: comment.trim().isEmpty ? null : comment.trim(),
        ),
      );

  Future<bool> delete(String reviewId) => _run((repository) => repository.delete(reviewId));

  Future<bool> _run(Future<void> Function(ReviewRepository repository) action) async {
    state = const AsyncLoading();
    try {
      await action(ref.read(reviewRepositoryProvider));
      return true;
    } on Object catch (error, stackTrace) {
      if (ref.mounted) {
        state = AsyncError(error, stackTrace);
      }

      return false;
    } finally {
      if (ref.mounted) {
        // Also after a failure: a refusal usually means what is shown is out of date.
        // Profiles and the discovery list carry the average, which a review changes.
        ref.invalidate(userReviewsProvider);
        ref.invalidate(reviewEligibilityProvider);
        ref.invalidate(userProfileProvider);
        ref.invalidate(ownProfileProvider);
        ref.invalidate(discoveryFeedProvider);
        if (state.isLoading) {
          state = const AsyncData(null);
        }
      }
    }
  }
}
