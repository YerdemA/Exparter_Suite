import 'dart:convert';

import 'package:flutter_secure_storage/flutter_secure_storage.dart';

import '../api/generated/export.dart';

/// Everything needed to resume a session after the app restarts.
class StoredSession {
  const StoredSession({
    required this.accessToken,
    required this.refreshToken,
    required this.account,
  });

  factory StoredSession.fromJson(Map<String, Object?> json) => StoredSession(
        accessToken: json['accessToken']! as String,
        refreshToken: json['refreshToken']! as String,
        account: Account.fromJson(json['account']! as Map<String, Object?>),
      );

  final String accessToken;
  final String refreshToken;
  final Account account;

  StoredSession copyWith({String? accessToken, String? refreshToken, Account? account}) =>
      StoredSession(
        accessToken: accessToken ?? this.accessToken,
        refreshToken: refreshToken ?? this.refreshToken,
        account: account ?? this.account,
      );

  Map<String, Object?> toJson() => {
        'accessToken': accessToken,
        'refreshToken': refreshToken,
        'account': account.toJson(),
      };
}

/// Where the serialized session is kept between launches.
abstract interface class SessionPersistence {
  Future<String?> read();

  Future<void> write(String value);

  Future<void> delete();
}

class SecureSessionPersistence implements SessionPersistence {
  const SecureSessionPersistence([this._storage = const FlutterSecureStorage()]);

  static const _key = 'exparter.session';

  final FlutterSecureStorage _storage;

  @override
  Future<String?> read() => _storage.read(key: _key);

  @override
  Future<void> write(String value) => _storage.write(key: _key, value: value);

  @override
  Future<void> delete() => _storage.delete(key: _key);
}

/// Holds the current session in memory and mirrors it to secure storage.
///
/// The in-memory copy lets the HTTP interceptor read the access token
/// synchronously on every request.
class SessionStore {
  SessionStore(this._persistence);

  final SessionPersistence _persistence;

  StoredSession? _current;

  StoredSession? get current => _current;

  Future<StoredSession?> load() async {
    try {
      final raw = await _persistence.read();
      _current = raw == null
          ? null
          : StoredSession.fromJson(jsonDecode(raw) as Map<String, Object?>);
    } on Object {
      // Unreadable data (for example written by an older app version) is the same as
      // no session: the user signs in again.
      _current = null;
      await _persistence.delete();
    }

    return _current;
  }

  Future<void> save(StoredSession session) async {
    _current = session;
    await _persistence.write(jsonEncode(session.toJson()));
  }

  Future<void> clear() async {
    _current = null;
    await _persistence.delete();
  }
}
