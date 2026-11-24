import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../app/tokens.dart';
import '../../core/api_client.dart';
import '../../core/async_view.dart';
import '../../core/format.dart';
import '../../shared/shared.dart';
import '../grading/grading.dart';
import 'awarding.dart';
import 'awarding_widgets.dart';

/// M-T4 (2/2): award slots, recipients and the publish checklist of one competition (UC-06).
/// After publishing: revoke, award extra, adjust evaluation.
class AwardingDetailScreen extends ConsumerWidget {
  const AwardingDetailScreen({super.key, required this.competitionId});

  final int competitionId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final detail = ref.watch(awardingDetailProvider(competitionId));
    return Scaffold(
      appBar: AppBar(title: const Text('Awards')),
      body: AsyncValueView(
        value: detail,
        onRetry: () => ref.invalidate(awardingDetailProvider(competitionId)),
        data: (d) => _Body(detail: d),
      ),
    );
  }
}

class _Body extends ConsumerStatefulWidget {
  const _Body({required this.detail});

  final AwardingDetail detail;

  @override
  ConsumerState<_Body> createState() => _BodyState();
}

class _BodyState extends ConsumerState<_Body> {
  bool _busy = false;

  AwardingDetail get _d => widget.detail;

  void _toast(String text) => ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(text)));

  // Runs a write: locks the buttons while waiting, shows the API's error, and always reloads
  // (a 409, BR-52, then shows the latest server state).
  Future<void> _run(Future<void> Function() action, {String? success}) async {
    final container = ProviderScope.containerOf(context);
    setState(() => _busy = true);
    try {
      await action();
      if (!mounted) return;
      if (success != null) _toast(success);
    } catch (e) {
      if (!mounted) return;
      _toast(ApiException.from(e).message);
    } finally {
      refreshAwardingData(container);
      if (mounted) setState(() => _busy = false);
    }
  }

  // ---- Remarks of a given award (the winner and the award come from the ranking, BR-30) ----
  Future<void> _edit(AwardSlot slot, IssuedAward award) async {
    final remarks = await showDialog<String>(
      context: context,
      builder: (context) => _RemarksDialog(title: '${slot.name} – remarks', initial: award.remarks ?? ''),
    );
    if (remarks == null || !mounted) return;
    await _run(
      () => ref.read(awardingRepositoryProvider).update(
            award,
            awardId: slot.id,
            submissionId: award.submissionId,
            remarks: remarks.isEmpty ? null : remarks,
          ),
      success: 'Remarks saved.',
    );
  }

  // ---- Delete an award (before publishing only) ----
  Future<void> _delete(AwardSlot slot, IssuedAward award) async {
    final ok = await _confirm(
      'Delete award?',
      '${slot.name} for ${award.studentName} will be deleted and the slot becomes free again.',
      'Delete',
    );
    if (ok != true || !mounted) return;
    await _run(() => ref.read(awardingRepositoryProvider).delete(award), success: 'Award deleted.');
  }

  // ---- Revoke an award (after publishing, reason required) ----
  Future<void> _revoke(AwardSlot slot, IssuedAward award) async {
    final reason = await _askReason('Revoke ${slot.name}', 'Reason for revoking *');
    if (reason == null || !mounted) return;
    await _run(() => ref.read(awardingRepositoryProvider).revoke(award, reason), success: 'Award revoked.');
  }

  // ---- Mark delivered / not delivered ----
  Future<void> _toggleDelivery(IssuedAward award) => _run(
        () => ref.read(awardingRepositoryProvider).setDelivery(award, !award.isDelivered),
        success: award.isDelivered ? 'Delivery mark removed.' : 'Delivery recorded.',
      );

  // ---- Publish results (UC-06 step 5): the awards are given from the ranking (BR-30) ----
  Future<void> _publish() async {
    final ok = await _confirm(
      'Publish results?',
      'The awards are given from the jury ranking shown on this page (equal scores share an award). '
          'Students and visitors see the results, and grades can no longer be changed.',
      'Publish',
    );
    if (ok != true || !mounted) return;
    await _run(() => ref.read(awardingRepositoryProvider).publish(_d.id), success: 'Results published.');
  }

  Future<bool?> _confirm(String title, String text, String action) => showDialog<bool>(
        context: context,
        builder: (context) => AlertDialog(
          title: Text(title),
          content: Text(text),
          actions: [
            TextButton(onPressed: () => Navigator.pop(context, false), child: const Text('Cancel')),
            FilledButton(onPressed: () => Navigator.pop(context, true), child: Text(action)),
          ],
        ),
      );

  Future<String?> _askReason(String title, String label) => showDialog<String>(
        context: context,
        builder: (context) => _ReasonDialog(title: title, label: label),
      );

  @override
  Widget build(BuildContext context) {
    final d = _d;
    final text = Theme.of(context).textTheme;

    return RefreshIndicator(
      onRefresh: () async {
        ref.invalidate(awardingDetailProvider(d.id));
        await ref.read(awardingDetailProvider(d.id).future);
      },
      child: ListView(
        physics: const AlwaysScrollableScrollPhysics(),
        padding: const EdgeInsets.all(AppSpacing.gutter),
        children: [
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Expanded(child: Text(d.title, style: text.headlineSmall)),
              const SizedBox(width: AppSpacing.sm),
              StatusChip(d.status),
            ],
          ),
          if (d.publishedAt != null)
            Text('Published ${formatDateTime(d.publishedAt!)}', style: text.bodySmall),
          if (d.adjustedAt != null) AdjustedLabel(d.adjustedAt!),
          const SizedBox(height: AppSpacing.md),

          // Publish checklist (BR-32): only before publishing
          if (!d.isPublished) _Checklist(detail: d, busy: _busy, onPublish: _publish),
          if (d.isPublished)
            Container(
              padding: const EdgeInsets.all(AppSpacing.md),
              decoration: BoxDecoration(
                color: AppColors.mist,
                borderRadius: AppRadius.mdAll,
                border: Border.all(color: AppColors.line),
              ),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Text('Results are published and the awards were given from the ranking. An award can only be revoked, with a reason.'),
                  // Evaluations are changed per entry; go to the entry list of the competition
                  if (d.canAdjustEvaluation) ...[
                    const SizedBox(height: AppSpacing.sm),
                    OutlinedButton.icon(
                      onPressed: () => context.push('/staff/competitions/${d.id}/submissions'),
                      icon: const Icon(Icons.rate_review_outlined),
                      label: const Text('Adjust one evaluation'),
                    ),
                  ],
                ],
              ),
            ),
          const SizedBox(height: AppSpacing.sm),

          if (d.ranking.isNotEmpty) _RankingCard(detail: d),
          const SizedBox(height: AppSpacing.sm),

          for (final slot in d.awards)
            _SlotCard(
              slot: slot,
              published: d.isPublished,
              canMarkDelivered: d.canMarkDelivered,
              canAdjustEvaluation: d.canAdjustEvaluation,
              busy: _busy,
              canAdd: false,
              onAdd: () {},
              onEdit: (a) => _edit(slot, a),
              onDelete: (a) => _delete(slot, a),
              onRevoke: (a) => _revoke(slot, a),
              onDelivery: _toggleDelivery,
              onAdjust: (_) {},
            ),
          if (d.awards.isEmpty)
            const EmptyState(
              icon: Icons.emoji_events_outlined,
              title: 'No awards defined',
              message: 'This competition has no prizes yet.',
            ),
        ],
      ),
    );
  }
}

