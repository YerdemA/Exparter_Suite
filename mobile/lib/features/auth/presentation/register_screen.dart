import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/api/error_messages.dart';
import '../../../core/legal/legal_documents.dart';
import '../../../core/legal/legal_links.dart';
import '../../../core/router/app_routes.dart';
import '../../../core/theme/app_tokens.dart';
import '../../../l10n/app_localizations.dart';
import '../domain/auth_validators.dart';
import 'auth_controller.dart';
import 'widgets/auth_fields.dart';
import 'widgets/auth_scaffold.dart';

class RegisterScreen extends ConsumerStatefulWidget {
  const RegisterScreen({super.key});

  @override
  ConsumerState<RegisterScreen> createState() => _RegisterScreenState();
}

class _RegisterScreenState extends ConsumerState<RegisterScreen> {
  final _formKey = GlobalKey<FormState>();
  final _email = TextEditingController();
  final _username = TextEditingController();
  final _password = TextEditingController();
  // One box for both: the user is 18 or older and accepts the terms of use.
  bool _agreed = false;

  @override
  void dispose() {
    _email.dispose();
    _username.dispose();
    _password.dispose();
    super.dispose();
  }

  void _submit() {
    if (_formKey.currentState!.validate()) {
      ref.read(authControllerProvider.notifier).register(
            email: _email.text,
            username: _username.text,
            password: _password.text,
            ageConfirmed: _agreed,
            termsAccepted: _agreed,
          );
    }
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final action = ref.watch(authControllerProvider);

    return AuthScaffold(
      title: l10n.registerTitle,
      child: Form(
        key: _formKey,
        child: AutofillGroup(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              TextFormField(
                controller: _email,
                keyboardType: TextInputType.emailAddress,
                autocorrect: false,
                autofillHints: const [AutofillHints.email],
                textInputAction: TextInputAction.next,
                validator: (value) => fieldErrorText(l10n, AuthValidators.email(value)),
                decoration: InputDecoration(labelText: l10n.registerEmailLabel),
              ),
              const SizedBox(height: AppSpacing.md),
              TextFormField(
                controller: _username,
                autocorrect: false,
                enableSuggestions: false,
                // Stops the keyboard from capitalizing the first letter, which the
                // lowercase-only rule would reject.
                textCapitalization: TextCapitalization.none,
                // A username cannot contain spaces, so they are not let in, typed or pasted.
                inputFormatters: [FilteringTextInputFormatter.deny(RegExp(r'\s'))],
                autofillHints: const [AutofillHints.newUsername],
                textInputAction: TextInputAction.next,
                validator: (value) => fieldErrorText(l10n, AuthValidators.username(value)),
                decoration: InputDecoration(
                  labelText: l10n.registerUsernameLabel,
                  helperText: l10n.registerUsernameHelper,
                  prefixText: '@',
                ),
              ),
              const SizedBox(height: AppSpacing.md),
              PasswordField(
                controller: _password,
                isNewPassword: true,
                validator: (value) => fieldErrorText(l10n, AuthValidators.password(value)),
                onSubmitted: _submit,
              ),
              const SizedBox(height: AppSpacing.sm),
              FormField<bool>(
                initialValue: _agreed,
                validator: (value) => (value ?? false) ? null : l10n.validationAgeConfirmation,
                builder: (field) => Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    CheckboxListTile(
                      value: _agreed,
                      onChanged: (value) {
                        setState(() => _agreed = value ?? false);
                        field.didChange(_agreed);
                      },
                      title: Text(l10n.registerAgeConfirmation),
                      controlAffinity: ListTileControlAffinity.leading,
                      contentPadding: EdgeInsets.zero,
                      isError: field.hasError,
                    ),
                    if (field.hasError)
                      Padding(
                        padding: const EdgeInsets.only(left: AppSpacing.md),
                        child: Text(
                          field.errorText!,
                          style: Theme.of(context).textTheme.bodySmall?.copyWith(
                                color: Theme.of(context).colorScheme.error,
                              ),
                        ),
                      ),
                  ],
                ),
              ),
              // The data notice is read, not agreed to: processing does not rest on consent
              // (docs/legal/compliance-research.md).
              Text(l10n.registerDataNotice, style: Theme.of(context).textTheme.bodySmall),
              const LegalLinks(documents: [LegalDocument.terms, LegalDocument.dataNotice]),
              if (action.hasError && !action.isLoading) ...[
                const SizedBox(height: AppSpacing.md),
                FormErrorBanner(message: errorMessage(l10n, action.error!)),
              ],
              const SizedBox(height: AppSpacing.lg),
              SubmitButton(
                label: l10n.registerSubmit,
                isLoading: action.isLoading,
                onPressed: _submit,
              ),
              const SizedBox(height: AppSpacing.md),
              Row(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  Flexible(child: Text(l10n.registerHaveAccount)),
                  TextButton(
                    onPressed: action.isLoading ? null : () => context.go(AppRoutes.login),
                    child: Text(l10n.registerGoToLogin),
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
