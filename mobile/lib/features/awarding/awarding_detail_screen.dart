import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/api_client.dart';
import '../../core/async_view.dart';
import '../../core/format.dart';
import '../competitions/competition_widgets.dart';
import '../submissions/submission_widgets.dart';
import '../submissions/submissions.dart';
import 'awarding.dart';
import 'awarding_widgets.dart';

/// Màn trao giải và công bố của một cuộc thi (UC-06, SCR trao giải):
/// danh sách giải + người nhận, checklist công bố; sau công bố là thu hồi/trao bổ sung/phát thưởng.
class AwardingDetailScreen extends ConsumerWidget {
  const AwardingDetailScreen({super.key, required this.competitionId});

  final int competitionId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final detail = ref.watch(awardingDetailProvider(competitionId));
    return Scaffold(
      appBar: AppBar(title: const Text('Trao giải')),
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

  // Chạy một thao tác ghi: khóa nút trong lúc chờ, báo lỗi tiếng Việt, 409 (BR-52) thì tải lại bản mới nhất
  Future<void> _run(Future<void> Function() action, {String? success}) async {
    final container = ProviderScope.containerOf(context);
    setState(() => _busy = true);
    try {
      await action();
      if (!mounted) return;
      if (success != null) _toast(success);
    } catch (e) {
      if (!mounted) return;
      final err = ApiException.from(e);
      _toast(err.message);
    } finally {
      // Dù thành công hay xung đột đều nạp lại dữ liệu để màn hình khớp với server
      refreshAwardingData(container);
      if (mounted) setState(() => _busy = false);
    }
  }

  // ---- Trao giải mới ----
  Future<void> _issue(AwardSlot slot) async {
    final input = await showDialog<_IssueInput>(
      context: context,
      builder: (context) => _IssueDialog(
        competitionId: _d.id,
        title: 'Trao ${slot.name}',
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
      success: 'Đã trao ${slot.name}.',
    );
  }

  // ---- Sửa giải (chỉ khi chưa công bố) ----
  Future<void> _edit(AwardSlot slot, IssuedAward award) async {
    final input = await showDialog<_IssueInput>(
      context: context,
      builder: (context) => _IssueDialog(
        competitionId: _d.id,
        title: 'Sửa ${slot.name}',
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
      success: 'Đã cập nhật giải.',
    );
  }

  // ---- Xóa giải (chỉ khi chưa công bố) ----
  Future<void> _delete(AwardSlot slot, IssuedAward award) async {
    final ok = await _confirm(
      'Xóa giải?',
      '${slot.name} của ${award.studentName} sẽ bị xóa, suất giải được trả lại.',
      'Xóa',
    );
    if (ok != true || !mounted) return;
    await _run(() => ref.read(awardingRepositoryProvider).delete(award), success: 'Đã xóa giải.');
  }

  // ---- Thu hồi giải (sau công bố, bắt buộc lý do) ----
  Future<void> _revoke(AwardSlot slot, IssuedAward award) async {
    final reason = await _askReason('Thu hồi ${slot.name}', 'Lý do thu hồi *');
    if (reason == null || !mounted) return;
    await _run(() => ref.read(awardingRepositoryProvider).revoke(award, reason), success: 'Đã thu hồi giải.');
  }

  // ---- Đánh dấu đã/chưa phát thưởng ----
  Future<void> _toggleDelivery(IssuedAward award) => _run(
        () => ref.read(awardingRepositoryProvider).setDelivery(award, !award.isDelivered),
        success: award.isDelivered ? 'Đã bỏ đánh dấu phát thưởng.' : 'Đã ghi nhận phát thưởng.',
      );

  // ---- Công bố kết quả (UC-06 bước 5) ----
  Future<void> _publish() async {
    final confirmEmpty = await showDialog<bool>(
      context: context,
      builder: (context) => _PublishDialog(detail: _d),
    );
    if (confirmEmpty == null || !mounted) return;
    await _run(
      () => ref.read(awardingRepositoryProvider).publish(_d.id, confirmEmptySlots: confirmEmpty),
      success: 'Đã công bố kết quả.',
    );
  }

  Future<bool?> _confirm(String title, String text, String action) => showDialog<bool>(
        context: context,
        builder: (context) => AlertDialog(
          title: Text(title),
          content: Text(text),
          actions: [
            TextButton(onPressed: () => Navigator.pop(context, false), child: const Text('Hủy')),
            FilledButton(onPressed: () => Navigator.pop(context, true), child: Text(action)),
          ],
        ),
      );

  // Hộp nhập lý do bắt buộc; trả về null nếu hủy
  Future<String?> _askReason(String title, String label) => showDialog<String>(
        context: context,
        builder: (context) => _ReasonDialog(title: title, label: label),
      );

  @override
  Widget build(BuildContext context) {
    final d = _d;
    final textTheme = Theme.of(context).textTheme;
    final scheme = Theme.of(context).colorScheme;
    // Trao thêm: trước công bố tự do; sau công bố là trao bổ sung (bắt buộc lý do)
    final canAdd = d.canAward || d.isPublished;

    return RefreshIndicator(
      onRefresh: () async {
        ref.invalidate(awardingDetailProvider(d.id));
        await ref.read(awardingDetailProvider(d.id).future);
      },
      child: ListView(
        physics: const AlwaysScrollableScrollPhysics(),
        padding: const EdgeInsets.all(16),
        children: [
          Row(
            children: [
              Expanded(child: Text(d.title, style: textTheme.headlineSmall)),
              StatusChip(d.status),
            ],
          ),
          if (d.publishedAt != null) Text('Công bố lúc ${formatDateTime(d.publishedAt!)}', style: textTheme.bodySmall),
          if (d.adjustedAt != null) AdjustedLabel(d.adjustedAt!),
          const SizedBox(height: 12),

          // Checklist công bố (BR-32) chỉ hiện khi chưa công bố
          if (!d.isPublished) _Checklist(detail: d, busy: _busy, onPublish: _publish),
          if (d.isPublished)
            Card(
              color: scheme.secondaryContainer,
              child: Padding(
                padding: const EdgeInsets.all(12),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    const Text('Kết quả đã công bố. Mọi sửa đổi từ đây là điều chỉnh sau công bố và bắt buộc có lý do.'),
                    const SizedBox(height: 8),
                    // Sửa Evaluation đi qua màn chấm bài của từng bài; từ đây vào danh sách bài của cuộc thi
                    OutlinedButton.icon(
                      onPressed: () => context.push('/staff/competitions/${d.id}/submissions'),
                      icon: const Icon(Icons.rate_review_outlined),
                      label: const Text('Điều chỉnh đánh giá một bài'),
                    ),
                  ],
                ),
              ),
            ),

          for (final slot in d.awards)
            _SlotCard(
              slot: slot,
              published: d.isPublished,
              busy: _busy,
              canAdd: canAdd && slot.remaining > 0,
              onAdd: () => _issue(slot),
              onEdit: (a) => _edit(slot, a),
              onDelete: (a) => _delete(slot, a),
              onRevoke: (a) => _revoke(slot, a),
              onDelivery: _toggleDelivery,
              onAdjust: (a) => context.push('/staff/awarding/submissions/${a.submissionId}/adjust'),
            ),
          if (d.awards.isEmpty) const Padding(padding: EdgeInsets.all(16), child: Text('Cuộc thi chưa định nghĩa giải thưởng.')),
        ],
      ),
    );
  }
}

/// Checklist trước khi công bố: bài đã chấm hết chưa, còn bao nhiêu suất giải trống (BR-08, BR-32).
class _Checklist extends StatelessWidget {
  const _Checklist({required this.detail, required this.busy, required this.onPublish});

