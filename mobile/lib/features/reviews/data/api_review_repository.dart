import 'package:riverpod_annotation/riverpod_annotation.dart';

import '../../../core/api/api_exception.dart';
import '../../../core/api/api_providers.dart';
import '../../../core/api/generated/export.dart';
import '../domain/review_repository.dart';

part 'api_review_repository.g.dart';

@Riverpod(keepAlive: true)
ReviewRepository reviewRepository(Ref ref) => ApiReviewRepository(ref.watch(exparterApiProvider));

class ApiReviewRepository implements ReviewRepository {
  const ApiReviewRepository(this._api);

  /// The largest page the API serves; a profile shows the first page only.
  static const _pageSize = 50;

  final ExparterApi _api;

  @override
  Future<List<Review>> listReceived(String userId) => guardApiCall(
        () async => (await _api.reviews.listUserReviews(userId: userId, pageSize: _pageSize)).items,
      );

  @override
  Future<ReviewEligibility> getEligibility(String userId) =>
      guardApiCall(() => _api.reviews.getReviewEligibility(userId: userId));

  @override
  Future<Review> create(String revieweeId, {required int rating, required String? comment}) => guardApiCall(
        () => _api.reviews.createReview(
          body: CreateReviewRequest(revieweeId: revieweeId, rating: rating, comment: comment),
        ),
      );

  @override
  Future<void> delete(String reviewId) => guardApiCall(() => _api.reviews.deleteReview(reviewId: reviewId));
}
