// Renders the Google Play listing graphics from the logo painter into docs/store/:
// the 512 px app icon and the 1024x500 feature graphic. Run from mobile/ after changing
// the mark, the palette or the tagline, and commit the images:
//
//     flutter test tool/generate_store_assets.dart
//
// Play wants both as PNG without an alpha channel, which dart:ui does not write, so a
// minimal RGB encoder is included. The text uses the Roboto that ships with Flutter,
// since tests render text in a placeholder font.
import 'dart:io';
import 'dart:typed_data';
import 'dart:ui' as ui;

import 'package:exparter/core/theme/app_logo.dart';
import 'package:exparter/core/theme/app_tokens.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';

const _out = '../docs/store';

const _gradient = LinearGradient(
  begin: Alignment.topLeft,
  end: Alignment.bottomRight,
  colors: [AppPalette.indigoDeep, AppPalette.indigo, AppPalette.turquoiseDeep],
  stops: [0, 0.45, 1],
);

Future<void> _loadRoboto() async {
  final fonts = '${Platform.environment['FLUTTER_ROOT']}/bin/cache/artifacts/material_fonts';
  final loader = FontLoader('Roboto');
  for (final weight in ['regular', 'bold']) {
    final bytes = File('$fonts/roboto-$weight.ttf').readAsBytesSync();
    loader.addFont(Future.value(ByteData.sublistView(bytes)));
  }
  await loader.load();
}

Future<void> _write(String name, int width, int height, void Function(Canvas canvas, Size size) draw) async {
  final recorder = ui.PictureRecorder();
  final size = Size(width.toDouble(), height.toDouble());
  final canvas = Canvas(recorder);
  // Opaque everywhere, so dropping the alpha channel loses nothing.
  canvas.drawRect(Offset.zero & size, Paint()..shader = _gradient.createShader(Offset.zero & size));
  draw(canvas, size);
  final image = await recorder.endRecording().toImage(width, height);
  final rgba = await image.toByteData(format: ui.ImageByteFormat.rawRgba);
  File('$_out/$name')
    ..parent.createSync(recursive: true)
    ..writeAsBytesSync(_rgbPng(rgba!.buffer.asUint8List(), width, height));
}

void _mark(Canvas canvas, Offset center, double box) {
  canvas
    ..save()
    ..translate(center.dx - box / 2, center.dy - box / 2);
  const LogoMarkPainter(AppPalette.white).paint(canvas, Size.square(box));
  canvas.restore();
}

TextPainter _text(String text, double fontSize, FontWeight weight, double maxWidth, {double opacity = 1}) =>
    TextPainter(
      text: TextSpan(
        text: text,
        style: TextStyle(
          fontFamily: 'Roboto',
          fontSize: fontSize,
          fontWeight: weight,
          height: 1.2,
          color: AppPalette.white.withValues(alpha: opacity),
        ),
      ),
      textDirection: TextDirection.ltr,
    )..layout(maxWidth: maxWidth);

void main() {
  test('generate store assets', () async {
    await _loadRoboto();

    // Play masks the icon itself, so it is a full square without rounded corners.
    await _write('icon-512.png', 512, 512, (canvas, size) {
      _mark(canvas, size.center(Offset.zero), size.width * 0.62);
    });

    await _write('feature-graphic.png', 1024, 500, (canvas, size) {
      _mark(canvas, Offset(220, size.height / 2), 260);
      const left = 390.0;
      final title = _text('Exparter', 104, FontWeight.bold, 600);
      final tagline = _text('Bildiğinizi öğretin,\nöğrenmek istediğinizi öğrenin.', 38, FontWeight.normal, 600,
          opacity: 0.9);
      const gap = 18.0;
      final top = (size.height - title.height - gap - tagline.height) / 2;
      title.paint(canvas, Offset(left, top));
      tagline.paint(canvas, Offset(left, top + title.height + gap));
    });
  });
}

/// An 8-bit RGB PNG: one IHDR, one IDAT with unfiltered scanlines, one IEND.
Uint8List _rgbPng(Uint8List rgba, int width, int height) {
  final raw = BytesBuilder(copy: false);
  for (var y = 0; y < height; y++) {
    final row = Uint8List(1 + width * 3);
    for (var x = 0; x < width; x++) {
      final i = (y * width + x) * 4;
      row.setRange(1 + x * 3, 4 + x * 3, rgba, i);
    }
    raw.add(row);
  }
  final header = ByteData(13)
    ..setUint32(0, width)
    ..setUint32(4, height)
    ..setUint8(8, 8)
    ..setUint8(9, 2);
  return (BytesBuilder()
        ..add(const [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A])
        ..add(_chunk('IHDR', header.buffer.asUint8List()))
        ..add(_chunk('IDAT', Uint8List.fromList(zlib.encode(raw.takeBytes()))))
        ..add(_chunk('IEND', Uint8List(0))))
      .takeBytes();
}

Uint8List _chunk(String type, Uint8List data) {
  final typed = Uint8List.fromList([...type.codeUnits, ...data]);
  final length = ByteData(4)..setUint32(0, data.length);
  final crc = ByteData(4)..setUint32(0, _crc32(typed));
  return Uint8List.fromList([...length.buffer.asUint8List(), ...typed, ...crc.buffer.asUint8List()]);
}

int _crc32(Uint8List bytes) {
  var crc = 0xFFFFFFFF;
  for (final byte in bytes) {
    crc ^= byte;
    for (var k = 0; k < 8; k++) {
      crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320 : crc >> 1;
    }
  }
  return crc ^ 0xFFFFFFFF;
}
