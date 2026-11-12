import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/api/error_messages.dart';
import '../../../core/api/generated/export.dart';
import '../../../core/router/app_routes.dart';
import '../../../core/theme/app_tokens.dart';
import '../../../l10n/app_localizations.dart';
import '../../auth/presentation/widgets/auth_scaffold.dart';
import '../data/api_profile_repository.dart';
import 'profile_controller.dart';

/// Fills in the profile for the first time and edits it later.
class ProfileFormScreen extends ConsumerStatefulWidget {
  const ProfileFormScreen({super.key});

  @override
  ConsumerState<ProfileFormScreen> createState() => _ProfileFormScreenState();
}

class _ProfileFormScreenState extends ConsumerState<ProfileFormScreen> {
  static const _displayNameMin = 2;
  static const _displayNameMax = 50;
  static const _bioMax = 500;

  final _formKey = GlobalKey<FormState>();
  final _displayName = TextEditingController();
  final _bio = TextEditingController();
  String? _city;
  bool _initialized = false;

  @override
  void dispose() {
    _displayName.dispose();
    _bio.dispose();
    super.dispose();
  }

  /// Copies the loaded profile into the form once; later rebuilds must not overwrite
  /// what the user is typing.
  void _initializeFrom(Profile profile) {
    if (_initialized) {
      return;
    }

    _initialized = true;
    _displayName.text = profile.displayName ?? '';
    _bio.text = profile.bio ?? '';
    _city = profile.city;
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) {
      return;
    }

    final saved = await ref.read(profileActionsProvider.notifier).save(
          displayName: _displayName.text,
          bio: _bio.text,
          city: _city,
        );

    if (saved && mounted) {
      context.go(AppRoutes.profile);
    }
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final profile = ref.watch(ownProfileProvider);

    final Widget body;
    switch (profile) {
      case AsyncData(:final value?):
        _initializeFrom(value);
        body = _form(l10n);
      case AsyncError(:final error):
        body = _LoadError(
          message: errorMessage(l10n, error),
          onRetry: () => ref.invalidate(ownProfileProvider),
        );
      default:
        body = const Padding(
          padding: EdgeInsets.all(AppSpacing.xl),
          child: Center(child: CircularProgressIndicator()),
        );
    }

    return Scaffold(
      appBar: AppBar(title: Text(l10n.profileEditTitle)),
      body: SafeArea(
        child: Align(
          alignment: Alignment.topCenter,
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: AppSizes.formMaxWidth),
            child: SingleChildScrollView(
              padding: const EdgeInsets.all(AppSpacing.lg),
              child: body,
            ),
          ),
        ),
      ),
    );
  }

  Widget _form(AppLocalizations l10n) {
    final action = ref.watch(profileActionsProvider);
    final cities = ref.watch(citiesProvider);

    return Form(
      key: _formKey,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          TextFormField(
            controller: _displayName,
            textCapitalization: TextCapitalization.words,
            autofillHints: const [AutofillHints.name],
            textInputAction: TextInputAction.next,
            maxLength: _displayNameMax,
            validator: (value) {
              final length = value?.trim().length ?? 0;
              if (length == 0) {
                return l10n.validationRequired;
              }

              return length < _displayNameMin ? l10n.validationDisplayName : null;
            },
            decoration: InputDecoration(
              labelText: l10n.profileDisplayNameLabel,
              helperText: l10n.profileDisplayNameHelper,
            ),
          ),
          const SizedBox(height: AppSpacing.sm),
          TextFormField(
            controller: _bio,
            textCapitalization: TextCapitalization.sentences,
            minLines: 3,
            maxLines: 6,
            maxLength: _bioMax,
            decoration: InputDecoration(
              labelText: l10n.profileBioLabel,
              hintText: l10n.profileBioHint,
              alignLabelWithHint: true,
            ),
          ),
          const SizedBox(height: AppSpacing.sm),
          switch (cities) {
            AsyncData(:final value) => DropdownButtonFormField<String?>(
                initialValue: _city,
                isExpanded: true,
                items: [
                  DropdownMenuItem(child: Text(l10n.profileCityNone)),
                  for (final city in value) DropdownMenuItem(value: city.code, child: Text(city.name)),
                ],
                onChanged: (value) => setState(() => _city = value),
                decoration: InputDecoration(
                  labelText: l10n.profileCityLabel,
                  helperText: l10n.profileCityHelper,
                ),
              ),
            AsyncError(:final error) => _LoadError(
                message: errorMessage(l10n, error),
                onRetry: () => ref.invalidate(citiesProvider),
              ),
            _ => const Center(child: CircularProgressIndicator()),
          },
          if (action.hasError && !action.isLoading) ...[
            const SizedBox(height: AppSpacing.md),
            FormErrorBanner(message: errorMessage(l10n, action.error!)),
          ],
          const SizedBox(height: AppSpacing.lg),
          SubmitButton(
            label: l10n.profileSave,
            isLoading: action.isLoading,
            onPressed: _submit,
          ),
        ],
      ),
    );
  }
}

class _LoadError extends StatelessWidget {
  const _LoadError({required this.message, required this.onRetry});

  final String message;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) => Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          FormErrorBanner(message: message),
          const SizedBox(height: AppSpacing.sm),
          OutlinedButton(onPressed: onRetry, child: Text(AppLocalizations.of(context).errorRetry)),
        ],
      );
}
