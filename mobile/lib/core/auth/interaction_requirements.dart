import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';

import '../../l10n/app_localizations.dart';
import '../api/api_exception.dart';
import '../api/api_providers.dart';
import '../api/generated/export.dart';
import '../riverpod.dart';
import '../router/app_routes.dart';
import 'session_controller.dart';

part 'interaction_requirements.g.dart';

/// What the signed-in user still needs before interacting with others; null when
/// signed out. Invalidate it after anything that can change the answer: verifying the
/// email, saving the profile, adding or removing an offered skill.
@Riverpod(keepAlive: true, retry: noRetry)
Future<InteractionRequirements?> interactionRequirements(Ref ref) async {
  final accountId = ref.watch(sessionControllerProvider.select((session) => session.value?.id));
  if (accountId == null) {
    return null;
  }

  return guardApiCall(ref.watch(exparterApiProvider).account.getInteractionRequirements);
}

/// Whether [error] is the backend refusing an action because the user does not meet
/// the interaction requirements yet.
bool isInteractionRequirementError(Object? error) =>
    error is ApiException &&
    (error.code == 'interaction_requirements_not_met' || error.code == 'email_not_verified');

/// Tells the user why an interaction was refused and offers to take them to the
/// profile, where the missing requirements are listed.
Future<void> showInteractionRequiredDialog(BuildContext context) => showDialog<void>(
      context: context,
      builder: (dialogContext) {
        final l10n = AppLocalizations.of(dialogContext);
        return AlertDialog(
          icon: const Icon(Icons.lock_outline),
          content: Text(l10n.interactionRequirements, textAlign: TextAlign.center),
          actions: [
            TextButton(
              onPressed: () => Navigator.pop(dialogContext),
              child: Text(l10n.actionClose),
            ),
            FilledButton(
              onPressed: () {
                Navigator.pop(dialogContext);
                context.go(AppRoutes.profile);
              },
              child: Text(l10n.interactionGoToProfile),
            ),
          ],
        );
      },
    );
