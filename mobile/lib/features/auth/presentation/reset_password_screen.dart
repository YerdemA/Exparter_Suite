import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/api/error_messages.dart';
import '../../../core/router/app_routes.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/theme/app_tokens.dart';
import '../../../l10n/app_localizations.dart';
import '../domain/auth_validators.dart';
import 'auth_controller.dart';
import 'recovery_controller.dart';
import 'widgets/auth_fields.dart';
import 'widgets/auth_scaffold.dart';

/// Opened by the link in the password reset email.
class ResetPasswordScreen extends ConsumerStatefulWidget {
  const ResetPasswordScreen({required this.token, super.key});

  /// Null when the link was opened without a token.
  final String? token;

  @override
  ConsumerState<ResetPasswordScreen> createState() => _ResetPasswordScreenState();
}

class _ResetPasswordScreenState extends ConsumerState<ResetPasswordScreen> {
  final _formKey = GlobalKey<FormState>();
  final _password = TextEditingController();
  bool _done = false;

  @override
  void dispose() {
    _password.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) {
      return;
    }

    final changed = await ref.read(recoveryControllerProvider.notifier).confirmPasswordReset(
          token: widget.token!,
          newPassword: _password.text,
        );
    if (!changed || !mounted) {
      return;
    }

    setState(() => _done = true);
    // The server has revoked every session of this account. Ending the local one now
    // avoids a confusing "session expired" a few minutes later.
    await ref.read(authControllerProvider.notifier).logout();
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final theme = Theme.of(context);
    final action = ref.watch(recoveryControllerProvider);
    final hasToken = widget.token != null && widget.token!.isNotEmpty;

    final Widget child;
    if (!hasToken || _done) {
      child = Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Semantics(
            liveRegion: true,
            child: Column(
              children: [
                Icon(
                  _done ? Icons.lock_reset : Icons.link_off,
                  color: _done ? context.brand.success : theme.colorScheme.error,
                  size: AppSpacing.xxl,
                ),
                const SizedBox(height: AppSpacing.md),
                Text(
                  _done ? l10n.resetPasswordSuccess : l10n.errorInvalidLink,
                  textAlign: TextAlign.center,
                  style: theme.textTheme.bodyLarge,
                ),
              ],
            ),
          ),
          const SizedBox(height: AppSpacing.lg),
          FilledButton(
            onPressed: () => context.go(AppRoutes.login),
            child: Text(l10n.actionBackToLogin),
          ),
        ],
      );
    } else {
      child = Form(
        key: _formKey,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text(l10n.resetPasswordHint, style: theme.textTheme.bodyLarge),
            const SizedBox(height: AppSpacing.md),
            PasswordField(
              controller: _password,
              isNewPassword: true,
              validator: (value) => fieldErrorText(l10n, AuthValidators.password(value)),
              onSubmitted: _submit,
            ),
            if (action.hasError && !action.isLoading) ...[
              const SizedBox(height: AppSpacing.md),
              FormErrorBanner(message: errorMessage(l10n, action.error!)),
            ],
            const SizedBox(height: AppSpacing.lg),
            SubmitButton(
              label: l10n.resetPasswordSubmit,
              isLoading: action.isLoading,
              onPressed: _submit,
            ),
          ],
        ),
      );
    }

    return AuthScaffold(title: l10n.resetPasswordTitle, child: child);
  }
}
