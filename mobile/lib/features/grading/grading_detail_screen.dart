import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/api_client.dart';
import '../../core/async_view.dart';
import '../../core/file_download.dart';
import '../../core/format.dart';
import '../competitions/competition_widgets.dart';
import '../competitions/competitions.dart';
import '../submissions/submission_widgets.dart';
import '../submissions/submissions.dart';
import 'grading.dart';

/// Màn chấm bài: ảnh, nội dung bài, form xếp loại + nhận xét (UC-05, SCR-27).
class GradingDetailScreen extends ConsumerWidget {
  const GradingDetailScreen({super.key, required this.id});

  final int id;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final detail = ref.watch(gradingDetailProvider(id));
    return Scaffold(
      appBar: AppBar(title: const Text('Chấm bài')),
      body: AsyncValueView(
        value: detail,
        onRetry: () => ref.invalidate(gradingDetailProvider(id)),
        // Key theo RowVersion: khi tải lại sau xung đột / xóa, form được dựng lại với dữ liệu mới
        data: (d) => _Body(key: ValueKey('${d.id}-${d.evaluation?.rowVersion}'), detail: d),
      ),
    );
  }
}

class _Body extends ConsumerStatefulWidget {
  const _Body({super.key, required this.detail});

  final GradingDetail detail;

  @override
  ConsumerState<_Body> createState() => _BodyState();
}

class _BodyState extends ConsumerState<_Body> {
  final _formKey = GlobalKey<FormState>();
  late final TextEditingController _positive;
  late final TextEditingController _negative;
  late final TextEditingController _improvement;
  SubmissionRating? _rating;
  bool _busy = false;

  GradingDetail get _d => widget.detail;

  @override
  void initState() {
    super.initState();
    // Điền sẵn khi đã có đánh giá
    final ev = _d.evaluation;
    _positive = TextEditingController(text: ev?.positivePoints ?? '');
    _negative = TextEditingController(text: ev?.negativePoints ?? '');
    _improvement = TextEditingController(text: ev?.improvementAreas ?? '');
    _rating = ev?.rating;
  }

  @override
  void dispose() {
    _positive.dispose();
    _negative.dispose();
    _improvement.dispose();
    super.dispose();
  }

