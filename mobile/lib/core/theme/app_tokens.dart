import 'package:flutter/material.dart';

/// Raw palette ("indigo and turquoise", docs/roadmap.md). Only the theme reads these;
/// widgets take colors from `Theme.of(context)`, so the same screens work in both themes.
abstract final class AppPalette {
  static const indigo = Color(0xFF4F46E5);
  static const indigoDeep = Color(0xFF3730A3);
  static const indigoLight = Color(0xFFA5B4FC);
  static const indigoInk = Color(0xFF1E1B4B);
  static const turquoise = Color(0xFF0EA5A4);
  static const turquoiseDeep = Color(0xFF0E7C7B);
  static const turquoiseLight = Color(0xFF5EEAD4);
  static const turquoiseInk = Color(0xFF042F2E);
  static const amber = Color(0xFFF59E0B);
  static const amberLight = Color(0xFFFCD34D);
  static const amberInk = Color(0xFF3B2601);
  static const white = Color(0xFFFFFFFF);

  // Light theme.
  static const ink = Color(0xFF1E1B3A);
  static const inkMuted = Color(0xFF5B5E78);
  static const background = Color(0xFFF8F9FC);
  static const container = Color(0xFFF0F1F8);
  static const containerHigh = Color(0xFFE9EBF5);
  static const containerHighest = Color(0xFFE4E6F2);
  static const outline = Color(0xFFCDD0E2);
  static const outlineSoft = Color(0xFFE2E4EF);
  static const danger = Color(0xFFC62838);

  // Dark theme.
  static const night = Color(0xFF0F1020);
  static const nightLowest = Color(0xFF0B0C18);
  static const nightSurface = Color(0xFF1A1B2E);
  static const nightContainer = Color(0xFF1F2135);
  static const nightContainerHigh = Color(0xFF25273D);
  static const nightContainerHighest = Color(0xFF2C2E46);
  static const nightInk = Color(0xFFE6E6F0);
  static const nightInkMuted = Color(0xFFA6A8C0);
  static const nightOutline = Color(0xFF45486A);
  static const nightOutlineSoft = Color(0xFF2E3049);
  static const dangerLight = Color(0xFFFFA4A9);
  static const dangerInk = Color(0xFF4A0C12);
}

abstract final class AppSpacing {
  static const xs = 4.0;
  static const sm = 8.0;
  static const md = 16.0;
  static const lg = 24.0;
  static const xl = 32.0;
  static const xxl = 48.0;
}

abstract final class AppRadius {
  static const sm = 8.0;
  static const md = 14.0;
  static const lg = 28.0;
}

abstract final class AppSizes {
  /// Minimum height of tappable controls.
  static const control = 52.0;

  /// The mark above the app's name on the sign-in screens.
  static const logo = 72.0;

  /// Forms stop growing at this width on tablets and landscape phones.
  static const formMaxWidth = 440.0;
}
