import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';

import '../../../core/api/generated/export.dart';
import '../../../core/auth/interaction_requirements.dart';
import '../../../core/auth/session_controller.dart';
import '../../../core/riverpod.dart';
import '../data/api_skill_repository.dart';
import '../domain/skill_repository.dart';

part 'skill_controller.g.dart';

@Riverpod(keepAlive: true, retry: noRetry)
Future<List<SkillCategory>> skillCategories(Ref ref) => ref.watch(skillRepositoryProvider).listCategories();

/// Catalog skills for the picker, narrowed by category and search text.
@Riverpod(retry: noRetry)
Future<SkillPage> skillSearch(Ref ref, {required String? categoryId, required String query}) =>
    ref.watch(skillRepositoryProvider).searchSkills(categoryId: categoryId, query: query);

@Riverpod(retry: noRetry)
Future<List<Skill>> ownSkillProposals(Ref ref) => ref.watch(skillRepositoryProvider).listOwnProposals();

/// The signed-in user's skills, offered and wanted; empty when signed out.
@Riverpod(keepAlive: true, retry: noRetry)
class OwnSkills extends _$OwnSkills {
  @override
  Future<List<UserSkill>> build() async {
    final accountId = ref.watch(sessionControllerProvider.select((session) => session.value?.id));
    return accountId == null ? const [] : ref.watch(skillRepositoryProvider).listOwnSkills();
  }
}

/// Runs the skill write actions. Its state is the progress of the action in flight;
/// each method reports whether it succeeded.
@riverpod
class SkillActions extends _$SkillActions {
  @override
  FutureOr<void> build() {}

  Future<bool> add({required String skillId, required UserSkillType type, required SkillLevel level}) =>
      _run((repository) => repository.addOwnSkill(skillId: skillId, type: type, level: level));

  Future<bool> changeLevel({required String userSkillId, required SkillLevel level}) =>
      _run((repository) => repository.changeOwnSkillLevel(userSkillId: userSkillId, level: level));

  Future<bool> remove({required String userSkillId}) =>
      _run((repository) => repository.removeOwnSkill(userSkillId: userSkillId));

  Future<bool> propose({required String categoryId, required String name}) => _run(
        (repository) => repository.proposeSkill(categoryId: categoryId, name: name.trim()),
        refreshSkills: false,
      );

  Future<bool> _run(
    Future<void> Function(SkillRepository repository) action, {
    bool refreshSkills = true,
  }) async {
    state = const AsyncLoading();
    try {
      await action(ref.read(skillRepositoryProvider));
      if (ref.mounted) {
        if (refreshSkills) {
          ref.invalidate(ownSkillsProvider);
          // Adding or removing an offered skill can change whether the user may interact.
          ref.invalidate(interactionRequirementsProvider);
        } else {
          ref.invalidate(ownSkillProposalsProvider);
        }

        state = const AsyncData(null);
      }

      return true;
    } on Object catch (error, stackTrace) {
      if (ref.mounted) {
        state = AsyncError(error, stackTrace);
      }

      return false;
    }
  }
}
