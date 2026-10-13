import '../../../core/api/generated/export.dart';
import '../../../l10n/app_localizations.dart';

/// The levels a user can choose, lowest first. Excludes the generated `$unknown`.
const selectableSkillLevels = [
  SkillLevel.beginner,
  SkillLevel.intermediate,
  SkillLevel.advanced,
  SkillLevel.expert,
];

String skillLevelLabel(AppLocalizations l10n, SkillLevel level) => switch (level) {
      SkillLevel.beginner => l10n.skillLevelBeginner,
      SkillLevel.intermediate => l10n.skillLevelIntermediate,
      SkillLevel.advanced => l10n.skillLevelAdvanced,
      SkillLevel.expert => l10n.skillLevelExpert,
      SkillLevel.$unknown => l10n.skillLevelUnknown,
    };

/// A level in the context of a skill type: the user's own level for an offered skill,
/// the minimum level of the teacher they look for in a wanted one.
String userSkillLevelLabel(AppLocalizations l10n, UserSkillType type, SkillLevel level) {
  final label = skillLevelLabel(l10n, level);
  return type == UserSkillType.wanted ? l10n.skillWantedLevel(label) : label;
}

String skillStatusLabel(AppLocalizations l10n, SkillStatus status) => switch (status) {
      SkillStatus.pendingApproval => l10n.skillStatusPending,
      SkillStatus.approved => l10n.skillStatusApproved,
      SkillStatus.rejected => l10n.skillStatusRejected,
      SkillStatus.$unknown => l10n.skillLevelUnknown,
    };