  final AwardingDetail detail;
  final bool busy;
  final VoidCallback onPublish;

  @override
  Widget build(BuildContext context) {
    final d = detail;
    final scheme = Theme.of(context).colorScheme;
    final allGraded = d.pendingEvaluations == 0;

    Widget line(bool ok, String text) => Padding(
          padding: const EdgeInsets.symmetric(vertical: 2),
          child: Row(
            children: [
              Icon(ok ? Icons.check_circle : Icons.error_outline, size: 20, color: ok ? scheme.primary : scheme.error),
              const SizedBox(width: 8),
              Expanded(child: Text(text)),
            ],
          ),
        );

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('Checklist công bố', style: Theme.of(context).textTheme.titleMedium),
            const SizedBox(height: 4),
            line(allGraded, allGraded
                ? 'Đã chấm đủ ${d.submissionCount} bài'
                : 'Còn ${d.pendingEvaluations} bài chưa chấm (${d.evaluatedCount}/${d.submissionCount})'),
            line(d.emptySlots == 0, d.emptySlots == 0
                ? 'Đã trao đủ mọi suất giải'
                : 'Còn ${d.emptySlots} suất giải chưa trao (cần xác nhận để trống)'),
            const SizedBox(height: 8),
            FilledButton.icon(
              onPressed: (d.canPublish && !busy) ? onPublish : null,
              icon: const Icon(Icons.campaign),
              label: const Text('Công bố kết quả'),
            ),
            if (d.publishBlockReason != null)
              Padding(
                padding: const EdgeInsets.only(top: 6),
                child: Text(d.publishBlockReason!, style: TextStyle(color: scheme.error)),
              ),
          ],
        ),
      ),
    );
  }
}

