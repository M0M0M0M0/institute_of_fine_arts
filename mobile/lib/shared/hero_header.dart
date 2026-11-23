import 'package:flutter/material.dart';

import '../app/tokens.dart';

/// Hero block, like the opening wall of an exhibition: the [background] image (any widget that fills its box,
/// e.g. a `PublicImage`) fades into dark ink at the bottom, with a small caps line, a large light title,
/// an optional subtitle and an optional outlined button.
/// Without a background it is a plain ink panel. On wide screens it grows taller and the text keeps to the
/// page gutter.
class HeroHeader extends StatelessWidget {
  const HeroHeader({
    super.key,
    required this.title,
    this.subtitle,
    this.background,
    this.ctaLabel,
    this.onCta,
    this.height = 280,
    this.eyebrow,
  });

  final String title;
  final String? subtitle;
  final Widget? background;
  final String? ctaLabel;
  final VoidCallback? onCta;
  final double height;

  /// Small uppercase line above the title (e.g. "Now showing").
  final String? eyebrow;

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    final width = MediaQuery.sizeOf(context).width;
    final wide = width >= AppLayout.wide;
    final gutter = wide ? AppLayout.gutterFor(width) : AppSpacing.lg;
    final boxHeight = wide ? height + 140 : height;

    return SizedBox(
      height: boxHeight,
      width: double.infinity,
      child: Stack(
        fit: StackFit.expand,
        children: [
          const ColoredBox(color: AppColors.ink),
          ?background,
          // Light veil on top, deep ink at the bottom where the text sits
          const DecoratedBox(
            decoration: BoxDecoration(
              gradient: LinearGradient(
                begin: Alignment.topCenter,
                end: Alignment.bottomCenter,
                stops: [0, 0.35, 0.85],
                colors: [Color(0x330B2A33), Color(0x800B2A33), Color(0xF50B2A33)],
              ),
            ),
          ),
          Padding(
            padding: EdgeInsets.fromLTRB(gutter, AppSpacing.lg, gutter, wide ? AppSpacing.xxl : AppSpacing.lg),
            child: Align(
              alignment: Alignment.bottomLeft,
              child: ConstrainedBox(
                constraints: const BoxConstraints(maxWidth: 640),
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    if (eyebrow != null) ...[
                      Text(
                        eyebrow!.toUpperCase(),
                        style: text.labelSmall!.copyWith(color: AppColors.stone, letterSpacing: 2.2),
                      ),
                      const SizedBox(height: AppSpacing.sm),
                    ],
                    // Short brass rule: the gilt of a frame
                    const SizedBox(width: 36, height: 2, child: ColoredBox(color: AppColors.brass)),
                    const SizedBox(height: AppSpacing.md),
                    Semantics(
                      header: true,
                      child: Text(
                        title,
                        maxLines: 3,
                        style: (wide ? text.displayMedium : text.headlineLarge)!.copyWith(color: Colors.white),
                      ),
                    ),
                    if (subtitle != null) ...[
                      const SizedBox(height: AppSpacing.sm),
                      Text(
                        subtitle!,
                        style: text.bodyLarge!.copyWith(color: Colors.white.withValues(alpha: 0.88)),
                      ),
                    ],
                    if (ctaLabel != null && onCta != null) ...[
                      const SizedBox(height: AppSpacing.lg),
                      OutlinedButton(
                        onPressed: onCta,
                        style: OutlinedButton.styleFrom(
                          foregroundColor: Colors.white,
                          side: const BorderSide(color: Colors.white, width: 1),
                          padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 14),
                        ),
                        child: Text('${ctaLabel!.toUpperCase()}  →'),
                      ),
                    ],
                  ],
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }
}
