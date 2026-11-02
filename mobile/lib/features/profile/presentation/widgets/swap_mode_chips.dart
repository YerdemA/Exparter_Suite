import 'package:flutter/material.dart';

import '../../../../core/theme/app_tokens.dart';
import '../../../../l10n/app_localizations.dart';

/// How a user is willing to swap, on their own profile and when others look at it.
class SwapModeChips extends StatelessWidget {
  const SwapModeChips({
    required this.acceptsOnline,
    required this.acceptsInPerson,
    required this.cityName,
    super.key,
  });

  final bool acceptsOnline;
  final bool acceptsInPerson;
  final String? cityName;

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);

    return Wrap(
      alignment: WrapAlignment.center,
      spacing: AppSpacing.sm,
      runSpacing: AppSpacing.sm,
      children: [
        if (acceptsOnline) Chip(avatar: const Icon(Icons.videocam_outlined), label: Text(l10n.profileModeOnline)),
        if (acceptsInPerson)
          Chip(
            avatar: const Icon(Icons.place_outlined),
            label: Text(cityName == null ? l10n.profileModeInPerson : l10n.profileModeInPersonIn(cityName!)),
          ),
      ],
    );
  }
}