/// Một giải định nghĩa với các suất đã trao.
class _SlotCard extends StatelessWidget {
  const _SlotCard({
    required this.slot,
    required this.published,
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
    final textTheme = Theme.of(context).textTheme;
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                CircleAvatar(radius: 14, child: Text('${slot.rank}')),
                const SizedBox(width: 8),
                Expanded(child: Text('${slot.name} • ${formatVnd(slot.prizeValue)}', style: textTheme.titleMedium)),
              ],
            ),
            Text('Đã trao ${slotsLabel(slot.activeCount, slot.quantity)} suất'
                '${slot.remaining > 0 ? ' • còn ${slot.remaining} suất' : ''}', style: textTheme.bodySmall),
            for (final a in slot.issued)
              _IssuedTile(
                award: a,
                published: published,
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
                  label: Text(published ? 'Trao bổ sung' : 'Trao giải'),
                ),
              ),
          ],
        ),
      ),
    );
  }
}

/// Một giải đã trao: người nhận, tranh, xếp loại và các thao tác phù hợp trạng thái.
class _IssuedTile extends StatelessWidget {
  const _IssuedTile({
    required this.award,
    required this.published,
    required this.busy,
    required this.onEdit,
    required this.onDelete,
    required this.onRevoke,
    required this.onDelivery,
    required this.onAdjust,
  });

  final IssuedAward award;
  final bool published;
  final bool busy;
  final VoidCallback onEdit;
  final VoidCallback onDelete;
  final VoidCallback onRevoke;
  final VoidCallback onDelivery;
  final VoidCallback onAdjust;

  @override
  Widget build(BuildContext context) {
    final a = award;
    final revoked = !a.isActive;
    final scheme = Theme.of(context).colorScheme;
    final strike = revoked ? const TextStyle(decoration: TextDecoration.lineThrough) : null;

    // Thao tác theo trạng thái: chưa công bố → sửa/xóa; sau công bố → phát thưởng/điều chỉnh đánh giá/thu hồi (BR-32, BR-33)
    final actions = <PopupMenuEntry<String>>[
      if (a.isActive && !published) ...const [
        PopupMenuItem(value: 'edit', child: Text('Sửa')),
        PopupMenuItem(value: 'delete', child: Text('Xóa')),
      ],
      if (a.isActive && published) ...[
        PopupMenuItem(value: 'delivery', child: Text(a.isDelivered ? 'Bỏ đánh dấu đã phát thưởng' : 'Đánh dấu đã phát thưởng')),
        const PopupMenuItem(value: 'adjust', child: Text('Điều chỉnh đánh giá')),
        const PopupMenuItem(value: 'revoke', child: Text('Thu hồi giải')),
      ],
    ];

    return ListTile(
      contentPadding: EdgeInsets.zero,
      title: Text('${a.studentName} (${a.studentCode})', style: strike),
      subtitle: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text('${a.submissionTitle} • ${formatDate(a.issuedDate)}', style: strike),
          if (a.remarks != null) Text('Ghi chú: ${a.remarks}'),
          Wrap(
            spacing: 6,
            children: [
              if (a.rating != null) RatingChip(a.rating!),
              if (revoked) _Tag('Đã thu hồi', scheme.error),
              if (a.isActive && a.isDelivered) _Tag('Đã phát thưởng', scheme.primary),
            ],
          ),
          if (revoked && a.revokedReason != null) Text('Lý do thu hồi: ${a.revokedReason}', style: TextStyle(color: scheme.error)),
        ],
      ),
      isThreeLine: true,
      trailing: actions.isEmpty
          ? null
          : PopupMenuButton<String>(
              enabled: !busy,
              onSelected: (v) => switch (v) {
                'edit' => onEdit(),
                'delete' => onDelete(),
                'delivery' => onDelivery(),
                'adjust' => onAdjust(),
                _ => onRevoke(),
              },
              itemBuilder: (context) => actions,
            ),
    );
  }
}

class _Tag extends StatelessWidget {
  const _Tag(this.text, this.color);

  final String text;
  final Color color;

  @override
  Widget build(BuildContext context) => Container(
        margin: const EdgeInsets.only(top: 4),
        padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
        decoration: BoxDecoration(color: color.withValues(alpha: 0.12), borderRadius: BorderRadius.circular(12)),
        child: Text(text, style: TextStyle(color: color, fontSize: 12, fontWeight: FontWeight.w600)),
      );
}

// ---------------- Hộp thoại ----------------

/// Kết quả hộp chọn bài trao giải.
class _IssueInput {
  const _IssueInput(this.submissionId, this.remarks, this.reason);

  final int submissionId;
  final String? remarks;
  final String? reason;
}

