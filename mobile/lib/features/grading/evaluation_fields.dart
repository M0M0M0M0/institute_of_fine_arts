import 'package:flutter/material.dart';

import '../../app/tokens.dart';
import '../../shared/shared.dart';
import 'grading.dart';

/// One score field per criterion (0–100, at most 2 decimals) with its weight and an optional comment (BR-23).
class CriterionScoreFields extends StatelessWidget {
  const CriterionScoreFields({
    super.key,
    required this.criteria,
    required this.scores,
    required this.comments,
    required this.enabled,
    required this.onChanged,
  });

  final List<GradingCriterion> criteria;

  /// criterion id → score controller / comment controller
  final Map<int, TextEditingController> scores;
  final Map<int, TextEditingController> comments;
  final bool enabled;
  final VoidCallback onChanged;

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        for (final c in criteria)
          Padding(
            padding: const EdgeInsets.only(bottom: AppSpacing.md),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text('${c.name} · ${c.weight.toStringAsFixed(c.weight % 1 == 0 ? 0 : 2)}%', style: text.titleSmall),
                          if (c.description != null && c.description!.isNotEmpty)
                            Text(c.description!, style: text.bodySmall!.copyWith(color: AppColors.muted)),
                        ],
                      ),
                    ),
                    const SizedBox(width: AppSpacing.sm),
                    SizedBox(
                      width: 96,
                      child: TextFormField(
                        controller: scores[c.id],
                        enabled: enabled,
                        keyboardType: const TextInputType.numberWithOptions(decimal: true),
                        textAlign: TextAlign.end,
                        decoration: const InputDecoration(labelText: 'Score *', hintText: '0–100', isDense: true),
                        onChanged: (_) => onChanged(),
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: AppSpacing.xs),
                TextFormField(
                  controller: comments[c.id],
                  enabled: enabled,
                  maxLength: 1000,
                  decoration: InputDecoration(labelText: 'Comment on ${c.name}', isDense: true, counterText: ''),
                ),
              ],
            ),
          ),
      ],
    );
  }
}

/// The three comment fields of a grade, written with the rich text editor (BR-50). When the entry is disqualified the
/// weaknesses field becomes the required reason (checked by [validateEvaluation]).
class EvaluationTextFields extends StatelessWidget {
  const EvaluationTextFields({
    super.key,
    required this.positive,
    required this.negative,
    required this.improvement,
    required this.disqualified,
    required this.enabled,
  });

  final RichTextController positive;
  final RichTextController negative;
  final RichTextController improvement;
  final bool disqualified;
  final bool enabled;

  @override
  Widget build(BuildContext context) {
    Widget field(RichTextController c, String label) => Padding(
          padding: const EdgeInsets.only(bottom: AppSpacing.sm),
          child: RichTextFormField(
            // A new field when the label changes (disqualified), so the box is rebuilt with its controller
            key: ValueKey(label),
            controller: c,
            label: label,
            requiredMessage: '',
            isRequired: false,
            enabled: enabled,
            maxLength: 2000,
            minHeight: 100,
            compact: true,
          ),
        );
    return Column(
      children: [
        if (!disqualified) field(positive, 'Strengths *'),
        // BR-23: for a disqualification this field carries the mandatory reason
        field(negative, disqualified ? 'Reason for disqualification *' : 'Weaknesses'),
        if (!disqualified) field(improvement, 'Areas to improve'),
      ],
    );
  }
}

/// The jury's score sheet (BR-24): one card per judge with their scores, then the averages, total and rating.
class ScoreSheetView extends StatelessWidget {
  const ScoreSheetView({super.key, required this.sheet});

  final ScoreSheet sheet;

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    String name(int criterionId) =>
        sheet.criteria.where((c) => c.id == criterionId).map((c) => c.name).firstOrNull ?? 'Criterion';
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          children: [
            if (sheet.rating case final r?) Text(ratingName(r), style: text.titleMedium),
            const Spacer(),
            if (sheet.totalScore case final t?) Text('Total ${formatScore(t)}', style: text.titleMedium),
          ],
        ),
        if (sheet.averages.isNotEmpty) ...[
          const SizedBox(height: AppSpacing.xs),
          for (final a in sheet.averages)
            Text('${name(a.criterionId)}: average ${formatScore(a.score ?? 0)}', style: text.bodySmall),
        ],
        for (final j in sheet.judges)
          Card(
            margin: const EdgeInsets.only(top: AppSpacing.sm),
            child: Padding(
              padding: const EdgeInsets.all(AppSpacing.md),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    children: [
                      Expanded(child: Text('${j.name}${j.isChair ? ' (chair)' : ''}', style: text.titleSmall)),
                      Text(j.disqualified ? 'Disqualified' : formatScore(j.totalScore ?? 0), style: text.titleSmall),
                    ],
                  ),
                  for (final s in j.scores)
                    Padding(
                      padding: const EdgeInsets.only(top: AppSpacing.xs),
                      child: Text(
                        '${name(s.criterionId)}: ${formatScore(s.score ?? 0)}'
                        '${s.comment != null && s.comment!.isNotEmpty ? ' – ${s.comment}' : ''}',
                        style: text.bodySmall,
                      ),
                    ),
                  for (final (label, value) in [
                    ('Strengths', j.positivePoints),
                    (j.disqualified ? 'Reason' : 'Weaknesses', j.negativePoints),
                    ('To improve', j.improvementAreas),
                  ])
                    if (value != null && value.trim().isNotEmpty) ...[
                      const SizedBox(height: AppSpacing.xs),
                      Text(label, style: text.labelMedium),
                      RichTextView(value),
                    ],
                ],
              ),
            ),
          ),
      ],
    );
  }
}
