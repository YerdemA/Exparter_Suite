import 'package:riverpod_annotation/riverpod_annotation.dart';

import '../../../core/api/api_exception.dart';
import '../../../core/api/api_providers.dart';
import '../../../core/api/generated/export.dart';
import '../domain/session_repository.dart';

part 'api_session_repository.g.dart';

@Riverpod(keepAlive: true)
SessionRepository sessionRepository(Ref ref) => ApiSessionRepository(ref.watch(exparterApiProvider));

class ApiSessionRepository implements SessionRepository {
  const ApiSessionRepository(this._api);

  /// The largest page the API serves; a match's list shows the first page only.
  static const _pageSize = 50;

  final ExparterApi _api;

  @override
  Future<List<SwapSession>> listMatchSessions(String matchId) => guardApiCall(
        () async => (await _api.sessions.listMatchSessions(matchId: matchId, pageSize: _pageSize)).items,
      );

  @override
  Future<SwapSession> propose(
    String matchId, {
    required DateTime scheduledAt,
    required int durationMinutes,
    required String? note,
  }) =>
      guardApiCall(
        () => _api.sessions.proposeSession(
          matchId: matchId,
          body: ProposeSessionRequest(
            // The server needs an unambiguous instant, not the device's wall clock.
            scheduledAt: scheduledAt.toUtc(),
            durationMinutes: ProposeSessionRequestDurationMinutes.fromJson(durationMinutes),
            note: note,
          ),
        ),
      );

  @override
  Future<SwapSession> accept(String sessionId) => guardApiCall(() => _api.sessions.acceptSession(sessionId: sessionId));

  @override
  Future<SwapSession> decline(String sessionId) =>
      guardApiCall(() => _api.sessions.declineSession(sessionId: sessionId));

  @override
  Future<SwapSession> markCompleted(String sessionId) =>
      guardApiCall(() => _api.sessions.markSessionCompleted(sessionId: sessionId));

  @override
  Future<SwapSession> confirmCompleted(String sessionId) =>
      guardApiCall(() => _api.sessions.confirmSessionCompleted(sessionId: sessionId));

  @override
  Future<SwapSession> cancel(String sessionId, {required String reason}) => guardApiCall(
        () => _api.sessions.cancelSession(sessionId: sessionId, body: CancelSessionRequest(reason: reason)),
      );

  @override
  Future<UserStats> getUserStats(String userId) => guardApiCall(() => _api.profiles.getUserStats(userId: userId));

  @override
  Future<CallToken> createCallToken(String sessionId) =>
      guardApiCall(() => _api.sessions.createSessionCallToken(sessionId: sessionId));
}
