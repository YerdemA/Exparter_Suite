import 'package:exparter/core/api/api_exception.dart';
import 'package:exparter/core/api/generated/export.dart';
import 'package:exparter/features/discovery/data/api_discovery_repository.dart';
import 'package:exparter/features/discovery/presentation/user_profile_screen.dart';
import 'package:exparter/features/matches/data/api_match_repository.dart';
import 'package:exparter/features/moderation/data/api_moderation_repository.dart';
import 'package:exparter/features/moderation/presentation/blocked_users_screen.dart';
import 'package:exparter/features/reviews/data/api_review_repository.dart';
import 'package:exparter/features/sessions/data/api_session_repository.dart';
import 'package:exparter/l10n/app_localizations_tr.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_riverpod/misc.dart' show Override;
import 'package:flutter_test/flutter_test.dart';
import 'package:mocktail/mocktail.dart';

import '../../helpers.dart';

void main() {
  final l10n = AppLocalizationsTr();

  late MockModerationRepository moderation;
  late MockDiscoveryRepository discovery;

  setUpAll(() {
    registerFallbackValue(ReportTargetType.user);
    registerFallbackValue(ReportReason.spam);
  });

  setUp(() {
    moderation = MockModerationRepository();
    discovery = MockDiscoveryRepository();
    when(() => discovery.getUserProfile('user-deniz')).thenAnswer(
      (_) async => const PublicProfile(
        userId: 'user-deniz',
        username: 'deniz',
        displayName: 'Deniz Kaya',
        acceptsOnline: true,
        acceptsInPerson: false,
        rating: RatingSummary(reviewCount: 0),
      ),
    );
    when(() => discovery.listUserSkills('user-deniz')).thenAnswer((_) async => []);
    when(() => moderation.block(any())).thenAnswer((_) async {});
    when(() => moderation.unblock(any())).thenAnswer((_) async {});
    when(
      () => moderation.report(
        targetType: any(named: 'targetType'),
        targetId: any(named: 'targetId'),
        reason: any(named: 'reason'),
        description: any(named: 'description'),
      ),
    ).thenAnswer((_) async {});
  });

  List<Override> overrides() => [
        moderationRepositoryProvider.overrideWithValue(moderation),
        discoveryRepositoryProvider.overrideWithValue(discovery),
        matchRepositoryProvider.overrideWithValue(quietMatchRepository()),
        sessionRepositoryProvider.overrideWithValue(quietSessionRepository()),
        reviewRepositoryProvider.overrideWithValue(quietReviewRepository()),
      ];

  Future<void> pumpProfile(WidgetTester tester) async {
    await tester.pumpWidget(
      ProviderScope(overrides: overrides(), child: routed(const UserProfileScreen(userId: 'user-deniz'))),
    );
    await tester.pumpAndSettle();
    await tester.tap(find.byType(PopupMenuButton<void>));
    await tester.pumpAndSettle();
  }

  group('UserProfileScreen', () {
    testWidgets('blocking explains what happens, then leaves the profile', (tester) async {
      await pumpProfile(tester);

      await tester.tap(find.text(l10n.moderationBlock));
      await tester.pumpAndSettle();
      expect(find.text(l10n.blockTitle('Deniz Kaya')), findsOneWidget);
      await tester.tap(find.widgetWithText(FilledButton, l10n.moderationBlock));
      await tester.pumpAndSettle();

      verify(() => moderation.block('user-deniz')).called(1);
      expect(find.text(l10n.blockDone), findsOneWidget);
      expect(find.text('route:/'), findsOneWidget);
    });

    testWidgets('changing one\'s mind blocks nobody', (tester) async {
      await pumpProfile(tester);

      await tester.tap(find.text(l10n.moderationBlock));
      await tester.pumpAndSettle();
      await tester.tap(find.text(l10n.actionCancel));
      await tester.pumpAndSettle();

      verifyNever(() => moderation.block(any()));
      expect(find.text('Deniz Kaya'), findsOneWidget);
    });

    testWidgets('a report for another reason is sent only with a description', (tester) async {
      await pumpProfile(tester);

      await tester.tap(find.text(l10n.moderationReport));
      await tester.pumpAndSettle();
      expect(find.text(l10n.reportUserTitle), findsOneWidget);
      final submit = find.widgetWithText(FilledButton, l10n.reportSubmit);
      expect(tester.widget<FilledButton>(submit).onPressed, isNull);

      await tester.tap(find.text(l10n.reportReasonOther));
      await tester.pumpAndSettle();
      expect(tester.widget<FilledButton>(submit).onPressed, isNull);
      await tester.enterText(find.byType(TextField), 'Sürekli rahatsız ediyor.');
      await tester.pumpAndSettle();
      await tester.tap(submit);
      await tester.pumpAndSettle();

      verify(
        () => moderation.report(
          targetType: ReportTargetType.user,
          targetId: 'user-deniz',
          reason: ReportReason.other,
          description: 'Sürekli rahatsız ediyor.',
        ),
      ).called(1);
      expect(find.text(l10n.reportSent), findsOneWidget);
    });

    testWidgets('a second report of the same thing is explained', (tester) async {
      when(
        () => moderation.report(
          targetType: any(named: 'targetType'),
          targetId: any(named: 'targetId'),
          reason: any(named: 'reason'),
          description: any(named: 'description'),
        ),
      ).thenThrow(const ApiException(statusCode: 409, code: 'report_already_open'));
      await pumpProfile(tester);

      await tester.tap(find.text(l10n.moderationReport));
      await tester.pumpAndSettle();
      await tester.tap(find.text(l10n.reportReasonSpam));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(FilledButton, l10n.reportSubmit));
      await tester.pumpAndSettle();

      expect(find.text(l10n.errorReportAlreadyOpen), findsOneWidget);
    });
  });

  group('BlockedUsersScreen', () {
    Future<void> pump(WidgetTester tester) async {
      await tester.pumpWidget(ProviderScope(overrides: overrides(), child: localized(const BlockedUsersScreen())));
      await tester.pumpAndSettle();
    }

    testWidgets('lists the blocked users and lifts a block', (tester) async {
      var blocks = [
        Block(
          user: const UserSummary(userId: 'user-deniz', isDeleted: false, username: 'deniz', displayName: 'Deniz Kaya'),
          createdAt: DateTime.utc(2026, 10, 14),
        ),
      ];
      when(() => moderation.listBlocks()).thenAnswer((_) async => blocks);
      await pump(tester);

      expect(find.text('Deniz Kaya'), findsOneWidget);

      blocks = [];
      await tester.tap(find.text(l10n.moderationUnblock));
      await tester.pumpAndSettle();

      verify(() => moderation.unblock('user-deniz')).called(1);
      expect(find.text(l10n.unblockDone), findsOneWidget);
      expect(find.text(l10n.blockedUsersEmpty), findsOneWidget);
    });

    testWidgets('says so when nobody is blocked', (tester) async {
      when(() => moderation.listBlocks()).thenAnswer((_) async => []);
      await pump(tester);

      expect(find.text(l10n.blockedUsersEmpty), findsOneWidget);
    });

    testWidgets('shows a safe message when the list cannot be loaded', (tester) async {
      when(() => moderation.listBlocks()).thenThrow(const ApiException(statusCode: 500, code: 'internal_error'));
      await pump(tester);

      expect(find.text(l10n.errorRetry), findsOneWidget);
    });
  });
}
