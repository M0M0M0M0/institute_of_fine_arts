import 'package:flutter/material.dart';

import '../app/tokens.dart';

/// Hero block: [background] image (any widget that fills its box, e.g. a `PublicImage`)
/// under a navy gradient, with a serif title, optional subtitle and an optional CTA button.
/// Without a background it is a plain navy panel.
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

  /// Small uppercase line above the title (e.g. "Latest winner").
  final String? eyebrow;

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    return SizedBox(
      height: height,
      width: double.infinity,
      child: Stack(
        fit: StackFit.expand,
        children: [
          const ColoredBox(color: AppColors.ink),
          ?background,
          const DecoratedBox(
            decoration: BoxDecoration(
              gradient: LinearGradient(
                begin: Alignment.topCenter,
                end: Alignment.bottomCenter,
                colors: [Color(0x330B2A33), Color(0xE60B2A33)],
              ),
            ),
          ),
          Padding(
            padding: const EdgeInsets.all(AppSpacing.lg),
            child: Column(
              mainAxisAlignment: MainAxisAlignment.end,
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                if (eyebrow != null)
                  Text(
                    eyebrow!.toUpperCase(),
                    style: text.labelSmall!.copyWith(color: AppColors.stone, letterSpacing: 1.2),
                  ),
                Semantics(
                  header: true,
                  child: Text(title, style: text.headlineMedium!.copyWith(color: Colors.white), maxLines: 3),
                ),
                if (subtitle != null) ...[
                  const SizedBox(height: AppSpacing.xs),
                  Text(subtitle!, style: text.bodyMedium!.copyWith(color: Colors.white.withValues(alpha: 0.9))),
                ],
                if (ctaLabel != null && onCta != null) ...[
                  const SizedBox(height: AppSpacing.md),
                  FilledButton(
                    onPressed: onCta,
                    style: FilledButton.styleFrom(backgroundColor: Colors.white, foregroundColor: AppColors.ink),
                    child: Text(ctaLabel!),
                  ),
                ],
              ],
            ),
          ),
        ],
      ),
    );
  }
}
