import 'package:flutter/material.dart';

import '../../../../core/theme/app_theme.dart';
import '../../../../core/theme/app_tokens.dart';
import '../../../../l10n/app_localizations.dart';

/// Shared frame of the sign-in and sign-up screens: brand header above a form card.
class AuthScaffold extends StatelessWidget {
  const AuthScaffold({required this.title, required this.child, super.key});

  final String title;
  final Widget child;

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final theme = Theme.of(context);
    final brand = context.brand;

    return Scaffold(
      body: DecoratedBox(
        decoration: BoxDecoration(gradient: brand.heroGradient),
        child: SafeArea(
          bottom: false,
          child: LayoutBuilder(
            builder: (context, constraints) => SingleChildScrollView(
              child: ConstrainedBox(
                constraints: BoxConstraints(minHeight: constraints.maxHeight),
                child: Column(
                  mainAxisAlignment: MainAxisAlignment.end,
                  children: [
                    Padding(
                      padding: const EdgeInsets.fromLTRB(
                        AppSpacing.lg,
                        AppSpacing.xxl,
                        AppSpacing.lg,
                        AppSpacing.xl,
                      ),
                      child: Column(
                        children: [
                          Text(
                            l10n.appName,
                            style: theme.textTheme.displaySmall?.copyWith(
                              color: brand.onHero,
                              fontWeight: FontWeight.w900,
                            ),
                          ),
                          const SizedBox(height: AppSpacing.sm),
                          Text(
                            l10n.appTagline,
                            textAlign: TextAlign.center,
                            style: theme.textTheme.bodyLarge?.copyWith(color: brand.onHero),
                          ),
                        ],
                      ),
                    ),
                    // Material, not a plain colored box: form controls such as the
                    // checkbox tile paint their ink on the nearest Material.
                    Material(
                      color: theme.colorScheme.surface,
                      borderRadius: const BorderRadius.vertical(
                        top: Radius.circular(AppRadius.lg),
                      ),
                      child: SafeArea(
                        top: false,
                        child: Padding(
                          padding: const EdgeInsets.all(AppSpacing.lg),
                          child: Center(
                            child: ConstrainedBox(
                              constraints: const BoxConstraints(maxWidth: AppSizes.formMaxWidth),
                              child: Column(
                                crossAxisAlignment: CrossAxisAlignment.stretch,
                                children: [
                                  Text(title, style: theme.textTheme.headlineMedium),
                                  const SizedBox(height: AppSpacing.lg),
                                  child,
                                ],
                              ),
                            ),
                          ),
                        ),
                      ),
                    ),
                  ],
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}

/// Inline error box used above a form's submit button.
class FormErrorBanner extends StatelessWidget {
  const FormErrorBanner({required this.message, super.key});

  final String message;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;

    return Semantics(
      liveRegion: true,
      child: Container(
        padding: const EdgeInsets.all(AppSpacing.md),
        decoration: BoxDecoration(
          color: scheme.error.withValues(alpha: 0.08),
          borderRadius: BorderRadius.circular(AppRadius.md),
          border: Border.all(color: scheme.error),
        ),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Icon(Icons.error_outline, color: scheme.error),
            const SizedBox(width: AppSpacing.sm),
            Expanded(
              child: Text(message, style: TextStyle(color: scheme.error)),
            ),
          ],
        ),
      ),
    );
  }
}

/// Submit button that shows a spinner and ignores taps while [isLoading].
class SubmitButton extends StatelessWidget {
  const SubmitButton({
    required this.label,
    required this.isLoading,
    required this.onPressed,
    super.key,
  });

  final String label;
  final bool isLoading;
  final VoidCallback onPressed;

  @override
  Widget build(BuildContext context) => FilledButton(
        onPressed: isLoading ? null : onPressed,
        child: isLoading
            ? const SizedBox.square(
                dimension: AppSpacing.lg,
                child: CircularProgressIndicator(strokeWidth: 3),
              )
            : Text(label),
      );
}
