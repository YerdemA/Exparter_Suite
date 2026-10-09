import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/auth/session_controller.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/theme/app_tokens.dart';
import '../../../l10n/app_localizations.dart';
import '../../../core/api/error_messages.dart';
import '../../auth/presentation/auth_controller.dart';
import '../../auth/presentation/recovery_controller.dart';

/// Placeholder landing screen for a signed-in user until the discovery feature exists.
class HomeScreen extends ConsumerWidget {
  const HomeScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppLocalizations.of(context);
    final theme = Theme.of(context);
    final brand = context.brand;
    final account = ref.watch(sessionControllerProvider).value;
    final loggingOut = ref.watch(authControllerProvider).isLoading;
    final restored = ref.watch(accountRestoredNoticeProvider);

    // The router leaves this screen as soon as the session ends; this covers the frame in between.
    if (account == null) {
      return const Scaffold(body: Center(child: CircularProgressIndicator()));
    }

    return Scaffold(
      appBar: AppBar(title: Text(l10n.appName)),
      body: SafeArea(
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: AppSizes.formMaxWidth),
            child: ListView(
              padding: const EdgeInsets.all(AppSpacing.lg),
              children: [
                Text(l10n.homeGreeting(account.username), style: theme.textTheme.headlineMedium),
                const SizedBox(height: AppSpacing.xs),
                Text(
                  account.email,
                  style: theme.textTheme.bodyLarge?.copyWith(
                    color: theme.colorScheme.onSurfaceVariant,
                  ),
                ),
                const SizedBox(height: AppSpacing.lg),
                if (restored) ...[
                  _StatusTile(
                    icon: Icons.restore,
                    color: brand.success,
                    text: l10n.homeAccountRestored,
                  ),
                  const SizedBox(height: AppSpacing.sm),
                ],
                _StatusTile(
                  icon: account.emailVerified ? Icons.verified_outlined : Icons.mark_email_unread_outlined,
                  color: account.emailVerified ? brand.success : brand.warning,
                  text: account.emailVerified ? l10n.homeEmailVerified : l10n.homeEmailNotVerified,
                ),
                if (!account.emailVerified) const _ResendVerificationButton(),
                const SizedBox(height: AppSpacing.xl),
                Text(
                  l10n.homeComingSoon,
                  style: theme.textTheme.bodyLarge,
                ),
                const SizedBox(height: AppSpacing.xl),
                OutlinedButton.icon(
                  onPressed: loggingOut
                      ? null
                      : () {
                          ref.read(accountRestoredNoticeProvider.notifier).dismiss();
                          ref.read(authControllerProvider.notifier).logout();
                        },
                  icon: const Icon(Icons.logout),
                  label: Text(l10n.homeLogout),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

class _ResendVerificationButton extends ConsumerWidget {
  const _ResendVerificationButton();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppLocalizations.of(context);
    final action = ref.watch(recoveryControllerProvider);

    return Align(
      alignment: Alignment.centerLeft,
      child: TextButton.icon(
        onPressed: action.isLoading
            ? null
            : () async {
                final messenger = ScaffoldMessenger.of(context);
                final sent = await ref.read(recoveryControllerProvider.notifier).resendVerificationEmail();
                final error = ref.read(recoveryControllerProvider).error;
                messenger.showSnackBar(
                  SnackBar(
                    content: Text(sent ? l10n.homeVerificationSent : errorMessage(l10n, error ?? Object())),
                  ),
                );
              },
        icon: const Icon(Icons.send_outlined),
        label: Text(l10n.homeResendVerification),
      ),
    );
  }
}

class _StatusTile extends StatelessWidget {
  const _StatusTile({required this.icon, required this.color, required this.text});

  final IconData icon;
  final Color color;
  final String text;

  @override
  Widget build(BuildContext context) => Container(
        padding: const EdgeInsets.all(AppSpacing.md),
        decoration: BoxDecoration(
          color: color.withValues(alpha: 0.12),
          borderRadius: BorderRadius.circular(AppRadius.md),
        ),
        child: Row(
          children: [
            Icon(icon, color: color),
            const SizedBox(width: AppSpacing.md),
            Expanded(child: Text(text)),
          ],
        ),
      );
}
