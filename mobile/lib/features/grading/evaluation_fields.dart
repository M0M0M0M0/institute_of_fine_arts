import 'package:flutter/material.dart';

import '../../app/tokens.dart';
import '../submissions/submissions.dart';
import 'grading.dart';

/// Rating picker as ChoiceChips (used by the grading form and the "adjust result" form).
class RatingChips extends StatelessWidget {
  const RatingChips({super.key, required this.value, required this.onChanged});

  /// Current rating; [onChanged] null = read-only.
  final SubmissionRating? value;
  final ValueChanged<SubmissionRating>? onChanged;

  @override
  Widget build(BuildContext context) {
    return Wrap(
      spacing: AppSpacing.sm,
      runSpacing: AppSpacing.xs,
      children: [
        for (final r in SubmissionRating.values)
          ChoiceChip(
            label: Text(ratingName(r)),
            selected: value == r,
            selectedColor: r == SubmissionRating.disqualified ? AppColors.danger.withValues(alpha: 0.18) : AppColors.mist,
            onSelected: onChanged == null ? null : (_) => onChanged!(r),
          ),
      ],
    );
  }
}

/// The three comment fields of an evaluation. For "Disqualified" the weaknesses field becomes the required reason.
class EvaluationTextFields extends StatelessWidget {
  const EvaluationTextFields({
    super.key,
    required this.positive,
    required this.negative,
    required this.improvement,
    required this.disqualified,
    required this.enabled,
  });

  final TextEditingController positive;
  final TextEditingController negative;
  final TextEditingController improvement;
  final bool disqualified;
  final bool enabled;

  @override
  Widget build(BuildContext context) {
    Widget field(TextEditingController c, String label) => Padding(
          padding: const EdgeInsets.only(bottom: AppSpacing.sm),
          child: TextFormField(
            controller: c,
            enabled: enabled,
            maxLength: 2000,
            maxLines: 3,
            decoration: InputDecoration(labelText: label, alignLabelWithHint: true),
          ),
        );
    return Column(
      children: [
        field(positive, disqualified ? 'Strengths' : 'Strengths *'),
        // BR-23: for DISQUALIFIED this field carries the mandatory reason
        field(negative, disqualified ? 'Reason for disqualification *' : 'Weaknesses'),
        field(improvement, 'Areas to improve'),
      ],
    );
  }
}
