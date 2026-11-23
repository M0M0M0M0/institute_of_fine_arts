import 'package:flutter/material.dart';

import '../../app/tokens.dart';
import '../../shared/shared.dart';
import '../submissions/submissions.dart';
import 'grading.dart';

/// Rating picker as a row of square buttons in small caps (used by the grading form and the "adjust result" form).
/// The chosen one is filled with ink (red for "Disqualified"). Read-only when [onChanged] is null.
class RatingChips extends StatelessWidget {
  const RatingChips({super.key, required this.value, required this.onChanged});

  /// Current rating; [onChanged] null = read-only.
  final SubmissionRating? value;
  final ValueChanged<SubmissionRating>? onChanged;

  @override
  Widget build(BuildContext context) {
    return Wrap(
      spacing: AppSpacing.sm,
      runSpacing: AppSpacing.sm,
      children: [
        for (final r in SubmissionRating.values)
          _RatingButton(
            label: ratingName(r),
            selected: value == r,
            danger: r == SubmissionRating.disqualified,
            onTap: onChanged == null ? null : () => onChanged!(r),
          ),
      ],
    );
  }
}

class _RatingButton extends StatelessWidget {
  const _RatingButton({
    required this.label,
    required this.selected,
    required this.danger,
    required this.onTap,
  });

  final String label;
  final bool selected;
  final bool danger;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final accent = danger ? AppColors.danger : AppColors.ink;
    final enabled = onTap != null;
    // A disabled button keeps its meaning (the chosen one stays filled) but loses its contrast a little
    final fill = selected
        ? (enabled ? accent : accent.withValues(alpha: 0.55))
        : AppColors.wall;
    final border = selected
        ? fill
        : (enabled ? AppColors.muted : AppColors.line);
    final textColor = selected
        ? Colors.white
        : (enabled
              ? (danger ? AppColors.danger : AppColors.ink)
              : AppColors.muted);
    return Semantics(
      button: true,
      selected: selected,
      enabled: enabled,
      child: InkWell(
        onTap: onTap,
        // No `alignment` here: it would stretch the button over the whole row instead of fitting its label
        child: Container(
          padding: const EdgeInsets.symmetric(
            horizontal: AppSpacing.md,
            vertical: 13,
          ),
          decoration: BoxDecoration(
            color: fill,
            border: Border.all(color: border),
          ),
          child: Text(
            label.toUpperCase(),
            style: Theme.of(context).textTheme.labelSmall!
                .copyWith(fontSize: 12.5, letterSpacing: 1.2, color: textColor),
          ),
        ),
      ),
    );
  }
}

/// The three comment fields of an evaluation, each with the small rich text toolbar (the value is HTML, like the
/// web's TinyMCE). For "Disqualified" the weaknesses field becomes the required reason.
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
    Widget field(
      RichTextController c,
      String label, {
      required bool required,
      required String message,
    }) => Padding(
      padding: const EdgeInsets.only(bottom: AppSpacing.sm),
      child: RichTextFormField(
        controller: c,
        label: label,
        requiredMessage: message,
        isRequired: required,
        enabled: enabled,
        minHeight: 64,
        compact: true,
      ),
    );
    return Column(
      children: [
        field(
          positive,
          disqualified ? 'Strengths' : 'Strengths *',
          required: !disqualified,
          message: 'Please enter the strengths',
        ),
        // BR-23: for DISQUALIFIED this field carries the mandatory reason
        field(
          negative,
          disqualified ? 'Reason for disqualification *' : 'Weaknesses',
          required: disqualified,
          message: 'The Disqualified rating needs a reason',
        ),
        field(improvement, 'Areas to improve', required: false, message: ''),
      ],
    );
  }
}
