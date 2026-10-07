import 'package:flutter/material.dart';

import 'app_tokens.dart';

/// Brand colors that have no slot in [ColorScheme].
@immutable
class AppBrandColors extends ThemeExtension<AppBrandColors> {
  const AppBrandColors({
    required this.heroGradient,
    required this.onHero,
    required this.success,
    required this.warning,
  });

  final Gradient heroGradient;
  final Color onHero;
  final Color success;
  final Color warning;

  @override
  AppBrandColors copyWith({
    Gradient? heroGradient,
    Color? onHero,
    Color? success,
    Color? warning,
  }) =>
      AppBrandColors(
        heroGradient: heroGradient ?? this.heroGradient,
        onHero: onHero ?? this.onHero,
        success: success ?? this.success,
        warning: warning ?? this.warning,
      );

  @override
  AppBrandColors lerp(AppBrandColors? other, double t) {
    if (other == null) {
      return this;
    }

    return AppBrandColors(
      heroGradient: Gradient.lerp(heroGradient, other.heroGradient, t)!,
      onHero: Color.lerp(onHero, other.onHero, t)!,
      success: Color.lerp(success, other.success, t)!,
      warning: Color.lerp(warning, other.warning, t)!,
    );
  }
}

extension AppThemeContext on BuildContext {
  AppBrandColors get brand => Theme.of(this).extension<AppBrandColors>()!;
}

abstract final class AppTheme {
  static ThemeData light() {
    const scheme = ColorScheme(
      brightness: Brightness.light,
      primary: AppPalette.pink,
      onPrimary: AppPalette.white,
      secondary: AppPalette.purple,
      onSecondary: AppPalette.white,
      tertiary: AppPalette.turquoise,
      onTertiary: AppPalette.white,
      error: AppPalette.danger,
      onError: AppPalette.white,
      surface: AppPalette.surface,
      onSurface: AppPalette.ink,
      onSurfaceVariant: AppPalette.inkMuted,
      surfaceContainerHighest: AppPalette.surfaceTint,
      outline: AppPalette.outline,
    );

    final base = ThemeData(colorScheme: scheme, useMaterial3: true);
    final text = base.textTheme.apply(
      bodyColor: scheme.onSurface,
      displayColor: scheme.onSurface,
    );
    final fieldBorder = OutlineInputBorder(
      borderRadius: BorderRadius.circular(AppRadius.md),
      borderSide: BorderSide(color: scheme.outline),
    );

    return base.copyWith(
      scaffoldBackgroundColor: scheme.surface,
      textTheme: text.copyWith(
        headlineMedium: text.headlineMedium?.copyWith(fontWeight: FontWeight.w800),
        titleLarge: text.titleLarge?.copyWith(fontWeight: FontWeight.w700),
        labelLarge: text.labelLarge?.copyWith(fontWeight: FontWeight.w700),
      ),
      inputDecorationTheme: InputDecorationTheme(
        filled: true,
        fillColor: scheme.surfaceContainerHighest,
        contentPadding: const EdgeInsets.symmetric(
          horizontal: AppSpacing.md,
          vertical: AppSpacing.md,
        ),
        border: fieldBorder,
        enabledBorder: fieldBorder,
        focusedBorder: fieldBorder.copyWith(
          borderSide: BorderSide(color: scheme.primary, width: 2),
        ),
        errorBorder: fieldBorder.copyWith(
          borderSide: BorderSide(color: scheme.error),
        ),
        focusedErrorBorder: fieldBorder.copyWith(
          borderSide: BorderSide(color: scheme.error, width: 2),
        ),
        errorMaxLines: 3,
        helperMaxLines: 2,
      ),
      filledButtonTheme: FilledButtonThemeData(
        style: FilledButton.styleFrom(
          minimumSize: const Size.fromHeight(AppSizes.control),
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(AppRadius.md),
          ),
        ),
      ),
      outlinedButtonTheme: OutlinedButtonThemeData(
        style: OutlinedButton.styleFrom(
          minimumSize: const Size.fromHeight(AppSizes.control),
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(AppRadius.md),
          ),
        ),
      ),
      extensions: const [
        AppBrandColors(
          heroGradient: LinearGradient(
            begin: Alignment.topLeft,
            end: Alignment.bottomRight,
            colors: [AppPalette.purple, AppPalette.pink, AppPalette.coral, AppPalette.amber],
            stops: [0, 0.45, 0.8, 1],
          ),
          onHero: AppPalette.white,
          success: AppPalette.turquoise,
          warning: AppPalette.amber,
        ),
      ],
    );
  }
}
