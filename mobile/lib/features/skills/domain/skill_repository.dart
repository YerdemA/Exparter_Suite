import '../../../core/api/generated/export.dart';

/// Skill catalog and user skill operations. Failures are thrown as `ApiException`.
abstract interface class SkillRepository {
  Future<List<SkillCategory>> listCategories();

  /// The first page of approved skills matching the filters, ordered by name.
  Future<SkillPage> searchSkills({String? categoryId, String? query});

  Future<List<UserSkill>> listOwnSkills();

  Future<UserSkill> addOwnSkill({
    required String skillId,
    required UserSkillType type,
    required SkillLevel level,
  });

  Future<UserSkill> changeOwnSkillLevel({required String userSkillId, required SkillLevel level});

  Future<void> removeOwnSkill({required String userSkillId});

  Future<Skill> proposeSkill({required String categoryId, required String name});

  Future<List<Skill>> listOwnProposals();
}
