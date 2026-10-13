import '../../../l10n/app_localizations.dart';

/// How a user is willing to meet, e.g. "Çevrimiçi · Yüz yüze · İzmir". Null if they
/// have not said yet.
String? swapModesLabel(
  AppLocalizations l10n, {
  required bool acceptsOnline,
  required bool acceptsInPerson,
  required String? cityName,
}) {
  final parts = [
    if (acceptsOnline) l10n.profileModeOnline,
    if (acceptsInPerson) cityName == null ? l10n.profileModeInPerson : l10n.profileModeInPersonIn(cityName),
  ];
  return parts.isEmpty ? null : parts.join(' · ');
}
