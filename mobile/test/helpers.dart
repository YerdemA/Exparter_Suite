import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:exparter/core/api/generated/export.dart';
import 'package:exparter/core/auth/session_store.dart';
import 'package:exparter/core/theme/app_theme.dart';
import 'package:exparter/features/auth/domain/auth_repository.dart';
import 'package:exparter/l10n/app_localizations.dart';
import 'package:flutter/material.dart';
import 'package:mocktail/mocktail.dart';

class MockAuthRepository extends Mock implements AuthRepository {}

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

/// Wraps a widget with what screens expect from the app: its theme and localizations.
Widget localized(Widget child) => MaterialApp(
      theme: AppTheme.light(),
      locale: const Locale('tr'),
      localizationsDelegates: AppLocalizations.localizationsDelegates,
      supportedLocales: AppLocalizations.supportedLocales,
      home: child,
    );
