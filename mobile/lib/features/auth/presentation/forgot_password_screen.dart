import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/api/error_messages.dart';
import '../../../core/router/app_routes.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/theme/app_tokens.dart';
import '../../../l10n/app_localizations.dart';
import '../domain/auth_validators.dart';
import 'recovery_controller.dart';
import 'widgets/auth_fields.dart';
import 'widgets/auth_scaffold.dart';

class ForgotPasswordScreen extends ConsumerStatefulWidget {
  const ForgotPasswordScreen({super.key});

  @override
  ConsumerState<ForgotPasswordScreen> createState() => _ForgotPasswordScreenState();
}

class _ForgotPasswordScreenState extends ConsumerState<ForgotPasswordScreen> {
  final _formKey = GlobalKey<FormState>();
  final _email = TextEditingController();
  bool _sent = false;

  @override
  void dispose() {
    _email.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) {
      return;
    }

    final sent = await ref
        .read(recoveryControllerProvider.notifier)
        .requestPasswordReset(email: _email.text);
    if (sent && mounted) {
      setState(() => _sent = true);
    }
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final action = ref.watch(recoveryControllerProvider);

    return AuthScaffold(
      title: l10n.forgotPasswordTitle,
      child: _sent
          ? Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Semantics(
                  liveRegion: true,
                  child: Column(
                    children: [
                      Icon(
                        Icons.mark_email_read_outlined,
                        color: context.brand.success,
                        size: AppSpacing.xxl,
                      ),
                      const SizedBox(height: AppSpacing.md),
                      Text(
                        l10n.forgotPasswordSent,
                        textAlign: TextAlign.center,
                        style: Theme.of(context).textTheme.bodyLarge,
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
            )
          : Form(
              key: _formKey,
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Text(l10n.forgotPasswordHint, style: Theme.of(context).textTheme.bodyLarge),
                  const SizedBox(height: AppSpacing.md),
                  TextFormField(
                    controller: _email,
                    keyboardType: TextInputType.emailAddress,
                    autocorrect: false,
                    autofillHints: const [AutofillHints.email],
                    textInputAction: TextInputAction.done,
                    onFieldSubmitted: (_) => _submit(),
                    validator: (value) => fieldErrorText(l10n, AuthValidators.email(value)),
                    decoration: InputDecoration(labelText: l10n.registerEmailLabel),
                  ),
                  if (action.hasError && !action.isLoading) ...[
                    const SizedBox(height: AppSpacing.md),
                    FormErrorBanner(message: errorMessage(l10n, action.error!)),
                  ],
                  const SizedBox(height: AppSpacing.lg),
                  SubmitButton(
                    label: l10n.forgotPasswordSubmit,
                    isLoading: action.isLoading,
                    onPressed: _submit,
                  ),
                  const SizedBox(height: AppSpacing.sm),
                  TextButton(
                    onPressed: action.isLoading ? null : () => context.go(AppRoutes.login),
                    child: Text(l10n.actionBackToLogin),
                  ),
                ],
              ),
            ),
    );
  }
}