/// Chọn bài nhận giải trong danh sách bài đủ điều kiện (BEST xếp trước – BR-30), kèm ghi chú và (sau công bố) lý do.
/// [current] khác null khi sửa: bài hiện tại của giải cũng nằm trong danh sách chọn.
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
      setState(() => _error = 'Vui lòng chọn bài nhận giải');
      return;
    }
    if (widget.needReason && validateReason(_reason.text) != null) {
      setState(() => _error = 'Trao bổ sung sau công bố bắt buộc nhập lý do');
      return;
    }
    Navigator.pop(
      context,
      _IssueInput(_selected!, _remarks.text.trim().isEmpty ? null : _remarks.text.trim(),
          widget.needReason ? _reason.text.trim() : null),
    );
  }

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
                        // Khi sửa: bài đang nhận giải này không còn trong danh sách ứng viên nên thêm lại ở đầu
                        if (current != null)
                          RadioListTile<int>(
                            value: current.submissionId,
                            title: Text(current.submissionTitle),
                            subtitle: Text('${current.studentName} (${current.studentCode}) • bài hiện tại'),
                          ),
                        for (final c in value)
                          RadioListTile<int>(
                            value: c.submissionId,
                            title: Text(c.title),
                            subtitle: Text('${c.studentName} (${c.studentCode}) • ${c.rating.label}'),
                          ),
                        if (value.isEmpty && current == null)
                          const Padding(
                            padding: EdgeInsets.all(8),
                            child: Text('Không còn bài nào đủ điều kiện (đã chấm, không bị loại, chưa có giải).'),
                          ),
                      ],
                    ),
                  ),
                AsyncError(:final error) => Text(ApiException.from(error).message),
                _ => const Padding(padding: EdgeInsets.all(16), child: Center(child: CircularProgressIndicator())),
              },
              const SizedBox(height: 8),
              TextField(
                controller: _remarks,
                maxLength: 1000,
                maxLines: 2,
                decoration: const InputDecoration(labelText: 'Ghi chú / nhận xét', border: OutlineInputBorder()),
              ),
              if (widget.needReason)
                TextField(
                  controller: _reason,
                  maxLength: 1000,
                  maxLines: 2,
                  decoration: const InputDecoration(labelText: 'Lý do trao bổ sung *', border: OutlineInputBorder()),
                ),
              if (_error != null) Text(_error!, style: TextStyle(color: Theme.of(context).colorScheme.error)),
            ],
          ),
        ),
      ),
      actions: [
        TextButton(onPressed: () => Navigator.pop(context), child: const Text('Hủy')),
        FilledButton(onPressed: _submit, child: const Text('Lưu')),
      ],
    );
  }
}

/// Hộp xác nhận công bố: nếu còn suất trống thì bắt buộc tick "để trống" (BR-32). Trả về giá trị đã tick; null nếu hủy.
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
    // Còn suất trống mà chưa tick thì chưa cho công bố
    final canConfirm = empty == 0 || _confirmEmpty;
    return AlertDialog(
      title: const Text('Công bố kết quả?'),
      content: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text('Xếp loại, nhận xét và giải sẽ hiển thị cho học viên và trang chủ. '
              'Sau đó Evaluation và giải bị khóa; chỉ sửa được qua Điều chỉnh kết quả (kèm lý do).'),
          if (empty > 0) ...[
            const SizedBox(height: 12),
            Text(emptySlotsMessage(empty)),
            CheckboxListTile(
              contentPadding: EdgeInsets.zero,
              controlAffinity: ListTileControlAffinity.leading,
              value: _confirmEmpty,
              onChanged: (v) => setState(() => _confirmEmpty = v ?? false),
              title: Text('Xác nhận để trống $empty suất'),
            ),
          ],
        ],
      ),
      actions: [
        TextButton(onPressed: () => Navigator.pop(context), child: const Text('Hủy')),
        FilledButton(onPressed: canConfirm ? () => Navigator.pop(context, _confirmEmpty) : null, child: const Text('Công bố')),
      ],
    );
  }
}

/// Hộp nhập lý do bắt buộc (thu hồi giải…). Trả về lý do đã cắt khoảng trắng; null nếu hủy.
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
        decoration: InputDecoration(labelText: widget.label, errorText: _error, border: const OutlineInputBorder()),
      ),
      actions: [
        TextButton(onPressed: () => Navigator.pop(context), child: const Text('Hủy')),
        FilledButton(
          onPressed: () {
            final error = validateReason(_controller.text);
            if (error != null) {
              setState(() => _error = error);
              return;
            }
            Navigator.pop(context, _controller.text.trim());
          },
          child: const Text('Xác nhận'),
        ),
      ],
    );
  }
}
