import 'package:riverpod_annotation/riverpod_annotation.dart';

import '../../../core/api/api_exception.dart';
import '../../../core/api/api_providers.dart';
import '../../../core/api/generated/export.dart';
import '../domain/skill_repository.dart';

part 'api_skill_repository.g.dart';

@Riverpod(keepAlive: true)
SkillRepository skillRepository(Ref ref) => ApiSkillRepository(ref.watch(exparterApiProvider));

class ApiSkillRepository implements SkillRepository {
  const ApiSkillRepository(this._api);

  /// The largest page the API serves. A user has at most ten skills and the picker
  /// narrows by category or search, so one page is enough everywhere this is used.
  static const _pageSize = 50;

  final ExparterApi _api;

  @override
  Future<List<SkillCategory>> listCategories() => guardApiCall(_api.skills.listSkillCategories);

  @override
  Future<SkillPage> searchSkills({String? categoryId, String? query}) => guardApiCall(
        () => _api.skills.listSkills(
          categoryId: categoryId,
          q: (query == null || query.trim().isEmpty) ? null : query.trim(),
          pageSize: _pageSize,
        ),
      );

  @override
  Future<List<UserSkill>> listOwnSkills() =>
      guardApiCall(() async => (await _api.skills.listOwnSkills(pageSize: _pageSize)).items);

  @override
  Future<UserSkill> addOwnSkill({
    required String skillId,
    required UserSkillType type,
    required SkillLevel level,
  }) =>
      guardApiCall(
        () => _api.skills.addOwnSkill(
          body: AddUserSkillRequest(skillId: skillId, type: type, level: level),
        ),
      );

  @override
  Future<UserSkill> changeOwnSkillLevel({required String userSkillId, required SkillLevel level}) =>
      guardApiCall(
        () => _api.skills.updateOwnSkill(
          userSkillId: userSkillId,
          body: UpdateUserSkillRequest(level: level),
        ),
      );

  @override
  Future<void> removeOwnSkill({required String userSkillId}) =>
      guardApiCall(() => _api.skills.removeOwnSkill(userSkillId: userSkillId));

  @override
  Future<Skill> proposeSkill({required String categoryId, required String name}) => guardApiCall(
        () => _api.skills.proposeSkill(body: ProposeSkillRequest(categoryId: categoryId, name: name)),
      );

  @override
  Future<List<Skill>> listOwnProposals() =>
      guardApiCall(() async => (await _api.skills.listOwnSkillProposals(pageSize: _pageSize)).items);
}
