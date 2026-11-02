import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/api/error_messages.dart';
import '../../../core/legal/legal_documents.dart';
import '../../../core/legal/legal_links.dart';
import '../../../core/router/app_routes.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/theme/app_tokens.dart';
import '../../../core/theme/theme_mode_setting.dart';
import '../../../l10n/app_localizations.dart';
import '../../auth/presentation/auth_controller.dart';
import '../../notifications/domain/push_client.dart';
import '../../notifications/presentation/push_controller.dart';
import 'widgets/status_tile.dart';

/// How the app behaves on this phone, the legal texts, and leaving the account.
class SettingsScreen extends ConsumerWidget {
  const SettingsScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppLocalizations.of(context);
    final theme = Theme.of(context);
    final loggingOut = ref.watch(authControllerProvider).isLoading;

    return Scaffold(
      appBar: AppBar(title: Text(l10n.settingsTitle)),
      body: SafeArea(
        child: Align(
          alignment: Alignment.topCenter,
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: AppSizes.formMaxWidth),
            child: SingleChildScrollView(
              padding: const EdgeInsets.all(AppSpacing.lg),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  const _ThemeChoice(),
                  const _PushStatus(),
                  const SizedBox(height: AppSpacing.md),
                  ListTile(
                    contentPadding: EdgeInsets.zero,
                    leading: const Icon(Icons.block),
                    title: Text(l10n.blockedUsersTitle),
                    trailing: const Icon(Icons.chevron_right),
                    onTap: () => context.go(AppRoutes.blockedUsers),
                  ),
                  const SizedBox(height: AppSpacing.md),
                  Text(l10n.legalTitle, style: theme.textTheme.titleMedium),
                  const LegalLinks(documents: LegalDocument.values),
                  const SizedBox(height: AppSpacing.lg),
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
                  const SizedBox(height: AppSpacing.sm),
                  TextButton(
                    onPressed: loggingOut ? null : () => _deleteAccount(context, ref),
                    style: TextButton.styleFrom(foregroundColor: theme.colorScheme.error),
                    child: Text(l10n.accountDelete),
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}

/// Asks for the password, then for the deletion; a refusal is explained.
Future<void> _deleteAccount(BuildContext context, WidgetRef ref) async {
  final l10n = AppLocalizations.of(context);
  final messenger = ScaffoldMessenger.of(context);
  final password = await showDialog<String>(context: context, builder: (context) => const _DeleteAccountDialog());
  if (password == null) {
    return;
  }

  ref.read(accountRestoredNoticeProvider.notifier).dismiss();
  final deleted = await ref.read(authControllerProvider.notifier).deleteAccount(password: password);
  messenger.showSnackBar(
    SnackBar(
      content: Text(
        deleted ? l10n.accountDeleteScheduled : errorMessage(l10n, ref.read(authControllerProvider).error ?? Object()),
      ),
    ),
  );
}

/// Explains what deleting the account does and collects the password that confirms
/// it. Pops with the password, or null if dismissed.
class _DeleteAccountDialog extends StatefulWidget {
  const _DeleteAccountDialog();

  @override
  State<_DeleteAccountDialog> createState() => _DeleteAccountDialogState();
}

class _DeleteAccountDialogState extends State<_DeleteAccountDialog> {
  final _password = TextEditingController();

  @override
  void dispose() {
    _password.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final theme = Theme.of(context);

    return AlertDialog(
      title: Text(l10n.accountDeleteTitle),
      content: SingleChildScrollView(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text(l10n.accountDeleteBody),
            const SizedBox(height: AppSpacing.md),
            TextField(
              controller: _password,
              obscureText: true,
              onChanged: (_) => setState(() {}),
              decoration: InputDecoration(labelText: l10n.passwordLabel, helperText: l10n.accountDeletePasswordHint),
            ),
          ],
        ),
      ),
      actions: [
        TextButton(onPressed: () => Navigator.pop(context), child: Text(l10n.actionCancel)),
        FilledButton(
          onPressed: _password.text.isEmpty ? null : () => Navigator.pop(context, _password.text),
          style: FilledButton.styleFrom(
            backgroundColor: theme.colorScheme.error,
            foregroundColor: theme.colorScheme.onError,
          ),
          child: Text(l10n.accountDelete),
        ),
      ],
    );
  }
}

/// Light, dark, or as the phone is set.
class _ThemeChoice extends ConsumerWidget {
  const _ThemeChoice();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppLocalizations.of(context);
    final mode = ref.watch(themeModeSettingProvider).value ?? ThemeMode.system;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Text(l10n.themeTitle, style: Theme.of(context).textTheme.titleMedium),
        const SizedBox(height: AppSpacing.sm),
        SegmentedButton<ThemeMode>(
          segments: [
            ButtonSegment(value: ThemeMode.system, icon: const Icon(Icons.brightness_auto_outlined), label: Text(l10n.themeSystem)),
            ButtonSegment(value: ThemeMode.light, icon: const Icon(Icons.light_mode_outlined), label: Text(l10n.themeLight)),
            ButtonSegment(value: ThemeMode.dark, icon: const Icon(Icons.dark_mode_outlined), label: Text(l10n.themeDark)),
          ],
          selected: {mode},
          showSelectedIcon: false,
          onSelectionChanged: (selection) => ref.read(themeModeSettingProvider.notifier).select(selection.single),
        ),
      ],
    );
  }
}

/// Whether pushes reach this phone, and a way to allow them after saying no.
class _PushStatus extends ConsumerWidget {
  const _PushStatus();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppLocalizations.of(context);
    final brand = context.brand;
    final permission = ref.watch(pushPermissionStatusProvider).value;
    if (permission == null || permission == PushPermission.unavailable) {
      return const SizedBox.shrink();
    }

    final granted = permission == PushPermission.granted;
    return Padding(
      padding: const EdgeInsets.only(top: AppSpacing.sm),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          StatusTile(
            icon: granted ? Icons.notifications_active_outlined : Icons.notifications_off_outlined,
            color: granted ? brand.success : brand.warning,
            text: granted ? l10n.pushEnabled : l10n.pushDisabled,
          ),
          if (!granted)
            TextButton(
              onPressed: () async {
                final messenger = ScaffoldMessenger.of(context);
                final result = await ref.read(pushPermissionStatusProvider.notifier).request();
                if (result != PushPermission.granted) {
                  // Android shows its dialog only so many times; after that only the
                  // phone's settings can allow notifications.
                  messenger.showSnackBar(SnackBar(content: Text(l10n.pushEnableInSettings)));
                }
              },
              child: Text(l10n.pushEnable),
            ),
        ],
      ),
    );
  }
}