  void _toast(String text) => ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(text)));

  // Lỗi 409 = dữ liệu đã bị người khác đổi (BR-52): báo rõ và tải lại bản mới nhất
  void _handleError(Object e) {
    final err = ApiException.from(e);
    _toast(err.message);
    if (err.statusCode == 409) ref.invalidate(gradingDetailProvider(_d.id));
    setState(() => _busy = false);
  }

  // Lưu đánh giá: chưa có → tạo, đã có → sửa kèm RowVersion
  Future<void> _save() async {
    final error = validateEvaluation(_rating, _positive.text, _negative.text);
    if (error != null) {
      _toast(error);
      return;
    }
    final creating = _d.evaluation == null;
    // Giữ container để làm mới dữ liệu SAU khi rời màn hình (ref của widget đã dispose thì không dùng được)
    final container = ProviderScope.containerOf(context);
    final id = _d.id;
    setState(() => _busy = true);
    try {
      await ref.read(gradingRepositoryProvider).save(
            _d.id,
            EvaluationInput(
              rating: _rating!,
              positivePoints: _positive.text.trim(),
              negativePoints: _negative.text.trim(),
              improvementAreas: _improvement.text.trim(),
              rowVersion: _d.evaluation?.rowVersion,
            ),
          );
      if (!mounted) return;
      _toast('Đã lưu đánh giá.');
      // UC-05 bước 5: chấm mới xong thì chuyển sang bài chưa chấm kế tiếp
      // (hỏi API trực tiếp, chưa làm mới provider để màn hình này không bị dựng lại giữa chừng)
      final nextId = creating ? await _nextAfterSave() : null;
      if (!mounted) return;
      if (nextId != null) {
        context.pushReplacement('/staff/submissions/$nextId/evaluate');
      } else {
        if (creating) _toast('Đã chấm hết các bài của cuộc thi này.');
        context.pop();
      }
      _invalidateAfterChange(container, id);
    } catch (e) {
      if (mounted) _handleError(e);
    }
  }

  // Làm mới mọi dữ liệu phụ thuộc đánh giá: tiến độ, danh sách, chi tiết chấm và chi tiết bài của học viên
  void _invalidateAfterChange(ProviderContainer container, int id) {
    container.invalidate(gradingCompetitionsProvider);
    container.invalidate(gradingListProvider);
    container.invalidate(gradingDetailProvider(id));
    container.invalidate(submissionDetailProvider(id));
  }

  // Tải lại chi tiết để biết bài chưa chấm kế tiếp (do API tính sau khi lưu)
  Future<int?> _nextAfterSave() async {
    try {
      final fresh = await ref.read(gradingRepositoryProvider).detail(_d.id);
      return fresh.nextUnevaluatedId;
    } catch (_) {
      return null;
    }
  }

  // Xóa đánh giá sau khi xác nhận (F-M07-05)
  Future<void> _delete() async {
    final ev = _d.evaluation!;
    final ok = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Xóa đánh giá?'),
        content: const Text('Đánh giá của bài này sẽ bị xóa, bài trở về trạng thái chưa chấm.'),
        actions: [
          TextButton(onPressed: () => Navigator.pop(context, false), child: const Text('Hủy')),
          FilledButton(onPressed: () => Navigator.pop(context, true), child: const Text('Xóa')),
        ],
      ),
    );
    if (ok != true || !mounted) return;

    setState(() => _busy = true);
    try {
      await ref.read(gradingRepositoryProvider).deleteEvaluation(_d.id, ev.rowVersion);
      if (!mounted) return;
      _toast('Đã xóa đánh giá.');
      // Ở lại màn hình: tải lại chi tiết, form được dựng lại ở trạng thái "chưa chấm"
      _invalidateAfterChange(ProviderScope.containerOf(context), _d.id);
    } catch (e) {
      if (mounted) _handleError(e);
    }
  }

  // Tải file gốc về máy (F-M06-06)
  Future<void> _download() async {
    setState(() => _busy = true);
    try {
      final bytes = await ref.read(submissionRepositoryProvider).imageBytes(_d.id, download: true);
      final saved = await saveBytesAsFile(bytes, 'submission-${_d.id}.jpg');
      if (mounted) _toast(saved ? 'Đã tải ảnh về máy.' : 'Nền tảng này chưa hỗ trợ tải về (hãy chạy trên Chrome).');
    } catch (e) {
      if (mounted) _toast(ApiException.from(e).message);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final d = _d;
    final textTheme = Theme.of(context).textTheme;
    final scheme = Theme.of(context).colorScheme;
    final editable = d.canEvaluate && !_busy;
    final disqualified = _rating == SubmissionRating.disqualified;

    return Form(
      key: _formKey,
      child: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          SubmissionImage(submissionId: d.id, height: 320),
          const SizedBox(height: 12),
          Row(
            children: [
              Expanded(child: Text(d.title, style: textTheme.headlineSmall)),
              StatusChip(d.competitionStatus),
            ],
          ),
          const SizedBox(height: 4),
          Text('${d.studentName} (${d.studentCode}) • ${d.competitionTitle}'),
          Text('Nộp: ${formatDateTime(d.submittedAt)}'
              '${d.updatedAt != null ? ' • Sửa: ${formatDateTime(d.updatedAt!)}' : ''}'),
          Text('Đã chấm ${progressLabel(d.evaluated, d.total)} bài', style: textTheme.bodySmall),
          const SectionTitle('Mô tả'),
          Text(d.description),
          SectionTitle(d.textType.label),
          Text(d.textContent),
          Align(
            alignment: Alignment.centerLeft,
            child: TextButton.icon(
              onPressed: _busy ? null : _download,
              icon: const Icon(Icons.download),
              label: const Text('Tải ảnh gốc'),
            ),
          ),
          const Divider(height: 32),
          Text('Đánh giá', style: textTheme.titleLarge),
          // UC-05 1a / 4a: khóa form và nói rõ lý do
          if (d.lockReason != null)
            Card(
              color: scheme.errorContainer,
              child: Padding(
                padding: const EdgeInsets.all(12),
                child: Row(
                  children: [
                    const Icon(Icons.lock_outline),
                    const SizedBox(width: 8),
                    Expanded(child: Text(d.lockReason!)),
                  ],
                ),
              ),
            ),
          if (d.evaluation case final ev?)
            Padding(
              padding: const EdgeInsets.symmetric(vertical: 4),
              child: Text('Chấm bởi ${ev.evaluatedByName} lúc ${formatDateTime(ev.evaluatedAt)}', style: textTheme.bodySmall),
            ),
          // Cuộc thi đã công bố: sửa đánh giá chỉ qua form Điều chỉnh kết quả (BR-34, bắt buộc lý do)
          if (d.competitionStatus == CompetitionStatus.graded && d.evaluation != null)
            Align(
              alignment: Alignment.centerLeft,
              child: FilledButton.tonalIcon(
                onPressed: () => context.push('/staff/awarding/submissions/${d.id}/adjust'),
                icon: const Icon(Icons.edit_note),
                label: const Text('Điều chỉnh kết quả'),
              ),
            ),
          const SizedBox(height: 8),
          DropdownButtonFormField<SubmissionRating>(
            initialValue: _rating,
            decoration: const InputDecoration(labelText: 'Xếp loại *', border: OutlineInputBorder()),
            items: [for (final r in SubmissionRating.values) DropdownMenuItem(value: r, child: Text(r.label))],
            onChanged: editable ? (v) => setState(() => _rating = v) : null,
          ),
          const SizedBox(height: 12),
          TextFormField(
            controller: _positive,
            enabled: editable,
            maxLength: 2000,
            maxLines: 3,
            decoration: InputDecoration(
              labelText: disqualified ? 'Điểm mạnh' : 'Điểm mạnh *',
              alignLabelWithHint: true,
              border: const OutlineInputBorder(),
            ),
          ),
          TextFormField(
            controller: _negative,
            enabled: editable,
            maxLength: 2000,
            maxLines: 3,
            decoration: InputDecoration(
              // BR-23: DISQUALIFIED dùng ô này để ghi lý do bắt buộc
              labelText: disqualified ? 'Lý do bị loại *' : 'Điểm yếu',
              alignLabelWithHint: true,
              border: const OutlineInputBorder(),
            ),
          ),
          const SizedBox(height: 8),
          TextFormField(
            controller: _improvement,
            enabled: editable,
            maxLength: 2000,
            maxLines: 3,
            decoration: const InputDecoration(
              labelText: 'Điểm cần cải thiện',
              alignLabelWithHint: true,
              border: OutlineInputBorder(),
            ),
          ),
          const SizedBox(height: 8),
          Wrap(
            spacing: 12,
            runSpacing: 8,
            children: [
              FilledButton.icon(
                onPressed: editable ? _save : null,
                icon: _busy
                    ? const SizedBox(width: 16, height: 16, child: CircularProgressIndicator(strokeWidth: 2))
                    : const Icon(Icons.save),
                label: Text(d.evaluation == null ? 'Lưu đánh giá' : 'Lưu thay đổi'),
              ),
              if (d.evaluation != null)
                FilledButton.tonalIcon(
                  onPressed: (d.canDeleteEvaluation && !_busy) ? _delete : null,
                  icon: const Icon(Icons.delete),
                  label: const Text('Xóa đánh giá'),
                ),
            ],
          ),
          // Giải thích vì sao không xóa được dù cuộc thi chưa công bố
          if (d.evaluation != null && !d.canDeleteEvaluation && d.canEvaluate)
            Padding(
              padding: const EdgeInsets.only(top: 8),
              child: Text('Bài đã được trao giải hoặc gửi triển lãm nên không thể xóa đánh giá.',
                  style: textTheme.bodySmall),
            ),
        ],
      ),
    );
  }
}
