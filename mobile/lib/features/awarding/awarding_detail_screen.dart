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

  // ---- Issue a new award ----
  Future<void> _issue(AwardSlot slot) async {
    final input = await showDialog<_IssueInput>(
      context: context,
      builder: (context) => _IssueDialog(
        competitionId: _d.id,
        title: 'Award ${slot.name}',
        needReason: _d.isPublished,
      ),
    );
    if (input == null || !mounted) return;
    await _run(
      () => ref.read(awardingRepositoryProvider).issue(
            _d.id,
            awardId: slot.id,
            submissionId: input.submissionId,
            remarks: input.remarks,
            reason: input.reason,
          ),
      success: '${slot.name} awarded.',
    );
  }

  // ---- Edit an award (before publishing only) ----
  Future<void> _edit(AwardSlot slot, IssuedAward award) async {
    final input = await showDialog<_IssueInput>(
      context: context,
      builder: (context) => _IssueDialog(
        competitionId: _d.id,
        title: 'Edit ${slot.name}',
        needReason: false,
        current: award,
      ),
    );
    if (input == null || !mounted) return;
    await _run(
      () => ref.read(awardingRepositoryProvider).update(
            award,
            awardId: slot.id,
            submissionId: input.submissionId,
            remarks: input.remarks,
          ),
      success: 'Award updated.',
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

  // ---- Publish results (UC-06 step 5) ----
  Future<void> _publish() async {
    final confirmEmpty = await showDialog<bool>(
      context: context,
      builder: (context) => _PublishDialog(detail: _d),
    );
    if (confirmEmpty == null || !mounted) return;
    await _run(
      () => ref.read(awardingRepositoryProvider).publish(_d.id, confirmEmptySlots: confirmEmpty),
      success: 'Results published.',
    );
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
    // Adding: free before publishing; after publishing it is an extra award (reason required)
    final canAdd = d.canAward || d.isPublished;

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
                  const Text('Results are published. From now on every change is an adjustment and needs a reason.'),
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

          for (final slot in d.awards)
            _SlotCard(
              slot: slot,
              published: d.isPublished,
              canMarkDelivered: d.canMarkDelivered,
              canAdjustEvaluation: d.canAdjustEvaluation,
              busy: _busy,
              canAdd: canAdd && slot.remaining > 0,
              onAdd: () => _issue(slot),
              onEdit: (a) => _edit(slot, a),
              onDelete: (a) => _delete(slot, a),
              onRevoke: (a) => _revoke(slot, a),
              onDelivery: _toggleDelivery,
              onAdjust: (a) => context.push('/staff/awarding/submissions/${a.submissionId}/adjust'),
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

/// Checklist before publishing: everything graded, how many slots are still empty (BR-08, BR-32).
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
            line(
              d.emptySlots == 0,
              d.emptySlots == 0
                  ? 'Every award slot is filled'
                  : '${d.emptySlots} award ${d.emptySlots == 1 ? 'slot' : 'slots'} still empty (leaving them empty needs confirmation)',
            ),
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

/// Result of the issue dialog.
class _IssueInput {
  const _IssueInput(this.submissionId, this.remarks, this.reason);

  final int submissionId;
  final String? remarks;
  final String? reason;
}

/// Pick the entry that receives the award from the eligible candidates (BEST first, BR-30),
/// with a note and, after publishing, a reason. When editing, [current] is also selectable.
class _IssueDialog extends ConsumerStatefulWidget {
  const _IssueDialog({required this.competitionId, required this.title, required this.needReason, this.current});

  final int competitionId;
  final String title;
  final bool needReason;
  final IssuedAward? current;

  @override
  ConsumerState<_IssueDialog> createState() => _IssueDialogState();
}

class _IssueDialogState extends ConsumerState<_IssueDialog> {
  late final TextEditingController _remarks;
  final _reason = TextEditingController();
  int? _selected;
  String? _error;

  @override
  void initState() {
    super.initState();
    _remarks = TextEditingController(text: widget.current?.remarks ?? '');
    _selected = widget.current?.submissionId;
  }

  @override
  void dispose() {
    _remarks.dispose();
    _reason.dispose();
    super.dispose();
  }

  void _submit() {
    if (_selected == null) {
      setState(() => _error = 'Please choose the winning entry');
      return;
    }
    if (widget.needReason && validateReason(_reason.text) != null) {
      setState(() => _error = 'A reason is required for an extra award after publishing');
      return;
    }
    Navigator.pop(
      context,
      _IssueInput(_selected!, _remarks.text.trim().isEmpty ? null : _remarks.text.trim(),
          widget.needReason ? _reason.text.trim() : null),
    );
  }

  Widget _thumb(int submissionId, bool hasThumbnail, String title) => SizedBox(
        width: 56,
        height: 56,
        child: ArtworkFrame(
          aspectRatio: null,
          matWidth: 3,
          child: hasThumbnail
              ? AuthImage(submissionId: submissionId, semanticLabel: title)
              : const ImagePlaceholder(),
        ),
      );

  @override
  Widget build(BuildContext context) {
    final candidates = ref.watch(awardCandidatesProvider(widget.competitionId));
    final current = widget.current;
    return AlertDialog(
      title: Text(widget.title),
      content: SizedBox(
        width: double.maxFinite,
        child: SingleChildScrollView(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              switch (candidates) {
                AsyncData(:final value) => RadioGroup<int>(
                    groupValue: _selected,
                    onChanged: (v) => setState(() {
                      _selected = v;
                      _error = null;
                    }),
                    child: Column(
                      children: [
                        // When editing, the current winner is no longer a candidate, so list it first
                        if (current != null)
                          RadioListTile<int>(
                            contentPadding: EdgeInsets.zero,
                            value: current.submissionId,
                            title: Text(current.submissionTitle, maxLines: 2, overflow: TextOverflow.ellipsis),
                            subtitle: Text('${current.studentName} (${current.studentCode}) · current'),
                            secondary: _thumb(current.submissionId, current.hasThumbnail, current.submissionTitle),
                          ),
                        for (final c in value)
                          RadioListTile<int>(
                            contentPadding: EdgeInsets.zero,
                            value: c.submissionId,
                            title: Text(c.title, maxLines: 2, overflow: TextOverflow.ellipsis),
                            subtitle: Text('${c.studentName} (${c.studentCode}) · ${ratingName(c.rating)}'),
                            secondary: _thumb(c.submissionId, c.hasThumbnail, c.title),
                          ),
                        if (value.isEmpty && current == null)
                          const Padding(
                            padding: EdgeInsets.all(AppSpacing.sm),
                            child: Text('No eligible entries left (graded, not disqualified and without an award).'),
                          ),
                      ],
                    ),
                  ),
                AsyncError(:final error) => Text(ApiException.from(error).message),
                _ => const Padding(
                    padding: EdgeInsets.all(AppSpacing.md),
                    child: Center(child: CircularProgressIndicator()),
                  ),
              },
              const SizedBox(height: AppSpacing.sm),
              TextField(
                controller: _remarks,
                maxLength: 1000,
                maxLines: 2,
                decoration: const InputDecoration(labelText: 'Note / comment'),
              ),
              if (widget.needReason)
                TextField(
                  controller: _reason,
                  maxLength: 1000,
                  maxLines: 2,
                  decoration: const InputDecoration(labelText: 'Reason for the extra award *'),
                ),
              if (_error != null) Text(_error!, style: const TextStyle(color: AppColors.danger)),
            ],
          ),
        ),
      ),
      actions: [
        TextButton(onPressed: () => Navigator.pop(context), child: const Text('Cancel')),
        FilledButton(onPressed: _submit, child: const Text('Save')),
      ],
    );
  }
}

/// Confirms publishing: if slots are empty the staff must tick "leave empty" (BR-32). Returns the ticked value; null = cancelled.
class _PublishDialog extends StatefulWidget {
  const _PublishDialog({required this.detail});

  final AwardingDetail detail;

  @override
  State<_PublishDialog> createState() => _PublishDialogState();
}

class _PublishDialogState extends State<_PublishDialog> {
  bool _confirmEmpty = false;

  @override
  Widget build(BuildContext context) {
    final empty = widget.detail.emptySlots;
    final canConfirm = empty == 0 || _confirmEmpty;
    return AlertDialog(
      title: const Text('Publish results?'),
      content: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text('Ratings, comments and awards become visible to students and on the public pages. '
              'Afterwards they are locked and can only change through "Adjust result", with a reason.'),
          if (empty > 0) ...[
            const SizedBox(height: AppSpacing.md),
            Text(emptySlotsMessage(empty)),
            CheckboxListTile(
              contentPadding: EdgeInsets.zero,
              controlAffinity: ListTileControlAffinity.leading,
              value: _confirmEmpty,
              onChanged: (v) => setState(() => _confirmEmpty = v ?? false),
              title: Text('Leave $empty ${empty == 1 ? 'slot' : 'slots'} empty'),
            ),
          ],
        ],
      ),
      actions: [
        TextButton(onPressed: () => Navigator.pop(context), child: const Text('Cancel')),
        FilledButton(onPressed: canConfirm ? () => Navigator.pop(context, _confirmEmpty) : null, child: const Text('Publish')),
      ],
    );
  }
}

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
