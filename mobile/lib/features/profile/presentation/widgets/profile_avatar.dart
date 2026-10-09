import 'package:flutter/material.dart';

import '../../../../core/theme/app_theme.dart';

/// A user's photo, or their initial on the brand gradient when there is none or it
/// fails to load.
class ProfileAvatar extends StatelessWidget {
  const ProfileAvatar({required this.name, required this.imageUrl, required this.size, super.key});

  /// Used for the initial; may be null for a profile that is not filled in yet.
  final String? name;
  final String? imageUrl;
  final double size;

  @override
  Widget build(BuildContext context) {
    final brand = context.brand;
    final trimmed = name?.trim() ?? '';
    final fallback = DecoratedBox(
      decoration: BoxDecoration(gradient: brand.heroGradient),
      child: Center(
        child: trimmed.isEmpty
            ? Icon(Icons.person, color: brand.onHero, size: size * 0.55)
            : Text(
                trimmed.characters.first.toUpperCase(),
                style: TextStyle(
                  color: brand.onHero,
                  fontSize: size * 0.42,
                  fontWeight: FontWeight.w800,
                ),
              ),
      ),
    );

    return ClipOval(
      child: SizedBox.square(
        dimension: size,
        child: imageUrl == null
            ? fallback
            : Image.network(
                imageUrl!,
                // Photos are not cropped on upload; this shows the centre square.
                fit: BoxFit.cover,
                errorBuilder: (context, error, stackTrace) => fallback,
                loadingBuilder: (context, child, progress) => progress == null ? child : fallback,
              ),
      ),
    );
  }
}
