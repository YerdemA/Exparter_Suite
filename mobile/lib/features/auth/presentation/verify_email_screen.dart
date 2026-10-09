import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/api/error_messages.dart';
import '../../../core/router/app_routes.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/theme/app_tokens.dart';
import '../../../l10n/app_localizations.dart';
import 'recovery_controller.dart';
import 'widgets/auth_scaffold.dart';

/// Opened by the link in the verification email. Confirms the token on arrival.
class VerifyEmailScreen extends ConsumerWidget {
  const VerifyEmailScreen({required this.token, super.key});

  /// Null when the link was opened without a token.
  final String? token;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppLocalizations.of(context);
    final token = this.token;

    final Widget body;
    if (token == null || token.isEmpty) {
      body = _Result(
        icon: Icons.link_off,
        color: Theme.of(context).colorScheme.error,
        message: l10n.errorInvalidLink,
      );
    } else {
      body = switch (ref.watch(emailVerificationProvider(token))) {
        AsyncData() => _Result(
            icon: Icons.verified_outlined,
            color: context.brand.success,
            message: l10n.verifyEmailSuccess,
          ),
        AsyncError(:final error) => _Result(
            icon: Icons.error_outline,
            color: Theme.of(context).colorScheme.error,
            message: errorMessage(l10n, error),
          ),
        _ => Column(
            children: [
              const SizedBox(height: AppSpacing.lg),
              const CircularProgressIndicator(),
              const SizedBox(height: AppSpacing.md),
              Text(l10n.verifyEmailInProgress, textAlign: TextAlign.center),
              const SizedBox(height: AppSpacing.lg),
            ],
          ),
      };
    }

    return AuthScaffold(
      title: l10n.verifyEmailTitle,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          body,
          const SizedBox(height: AppSpacing.lg),
          // The router sends a signed-out user on to the sign-in screen.
          FilledButton(
            onPressed: () => context.go(AppRoutes.home),
            child: Text(l10n.actionContinue),
          ),
        ],
      ),
    );
  }
}

class _Result extends StatelessWidget {
  const _Result({required this.icon, required this.color, required this.message});

  final IconData icon;
  final Color color;
  final String message;

  @override
  Widget build(BuildContext context) => Semantics(
        liveRegion: true,
        child: Column(
          children: [
            Icon(icon, color: color, size: AppSpacing.xxl),
            const SizedBox(height: AppSpacing.md),
            Text(
              message,
              textAlign: TextAlign.center,
              style: Theme.of(context).textTheme.bodyLarge,
            ),
          ],
        ),
      );
}
