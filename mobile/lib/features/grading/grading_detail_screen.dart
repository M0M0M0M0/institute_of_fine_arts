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

/// M-T3: grade one entry (UC-05, jury since 11/2026): the pictures (tap to zoom), entry info, who in the jury graded,
/// and the judge's own grade – a score from 0 to 100 for every weighted criterion (live total) or a disqualification
/// with its reason. After publishing: the jury's full score sheet, read only.
class GradingDetailScreen extends ConsumerWidget {
  const GradingDetailScreen({super.key, required this.id});

  final int id;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final detail = ref.watch(gradingDetailProvider(id));
    return Scaffold(
      appBar: AppBar(leading: const AppBackButton(), leadingWidth: 48, titleSpacing: 0, title: const Text('Grade entry')),
      body: AsyncValueView(
        value: detail,
        onRetry: () => ref.invalidate(gradingDetailProvider(id)),
        // Keyed by RowVersion: after a conflict reload / delete the form is rebuilt with fresh data
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
  // Rich text (BR-50): HTML like the web's editor; older grades are plain text
  late final RichTextController _positive;
  late final RichTextController _negative;
  late final RichTextController _improvement;
  late final Map<int, TextEditingController> _scores;
  late final Map<int, TextEditingController> _comments;
  bool _disqualified = false;
  bool _busy = false;

  /// Picture shown large (for "Download original"); null = the entry's own image.
  int? _pictureId;

  GradingDetail get _d => widget.detail;

  @override
  void initState() {
    super.initState();
    final ev = _d.evaluation;
    _positive = RichTextController(stored: ev?.positivePoints);
    _negative = RichTextController(stored: ev?.negativePoints);
    _improvement = RichTextController(stored: ev?.improvementAreas);
    _disqualified = ev?.disqualified ?? false;
    final mine = {for (final s in ev?.scores ?? const <GradingScore>[]) s.criterionId: s};
    _scores = {
      for (final c in _d.criteria)
        c.id: TextEditingController(text: mine[c.id]?.score == null ? '' : _plain(mine[c.id]!.score!)),
    };
    _comments = {for (final c in _d.criteria) c.id: TextEditingController(text: mine[c.id]?.comment ?? '')};
    _pictureId = _d.imageIds.isEmpty ? null : _d.imageIds.first;
  }

  /// 85 → "85", 85.5 → "85.5", 85.25 → "85.25".
  static String _plain(double v) => v == v.roundToDouble() ? v.toStringAsFixed(0) : '$v';

  @override
  void dispose() {
    for (final c in [_positive, _negative, _improvement]) {
      c.dispose();
    }
    for (final c in [..._scores.values, ..._comments.values]) {
      c.dispose();
    }
    super.dispose();
  }

  void _toast(String text) => ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(text)));

  // 409 = the grade changed in another window (BR-52): say so and reload the latest version
  void _handleError(Object e) {
    final err = ApiException.from(e);
    _toast(err.statusCode == 409 ? '${err.message} The entry was reloaded with the latest version.' : err.message);
    if (err.statusCode == 409) ref.invalidate(gradingDetailProvider(_d.id));
    setState(() => _busy = false);
  }

  double? get _liveTotal => judgeTotal(_d.criteria, {for (final c in _d.criteria) c.id: parseScore(_scores[c.id]!.text)});

