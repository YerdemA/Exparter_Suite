import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import '../../../../l10n/app_localizations.dart';
import '../../domain/auth_validators.dart';

String? fieldErrorText(AppLocalizations l10n, FieldError? error) => switch (error) {
      null => null,
      FieldError.required => l10n.validationRequired,
      FieldError.email => l10n.validationEmail,
      FieldError.username => l10n.validationUsername,
      FieldError.password => l10n.validationPassword,
    };

/// Usernames are lowercase only, so capitals are turned into small letters as they are
/// typed or pasted instead of being rejected afterwards.
class LowercaseFormatter extends TextInputFormatter {
  const LowercaseFormatter();

  @override
  TextEditingValue formatEditUpdate(TextEditingValue oldValue, TextEditingValue newValue) {
    // The Turkish dotted capital I would otherwise become "i" plus a combining dot.
    final text = newValue.text.replaceAll('İ', 'i').toLowerCase();
    if (text == newValue.text) {
      return newValue;
    }

    return text.length == newValue.text.length
        ? newValue.copyWith(text: text)
        : TextEditingValue(text: text, selection: TextSelection.collapsed(offset: text.length));
  }
}

class PasswordField extends StatefulWidget {
  const PasswordField({
    required this.controller,
    required this.validator,
    required this.isNewPassword,
    this.onSubmitted,
    super.key,
  });

  final TextEditingController controller;
  final FormFieldValidator<String> validator;

  /// Tells password managers whether to offer a saved password or suggest a new one.
  final bool isNewPassword;
  final VoidCallback? onSubmitted;

  @override
  State<PasswordField> createState() => _PasswordFieldState();
}

class _PasswordFieldState extends State<PasswordField> {
  bool _obscured = true;

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);

    return TextFormField(
      controller: widget.controller,
      obscureText: _obscured,
      autofillHints: [widget.isNewPassword ? AutofillHints.newPassword : AutofillHints.password],
      textInputAction: TextInputAction.done,
      onFieldSubmitted: (_) => widget.onSubmitted?.call(),
      validator: widget.validator,
      decoration: InputDecoration(
        labelText: l10n.passwordLabel,
        suffixIcon: IconButton(
          tooltip: _obscured ? l10n.passwordShow : l10n.passwordHide,
          icon: Icon(_obscured ? Icons.visibility_outlined : Icons.visibility_off_outlined),
          onPressed: () => setState(() => _obscured = !_obscured),
        ),
      ),
    );
  }
}
