import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/api_client.dart';
import '../../app/tokens.dart';
import '../../core/async_view.dart';
import '../../core/format.dart';
import '../competitions/competitions.dart';
import '../../shared/shared.dart';
import '../grading/evaluation_fields.dart';
import '../grading/grading.dart';
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
      appBar: AppBar(
        leading: const AppBackButton(),
        leadingWidth: 48,
        titleSpacing: 0,
        title: const Text('Adjust result'),
      ),
      body: AsyncValueView(
        value: detail,
        onRetry: () => ref.invalidate(gradingDetailProvider(id)),
        // Key theo RowVersion: tải lại sau xung đột thì form dựng lại với dữ liệu mới
        data: (d) => _Body(
          key: ValueKey('${d.id}-${d.evaluation?.rowVersion}'),
          detail: d,
        ),
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
  late final RichTextController _positive;
  late final RichTextController _negative;
  late final RichTextController _improvement;
  final _reason = TextEditingController();
  SubmissionRating? _rating;
  bool _busy = false;

  GradingDetail get _d => widget.detail;

  @override
  void initState() {
    super.initState();
    final ev = _d.evaluation;
    _positive = RichTextController(stored: ev?.positivePoints);
    _negative = RichTextController(stored: ev?.negativePoints);
    _improvement = RichTextController(stored: ev?.improvementAreas);
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

  void _toast(String text) =>
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(text)));

  Future<void> _save() async {
    final ev = _d.evaluation;
    if (ev == null) return;
    // Kiểm tra nhanh phía client; server vẫn là nơi quyết định (BR-23, BR-34)
    final error =
        validateEvaluation(_rating, _positive.plainText, _negative.plainText) ??
        validateReason(_reason.text);
    if (error != null) {
      _toast(error);
      return;
    }
    final container = ProviderScope.containerOf(context);
    setState(() => _busy = true);
    try {
      await ref
          .read(awardingRepositoryProvider)
          .adjustEvaluation(
            _d.id,
            EvaluationInput(
              rating: _rating!,
              positivePoints: _positive.html,
              negativePoints: _negative.html,
              improvementAreas: _improvement.html,
              rowVersion: ev.rowVersion,
            ),
            _reason.text.trim(),
          );
      if (!mounted) return;
      _toast('Result adjusted.');
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
    final text = Theme.of(context).textTheme;
    final disqualified = _rating == SubmissionRating.disqualified;
    final canAdjust =
        d.evaluation != null &&
        d.competitionStatus == CompetitionStatus.graded &&
        !_busy;

    return ListView(
      padding: readingPadding(
        context,
        top: AppSpacing.gutter,
        bottom: AppSpacing.gutter,
      ),
      children: [
        Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 360),
            child: InkWell(
              onTap: () => ArtworkViewer.open(
                context,
                ArtworkViewer.private(
                  submissionId: d.id,
                  title: d.title,
                  subtitle: '${d.studentName} (${d.studentCode})',
                ),
              ),
              child: ArtworkFrame(
                aspectRatio: 4 / 5,
                child: ColoredBox(
                  color: AppColors.stone,
                  child: AuthImage(
                    submissionId: d.id,
                    thumb: false,
                    fit: BoxFit.contain,
                    semanticLabel: d.title,
                  ),
                ),
              ),
            ),
          ),
        ),
        const SizedBox(height: AppSpacing.md),
        Text(
          d.title,
          style: text.headlineSmall!.copyWith(fontStyle: FontStyle.italic),
        ),
        const SizedBox(height: AppSpacing.sm),
        Align(
          alignment: Alignment.centerLeft,
          child: StatusChip(d.competitionStatus),
        ),
        const SizedBox(height: AppSpacing.xs),
        Text('${d.studentName} (${d.studentCode}) · ${d.competitionTitle}'),
        if (d.evaluation case final ev?)
          Text(
            'Graded by ${ev.evaluatedByName} on ${formatDateTime(ev.evaluatedAt)}',
            style: text.bodySmall,
          ),
        const SizedBox(height: AppSpacing.sm),
        if (!canAdjust && !_busy)
          Container(
            padding: const EdgeInsets.all(AppSpacing.md),
            decoration: BoxDecoration(
              color: AppColors.danger.withValues(alpha: 0.08),
              borderRadius: AppRadius.mdAll,
              border: Border.all(
                color: AppColors.danger.withValues(alpha: 0.4),
              ),
            ),
            child: const Text(
              'Only graded entries of a published competition can be adjusted here. '
              'For an unpublished competition, change the evaluation under Grading.',
            ),
          ),
        const Divider(height: AppSpacing.xl),
        Semantics(
          header: true,
          child: Text('Adjusted evaluation', style: text.titleLarge),
        ),
        const SizedBox(height: AppSpacing.sm),
        Text('Rating *', style: text.labelLarge),
        const SizedBox(height: AppSpacing.xs),
        RatingChips(
          value: _rating,
          onChanged: canAdjust ? (v) => setState(() => _rating = v) : null,
        ),
        const SizedBox(height: AppSpacing.md),
        EvaluationTextFields(
          positive: _positive,
          negative: _negative,
          improvement: _improvement,
          disqualified: disqualified,
          enabled: canAdjust,
        ),
        // BR-34: every adjustment after publishing needs a reason, kept in the change history
        TextFormField(
          controller: _reason,
          enabled: canAdjust,
          maxLength: 1000,
          maxLines: 2,
          decoration: const InputDecoration(
            labelText: 'Reason for the adjustment *',
            alignLabelWithHint: true,
          ),
        ),
        const SizedBox(height: AppSpacing.sm),
        FilledButton.icon(
          onPressed: canAdjust ? _save : null,
          icon: _busy
              ? const SizedBox(
                  width: 16,
                  height: 16,
                  child: CircularProgressIndicator(strokeWidth: 2),
                )
              : const Icon(Icons.save),
          label: const Text('Save adjustment'),
        ),
        const SizedBox(height: AppSpacing.lg),
      ],
    );
  }
}
