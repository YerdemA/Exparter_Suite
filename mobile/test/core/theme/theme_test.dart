import 'package:exparter/core/theme/app_logo.dart';
import 'package:exparter/core/theme/app_theme.dart';
import 'package:exparter/core/theme/theme_mode_setting.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import '../../helpers.dart';

/// WCAG contrast ratio of two opaque colors.
double contrast(Color a, Color b) {
  final (light, dark) = a.computeLuminance() > b.computeLuminance() ? (a, b) : (b, a);
  return (light.computeLuminance() + 0.05) / (dark.computeLuminance() + 0.05);
}

void main() {
  group('ThemeModeSetting', () {
    test('follows the phone until a choice is made, and remembers the choice', () async {
      final store = InMemoryThemeModeStore();
      final container = ProviderContainer.test(overrides: [themeModeStoreProvider.overrideWithValue(store)]);

      expect(await container.read(themeModeSettingProvider.future), ThemeMode.system);

      await container.read(themeModeSettingProvider.notifier).select(ThemeMode.dark);

      expect(container.read(themeModeSettingProvider).value, ThemeMode.dark);
      expect(store.value, ThemeMode.dark);

      final restarted = ProviderContainer.test(overrides: [themeModeStoreProvider.overrideWithValue(store)]);
      expect(await restarted.read(themeModeSettingProvider.future), ThemeMode.dark);
    });
  });

  // Text must stay readable in both themes: 4.5:1 for body text, 3:1 for large text
  // and for the white title on the header gradient.
  for (final (name, theme) in [('light', AppTheme.light()), ('dark', AppTheme.dark())]) {
    group('$name theme', () {
      final scheme = theme.colorScheme;
      final brand = theme.extension<AppBrandColors>()!;

      test('text on surfaces and buttons is readable', () {
        final pairs = {
          'onSurface on surface': (scheme.onSurface, scheme.surface),
          'onSurfaceVariant on surface': (scheme.onSurfaceVariant, scheme.surface),
          'onSurface on card': (scheme.onSurface, scheme.surfaceContainerLow),
          'onSurface on field': (scheme.onSurface, scheme.surfaceContainerHigh),
          'primary text on surface': (scheme.primary, scheme.surface),
          'onPrimary on primary': (scheme.onPrimary, scheme.primary),
          'onSecondary on secondary': (scheme.onSecondary, scheme.secondary),
          'onTertiary on tertiary': (scheme.onTertiary, scheme.tertiary),
          'error text on surface': (scheme.error, scheme.surface),
          'onError on error': (scheme.onError, scheme.error),
          'onPrimaryContainer on primaryContainer': (scheme.onPrimaryContainer, scheme.primaryContainer),
          'onSecondaryContainer on secondaryContainer': (scheme.onSecondaryContainer, scheme.secondaryContainer),
        };
        for (final MapEntry(key: label, value: (foreground, background)) in pairs.entries) {
          expect(contrast(foreground, background), greaterThanOrEqualTo(4.5), reason: label);
        }
      });

      test('the header text is readable all along the gradient', () {
        for (final color in (brand.heroGradient as LinearGradient).colors) {
          expect(contrast(brand.onHero, color), greaterThanOrEqualTo(3), reason: '$color');
        }
      });

      test('status colors stand out from the surface', () {
        expect(contrast(brand.success, scheme.surface), greaterThanOrEqualTo(3));
        expect(contrast(brand.warning, scheme.surface), greaterThanOrEqualTo(2));
      });
    });
  }

  testWidgets('the logo paints in the color it is given', (tester) async {
    await tester.pumpWidget(const Center(child: AppLogoMark(size: 72, color: Colors.white)));

    final paint = tester.widget<CustomPaint>(find.byType(CustomPaint).last);
    expect((paint.painter! as LogoMarkPainter).color, Colors.white);
    expect(tester.getSize(find.byType(AppLogoMark)), const Size.square(72));
  });
}