  /// Saves the grade (create, or update with RowVersion). [goNext] moves on to the next entry to grade.
  Future<void> _save({required bool goNext}) async {
    final error = validateEvaluation(
      disqualified: _disqualified,
      criteria: _d.criteria,
      scoreTexts: {for (final e in _scores.entries) e.key: e.value.text},
      positive: _positive.plainText,
      negative: _negative.plainText,
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
      await ref.read(gradingRepositoryProvider).save(
            _d.id,
            EvaluationInput(
              disqualified: _disqualified,
              scores: [
                for (final c in _d.criteria)
                  GradingScore(criterionId: c.id, score: parseScore(_scores[c.id]!.text), comment: _comments[c.id]!.text),
              ],
              positivePoints: _positive.html,
              negativePoints: _negative.html,
              improvementAreas: _improvement.html,
              rowVersion: _d.evaluation?.rowVersion,
            ),
          );
      if (!mounted) return;
      _toast('Grade saved.');
      // Ask the API directly for the next entry to grade (providers are refreshed only after leaving)
      final nextId = goNext ? await _nextAfterSave() : null;
      if (!mounted) return;
      if (nextId != null) {
        context.pushReplacement('${gradingBase(context)}/submissions/$nextId/evaluate');
      } else {
        if (goNext) _toast('You have graded every entry of this competition.');
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
      builder: (context) => AlertDialog(
        title: const Text('Delete your grade?'),
        content: const Text('Your scores and comments for this entry will be removed. The entry has no result until you grade it again.'),
        actions: [
          TextButton(onPressed: () => Navigator.pop(context, false), child: const Text('Cancel')),
          FilledButton(onPressed: () => Navigator.pop(context, true), child: const Text('Delete')),
        ],
      ),
    );
    if (ok != true || !mounted) return;

    setState(() => _busy = true);
    try {
      await ref.read(gradingRepositoryProvider).deleteEvaluation(_d.id, ev.rowVersion);
      if (!mounted) return;
      _toast('Grade deleted.');
      _invalidateAfterChange(ProviderScope.containerOf(context), _d.id);
    } catch (e) {
      if (mounted) _handleError(e);
    }
  }

  Future<void> _download() async {
    setState(() => _busy = true);
    try {
      final picture = _pictureId;
      final bytes = picture == null
          ? await ref.read(submissionRepositoryProvider).imageBytes(_d.id, download: true)
          : await ref.read(submissionRepositoryProvider).pictureBytes(picture, download: true);
      final saved = await saveBytesAsFile(bytes, 'submission-${_d.id}${picture == null ? '' : '-$picture'}.jpg');
      if (mounted) _toast(saved ? 'Picture downloaded.' : 'Downloading is not supported on this platform (use Chrome).');
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
    final total = _liveTotal;
    // Wide screens: the artwork on the left, the jury and the grade on the right
    final wide = MediaQuery.sizeOf(context).width >= AppLayout.wide;

    final left = <Widget>[
        EntryPictures(
          submissionId: d.id,
          imageIds: d.imageIds,
          title: d.title,
          subtitle: '${d.studentName} (${d.studentCode})',
          awarded: d.resultRating == SubmissionRating.best,
          onSelected: (id) => setState(() => _pictureId = id),
        ),
        Align(
          alignment: Alignment.centerRight,
          child: TextButton.icon(
            onPressed: _busy ? null : _download,
            icon: const Icon(Icons.download, size: 18),
            label: const Text('Download original'),
          ),
        ),
        Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Expanded(
              child: Text(d.title, style: text.headlineSmall!.copyWith(fontStyle: FontStyle.italic)),
            ),
            const SizedBox(width: AppSpacing.sm),
            StatusChip(d.competitionStatus),
          ],
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
        ProgressBarRow(label: 'Graded by you in this competition', value: d.evaluated, total: d.total),
        const SectionHeader('Description', padding: EdgeInsets.fromLTRB(0, AppSpacing.lg, 0, AppSpacing.xs)),
        RichTextView(d.description),
        SectionHeader(textTypeName(d.textType), padding: const EdgeInsets.fromLTRB(0, AppSpacing.lg, 0, AppSpacing.xs)),
        RichTextView(d.textContent, style: text.bodyMedium!.copyWith(fontStyle: FontStyle.italic)),
    ];
    final right = <Widget>[
        if (!wide) const Divider(height: AppSpacing.xxl),

        // Jury progress: who graded this entry (never their scores before publishing)
        if (d.jury.isNotEmpty) ...[
          SectionHeader('Jury', padding: EdgeInsets.fromLTRB(0, wide ? 0 : AppSpacing.sm, 0, AppSpacing.xs)),
          for (final j in d.jury)
            Row(
              children: [
                Icon(j.done ? Icons.check_circle : Icons.radio_button_unchecked,
                    size: 18, color: j.done ? AppColors.ink : AppColors.muted),
                const SizedBox(width: AppSpacing.sm),
                Expanded(child: Text('${j.name}${j.isChair ? ' (chair)' : ''}${j.isMe ? ' – you' : ''}')),
                Text(j.done ? 'Graded' : 'Not yet', style: text.bodySmall),
              ],
            ),
          if (d.resultRating case final r?)
            Padding(
              padding: const EdgeInsets.only(top: AppSpacing.sm),
              child: Text(
                'Result: ${ratingName(r)}${d.resultScore != null ? ' · ${formatScore(d.resultScore!)}' : ''}',
                style: text.titleSmall,
              ),
            ),
          const SizedBox(height: AppSpacing.lg),
        ],

        // Published: the whole score sheet
        if (d.scoreSheet case final sheet?) ...[
          const SectionHeader('Score sheet', padding: EdgeInsets.only(bottom: AppSpacing.sm)),
          ScoreSheetView(sheet: sheet),
          const SizedBox(height: AppSpacing.lg),
        ],

        const SectionHeader('Your grade', padding: EdgeInsets.only(bottom: AppSpacing.sm)),
        if (d.evaluation case final ev?)
          Padding(
            padding: const EdgeInsets.only(bottom: AppSpacing.sm),
            child: Text(
              'Saved on ${formatDateTime(ev.evaluatedAt)}'
              '${ev.disqualified ? ' · disqualified' : ev.totalScore != null ? ' · total ${formatScore(ev.totalScore!)}' : ''}',
              style: text.bodySmall,
            ),
          ),
        // Not open for grading (results published): no form and no rule banner, just the state
        if (!d.canEvaluate && d.evaluation == null)
          Text('Not graded yet.', style: text.bodyMedium!.copyWith(color: AppColors.muted)),
        if (d.canEvaluate) ...[
          SwitchListTile(
            contentPadding: EdgeInsets.zero,
            title: const Text('Disqualify this entry'),
            subtitle: const Text('Breaks the rules (late, copied, off topic). One disqualification is enough.'),
            value: _disqualified,
            onChanged: editable ? (v) => setState(() => _disqualified = v) : null,
          ),
          if (!_disqualified) ...[
            const SizedBox(height: AppSpacing.sm),
            CriterionScoreFields(
              criteria: d.criteria,
              scores: _scores,
              comments: _comments,
              enabled: editable,
              onChanged: () => setState(() {}),
            ),
            Semantics(
              liveRegion: true,
              child: Text(
                total == null ? 'Total: enter every score' : 'Total: ${formatScore(total)} / 100',
                style: text.titleMedium,
              ),
            ),
            const SizedBox(height: AppSpacing.md),
          ],
          EvaluationTextFields(
            positive: _positive,
            negative: _negative,
            improvement: _improvement,
            disqualified: _disqualified,
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
                    ? const SizedBox(width: 16, height: 16, child: CircularProgressIndicator(strokeWidth: 2))
                    : const Icon(Icons.skip_next),
                label: const Text('SAVE & NEXT'),
              ),
              OutlinedButton.icon(
                onPressed: editable ? () => _save(goNext: false) : null,
                icon: const Icon(Icons.save_outlined),
                label: const Text('SAVE'),
              ),
              if (d.evaluation != null && d.canDeleteEvaluation)
                TextButton.icon(
                  onPressed: _busy ? null : _delete,
                  icon: const Icon(Icons.delete_outline),
                  label: const Text('DELETE MY GRADE'),
                  style: TextButton.styleFrom(foregroundColor: AppColors.danger),
                ),
            ],
          ),
        ],
        const SizedBox(height: AppSpacing.lg),
    ];

    return ListView(
      padding: readingPadding(context, maxWidth: wide ? 1040 : 720, top: AppSpacing.gutter, bottom: AppSpacing.gutter),
      children: wide
          ? [
              Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Expanded(flex: 5, child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: left)),
                  const SizedBox(width: AppSpacing.xxl),
                  Expanded(flex: 5, child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: right)),
                ],
              ),
            ]
          : [...left, ...right],
    );
  }
}
