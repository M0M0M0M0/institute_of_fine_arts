import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../app/tokens.dart';
import '../../core/api_client.dart';
import '../../core/async_view.dart';
import '../../core/file_download.dart';
import '../../core/format.dart';
import '../../shared/shared.dart';
import '../submissions/submissions.dart';
import 'evaluation_fields.dart';
import 'grading.dart';

/// M-T3: grade one entry: framed picture (tap to zoom), entry info, evaluation form (UC-05).
class GradingDetailScreen extends ConsumerWidget {
  const GradingDetailScreen({super.key, required this.id});

  final int id;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final detail = ref.watch(gradingDetailProvider(id));
    return Scaffold(
      appBar: AppBar(
        leading: const AppBackButton(),
        leadingWidth: 48,
        titleSpacing: 0,
        title: const Text('Grade entry'),
      ),
      body: AsyncValueView(
        value: detail,
        onRetry: () => ref.invalidate(gradingDetailProvider(id)),
        // Keyed by RowVersion: after a conflict reload / delete the form is rebuilt with fresh data
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
    super.dispose();
  }

  void _toast(String text) =>
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(text)));

  // 409 = somebody else changed the data (BR-52): say so and reload the latest version
  void _handleError(Object e) {
    final err = ApiException.from(e);
    _toast(
      err.statusCode == 409
          ? '${err.message} The entry was reloaded with the latest version.'
          : err.message,
    );
    if (err.statusCode == 409) ref.invalidate(gradingDetailProvider(_d.id));
    setState(() => _busy = false);
  }

  /// Saves the evaluation (create, or update with RowVersion). [goNext] moves on to the next ungraded entry.
  Future<void> _save({required bool goNext}) async {
    final error = validateEvaluation(
      _rating,
      _positive.plainText,
      _negative.plainText,
    );
    if (error != null) {
      _toast(error);
      return;
    }
    // Keep the container to refresh data AFTER leaving the screen (a disposed widget's ref is unusable)
    final container = ProviderScope.containerOf(context);
    final id = _d.id;
    setState(() => _busy = true);
    try {
      await ref
          .read(gradingRepositoryProvider)
          .save(
            _d.id,
            EvaluationInput(
              rating: _rating!,
              positivePoints: _positive.html,
              negativePoints: _negative.html,
              improvementAreas: _improvement.html,
              rowVersion: _d.evaluation?.rowVersion,
            ),
          );
      if (!mounted) return;
      _toast('Evaluation saved.');
      // Ask the API directly for the next ungraded entry (providers are refreshed only after leaving)
      final nextId = goNext ? await _nextAfterSave() : null;
      if (!mounted) return;
      if (nextId != null) {
        context.pushReplacement(
          '${gradingBase(context)}/submissions/$nextId/evaluate',
        );
      } else {
        if (goNext) _toast('Every entry of this competition is graded.');
        context.pop();
      }
      _invalidateAfterChange(container, id);
    } catch (e) {
      if (mounted) _handleError(e);
    }
  }

  void _invalidateAfterChange(ProviderContainer container, int id) {
    container.invalidate(gradingCompetitionsProvider);
    container.invalidate(gradingListProvider);
    container.invalidate(gradingDetailProvider(id));
    container.invalidate(submissionDetailProvider(id));
  }

  Future<int?> _nextAfterSave() async {
    try {
      final fresh = await ref.read(gradingRepositoryProvider).detail(_d.id);
      return fresh.nextUnevaluatedId;
    } catch (_) {
      return null;
    }
  }

  Future<void> _delete() async {
    final ev = _d.evaluation!;
    final ok = await showDialog<bool>(
      context: context,
      builder: (context) => ArtDialog(
        title: const Text('Delete evaluation?'),
        content: const Text(
          'The evaluation of this entry will be removed and the entry goes back to "To grade".',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context, false),
            child: const Text('CANCEL'),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(context, true),
            child: const Text('DELETE'),
          ),
        ],
      ),
    );
    if (ok != true || !mounted) return;

    setState(() => _busy = true);
    try {
      await ref
          .read(gradingRepositoryProvider)
          .deleteEvaluation(_d.id, ev.rowVersion);
      if (!mounted) return;
      _toast('Evaluation deleted.');
      _invalidateAfterChange(ProviderScope.containerOf(context), _d.id);
    } catch (e) {
      if (mounted) _handleError(e);
    }
  }

  Future<void> _download() async {
    setState(() => _busy = true);
    try {
      final bytes = await ref
          .read(submissionRepositoryProvider)
          .imageBytes(_d.id, download: true);
      final saved = await saveBytesAsFile(bytes, 'submission-${_d.id}.jpg');
      if (mounted) {
        _toast(
          saved
              ? 'Picture downloaded.'
              : 'Downloading is not supported on this platform (use Chrome).',
        );
      }
    } catch (e) {
      if (mounted) _toast(ApiException.from(e).message);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final d = _d;
    final text = Theme.of(context).textTheme;
    final editable = d.canEvaluate && !_busy;
    final disqualified = _rating == SubmissionRating.disqualified;
    final awarded = _rating == SubmissionRating.best;

    final wide = MediaQuery.sizeOf(context).width >= AppLayout.wide;

    // Left: the painting and what is known about it. Right: the evaluation. On a wide screen they sit side by
    // side, so the teacher keeps the painting in view while writing; on a phone they follow each other.
    final left = <Widget>[
      // Large framed picture; tap to zoom
      Center(
        child: ConstrainedBox(
          constraints: const BoxConstraints(maxWidth: 440),
          child: Semantics(
            button: true,
            label: 'Open ${d.title} full screen',
            child: InkWell(
              onTap: () => ArtworkViewer.open(
                context,
                ArtworkViewer.private(
                  submissionId: d.id,
                  title: d.title,
                  subtitle: '${d.studentName} (${d.studentCode})',
                ),
              ),
              // The frame hugs the painting at its own proportions (portrait or landscape)
              child: ArtworkFrame(
                awarded: awarded,
                aspectRatio: null,
                child: NaturalArtworkImage.private(
                  submissionId: d.id,
                  semanticLabel: d.title,
                ),
              ),
            ),
          ),
        ),
      ),
      Align(
        alignment: Alignment.centerRight,
        child: TextButton.icon(
          onPressed: _busy ? null : _download,
          icon: const Icon(Icons.download, size: 18),
          label: const Text('DOWNLOAD ORIGINAL'),
        ),
      ),
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
      Text('${d.studentName} (${d.studentCode})', style: text.bodyLarge),
      Text(d.competitionTitle, style: text.bodyMedium),
      Text(
        'Submitted ${formatDateTime(d.submittedAt)}'
        '${d.updatedAt != null ? ' · edited ${formatDateTime(d.updatedAt!)}' : ''}',
        style: text.bodySmall,
      ),
      const SizedBox(height: AppSpacing.xs),
      ProgressBarRow(
        label: 'Graded in this competition',
        value: d.evaluated,
        total: d.total,
      ),
      const SectionHeader(
        'Description',
        padding: EdgeInsets.fromLTRB(0, AppSpacing.lg, 0, AppSpacing.xs),
      ),
      RichTextView(d.description, style: text.bodyMedium),
      SectionHeader(
        textTypeName(d.textType),
        padding: const EdgeInsets.fromLTRB(0, AppSpacing.lg, 0, AppSpacing.xs),
      ),
      Text(d.textContent),
    ];
    final right = <Widget>[
      if (!wide) const Divider(height: AppSpacing.xxl),
      SectionHeader(
        'Evaluation',
        padding: EdgeInsets.fromLTRB(
          0,
          wide ? 0 : AppSpacing.sm,
          0,
          AppSpacing.sm,
        ),
      ),
      if (d.evaluation case final ev?)
        Padding(
          padding: const EdgeInsets.only(bottom: AppSpacing.sm),
          child: Text(
            'Graded by ${ev.evaluatedByName} on ${formatDateTime(ev.evaluatedAt)}',
            style: text.bodySmall,
          ),
        ),
      // Not open for grading (competition still running, or results published): no form, just the result
      if (!d.canEvaluate && d.evaluation != null)
        Card(
          child: Padding(
            padding: const EdgeInsets.all(AppSpacing.md),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                StatusChip.rating(d.evaluation!.rating),
                for (final (label, value) in [
                  ('Strengths', d.evaluation!.positivePoints),
                  ('Weaknesses', d.evaluation!.negativePoints),
                  ('To improve', d.evaluation!.improvementAreas),
                ])
                  if (value != null && value.trim().isNotEmpty) ...[
                    const SizedBox(height: AppSpacing.sm),
                    Text(label, style: text.titleSmall),
                    RichTextView(value, style: text.bodyMedium),
                  ],
              ],
            ),
          ),
        ),
      if (!d.canEvaluate && d.evaluation == null)
        Text(
          'Not graded yet.',
          style: text.bodyMedium!.copyWith(color: AppColors.muted),
        ),
      if (d.canEvaluate) ...[
        const SizedBox(height: AppSpacing.sm),
        Text('Rating *', style: text.labelLarge),
        const SizedBox(height: AppSpacing.xs),
        RatingChips(
          value: _rating,
          onChanged: editable ? (v) => setState(() => _rating = v) : null,
        ),
        const SizedBox(height: AppSpacing.md),
        EvaluationTextFields(
          positive: _positive,
          negative: _negative,
          improvement: _improvement,
          disqualified: disqualified,
          enabled: editable,
        ),
        const SizedBox(height: AppSpacing.sm),
        Wrap(
          spacing: AppSpacing.md,
          runSpacing: AppSpacing.sm,
          children: [
            FilledButton.icon(
              onPressed: editable ? () => _save(goNext: true) : null,
              icon: _busy
                  ? const SizedBox(
                      width: 16,
                      height: 16,
                      child: CircularProgressIndicator(strokeWidth: 2),
                    )
                  : const Icon(Icons.skip_next),
              label: const Text('SAVE & NEXT'),
            ),
            OutlinedButton.icon(
              onPressed: editable ? () => _save(goNext: false) : null,
              icon: const Icon(Icons.save_outlined),
              label: const Text('SAVE'),
            ),
            // Only offered when it is allowed (no award, not sent to an exhibition)
            if (d.evaluation != null && d.canDeleteEvaluation)
              TextButton.icon(
                onPressed: _busy ? null : _delete,
                icon: const Icon(Icons.delete_outline),
                label: const Text('DELETE EVALUATION'),
                style: TextButton.styleFrom(foregroundColor: AppColors.danger),
              ),
          ],
        ),
      ],
      const SizedBox(height: AppSpacing.lg),
    ];

    return ListView(
      padding: readingPadding(
        context,
        maxWidth: wide ? 1040 : 720,
        top: AppSpacing.gutter,
        bottom: AppSpacing.gutter,
      ),
      children: wide
          ? [
              Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Expanded(
                    flex: 5,
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: left,
                    ),
                  ),
                  const SizedBox(width: AppSpacing.xxl),
                  Expanded(
                    flex: 5,
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: right,
                    ),
                  ),
                ],
              ),
            ]
          : [...left, ...right],
    );
  }
}
