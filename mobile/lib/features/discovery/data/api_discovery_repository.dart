import 'package:riverpod_annotation/riverpod_annotation.dart';

import '../../../core/api/api_exception.dart';
import '../../../core/api/api_providers.dart';
import '../../../core/api/generated/export.dart';
import '../domain/discovery_repository.dart';

part 'api_discovery_repository.g.dart';

@Riverpod(keepAlive: true)
DiscoveryRepository discoveryRepository(Ref ref) => ApiDiscoveryRepository(ref.watch(exparterApiProvider));

class ApiDiscoveryRepository implements DiscoveryRepository {
  const ApiDiscoveryRepository(this._api);

  /// The largest page the API serves; a user has at most ten skills.
  static const _skillPageSize = 50;

  final ExparterApi _api;

  @override
  Future<DiscoveryUserCursorPage> discover({required DiscoveryFilters filters, String? cursor}) => guardApiCall(
        () => _api.discovery.discoverUsers(
          q: filters.query.isEmpty ? null : filters.query,
          skillId: filters.skillId,
          categoryId: filters.categoryId,
          city: filters.city,
          mode: filters.mode,
          cursor: cursor,
        ),
      );

  @override
  Future<PublicProfile> getUserProfile(String userId) =>
      guardApiCall(() => _api.profiles.getUserProfile(userId: userId));

  @override
  Future<List<UserSkill>> listUserSkills(String userId) => guardApiCall(
        () async => (await _api.skills.listUserSkills(userId: userId, pageSize: _skillPageSize)).items,
      );
}
