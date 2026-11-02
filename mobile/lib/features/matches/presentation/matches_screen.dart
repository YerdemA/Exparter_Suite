import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/api/error_messages.dart';
import '../../../core/api/generated/export.dart';
import '../../../core/auth/interaction_requirements.dart';
import '../../../core/auth/session_controller.dart';
import '../../../core/router/app_routes.dart';
import '../../../core/theme/app_tokens.dart';
import '../../../l10n/app_localizations.dart';
import '../../profile/presentation/widgets/profile_avatar.dart';
import 'match_controller.dart';
import 'match_labels.dart';

/// The user's matches and the swap requests still waiting for an answer.
class MatchesScreen extends ConsumerWidget {
  const MatchesScreen({this.showIncoming = false, super.key});

  /// Opens on the received requests instead of the matches.
  final bool showIncoming;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppLocalizations.of(context);
    // Watched so the action notifier outlives the request it is running.
    final busy = ref.watch(matchActionsProvider).isLoading;

    return DefaultTabController(
      length: 3,
      initialIndex: showIncoming ? 1 : 0,
      child: Scaffold(
        appBar: AppBar(
          title: Text(l10n.navMatches),
          bottom: TabBar(
            tabs: [
              Tab(text: l10n.matchesTabMatches),
              Tab(text: l10n.matchesTabIncoming),
              Tab(text: l10n.matchesTabOutgoing),
            ],
          ),
        ),
        body: SafeArea(
          child: Column(
            children: [
              if (busy) const LinearProgressIndicator(),
              const Expanded(
                child: TabBarView(
                  children: [
                    _MatchList(),
                    _RequestList(direction: Direction.incoming),
                    _RequestList(direction: Direction.outgoing),
                  ],
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

/// The shared frame of the three tabs: loading, error with retry, empty text, or the
/// list, always refreshable by pulling down.
class _AsyncList<T> extends StatelessWidget {
  const _AsyncList({
    required this.value,
    required this.emptyText,
    required this.onRefresh,
    required this.itemBuilder,
  });

  final AsyncValue<List<T>> value;
  final String emptyText;
  final Future<void> Function() onRefresh;
  final Widget Function(T item) itemBuilder;

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);

    return switch (value) {
      AsyncData(value: final items) => RefreshIndicator(
          onRefresh: onRefresh,
          child: ListView(
            physics: const AlwaysScrollableScrollPhysics(),
            padding: const EdgeInsets.all(AppSpacing.md),
            children: [
              if (items.isEmpty)
                Padding(
                  padding: const EdgeInsets.all(AppSpacing.lg),
                  child: Text(emptyText, textAlign: TextAlign.center),
                ),
              for (final item in items) itemBuilder(item),
            ],
          ),
        ),
      AsyncError(:final error) => Center(
          child: Padding(
            padding: const EdgeInsets.all(AppSpacing.lg),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                Text(errorMessage(l10n, error), textAlign: TextAlign.center),
                const SizedBox(height: AppSpacing.md),
                OutlinedButton(onPressed: onRefresh, child: Text(l10n.errorRetry)),
              ],
            ),
          ),
        ),
      _ => const Center(child: CircularProgressIndicator()),
    };
  }
}

class _MatchList extends ConsumerWidget {
  const _MatchList();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppLocalizations.of(context);
    final ownUserId = ref.watch(sessionControllerProvider).value?.id;

    return _AsyncList<Match>(
      value: ref.watch(matchesProvider),
      emptyText: l10n.matchesEmpty,
      onRefresh: () => ref.refresh(matchesProvider.future),
      itemBuilder: (match) => _MatchCard(match: match, view: MatchView(match, ownUserId)),
    );
  }
}

class _MatchCard extends StatelessWidget {
  const _MatchCard({required this.match, required this.view});

  final Match match;
  final MatchView view;

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final theme = Theme.of(context);

    return Card(
      margin: const EdgeInsets.only(bottom: AppSpacing.md),
      clipBehavior: Clip.antiAlias,
      child: InkWell(
        onTap: () => context.go(AppRoutes.matchDetail(match.id)),
        child: Padding(
          padding: const EdgeInsets.all(AppSpacing.md),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              _UserHeader(user: view.other, trailing: matchStatusLabel(l10n, match.status)),
              const SizedBox(height: AppSpacing.sm),
              _SkillPair(taught: view.taught, learned: view.learned),
              if (view.endRequestedByOther) ...[
                const SizedBox(height: AppSpacing.sm),
                Text(
                  l10n.matchEndAskedByOther(userLabel(l10n, view.other)),
                  style: theme.textTheme.bodyMedium?.copyWith(color: theme.colorScheme.error),
                ),
              ],
            ],
          ),
        ),
      ),
    );
  }
}