/// Checklist before publishing: every entry graded by the whole jury (BR-08).
class _Checklist extends StatelessWidget {
  const _Checklist({required this.detail, required this.busy, required this.onPublish});

  final AwardingDetail detail;
  final bool busy;
  final VoidCallback onPublish;

  @override
  Widget build(BuildContext context) {
    final d = detail;
    final allGraded = d.pendingEvaluations == 0;

    Widget line(bool ok, String text) => Padding(
          padding: const EdgeInsets.symmetric(vertical: 2),
          child: Row(
            children: [
              Icon(ok ? Icons.check_circle : Icons.error_outline,
                  size: 20, color: ok ? AppColors.statusOngoing : AppColors.danger),
              const SizedBox(width: AppSpacing.sm),
              Expanded(child: Text(text)),
            ],
          ),
        );

    return Card(
      margin: EdgeInsets.zero,
      child: Padding(
        padding: const EdgeInsets.all(AppSpacing.md),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Semantics(header: true, child: Text('Before publishing', style: Theme.of(context).textTheme.titleMedium)),
            const SizedBox(height: AppSpacing.xs),
            line(
              allGraded,
              allGraded
                  ? 'All ${d.submissionCount} entries are graded'
                  : '${d.pendingEvaluations} entries still to grade (${d.evaluatedCount}/${d.submissionCount})',
            ),
            line(true, 'The awards are given from the jury ranking when you publish'),
            const SizedBox(height: AppSpacing.sm),
            FilledButton.icon(
              onPressed: (d.canPublish && !busy) ? onPublish : null,
              icon: const Icon(Icons.campaign),
              label: const Text('Publish results'),
            ),
            if (d.publishBlockReason != null)
              Padding(
                padding: const EdgeInsets.only(top: 6),
                child: Text(d.publishBlockReason!, style: const TextStyle(color: AppColors.danger)),
              ),
          ],
        ),
      ),
    );
  }
}

