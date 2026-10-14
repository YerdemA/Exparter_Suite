import 'package:riverpod_annotation/riverpod_annotation.dart';

import '../../../core/api/generated/export.dart';
import '../../../core/riverpod.dart';
import '../../discovery/presentation/discovery_controller.dart';
import '../../matches/presentation/match_controller.dart';
import '../../messages/presentation/message_controller.dart';
import '../data/api_moderation_repository.dart';
import '../domain/moderation_repository.dart';

part 'moderation_controller.g.dart';

const reportDescriptionMaxLength = 1000;

/// The users the signed-in user blocked, newest first.
@Riverpod(retry: noRetry)
Future<List<Block>> blocks(Ref ref) => ref.watch(moderationRepositoryProvider).listBlocks();

/// Blocks, unblocks and reports. Its state is the progress of the action in flight;
/// each method reports whether it succeeded.
///
/// Kept alive: it is started from menus and dialogs that close before it finishes, and
/// the screen reads the error afterwards.
@Riverpod(keepAlive: true)
class ModerationActions extends _$ModerationActions {
  @override
  FutureOr<void> build() {}

  Future<bool> block(String userId) => _run((repository) => repository.block(userId), changesWhatIsShared: true);

  Future<bool> unblock(String userId) => _run((repository) => repository.unblock(userId), changesWhatIsShared: true);

  Future<bool> report({
    required ReportTargetType targetType,
    required String targetId,
    required ReportReason reason,
    required String description,
  }) =>
      _run(
        (repository) => repository.report(
          targetType: targetType,
          targetId: targetId,
          reason: reason,
          description: description.trim().isEmpty ? null : description.trim(),
        ),
        changesWhatIsShared: false,
      );

  Future<bool> _run(Future<void> Function(ModerationRepository repository) action, {required bool changesWhatIsShared}) async {
    state = const AsyncLoading();
    try {
      await action(ref.read(moderationRepositoryProvider));
      return true;
    } on Object catch (error, stackTrace) {
      if (ref.mounted) {
        state = AsyncError(error, stackTrace);
      }

      return false;
    } finally {
      if (ref.mounted) {
        // A block ends the match, cancels requests, closes the conversation and changes
        // who can be found.
        if (changesWhatIsShared) {
          ref
            ..invalidate(blocksProvider)
            ..invalidate(discoveryFeedProvider)
            ..invalidate(userProfileProvider)
            ..invalidate(pendingRequestsProvider)
            ..invalidate(matchesProvider)
            ..invalidate(matchProvider)
            ..invalidate(conversationsProvider)
            ..invalidate(threadProvider);
        }

        if (state.isLoading) {
          state = const AsyncData(null);
        }
      }
    }
  }
}