class _RequestList extends ConsumerWidget {
  const _RequestList({required this.direction});

  final Direction direction;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppLocalizations.of(context);
    final incoming = direction == Direction.incoming;

    return _AsyncList<SwapRequest>(
      value: ref.watch(pendingRequestsProvider(direction)),
      emptyText: incoming ? l10n.requestsIncomingEmpty : l10n.requestsOutgoingEmpty,
      onRefresh: () => ref.refresh(pendingRequestsProvider(direction).future),
      itemBuilder: (request) => _RequestCard(request: request, incoming: incoming),
    );
  }
}

class _RequestCard extends ConsumerWidget {
  const _RequestCard({required this.request, required this.incoming});

  final SwapRequest request;
  final bool incoming;

  Future<void> _run(BuildContext context, WidgetRef ref, Future<bool> Function(MatchActions actions) action) async {
    final l10n = AppLocalizations.of(context);
    final messenger = ScaffoldMessenger.of(context);
    if (await action(ref.read(matchActionsProvider.notifier)) || !context.mounted) {
      return;
    }

    final error = ref.read(matchActionsProvider).error;
    if (isInteractionRequirementError(error)) {
      await showInteractionRequiredDialog(context);
    } else {
      messenger.showSnackBar(SnackBar(content: Text(errorMessage(l10n, error ?? Object()))));
    }
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppLocalizations.of(context);
    final theme = Theme.of(context);
    final busy = ref.watch(matchActionsProvider).isLoading;
    final other = incoming ? request.requester : request.recipient;

    return Card(
      margin: const EdgeInsets.only(bottom: AppSpacing.md),
      child: Padding(
        padding: const EdgeInsets.all(AppSpacing.md),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            InkWell(
              onTap: () => context.go(AppRoutes.userProfile(other.userId)),
              child: _UserHeader(user: other, trailing: remainingLabel(l10n, request.expiresAt, DateTime.now())),
            ),
            const SizedBox(height: AppSpacing.sm),
            // The requester teaches the offered skill; for the recipient it is the one to learn.
            _SkillPair(
              taught: incoming ? request.requestedSkill : request.offeredSkill,
              learned: incoming ? request.offeredSkill : request.requestedSkill,
            ),
            if (request.message != null) ...[
              const SizedBox(height: AppSpacing.sm),
              Text('“${request.message}”', style: theme.textTheme.bodyMedium?.copyWith(fontStyle: FontStyle.italic)),
            ],
            const SizedBox(height: AppSpacing.sm),
            // The theme's buttons fill the width they are given, so each gets half the row.
            if (incoming)
              Row(
                children: [
                  Expanded(
                    child: OutlinedButton(
                      onPressed: busy ? null : () => _run(context, ref, (actions) => actions.declineRequest(request.id)),
                      child: Text(l10n.requestDecline),
                    ),
                  ),
                  const SizedBox(width: AppSpacing.sm),
                  Expanded(
                    child: FilledButton(
                      onPressed: busy ? null : () => _run(context, ref, (actions) => actions.acceptRequest(request.id)),
                      child: Text(l10n.requestAccept),
                    ),
                  ),
                ],
              )
            else
              OutlinedButton(
                onPressed: busy ? null : () => _run(context, ref, (actions) => actions.cancelRequest(request.id)),
                child: Text(l10n.requestCancel),
              ),
          ],
        ),
      ),
    );
  }
}

class _UserHeader extends StatelessWidget {
  const _UserHeader({required this.user, required this.trailing});

  static const _avatarSize = 44.0;

  final UserSummary user;
  final String trailing;

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final theme = Theme.of(context);

    return Row(
      children: [
        ProfileAvatar(name: user.displayName ?? user.username, imageUrl: user.avatarUrl, size: _avatarSize),
        const SizedBox(width: AppSpacing.md),
        Expanded(child: Text(userLabel(l10n, user), style: theme.textTheme.titleMedium)),
        Text(trailing, style: theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.onSurfaceVariant)),
      ],
    );
  }
}

/// The two skills of a swap from the viewer's side.
class _SkillPair extends StatelessWidget {
  const _SkillPair({required this.taught, required this.learned});

  final SkillRef taught;
  final SkillRef learned;

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final style = Theme.of(context).textTheme.bodyMedium;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(l10n.swapYouTeach(taught.name), style: style),
        Text(l10n.swapYouLearn(learned.name), style: style),
      ],
    );
  }
}
