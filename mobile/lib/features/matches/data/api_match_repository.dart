import 'package:riverpod_annotation/riverpod_annotation.dart';

import '../../../core/api/api_exception.dart';
import '../../../core/api/api_providers.dart';
import '../../../core/api/generated/export.dart';
import '../domain/match_repository.dart';

part 'api_match_repository.g.dart';

@Riverpod(keepAlive: true)
MatchRepository matchRepository(Ref ref) => ApiMatchRepository(ref.watch(exparterApiProvider));

class ApiMatchRepository implements MatchRepository {
  const ApiMatchRepository(this._api);

  /// The largest page the API serves. Lists here show the first page only; that is
  /// enough until users have more than fifty open requests or matches.
  static const _pageSize = 50;

  final ExparterApi _api;

  @override
  Future<List<SwapRequest>> listPendingRequests(Direction direction) => guardApiCall(
        () async => (await _api.swapRequests.listSwapRequests(
          direction: direction,
          status: SwapRequestStatus.pending,
          pageSize: _pageSize,
        ))
            .items,
      );

  @override
  Future<SwapRequest?> findPendingRequest(Direction direction, String userId) => guardApiCall(
        () async => (await _api.swapRequests.listSwapRequests(
          direction: direction,
          status: SwapRequestStatus.pending,
          otherUserId: userId,
          pageSize: 1,
        ))
            .items
            .firstOrNull,
      );

  @override
  Future<SwapRequest> sendRequest({
    required String recipientId,
    required String offeredSkillId,
    required String requestedSkillId,
    required String? message,
  }) =>
      guardApiCall(
        () => _api.swapRequests.sendSwapRequest(
          body: SendSwapRequestRequest(
            recipientId: recipientId,
            offeredSkillId: offeredSkillId,
            requestedSkillId: requestedSkillId,
            message: message,
          ),
        ),
      );

  @override
  Future<Match> acceptRequest(String swapRequestId) =>
      guardApiCall(() => _api.swapRequests.acceptSwapRequest(swapRequestId: swapRequestId));

  @override
  Future<SwapRequest> declineRequest(String swapRequestId) =>
      guardApiCall(() => _api.swapRequests.declineSwapRequest(swapRequestId: swapRequestId));

  @override
  Future<SwapRequest> cancelRequest(String swapRequestId) =>
      guardApiCall(() => _api.swapRequests.cancelSwapRequest(swapRequestId: swapRequestId));

  @override
  Future<List<Match>> listMatches() =>
      guardApiCall(() async => (await _api.matches.listMatches(pageSize: _pageSize)).items);

  @override
  Future<Match?> findOpenMatch(String userId) => guardApiCall(() async {
        // Two users have one open match at most and it is their newest, so the first
        // of the newest-first list tells.
        final newest = (await _api.matches.listMatches(otherUserId: userId, pageSize: 1)).items.firstOrNull;
        return newest?.status == MatchStatus.ended ? null : newest;
      });

  @override
  Future<Match> getMatch(String matchId) => guardApiCall(() => _api.matches.getMatch(matchId: matchId));

  @override
  Future<Match> requestEnd(String matchId) => guardApiCall(() => _api.matches.requestMatchEnd(matchId: matchId));

  @override
  Future<Match> withdrawEndRequest(String matchId) =>
      guardApiCall(() => _api.matches.withdrawMatchEndRequest(matchId: matchId));

  @override
  Future<Match> acceptEnd(String matchId) => guardApiCall(() => _api.matches.acceptMatchEnd(matchId: matchId));

  @override
  Future<Match> rejectEnd(String matchId) => guardApiCall(() => _api.matches.rejectMatchEnd(matchId: matchId));
}
