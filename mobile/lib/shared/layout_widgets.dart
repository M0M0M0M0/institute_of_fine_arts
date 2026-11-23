import 'package:flutter/material.dart';

import '../app/tokens.dart';

/// Section title with a short brass rule above it, and an optional action on the right
/// (e.g. "See all"). Put it between blocks of a scrolling page.
class SectionHeader extends StatelessWidget {
  const SectionHeader(
    this.title, {
    super.key,
    this.actionLabel,
    this.onAction,
    this.subtitle,
    this.padding,
  });

  final String title;

  /// Text of the action button; shown only when [onAction] is also set.
  final String? actionLabel;
  final VoidCallback? onAction;
  final String? subtitle;

  /// Defaults to the page gutter on the sides and generous space above (a pause between rooms).
  final EdgeInsetsGeometry? padding;

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    final width = MediaQuery.sizeOf(context).width;
    final gutter = AppLayout.gutterFor(width);
    return Padding(
      padding: padding ?? EdgeInsets.fromLTRB(gutter, width >= AppLayout.wide ? AppSpacing.xxl : AppSpacing.xl, gutter, AppSpacing.md),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.end,
        children: [
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const SizedBox(width: 28, height: 2, child: ColoredBox(color: AppColors.brass)),
                const SizedBox(height: AppSpacing.sm),
                Semantics(header: true, child: Text(title, style: text.headlineSmall)),
                if (subtitle != null) ...[
                  const SizedBox(height: 2),
                  Text(subtitle!, style: text.bodySmall),
                ],
              ],
            ),
          ),
          if (actionLabel != null && onAction != null)
            TextButton(
              onPressed: onAction,
              child: Text('${actionLabel!.toUpperCase()}  →', style: text.labelSmall!.copyWith(letterSpacing: 1.4)),
            ),
        ],
      ),
    );
  }
}

/// Padding for a scrolling page of text and forms: on wide screens the content stays in a column of [maxWidth]
/// (instead of running 1000 px wide) while the scroll area itself still spans the page, so the mouse wheel works
/// over the margins too. On a phone it is the plain page gutter.
EdgeInsets readingPadding(
  BuildContext context, {
  double maxWidth = 720,
  double top = AppSpacing.md,
  double bottom = AppSpacing.xl,
}) {
  final width = MediaQuery.sizeOf(context).width;
  final side = ((width - maxWidth) / 2).clamp(AppLayout.gutterFor(width), double.infinity).toDouble();
  return EdgeInsets.fromLTRB(side, top, side, bottom);
}

/// Lays [children] out in as many equal columns as fit ([minTileWidth] wide at least), top aligned.
/// Unlike a fixed-height grid it lets every tile keep its own height, so wall labels never overflow.
class AdaptiveTileGrid extends StatelessWidget {
  const AdaptiveTileGrid({
    super.key,
    required this.children,
    this.minTileWidth = 260,
    this.maxColumns = 4,
    this.spacing = AppSpacing.lg,
    this.padding,
  });

  final List<Widget> children;
  final double minTileWidth;
  final int maxColumns;
  final double spacing;

  /// Defaults to the page gutter on the sides.
  final EdgeInsetsGeometry? padding;

  @override
  Widget build(BuildContext context) {
    final gutter = AppLayout.gutterFor(MediaQuery.sizeOf(context).width);
    final pad = (padding ?? EdgeInsets.symmetric(horizontal: gutter)).resolve(Directionality.of(context));
    return Padding(
      padding: pad,
      child: LayoutBuilder(
        builder: (context, constraints) {
          final columns = ((constraints.maxWidth + spacing) / (minTileWidth + spacing)).floor().clamp(1, maxColumns);
          final tileWidth = (constraints.maxWidth - spacing * (columns - 1)) / columns;
          return Wrap(
            spacing: spacing,
            runSpacing: spacing,
            children: [for (final c in children) SizedBox(width: tileWidth, child: c)],
          );
        },
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
            Icon(icon, size: 40, color: AppColors.brass, weight: 200),
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