/// One defined award with its issued places.
class _SlotCard extends StatelessWidget {
  const _SlotCard({
    required this.slot,
    required this.published,
    required this.canMarkDelivered,
    required this.canAdjustEvaluation,
    required this.busy,
    required this.canAdd,
    required this.onAdd,
    required this.onEdit,
    required this.onDelete,
    required this.onRevoke,
    required this.onDelivery,
    required this.onAdjust,
  });

  final AwardSlot slot;
  final bool published;
  final bool canMarkDelivered;
  final bool canAdjustEvaluation;
  final bool busy;
  final bool canAdd;
  final VoidCallback onAdd;
  final void Function(IssuedAward) onEdit;
  final void Function(IssuedAward) onDelete;
  final void Function(IssuedAward) onRevoke;
  final void Function(IssuedAward) onDelivery;
  final void Function(IssuedAward) onAdjust;

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    return Card(
      margin: const EdgeInsets.only(top: AppSpacing.md),
      child: Padding(
        padding: const EdgeInsets.all(AppSpacing.md),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                CircleAvatar(
                  radius: 14,
                  backgroundColor: AppColors.brass,
                  foregroundColor: Colors.white,
                  child: Text('${slot.rank}', style: text.labelMedium!.copyWith(color: Colors.white)),
                ),
                const SizedBox(width: AppSpacing.sm),
                Expanded(child: Text('${slot.name} · ${formatMoney(slot.prizeValue)}', style: text.titleMedium)),
              ],
            ),
            const SizedBox(height: AppSpacing.xs),
            Text(
              '${slotsLabel(slot.activeCount, slot.quantity)} awarded'
              '${slot.remaining > 0 ? ' · ${slot.remaining} free' : ''}',
              style: text.bodySmall,
            ),
            for (final a in slot.issued)
              _IssuedTile(
                award: a,
                published: published,
                canMarkDelivered: canMarkDelivered,
                canAdjustEvaluation: canAdjustEvaluation,
                busy: busy,
                onEdit: () => onEdit(a),
                onDelete: () => onDelete(a),
                onRevoke: () => onRevoke(a),
                onDelivery: () => onDelivery(a),
                onAdjust: () => onAdjust(a),
              ),
            if (canAdd)
              Align(
                alignment: Alignment.centerLeft,
                child: TextButton.icon(
                  onPressed: busy ? null : onAdd,
                  icon: const Icon(Icons.add),
                  label: Text(published ? 'Award extra' : 'Award'),
                ),
              ),
          ],
        ),
      ),
    );
  }
}

