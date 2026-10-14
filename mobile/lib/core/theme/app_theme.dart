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
  static ThemeData light() => _build(
        // The seed fills the roles not set below (containers of primary and the like) in
        // tones of the brand indigo; the brand colors themselves are set exactly.
        ColorScheme.fromSeed(seedColor: AppPalette.indigo).copyWith(
          primary: AppPalette.indigo,
          onPrimary: AppPalette.white,
          secondary: AppPalette.turquoise,
          // Dark text: white on this turquoise is too faint to read.
          onSecondary: AppPalette.turquoiseInk,
          tertiary: AppPalette.amber,
          onTertiary: AppPalette.amberInk,
          error: AppPalette.danger,
          onError: AppPalette.white,
          surface: AppPalette.background,
          onSurface: AppPalette.ink,
          onSurfaceVariant: AppPalette.inkMuted,
          surfaceContainerLowest: AppPalette.white,
          surfaceContainerLow: AppPalette.white,
          surfaceContainer: AppPalette.container,
          surfaceContainerHigh: AppPalette.containerHigh,
          surfaceContainerHighest: AppPalette.containerHighest,
          outline: AppPalette.outline,
          outlineVariant: AppPalette.outlineSoft,
        ),
        const AppBrandColors(
          // The header fills the upper half of the sign-in screens; ending at the right
          // edge keeps the turquoise end in view.
          heroGradient: LinearGradient(
            begin: Alignment.topLeft,
            end: Alignment.centerRight,
            colors: [AppPalette.indigoDeep, AppPalette.indigo, AppPalette.turquoiseDeep],
            stops: [0, 0.45, 1],
          ),
          onHero: AppPalette.white,
          success: AppPalette.turquoiseDeep,
          warning: AppPalette.amber,
        ),
      );

  static ThemeData dark() => _build(
        ColorScheme.fromSeed(seedColor: AppPalette.indigo, brightness: Brightness.dark).copyWith(
          primary: AppPalette.indigoLight,
          onPrimary: AppPalette.indigoInk,
          secondary: AppPalette.turquoiseLight,
          onSecondary: AppPalette.turquoiseInk,
          tertiary: AppPalette.amberLight,
          onTertiary: AppPalette.amberInk,
          error: AppPalette.dangerLight,
          onError: AppPalette.dangerInk,
          surface: AppPalette.night,
          onSurface: AppPalette.nightInk,
          onSurfaceVariant: AppPalette.nightInkMuted,
          surfaceContainerLowest: AppPalette.nightLowest,
          surfaceContainerLow: AppPalette.nightSurface,
          surfaceContainer: AppPalette.nightContainer,
          surfaceContainerHigh: AppPalette.nightContainerHigh,
          surfaceContainerHighest: AppPalette.nightContainerHighest,
          outline: AppPalette.nightOutline,
          outlineVariant: AppPalette.nightOutlineSoft,
        ),
        const AppBrandColors(
          // Deeper than in the light theme, so the header does not glare on a dark screen.
          heroGradient: LinearGradient(
            begin: Alignment.topLeft,
            end: Alignment.centerRight,
            colors: [AppPalette.indigoInk, AppPalette.indigoDeep, AppPalette.turquoiseInk],
            stops: [0, 0.5, 1],
          ),
          onHero: AppPalette.white,
          success: AppPalette.turquoiseLight,
          warning: AppPalette.amberLight,
        ),
      );

  static ThemeData _build(ColorScheme scheme, AppBrandColors brand) {
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
      cardTheme: CardThemeData(
        color: scheme.surfaceContainerLow,
        elevation: 0,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(AppRadius.md),
          side: BorderSide(color: scheme.outlineVariant),
        ),
      ),
      inputDecorationTheme: InputDecorationTheme(
        filled: true,
        fillColor: scheme.surfaceContainerHigh,
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
      extensions: [brand],
    );
  }
}
