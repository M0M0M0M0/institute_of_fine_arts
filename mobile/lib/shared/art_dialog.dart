import 'package:flutter/material.dart';

import '../app/tokens.dart';

/// A dialog in the style of the rest of the app: a short brass rule, a large light title, square corners with a
/// hairline. Use it instead of `AlertDialog` (same `title`, `content` and `actions`).
class ArtDialog extends StatelessWidget {
  const ArtDialog({super.key, this.title, this.content, this.actions});

  final Widget? title;
  final Widget? content;
  final List<Widget>? actions;

  @override
  Widget build(BuildContext context) {
    return AlertDialog(
      insetPadding: const EdgeInsets.symmetric(horizontal: AppSpacing.md, vertical: AppSpacing.lg),
      titlePadding: const EdgeInsets.fromLTRB(AppSpacing.lg, AppSpacing.xl, AppSpacing.lg, 0),
      contentPadding: const EdgeInsets.fromLTRB(AppSpacing.lg, AppSpacing.md, AppSpacing.lg, AppSpacing.sm),
      actionsPadding: const EdgeInsets.fromLTRB(AppSpacing.lg, AppSpacing.sm, AppSpacing.lg, AppSpacing.lg),
      title: title == null
          ? null
          : Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const SizedBox(width: 28, height: 2, child: ColoredBox(color: AppColors.brass)),
                const SizedBox(height: AppSpacing.md),
                title!,
              ],
            ),
      // A dialog never gets wider than a comfortable reading width, even on a wide screen
      content: content == null
          ? null
          : ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 480),
              // Fill the width the dialog allows, so a short message or a lone text field is not squeezed narrow
              // (`maxFinite`, not `infinity`: AlertDialog measures its content, and this is the width it accepts)
              child: SizedBox(width: double.maxFinite, child: content),
            ),
      actions: actions,
    );
  }
}
