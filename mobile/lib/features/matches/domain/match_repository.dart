import '../../../core/api/generated/export.dart';

/// Swap requests and the matches they turn into. Failures are thrown as `ApiException`.
abstract interface class MatchRepository {
  /// Requests still waiting for an answer, newest first.
  Future<List<SwapRequest>> listPendingRequests(Direction direction);

  Future<SwapRequest> sendRequest({
    required String recipientId,
    required String offeredSkillId,
    required String requestedSkillId,
    required String? message,
  });

  Future<Match> acceptRequest(String swapRequestId);

  Future<SwapRequest> declineRequest(String swapRequestId);

  Future<SwapRequest> cancelRequest(String swapRequestId);

  /// Every match of the signed-in user, newest first.
  Future<List<Match>> listMatches();

  Future<Match> getMatch(String matchId);

  Future<Match> requestEnd(String matchId);

  Future<Match> withdrawEndRequest(String matchId);

  Future<Match> acceptEnd(String matchId);

  Future<Match> rejectEnd(String matchId);
}
