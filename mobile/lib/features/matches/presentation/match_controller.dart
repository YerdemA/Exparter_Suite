import 'package:riverpod_annotation/riverpod_annotation.dart';

import '../../../core/api/generated/export.dart';
import '../../../core/riverpod.dart';
import '../data/api_match_repository.dart';
import '../../sessions/presentation/session_controller.dart';
import '../domain/match_repository.dart';

part 'match_controller.g.dart';

/// Requests waiting for an answer, received or sent.
@Riverpod(retry: noRetry)
Future<List<SwapRequest>> pendingRequests(Ref ref, Direction direction) =>
    ref.watch(matchRepositoryProvider).listPendingRequests(direction);

@Riverpod(retry: noRetry)
Future<List<Match>> matches(Ref ref) => ref.watch(matchRepositoryProvider).listMatches();

@Riverpod(retry: noRetry)
Future<Match> match(Ref ref, String matchId) => ref.watch(matchRepositoryProvider).getMatch(matchId);

/// Where the signed-in user stands with another user, which decides whether a new
/// swap request makes sense.
sealed class SwapStanding {
  const SwapStanding();
}

class NoOpenSwap extends SwapStanding {
  const NoOpenSwap();
}

class OpenMatch extends SwapStanding {
  const OpenMatch(this.matchId);

  final String matchId;
}

class RequestSent extends SwapStanding {
  const RequestSent();
}

class RequestReceived extends SwapStanding {
  const RequestReceived();
}

@Riverpod(retry: noRetry)
Future<SwapStanding> swapStanding(Ref ref, String userId) async {
  final repository = ref.watch(matchRepositoryProvider);
  final match = await repository.findOpenMatch(userId);
  if (match != null) {
    return OpenMatch(match.id);
  }

  if (await repository.findPendingRequest(Direction.outgoing, userId) != null) {
    return const RequestSent();
  }

  return await repository.findPendingRequest(Direction.incoming, userId) != null
      ? const RequestReceived()
      : const NoOpenSwap();
}

/// Runs the request and match actions. Its state is the progress of the action in
/// flight; each method reports whether it succeeded.
@riverpod
class MatchActions extends _$MatchActions {
  @override
  FutureOr<void> build() {}

  Future<bool> sendRequest({
    required String recipientId,
    required String offeredSkillId,
    required String requestedSkillId,
    required String message,
  }) =>
      _run(
        (repository) => repository.sendRequest(
          recipientId: recipientId,
          offeredSkillId: offeredSkillId,
          requestedSkillId: requestedSkillId,
          message: message.trim().isEmpty ? null : message.trim(),
        ),
      );

  Future<bool> acceptRequest(String swapRequestId) => _run((repository) => repository.acceptRequest(swapRequestId));

  Future<bool> declineRequest(String swapRequestId) => _run((repository) => repository.declineRequest(swapRequestId));

  Future<bool> cancelRequest(String swapRequestId) => _run((repository) => repository.cancelRequest(swapRequestId));

  Future<bool> requestEnd(String matchId) => _run((repository) => repository.requestEnd(matchId));

  Future<bool> withdrawEndRequest(String matchId) => _run((repository) => repository.withdrawEndRequest(matchId));

  Future<bool> acceptEnd(String matchId) => _run((repository) => repository.acceptEnd(matchId));

  Future<bool> rejectEnd(String matchId) => _run((repository) => repository.rejectEnd(matchId));

  Future<bool> _run(Future<void> Function(MatchRepository repository) action) async {
    state = const AsyncLoading();
    try {
      await action(ref.read(matchRepositoryProvider));
      return true;
    } on Object catch (error, stackTrace) {
      if (ref.mounted) {
        state = AsyncError(error, stackTrace);
      }

      return false;
    } finally {
      if (ref.mounted) {
        // Also after a failure: a refused action usually means the lists are out of
        // date (the other user answered first, a deadline passed).
        ref.invalidate(pendingRequestsProvider);
        ref.invalidate(matchesProvider);
        ref.invalidate(matchProvider);
        ref.invalidate(swapStandingProvider);
        // Starting or ending a match changes both users' statistics.
        ref.invalidate(userStatsProvider);
        if (state.isLoading) {
          state = const AsyncData(null);
        }
      }
    }
  }
}