/// An issued award: the framed picture, recipient, rating and the actions that fit the state.
class _IssuedTile extends StatelessWidget {
  const _IssuedTile({
    required this.award,
    required this.published,
    required this.canMarkDelivered,
    required this.canAdjustEvaluation,
    required this.busy,
    required this.onEdit,
    required this.onDelete,
    required this.onRevoke,
    required this.onDelivery,
    required this.onAdjust,
  });

  final IssuedAward award;
  final bool published;
  final bool canMarkDelivered;
  final bool canAdjustEvaluation;
  final bool busy;
  final VoidCallback onEdit;
  final VoidCallback onDelete;
  final VoidCallback onRevoke;
  final VoidCallback onDelivery;
  final VoidCallback onAdjust;

  @override
  Widget build(BuildContext context) {
    final a = award;
    final text = Theme.of(context).textTheme;
    final revoked = !a.isActive;
    final strike = revoked ? const TextStyle(decoration: TextDecoration.lineThrough) : null;

    // Actions by state: before publishing edit/delete; after publishing delivery/adjust/revoke (BR-32, BR-33).
    // Delivery and adjust are hidden when the API says they are unsupported.
    final actions = <PopupMenuEntry<String>>[
      if (a.isActive && !published) ...const [
        PopupMenuItem(value: 'edit', child: Text('Edit')),
        PopupMenuItem(value: 'delete', child: Text('Delete')),
      ],
      if (a.isActive && published) ...[
        if (canMarkDelivered)
          PopupMenuItem(value: 'delivery', child: Text(a.isDelivered ? 'Remove delivered mark' : 'Mark as delivered')),
        if (canAdjustEvaluation) const PopupMenuItem(value: 'adjust', child: Text('Adjust evaluation')),
        const PopupMenuItem(value: 'revoke', child: Text('Revoke award')),
      ],
    ];

    return Padding(
      padding: const EdgeInsets.only(top: AppSpacing.md),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          SizedBox(
            width: 84,
            child: ArtworkFrame(
              awarded: a.isActive,
              aspectRatio: 1,
              matWidth: 5,
              child: a.hasThumbnail
                  ? AuthImage(submissionId: a.submissionId, semanticLabel: a.submissionTitle)
                  : const ImagePlaceholder(),
            ),
          ),
          const SizedBox(width: AppSpacing.md),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  a.submissionTitle,
                  maxLines: 2,
                  overflow: TextOverflow.ellipsis,
                  style: text.titleSmall!.copyWith(
                    fontStyle: FontStyle.italic,
                    fontWeight: FontWeight.w500,
                    decoration: revoked ? TextDecoration.lineThrough : null,
                  ),
                ),
                Text('${a.studentName} (${a.studentCode})', style: text.bodyMedium?.merge(strike)),
                Text('Awarded ${formatDate(a.issuedDate)}', style: text.bodySmall),
                if (a.remarks != null && a.remarks!.isNotEmpty) Text('Note: ${a.remarks}', style: text.bodySmall),
                const SizedBox(height: AppSpacing.xs),
                Wrap(
                  spacing: 6,
                  runSpacing: 4,
                  children: [
                    if (a.rating != null) StatusChip.rating(a.rating!),
                    if (revoked) const StatusChip.custom(label: 'Revoked', color: AppColors.danger),
                    if (a.isActive && a.isDelivered)
                      const StatusChip.custom(label: 'Delivered', color: AppColors.statusOngoing),
                  ],
                ),
                if (revoked && a.revokedReason != null)
                  Text('Reason: ${a.revokedReason}', style: text.bodySmall!.copyWith(color: AppColors.danger)),
              ],
            ),
          ),
          if (actions.isNotEmpty)
            PopupMenuButton<String>(
              enabled: !busy,
              tooltip: 'Award actions',
              onSelected: (v) => switch (v) {
                'edit' => onEdit(),
                'delete' => onDelete(),
                'delivery' => onDelivery(),
                'adjust' => onAdjust(),
                _ => onRevoke(),
              },
              itemBuilder: (context) => actions,
            ),
        ],
      ),
    );
  }
}

