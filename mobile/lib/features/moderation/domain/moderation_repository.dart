import '../../../core/api/generated/export.dart';

/// Blocks and reports of the signed-in user. Failures are thrown as `ApiException`.
abstract interface class ModerationRepository {
  /// Newest first.
  Future<List<Block>> listBlocks();

  Future<void> block(String userId);

  Future<void> unblock(String userId);

  Future<void> report({
    required ReportTargetType targetType,
    required String targetId,
    required ReportReason reason,
    String? description,
  });
}
