import 'package:riverpod_annotation/riverpod_annotation.dart';

import '../../../core/api/generated/export.dart';
import '../../../core/riverpod.dart';
import '../../reviews/presentation/review_controller.dart';
import '../data/api_session_repository.dart';
import '../domain/session_repository.dart';

part 'session_controller.g.dart';

/// The lengths an online session can be planned for, in minutes.
const sessionDurations = [30, 45, 60, 90, 120];

const defaultSessionDuration = 60;

@Riverpod(retry: noRetry)
Future<List<SwapSession>> matchSessions(Ref ref, String matchId) =>
    ref.watch(sessionRepositoryProvider).listMatchSessions(matchId);

/// What a user's history of swaps says about them, shown on profiles.
@Riverpod(retry: noRetry)
Future<UserStats> userStats(Ref ref, String userId) => ref.watch(sessionRepositoryProvider).getUserStats(userId);

/// The time, refreshed every half minute, so that a session's call button appears when
/// its window opens without leaving the screen.
@riverpod
Stream<DateTime> callClock(Ref ref) => Stream.periodic(const Duration(seconds: 30), (_) => DateTime.now());

/// Runs the session actions. Its state is the progress of the action in flight; each
/// method reports whether it succeeded.
@riverpod
class SessionActions extends _$SessionActions {
  @override
  FutureOr<void> build() {}

  Future<bool> propose(
    String matchId, {
    required DateTime scheduledAt,
    required int durationMinutes,
    required String note,
  }) =>
      _run(
        (repository) => repository.propose(
          matchId,
          scheduledAt: scheduledAt,
          durationMinutes: durationMinutes,
          note: note.trim().isEmpty ? null : note.trim(),
        ),
      );

  Future<bool> reportInPerson(String matchId) => _run((repository) => repository.reportInPerson(matchId));

  Future<bool> accept(String sessionId) => _run((repository) => repository.accept(sessionId));

  Future<bool> decline(String sessionId) => _run((repository) => repository.decline(sessionId));

  Future<bool> markCompleted(String sessionId) => _run((repository) => repository.markCompleted(sessionId));

  Future<bool> confirmCompleted(String sessionId) => _run((repository) => repository.confirmCompleted(sessionId));

  Future<bool> cancel(String sessionId, {required String reason}) =>
      _run((repository) => repository.cancel(sessionId, reason: reason.trim()));

  Future<bool> _run(Future<void> Function(SessionRepository repository) action) async {
    state = const AsyncLoading();
    try {
      await action(ref.read(sessionRepositoryProvider));
      return true;
    } on Object catch (error, stackTrace) {
      if (ref.mounted) {
        state = AsyncError(error, stackTrace);
      }

      return false;
    } finally {
      if (ref.mounted) {
        // Also after a failure: a refused action usually means the list is out of date
        // (the other user answered first, a deadline passed).
        ref.invalidate(matchSessionsProvider);
        ref.invalidate(userStatsProvider);
        // A completed session is what lets the two users review each other.
        ref.invalidate(reviewEligibilityProvider);
        if (state.isLoading) {
          state = const AsyncData(null);
        }
      }
    }
  }
}
