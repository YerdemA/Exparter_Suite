import 'package:riverpod_annotation/riverpod_annotation.dart';

import '../../../core/api/generated/export.dart';
import '../../../core/riverpod.dart';
import '../data/api_discovery_repository.dart';
import '../domain/discovery_repository.dart';

part 'discovery_controller.g.dart';

/// The users loaded so far and whether there are more.
class DiscoveryFeedState {
  const DiscoveryFeedState({
    required this.users,
    required this.nextCursor,
    this.loadingMore = false,
    this.loadMoreFailed = false,
  });

  final List<DiscoveryUser> users;

  /// Null once the last page has been loaded.
  final String? nextCursor;
  final bool loadingMore;
  final bool loadMoreFailed;

  bool get hasMore => nextCursor != null;
}

/// The ranked list for one set of filters, loaded a page at a time.
@Riverpod(retry: noRetry)
class DiscoveryFeed extends _$DiscoveryFeed {
  @override
  Future<DiscoveryFeedState> build(DiscoveryFilters filters) async {
    final page = await ref.watch(discoveryRepositoryProvider).discover(filters: filters);
    return DiscoveryFeedState(users: page.items, nextCursor: page.nextCursor);
  }

  /// Appends the next page. Does nothing if there is none or one is already loading.
  Future<void> loadMore() async {
    final current = state.value;
    if (current == null || !current.hasMore || current.loadingMore) {
      return;
    }

    state = AsyncData(
      DiscoveryFeedState(users: current.users, nextCursor: current.nextCursor, loadingMore: true),
    );
    try {
      final page = await ref
          .read(discoveryRepositoryProvider)
          .discover(filters: filters, cursor: current.nextCursor);
      if (!ref.mounted) {
        return;
      }

      // Pages are positions in a list that can shift while the user scrolls, so a
      // user from an earlier page may come back; show each only once.
      final known = {for (final item in current.users) item.user.userId};
      state = AsyncData(
        DiscoveryFeedState(
          users: [...current.users, ...page.items.where((item) => !known.contains(item.user.userId))],
          nextCursor: page.nextCursor,
        ),
      );
    } on Object {
      if (ref.mounted) {
        state = AsyncData(
          DiscoveryFeedState(users: current.users, nextCursor: current.nextCursor, loadMoreFailed: true),
        );
      }
    }
  }
}

@Riverpod(retry: noRetry)
Future<PublicProfile> userProfile(Ref ref, String userId) =>
    ref.watch(discoveryRepositoryProvider).getUserProfile(userId);

@Riverpod(retry: noRetry)
Future<List<UserSkill>> userSkills(Ref ref, String userId) =>
    ref.watch(discoveryRepositoryProvider).listUserSkills(userId);
