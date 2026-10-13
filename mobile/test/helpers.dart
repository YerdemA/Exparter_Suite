import 'dart:async';
import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:exparter/core/api/generated/export.dart';
import 'package:exparter/core/auth/session_store.dart';
import 'package:exparter/core/realtime/realtime_client.dart';
import 'package:exparter/core/theme/app_theme.dart';
import 'package:exparter/features/auth/domain/auth_repository.dart';
import 'package:exparter/features/discovery/domain/discovery_repository.dart';
import 'package:exparter/features/matches/domain/match_repository.dart';
import 'package:exparter/features/messages/domain/message_repository.dart';
import 'package:exparter/features/profile/domain/profile_repository.dart';
import 'package:exparter/features/reviews/domain/review_repository.dart';
import 'package:exparter/features/sessions/domain/session_repository.dart';
import 'package:exparter/features/skills/domain/skill_repository.dart';
import 'package:exparter/l10n/app_localizations.dart';
import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:mocktail/mocktail.dart';

class MockAuthRepository extends Mock implements AuthRepository {}

class MockProfileRepository extends Mock implements ProfileRepository {}

class MockSkillRepository extends Mock implements SkillRepository {}

class MockDiscoveryRepository extends Mock implements DiscoveryRepository {}

class MockMatchRepository extends Mock implements MatchRepository {}

class MockMessageRepository extends Mock implements MessageRepository {}

class MockSessionRepository extends Mock implements SessionRepository {}

class MockReviewRepository extends Mock implements ReviewRepository {}

/// A review repository for tests that are about something else: no reviews, none can be written.
MockReviewRepository quietReviewRepository() {
  final repository = MockReviewRepository();
  when(() => repository.listReceived(any())).thenAnswer((_) async => []);
  when(() => repository.getEligibility(any())).thenAnswer(
    (_) async => const ReviewEligibility(canReview: false, reason: ReviewBlockReason.noCompletedSession),
  );
  return repository;
}

const testStats = UserStats(totalMatches: 5, completedMatches: 3, cancelledMatches: 1, completedSessions: 9);

/// A session repository for tests that are about something else: no sessions, fixed statistics.
MockSessionRepository quietSessionRepository() {
  final repository = MockSessionRepository();
  when(() => repository.listMatchSessions(any())).thenAnswer((_) async => []);
  when(() => repository.getUserStats(any())).thenAnswer((_) async => testStats);
  return repository;
}

/// A real-time connection that tests drive by hand.
class FakeRealtimeClient implements RealtimeClient {
  final _events = StreamController<RealtimeEvent>.broadcast();

  bool started = false;

  @override
  Stream<RealtimeEvent> get events => _events.stream;

  void emit(RealtimeEvent event) => _events.add(event);

  @override
  Future<void> start() async => started = true;

  @override
  Future<void> stop() async => started = false;
}

class InMemorySessionPersistence implements SessionPersistence {
  InMemorySessionPersistence([this.value]);

  String? value;

  @override
  Future<String?> read() async => value;

  @override
  Future<void> write(String value) async => this.value = value;

  @override
  Future<void> delete() async => value = null;
}

Account testAccount({String username = 'ayse', bool emailVerified = false}) => Account(
      id: '01a11705-1f92-7d54-bafb-dfc5049ec2e0',
      email: 'ayse@example.com',
      username: username,
      emailVerified: emailVerified,
      role: UserRole.user,
      createdAt: DateTime.utc(2026, 10, 7),
    );

Profile testProfile({bool complete = true, String? avatarUrl}) => Profile(
      userId: '01a11705-1f92-7d54-bafb-dfc5049ec2e0',
      displayName: complete ? 'Ayşe Yılmaz' : null,
      bio: complete ? 'Gitar çalıyorum.' : null,
      city: complete ? '34' : null,
      acceptsOnline: complete,
      acceptsInPerson: complete,
      avatarUrl: avatarUrl,
      isComplete: complete,
      rating: const RatingSummary(reviewCount: 0),
      createdAt: DateTime.utc(2026, 10, 7),
      updatedAt: DateTime.utc(2026, 10, 7),
    );

const testCategories = [
  SkillCategory(id: 'category-music', name: 'Müzik', slug: 'muzik'),
  SkillCategory(id: 'category-languages', name: 'Diller', slug: 'diller'),
];

Skill testSkill(String name, {SkillStatus status = SkillStatus.approved}) => Skill(
      id: 'skill-${name.toLowerCase()}',
      categoryId: 'category-music',
      name: name,
      slug: name.toLowerCase(),
      status: status,
    );

