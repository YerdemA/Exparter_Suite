import 'package:flutter/material.dart';

/// Raw palette. Only the theme reads these; widgets take colors from
/// `Theme.of(context)` so that a dark palette can be added without touching them.
abstract final class AppPalette {
  static const pink = Color(0xFFE91E78);
  static const coral = Color(0xFFFF6B5E);
  static const amber = Color(0xFFFFB020);
  static const turquoise = Color(0xFF12B5A6);
  static const purple = Color(0xFF7B3FE4);

  static const ink = Color(0xFF1F1530);
  static const inkMuted = Color(0xFF6B6078);
  static const surface = Color(0xFFFFFBFD);
  static const surfaceTint = Color(0xFFFBEFF5);
  static const outline = Color(0xFFE3D5DE);
  static const danger = Color(0xFFC62838);
  static const white = Color(0xFFFFFFFF);
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

  /// Forms stop growing at this width on tablets and landscape phones.
  static const formMaxWidth = 440.0;
}
