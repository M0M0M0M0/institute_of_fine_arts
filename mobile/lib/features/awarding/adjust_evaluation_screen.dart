import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/api_client.dart';
import '../../core/async_view.dart';
import '../../core/format.dart';
import '../competitions/competition_widgets.dart';
import '../competitions/competitions.dart';
import '../grading/grading.dart';
import '../submissions/submission_widgets.dart';
import '../submissions/submissions.dart';
import 'awarding.dart';

/// Form "Điều chỉnh kết quả" cho bài của cuộc thi đã công bố: sửa xếp loại/nhận xét kèm lý do bắt buộc (F-M05-10, BR-34).
class AdjustEvaluationScreen extends ConsumerWidget {
  const AdjustEvaluationScreen({super.key, required this.id});

  /// Mã bài dự thi.
  final int id;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    // Dùng lại chi tiết chấm bài của 4a: có sẵn đánh giá hiện tại và RowVersion
    final detail = ref.watch(gradingDetailProvider(id));
    return Scaffold(
      appBar: AppBar(title: const Text('Điều chỉnh kết quả')),
      body: AsyncValueView(
        value: detail,
        onRetry: () => ref.invalidate(gradingDetailProvider(id)),
        // Key theo RowVersion: tải lại sau xung đột thì form dựng lại với dữ liệu mới
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
  late final TextEditingController _positive;
  late final TextEditingController _negative;
  late final TextEditingController _improvement;
  final _reason = TextEditingController();
  SubmissionRating? _rating;
  bool _busy = false;

  GradingDetail get _d => widget.detail;

  @override
  void initState() {
    super.initState();
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
    _reason.dispose();
    super.dispose();
  }

  void _toast(String text) => ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(text)));

  Future<void> _save() async {
    final ev = _d.evaluation;
    if (ev == null) return;
    // Kiểm tra nhanh phía client; server vẫn là nơi quyết định (BR-23, BR-34)
    final error = validateEvaluation(_rating, _positive.text, _negative.text) ?? validateReason(_reason.text);
    if (error != null) {
      _toast(error);
      return;
    }
    final container = ProviderScope.containerOf(context);
    setState(() => _busy = true);
    try {
      await ref.read(awardingRepositoryProvider).adjustEvaluation(
            _d.id,
            EvaluationInput(
              rating: _rating!,
              positivePoints: _positive.text.trim(),
              negativePoints: _negative.text.trim(),
              improvementAreas: _improvement.text.trim(),
              rowVersion: ev.rowVersion,
            ),
            _reason.text.trim(),
          );
      if (!mounted) return;
      _toast('Đã điều chỉnh kết quả.');
      context.pop();
      refreshAwardingData(container);
    } catch (e) {
      if (!mounted) return;
      final err = ApiException.from(e);
      _toast(err.message);
      // 409 = dữ liệu đã bị người khác đổi (BR-52) → tải lại bản mới nhất
      if (err.statusCode == 409) refreshAwardingData(container);
      setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final d = _d;
    final textTheme = Theme.of(context).textTheme;
    final disqualified = _rating == SubmissionRating.disqualified;
    final canAdjust = d.evaluation != null && d.competitionStatus == CompetitionStatus.graded && !_busy;

    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        SubmissionImage(submissionId: d.id, height: 240),
        const SizedBox(height: 12),
        Row(
          children: [
            Expanded(child: Text(d.title, style: textTheme.headlineSmall)),
            StatusChip(d.competitionStatus),
          ],
        ),
        Text('${d.studentName} (${d.studentCode}) • ${d.competitionTitle}'),
        if (d.evaluation case final ev?)
          Text('Chấm bởi ${ev.evaluatedByName} lúc ${formatDateTime(ev.evaluatedAt)}', style: textTheme.bodySmall),
        const SizedBox(height: 8),
        if (!canAdjust && !_busy)
          Card(
            color: Theme.of(context).colorScheme.errorContainer,
            child: const Padding(
              padding: EdgeInsets.all(12),
              child: Text('Chỉ điều chỉnh được bài đã có đánh giá của cuộc thi đã công bố. '
                  'Cuộc thi chưa công bố thì sửa ở mục Chấm bài.'),
            ),
          ),
        const Divider(height: 24),
        Text('Đánh giá điều chỉnh', style: textTheme.titleLarge),
        const SizedBox(height: 8),
        DropdownButtonFormField<SubmissionRating>(
          initialValue: _rating,
          decoration: const InputDecoration(labelText: 'Xếp loại *', border: OutlineInputBorder()),
          items: [for (final r in SubmissionRating.values) DropdownMenuItem(value: r, child: Text(r.label))],
          onChanged: canAdjust ? (v) => setState(() => _rating = v) : null,
        ),
        const SizedBox(height: 12),
        TextFormField(
          controller: _positive,
          enabled: canAdjust,
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
          enabled: canAdjust,
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
          enabled: canAdjust,
          maxLength: 2000,
          maxLines: 3,
          decoration: const InputDecoration(
            labelText: 'Điểm cần cải thiện',
            alignLabelWithHint: true,
            border: OutlineInputBorder(),
          ),
        ),
        const SizedBox(height: 8),
        // BR-34: mọi điều chỉnh sau công bố đều phải có lý do, lưu vào lịch sử thay đổi
        TextFormField(
          controller: _reason,
          enabled: canAdjust,
          maxLength: 1000,
          maxLines: 2,
          decoration: const InputDecoration(
            labelText: 'Lý do điều chỉnh *',
            alignLabelWithHint: true,
            border: OutlineInputBorder(),
          ),
        ),
        const SizedBox(height: 8),
        FilledButton.icon(
          onPressed: canAdjust ? _save : null,
          icon: _busy
              ? const SizedBox(width: 16, height: 16, child: CircularProgressIndicator(strokeWidth: 2))
              : const Icon(Icons.save),
          label: const Text('Lưu điều chỉnh'),
        ),
      ],
    );
  }
}