UserSkill testUserSkill(
  String name, {
  UserSkillType type = UserSkillType.offered,
  SkillLevel level = SkillLevel.intermediate,
}) =>
    UserSkill(
      id: 'user-skill-${name.toLowerCase()}-${type.json}',
      skill: testSkill(name),
      type: type,
      level: level,
      createdAt: DateTime.utc(2026, 10, 13),
    );

SkillPage testSkillPage(List<Skill> items, {int? totalCount}) =>
    SkillPage(page: 1, pageSize: 50, totalCount: totalCount ?? items.length, items: items);

InteractionRequirements testRequirements({
  bool emailVerified = true,
  bool profileCompleted = true,
  bool hasOfferedSkill = true,
}) =>
    InteractionRequirements(
      emailVerified: emailVerified,
      profileCompleted: profileCompleted,
      hasOfferedSkill: hasOfferedSkill,
      canInteract: emailVerified && profileCompleted && hasOfferedSkill,
    );

DiscoveryUser testDiscoveryUser(
  String username, {
  String? displayName,
  Compatibility compatibility = Compatibility.none,
  List<DiscoveryUserSkill> offered = const [],
  List<DiscoveryUserSkill> wanted = const [],
}) =>
    DiscoveryUser(
      user: UserSummary(userId: 'user-$username', isDeleted: false, username: username, displayName: displayName),
      acceptsOnline: true,
      acceptsInPerson: true,
      city: '34',
      compatibility: compatibility,
      rating: const RatingSummary(reviewCount: 0),
      offeredSkills: offered,
      wantedSkills: wanted,
    );

DiscoveryUserSkill testDiscoverySkill(String name, {SkillLevel level = SkillLevel.intermediate}) =>
    DiscoveryUserSkill(skill: SkillRef(id: 'skill-${name.toLowerCase()}', name: name), level: level);

const testCities = [City(code: '06', name: 'Ankara'), City(code: '34', name: 'İstanbul')];

AuthResponse testAuthResponse({bool accountRestored = false}) => AuthResponse(
      accessToken: 'access-1',
      refreshToken: 'refresh-1',
      expiresIn: 900,
      account: testAccount(),
      accountRestored: accountRestored,
    );

StoredSession testSession({String accessToken = 'access-1', String refreshToken = 'refresh-1'}) =>
    StoredSession(accessToken: accessToken, refreshToken: refreshToken, account: testAccount());

/// Answers requests from a function instead of the network and records them.
class FakeHttpAdapter implements HttpClientAdapter {
  FakeHttpAdapter(this.respond);

  final ResponseBody Function(RequestOptions options) respond;
  final List<RequestOptions> requests = [];

  List<String> get paths => [for (final request in requests) request.path];

  @override
  Future<ResponseBody> fetch(
    RequestOptions options,
    Stream<Uint8List>? requestStream,
    Future<void>? cancelFuture,
  ) async {
    requests.add(options);
    return respond(options);
  }

  @override
  void close({bool force = false}) {}
}

ResponseBody jsonResponse(int statusCode, Object? body) => ResponseBody.fromString(
      jsonEncode(body),
      statusCode,
      headers: {
        Headers.contentTypeHeader: [Headers.jsonContentType],
      },
    );

ResponseBody problemResponse(int statusCode, String code) => jsonResponse(statusCode, {
      'type': 'about:blank',
      'title': 'Error',
      'status': statusCode,
      'code': code,
    });

/// Like [localized], for screens that navigate: [child] is the first route and every
/// other location shows its own path, so tests can assert where a screen went.
Widget routed(Widget child) => MaterialApp.router(
      theme: AppTheme.light(),
      locale: const Locale('tr'),
      localizationsDelegates: AppLocalizations.localizationsDelegates,
      supportedLocales: AppLocalizations.supportedLocales,
      routerConfig: GoRouter(
        initialLocation: '/under-test',
        routes: [
          GoRoute(path: '/under-test', builder: (context, state) => child),
          GoRoute(
            path: '/:rest(.*)',
            builder: (context, state) => Scaffold(body: Text('route:${state.uri.path}')),
          ),
        ],
      ),
    );

/// Wraps a widget with what screens expect from the app: its theme and localizations.
Widget localized(Widget child) => MaterialApp(
      theme: AppTheme.light(),
      locale: const Locale('tr'),
      localizationsDelegates: AppLocalizations.localizationsDelegates,
      supportedLocales: AppLocalizations.supportedLocales,
      home: child,
    );
