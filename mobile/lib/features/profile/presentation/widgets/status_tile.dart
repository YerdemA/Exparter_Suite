import 'package:flutter/material.dart';

import '../../../../core/theme/app_tokens.dart';

/// One line of account or device status, tinted by how it stands.
class StatusTile extends StatelessWidget {
  const StatusTile({required this.icon, required this.color, required this.text, super.key});

  final IconData icon;
  final Color color;
  final String text;

  @override
  Widget build(BuildContext context) => Container(
        padding: const EdgeInsets.all(AppSpacing.md),
        decoration: BoxDecoration(
          color: color.withValues(alpha: 0.12),
          borderRadius: BorderRadius.circular(AppRadius.md),
        ),
        child: Row(
          children: [
            Icon(icon, color: color),
            const SizedBox(width: AppSpacing.md),
            Expanded(child: Text(text)),
          ],
        ),
      );
}
