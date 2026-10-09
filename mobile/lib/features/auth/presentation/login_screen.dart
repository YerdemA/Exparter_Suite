import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/api/api_exception.dart';
import '../../../core/api/error_messages.dart';
import '../../../core/auth/session_controller.dart';
import '../../../core/router/app_routes.dart';
import '../../../core/theme/app_tokens.dart';
import '../../../l10n/app_localizations.dart';
import '../domain/auth_validators.dart';
import 'auth_controller.dart';
import 'widgets/auth_fields.dart';
import 'widgets/auth_scaffold.dart';

class LoginScreen extends ConsumerStatefulWidget {
  const LoginScreen({super.key});

  @override
  ConsumerState<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends ConsumerState<LoginScreen> {
  final _formKey = GlobalKey<FormState>();
  final _identifier = TextEditingController();
  final _password = TextEditingController();

  /// Captured once: the user landed here because their session expired.
  late final bool _sessionExpired;

  @override
  void initState() {
    super.initState();
    final session = ref.read(sessionControllerProvider.notifier);
    _sessionExpired = session.expired;
    session.expired = false;
  }

  @override
  void dispose() {
    _identifier.dispose();
    _password.dispose();
    super.dispose();
  }

  void _submit() {
    if (_formKey.currentState!.validate()) {
      ref.read(authControllerProvider.notifier).login(
            identifier: _identifier.text,
            password: _password.text,
          );
    }
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final action = ref.watch(authControllerProvider);
    final error = action.hasError
        ? action.error!
        : (_sessionExpired ? const ApiException(code: ApiException.sessionExpiredCode) : null);

    return AuthScaffold(
      title: l10n.loginTitle,
      child: Form(
        key: _formKey,
        child: AutofillGroup(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              TextFormField(
                controller: _identifier,
                keyboardType: TextInputType.emailAddress,
                autocorrect: false,
                autofillHints: const [AutofillHints.username, AutofillHints.email],
                textInputAction: TextInputAction.next,
                validator: (value) => fieldErrorText(l10n, AuthValidators.required(value)),
                decoration: InputDecoration(labelText: l10n.loginIdentifierLabel),
              ),
              const SizedBox(height: AppSpacing.md),
              PasswordField(
                controller: _password,
                isNewPassword: false,
                validator: (value) => fieldErrorText(
                  l10n,
                  (value == null || value.isEmpty) ? FieldError.required : null,
                ),
                onSubmitted: _submit,
              ),
              if (error != null && !action.isLoading) ...[
                const SizedBox(height: AppSpacing.md),
                FormErrorBanner(message: errorMessage(l10n, error)),
              ],
              const SizedBox(height: AppSpacing.lg),
              SubmitButton(
                label: l10n.loginSubmit,
                isLoading: action.isLoading,
                onPressed: _submit,
              ),
              const SizedBox(height: AppSpacing.md),
              Row(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  Flexible(child: Text(l10n.loginNoAccount)),
                  TextButton(
                    onPressed: action.isLoading ? null : () => context.go(AppRoutes.register),
                    child: Text(l10n.loginGoToRegister),
                  ),
                ],
              ),
            ],
          ),
        ),
      ),
    );
  }
}
