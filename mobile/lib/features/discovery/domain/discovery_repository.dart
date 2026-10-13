import '../../../core/api/generated/export.dart';

/// What the discovery list is narrowed by. An empty [query] lists suggestions; any
/// other value searches by name or username.
typedef DiscoveryFilters = ({String query, String? skillId, String? categoryId, String? city, SessionMode? mode});

const noDiscoveryFilters = (query: '', skillId: null, categoryId: null, city: null, mode: null);

/// Finding and looking at other users. Failures are thrown as `ApiException`.
abstract interface class DiscoveryRepository {
  /// One page of users in ranked order; pass the previous page's cursor for the next.
  Future<DiscoveryUserCursorPage> discover({required DiscoveryFilters filters, String? cursor});

  Future<PublicProfile> getUserProfile(String userId);

  Future<List<UserSkill>> listUserSkills(String userId);
}
