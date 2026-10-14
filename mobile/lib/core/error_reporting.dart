import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';
import 'package:sentry_flutter/sentry_flutter.dart';

import 'auth/session_controller.dart';

part 'error_reporting.g.dart';

/// Tags crash reports with the signed-in user's id, and nothing else about them, so that
/// a report shows how many users it reaches (ADR 0013). Without Sentry set up, nothing
/// happens.
@Riverpod(keepAlive: true)
void errorReportUser(Ref ref) {
  final userId = ref.watch(sessionControllerProvider.select((session) => session.value?.id));
  Sentry.configureScope((scope) => scope.setUser(userId == null ? null : SentryUser(id: userId)));
}
