import 'package:flutter/material.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';

part 'theme_mode_setting.g.dart';

/// Where the chosen theme is kept on the phone. It is not a secret; secure storage is
/// used only because the app already has it.
abstract interface class ThemeModeStore {
  Future<ThemeMode?> read();

  Future<void> write(ThemeMode mode);
}

class SecureThemeModeStore implements ThemeModeStore {
  const SecureThemeModeStore([this._storage = const FlutterSecureStorage()]);

  static const _key = 'theme_mode';

  final FlutterSecureStorage _storage;

  @override
  Future<ThemeMode?> read() async {
    final value = await _storage.read(key: _key);
    return ThemeMode.values.where((mode) => mode.name == value).firstOrNull;
  }

  @override
  Future<void> write(ThemeMode mode) => _storage.write(key: _key, value: mode.name);
}

@Riverpod(keepAlive: true)
ThemeModeStore themeModeStore(Ref ref) => const SecureThemeModeStore();

/// Light, dark, or whatever the phone uses (the default).
@Riverpod(keepAlive: true)
class ThemeModeSetting extends _$ThemeModeSetting {
  @override
  Future<ThemeMode> build() async {
    try {
      return await ref.watch(themeModeStoreProvider).read() ?? ThemeMode.system;
    } on Object {
      // An unreadable setting must not keep the app from starting.
      return ThemeMode.system;
    }
  }

  Future<void> select(ThemeMode mode) async {
    state = AsyncData(mode);
    try {
      await ref.read(themeModeStoreProvider).write(mode);
    } on Object {
      // The choice still holds until the app is closed.
    }
  }
}