// ---------------- Dialogs ----------------

/// Mandatory reason dialog (revoke...). Returns the trimmed reason; null = cancelled.
class _ReasonDialog extends StatefulWidget {
  const _ReasonDialog({required this.title, required this.label});

  final String title;
  final String label;

  @override
  State<_ReasonDialog> createState() => _ReasonDialogState();
}

class _ReasonDialogState extends State<_ReasonDialog> {
  final _controller = TextEditingController();
  String? _error;

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return AlertDialog(
      title: Text(widget.title),
      content: TextField(
        controller: _controller,
        maxLength: 1000,
        maxLines: 3,
        decoration: InputDecoration(labelText: widget.label, errorText: _error),
      ),
      actions: [
        TextButton(onPressed: () => Navigator.pop(context), child: const Text('Cancel')),
        FilledButton(
          onPressed: () {
            final error = validateReason(_controller.text);
            if (error != null) {
              setState(() => _error = error);
              return;
            }
            Navigator.pop(context, _controller.text.trim());
          },
          child: const Text('Confirm'),
        ),
      ],
    );
  }
}

/// Optional remarks of a given award. Returns the trimmed text ("" = none); null = cancelled.
class _RemarksDialog extends StatefulWidget {
  const _RemarksDialog({required this.title, required this.initial});

  final String title;
  final String initial;

  @override
  State<_RemarksDialog> createState() => _RemarksDialogState();
}

class _RemarksDialogState extends State<_RemarksDialog> {
  late final _controller = TextEditingController(text: widget.initial);

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => AlertDialog(
        title: Text(widget.title),
        content: TextField(
          controller: _controller,
          maxLength: 1000,
          maxLines: 3,
          decoration: const InputDecoration(labelText: 'Remarks'),
        ),
        actions: [
          TextButton(onPressed: () => Navigator.pop(context), child: const Text('Cancel')),
          FilledButton(onPressed: () => Navigator.pop(context, _controller.text.trim()), child: const Text('Save')),
        ],
      );
}

/// The jury ranking (BR-30, BR-31): place, entry, average and the award it gets (a preview before publishing).
class _RankingCard extends StatelessWidget {
  const _RankingCard({required this.detail});

  final AwardingDetail detail;

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    return Card(
      margin: EdgeInsets.zero,
      child: Padding(
        padding: const EdgeInsets.all(AppSpacing.md),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Semantics(
              header: true,
              child: Text(detail.isPublished ? 'Ranking' : 'Ranking preview', style: text.titleMedium),
            ),
            if (!detail.isPublished)
              Text('Only entries graded by the whole jury are ranked. Equal scores share a place.', style: text.bodySmall),
            const SizedBox(height: AppSpacing.xs),
            for (final r in detail.ranking)
              Padding(
                padding: const EdgeInsets.symmetric(vertical: 4),
                child: Row(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    SizedBox(width: 32, child: Text(r.place == null ? '–' : '${r.place}', style: text.titleSmall)),
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(r.title, style: text.bodyMedium),
                          Text(
                            '${r.studentName}${r.awardName != null ? ' · ${r.awardName}' : ''}',
                            style: text.bodySmall!.copyWith(color: r.awardName != null ? AppColors.brassText : AppColors.muted),
                          ),
                        ],
                      ),
                    ),
                    Text(
                      r.average == null ? 'Disqualified' : r.average!.toStringAsFixed(2),
                      style: text.bodyMedium,
                    ),
                  ],
                ),
              ),
          ],
        ),
      ),
    );
  }
}
