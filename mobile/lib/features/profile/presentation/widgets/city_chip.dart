import 'package:flutter/material.dart';

/// The city a user says they live in, on their own profile and when others look at it.
class CityChip extends StatelessWidget {
  const CityChip({required this.cityName, super.key});

  final String cityName;

  @override
  Widget build(BuildContext context) => Center(
        child: Chip(avatar: const Icon(Icons.place_outlined), label: Text(cityName)),
      );
}
