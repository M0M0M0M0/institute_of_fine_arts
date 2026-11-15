import 'package:flutter/material.dart';

import '../app/tokens.dart';

/// Section title in the display serif, with an optional action on the right
/// (e.g. "See all"). Put it between blocks of a scrolling page.
class SectionHeader extends StatelessWidget {
  const SectionHeader(
    this.title, {
    super.key,
    this.actionLabel,
    this.onAction,
    this.subtitle,
    this.padding = const EdgeInsets.fromLTRB(AppSpacing.gutter, AppSpacing.lg, AppSpacing.gutter, AppSpacing.sm),
  });

  final String title;

  /// Text of the action button; shown only when [onAction] is also set.
  final String? actionLabel;
  final VoidCallback? onAction;
  final String? subtitle;
  final EdgeInsetsGeometry padding;

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    return Padding(
      padding: padding,
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.end,
        children: [
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Semantics(header: true, child: Text(title, style: text.headlineSmall)),
                if (subtitle != null) Text(subtitle!, style: text.bodySmall),
              ],
            ),
          ),
          if (actionLabel != null && onAction != null)
            TextButton(onPressed: onAction, child: Text(actionLabel!)),
        ],
      ),
    );
  }
}

/// Centered empty state: icon, title and optional message / action.
class EmptyState extends StatelessWidget {
  const EmptyState({
    super.key,
    required this.title,
    this.message,
    this.icon = Icons.image_not_supported_outlined,
    this.actionLabel,
    this.onAction,
  });

  final String title;
  final String? message;
  final IconData icon;
  final String? actionLabel;
  final VoidCallback? onAction;

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(AppSpacing.lg),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(icon, size: 44, color: AppColors.brass),
            const SizedBox(height: AppSpacing.md),
            Text(title, style: text.titleMedium, textAlign: TextAlign.center),
            if (message != null) ...[
              const SizedBox(height: AppSpacing.xs),
              Text(message!, style: text.bodyMedium!.copyWith(color: AppColors.muted), textAlign: TextAlign.center),
            ],
            if (actionLabel != null && onAction != null) ...[
              const SizedBox(height: AppSpacing.md),
              OutlinedButton(onPressed: onAction, child: Text(actionLabel!)),
            ],
          ],
        ),
      ),
    );
  }
}

/// A labelled progress bar: "label ........ done / total" over a thin bar.
/// Used for grading progress and filled award slots.
class ProgressBarRow extends StatelessWidget {
  const ProgressBarRow({
    super.key,
    required this.label,
    required this.value,
    required this.total,
    this.color,
  });

  final String label;
  final int value;
  final int total;

  /// Bar colour (defaults to navy ink).
  final Color? color;

  /// 0..1 fraction; 0 when [total] is zero.
  double get fraction => total <= 0 ? 0 : (value / total).clamp(0, 1).toDouble();

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    return Semantics(
      label: '$label: $value of $total',
      excludeSemantics: true,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Expanded(child: Text(label, style: text.bodyMedium, overflow: TextOverflow.ellipsis)),
              Text('$value / $total', style: text.labelMedium),
            ],
          ),
          const SizedBox(height: 6),
          ClipRRect(
            borderRadius: AppRadius.smAll,
            child: LinearProgressIndicator(
              value: fraction,
              minHeight: 6,
              color: color ?? AppColors.ink,
              backgroundColor: AppColors.stone,
            ),
          ),
        ],
      ),
    );
  }
}
