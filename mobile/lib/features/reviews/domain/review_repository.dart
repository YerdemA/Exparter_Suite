import '../../../core/api/generated/export.dart';

/// Reviews between users. Failures are thrown as `ApiException`.
abstract interface class ReviewRepository {
  /// The reviews a user has received, newest first.
  Future<List<Review>> listReceived(String userId);

  /// Whether the signed-in user can review [userId], and if not, why.
  Future<ReviewEligibility> getEligibility(String userId);

  Future<Review> create(String revieweeId, {required int rating, required String? comment});

  /// Deletes a review the signed-in user wrote. It cannot be written again.
  Future<void> delete(String reviewId);
}
