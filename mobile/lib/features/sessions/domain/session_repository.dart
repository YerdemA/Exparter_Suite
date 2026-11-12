import '../../../core/api/generated/export.dart';

/// Sessions of a match and the statistics derived from them. Failures are thrown as
/// `ApiException`.
abstract interface class SessionRepository {
  /// Newest first.
  Future<List<SwapSession>> listMatchSessions(String matchId);

  /// Proposes an online session; [durationMinutes] is one of the offered lengths.
  Future<SwapSession> propose(
    String matchId, {
    required DateTime scheduledAt,
    required int durationMinutes,
    required String? note,
  });

  Future<SwapSession> accept(String sessionId);

  Future<SwapSession> decline(String sessionId);

  Future<SwapSession> markCompleted(String sessionId);

  Future<SwapSession> confirmCompleted(String sessionId);

  Future<SwapSession> cancel(String sessionId, {required String reason});

  Future<UserStats> getUserStats(String userId);

  /// A token to join the session's video call; refused outside its window.
  Future<CallToken> createCallToken(String sessionId);
}
