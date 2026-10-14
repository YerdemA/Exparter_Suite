import 'dart:math' as math;

import 'package:flutter/material.dart';

/// The Exparter mark: two arrows that turn into each other around a spark, for skills
/// that change hands. The app icons are rendered from the same painter
/// (tool/generate_icons.dart), so the mark has one source.
class AppLogoMark extends StatelessWidget {
  const AppLogoMark({required this.size, required this.color, super.key});

  final double size;
  final Color color;

  @override
  Widget build(BuildContext context) => Semantics(
        excludeSemantics: true,
        child: CustomPaint(size: Size.square(size), painter: LogoMarkPainter(color)),
      );
}

class LogoMarkPainter extends CustomPainter {
  const LogoMarkPainter(this.color);

  final Color color;

  @override
  void paint(Canvas canvas, Size size) {
    final s = size.shortestSide;
    final center = size.center(Offset.zero);
    final radius = s * 0.34;
    final stroke = Paint()
      ..color = color
      ..style = PaintingStyle.stroke
      ..strokeWidth = s * 0.1
      ..strokeCap = StrokeCap.round;
    final fill = Paint()..color = color;

    // Each arrow covers a little less than half the circle; its head sits where the
    // other one starts.
    for (final start in [200.0, 20.0]) {
      const sweep = 118.0;
      canvas.drawArc(
        Rect.fromCircle(center: center, radius: radius),
        _radians(start),
        _radians(sweep),
        false,
        stroke,
      );
      _arrowHead(canvas, center, radius, _radians(start + sweep), s, fill);
    }

    _spark(canvas, center, s * 0.15, s * 0.045, fill);
  }

  static double _radians(double degrees) => degrees * math.pi / 180;

  /// A head pointing along the circle, clockwise, at [angle].
  static void _arrowHead(Canvas canvas, Offset center, double radius, double angle, double s, Paint paint) {
    final at = center + Offset(math.cos(angle), math.sin(angle)) * radius;
    final forward = Offset(-math.sin(angle), math.cos(angle));
    final across = Offset(math.cos(angle), math.sin(angle));
    final length = s * 0.17;
    final width = s * 0.24;
    canvas.drawPath(
      Path()
        ..moveTo(at.dx + forward.dx * length, at.dy + forward.dy * length)
        ..lineTo(at.dx + across.dx * width / 2, at.dy + across.dy * width / 2)
        ..lineTo(at.dx - across.dx * width / 2, at.dy - across.dy * width / 2)
        ..close(),
      paint,
    );
  }

  /// A four-pointed star with curved sides.
  static void _spark(Canvas canvas, Offset center, double outer, double inner, Paint paint) {
    final path = Path()..moveTo(center.dx, center.dy - outer);
    for (var i = 0; i < 4; i++) {
      final next = _radians(-90 + 90.0 * (i + 1));
      final between = _radians(-90 + 90.0 * i + 45);
      path.quadraticBezierTo(
        center.dx + math.cos(between) * inner,
        center.dy + math.sin(between) * inner,
        center.dx + math.cos(next) * outer,
        center.dy + math.sin(next) * outer,
      );
    }

    canvas.drawPath(path..close(), paint);
  }

  @override
  bool shouldRepaint(LogoMarkPainter oldDelegate) => oldDelegate.color != color;
}
