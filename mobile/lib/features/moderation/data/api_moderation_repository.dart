import 'package:riverpod_annotation/riverpod_annotation.dart';

import '../../../core/api/api_exception.dart';
import '../../../core/api/api_providers.dart';
import '../../../core/api/generated/export.dart';
import '../domain/moderation_repository.dart';

part 'api_moderation_repository.g.dart';

@Riverpod(keepAlive: true)
ModerationRepository moderationRepository(Ref ref) => ApiModerationRepository(ref.watch(exparterApiProvider));

class ApiModerationRepository implements ModerationRepository {
  const ApiModerationRepository(this._api);

  /// The largest page the API serves; the list shows the first page only.
  static const _pageSize = 50;

  final ExparterApi _api;

  @override
  Future<List<Block>> listBlocks() =>
      guardApiCall(() async => (await _api.moderation.listBlocks(pageSize: _pageSize)).items);

  @override
  Future<void> block(String userId) =>
      guardApiCall(() => _api.moderation.blockUser(body: BlockUserRequest(userId: userId)));

  @override
  Future<void> unblock(String userId) => guardApiCall(() => _api.moderation.unblockUser(userId: userId));

  @override
  Future<void> report({
    required ReportTargetType targetType,
    required String targetId,
    required ReportReason reason,
    String? description,
  }) =>
      guardApiCall(
        () => _api.moderation.createReport(
          body: CreateReportRequest(targetType: targetType, targetId: targetId, reason: reason, description: description),
        ),
      );
}
