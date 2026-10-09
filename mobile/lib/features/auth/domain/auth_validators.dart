/// Client-side copies of the contract's field rules, so obvious mistakes are caught
/// before a request is sent. The backend remains the authority.
enum FieldError { required, email, username, password }

abstract final class AuthValidators {
  static final _email = RegExp(r'^[^@\s]+@[^@\s]+\.[^@\s]+$');
  static final _username = RegExp(r'^[a-z0-9_][a-z0-9_.]{1,28}[a-z0-9_]$');

  static const passwordMinLength = 8;

  static FieldError? required(String? value) =>
      (value == null || value.trim().isEmpty) ? FieldError.required : null;

  static FieldError? email(String? value) {
    final missing = required(value);
    if (missing != null) {
      return missing;
    }

    return _email.hasMatch(value!.trim()) ? null : FieldError.email;
  }

  static FieldError? username(String? value) {
    final missing = required(value);
    if (missing != null) {
      return missing;
    }

    return _username.hasMatch(value!) && !value.contains('..') ? null : FieldError.username;
  }

  static FieldError? password(String? value) {
    if (value == null || value.isEmpty) {
      return FieldError.required;
    }

    return value.length >= passwordMinLength ? null : FieldError.password;
  }
}
