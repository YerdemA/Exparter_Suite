// Renders the Android launcher, adaptive and notification icons from the logo painter.
// Run from mobile/ after changing the mark or the palette, and commit the images:
//
//     flutter test tool/generate_icons.dart
//
// It lives outside test/ so that `flutter test` does not run it.
import 'dart:io';
import 'dart:ui' as ui;

import 'package:exparter/core/theme/app_logo.dart';
import 'package:exparter/core/theme/app_tokens.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

const _res = 'android/app/src/main/res';

/// Pixels per dp of each density bucket.
const _densities = {'mdpi': 1.0, 'hdpi': 1.5, 'xhdpi': 2.0, 'xxhdpi': 3.0, 'xxxhdpi': 4.0};

const _gradient = LinearGradient(
  begin: Alignment.topLeft,
  end: Alignment.bottomRight,
  colors: [AppPalette.indigoDeep, AppPalette.indigo, AppPalette.turquoiseDeep],
  stops: [0, 0.45, 1],
);

Future<void> _write(String path, int size, void Function(Canvas canvas, double size) draw) async {
  final recorder = ui.PictureRecorder();
  draw(Canvas(recorder), size.toDouble());
  final image = await recorder.endRecording().toImage(size, size);
  final png = await image.toByteData(format: ui.ImageByteFormat.png);
  final file = File(path)..parent.createSync(recursive: true);
  file.writeAsBytesSync(png!.buffer.asUint8List());
}

void _mark(Canvas canvas, double size, double share) {
  final box = size * share;
  canvas
    ..save()
    ..translate((size - box) / 2, (size - box) / 2);
  const LogoMarkPainter(AppPalette.white).paint(canvas, Size.square(box));
  canvas.restore();
}

void main() {
  test('generate icons', () async {
    for (final MapEntry(key: bucket, value: scale) in _densities.entries) {
      // Before Android 8: the whole icon, a rounded square.
      await _write('$_res/mipmap-$bucket/ic_launcher.png', (48 * scale).round(), (canvas, size) {
        final rect = Offset.zero & Size.square(size);
        canvas.drawRRect(
          RRect.fromRectAndRadius(rect.deflate(size * 0.04), Radius.circular(size * 0.22)),
          Paint()..shader = _gradient.createShader(rect),
        );
        _mark(canvas, size, 0.6);
      });

      // Android 8 and later: the launcher masks a 108 dp layer, and only its middle
      // 66 dp is sure to show. The same layer serves as the monochrome (themed) icon.
      await _write('$_res/mipmap-$bucket/ic_launcher_foreground.png', (108 * scale).round(), (canvas, size) {
        _mark(canvas, size, 0.5);
      });

      // Status bar icons are white shapes; Android tints them.
      await _write('$_res/drawable-$bucket/ic_notification.png', (24 * scale).round(), (canvas, size) {
        _mark(canvas, size, 1);
      });
    }
  });
}
