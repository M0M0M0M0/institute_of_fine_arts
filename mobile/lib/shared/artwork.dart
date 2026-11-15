import 'package:flutter/material.dart';

import '../app/tokens.dart';
import 'api_urls.dart';
import 'auth_image.dart';
import 'public_image.dart';

/// A "passe-partout" frame around an artwork: stone mat with a hairline inside.
/// With [awarded] the outer edge becomes a gilt (brass) frame.
///
/// [child] is the artwork (a `PublicImage` / `AuthImage`); it is shown inside the mat
/// at [aspectRatio] (width / height, default 4:5 portrait). Pass `aspectRatio: null`
/// to let the child decide its own height.
class ArtworkFrame extends StatelessWidget {
  const ArtworkFrame({
    super.key,
    required this.child,
    this.awarded = false,
    this.aspectRatio = 4 / 5,
    this.matWidth = 10,
  });

  final Widget child;
  final bool awarded;
  final double? aspectRatio;

  /// Width of the stone mat on each side.
  final double matWidth;

  @override
  Widget build(BuildContext context) {
    Widget art = child;
    if (aspectRatio != null) art = AspectRatio(aspectRatio: aspectRatio!, child: art);
    return DecoratedBox(
      decoration: BoxDecoration(
        color: AppColors.stone,
        border: Border.all(color: awarded ? AppColors.brass : AppColors.line, width: awarded ? 3 : 1),
        boxShadow: awarded
            ? const [BoxShadow(color: Color(0x33A8814A), blurRadius: 8, offset: Offset(0, 2))]
            : const [BoxShadow(color: Color(0x14000000), blurRadius: 4, offset: Offset(0, 1))],
      ),
      child: Padding(
        padding: EdgeInsets.all(matWidth),
        child: DecoratedBox(
          decoration: BoxDecoration(border: Border.all(color: AppColors.line)),
          child: art,
        ),
      ),
    );
  }
}

/// Museum wall label: italic serif title, artist, and a small meta line (competition / award).
class WallLabel extends StatelessWidget {
  const WallLabel({
    super.key,
    required this.title,
    this.artist,
    this.meta,
    this.awardName,
    this.centered = false,
    this.maxTitleLines = 2,
  });

  final String title;
  final String? artist;

  /// Third line, e.g. the competition title or a year.
  final String? meta;

  /// Shown in brass above the meta line when the work is awarded.
  final String? awardName;
  final bool centered;
  final int maxTitleLines;

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    return Column(
      crossAxisAlignment: centered ? CrossAxisAlignment.center : CrossAxisAlignment.start,
      mainAxisSize: MainAxisSize.min,
      children: [
        Text(
          title,
          maxLines: maxTitleLines,
          overflow: TextOverflow.ellipsis,
          textAlign: centered ? TextAlign.center : TextAlign.start,
          style: text.titleSmall!.copyWith(fontStyle: FontStyle.italic, fontWeight: FontWeight.w500),
        ),
        if (artist != null && artist!.isNotEmpty)
          Text(artist!, maxLines: 1, overflow: TextOverflow.ellipsis, style: text.bodyMedium),
        if (awardName != null && awardName!.isNotEmpty)
          Text(
            awardName!,
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: text.labelMedium!.copyWith(color: AppColors.brassText, fontWeight: FontWeight.w600),
          ),
        if (meta != null && meta!.isNotEmpty)
          Text(meta!, maxLines: 1, overflow: TextOverflow.ellipsis, style: text.bodySmall),
      ],
    );
  }
}

/// Frame + wall label, tappable. For grids and horizontal strips.
///
/// Use the [ArtworkTile.public] / [ArtworkTile.private] constructors with a submission id,
/// or the default constructor with any [image] widget.
class ArtworkTile extends StatelessWidget {
  const ArtworkTile({
    super.key,
    required this.image,
    required this.title,
    this.artist,
    this.meta,
    this.awardName,
    this.awarded = false,
    this.onTap,
    this.aspectRatio = 4 / 5,
  });

  /// Public artwork (published results, gallery): thumbnail from `/files/submissions/{id}/thumb`.
  /// [awarded] defaults to `awardName != null`.
  ArtworkTile.public({
    super.key,
    required int submissionId,
    required this.title,
    this.artist,
    this.meta,
    this.awardName,
    bool? awarded,
    this.onTap,
    this.aspectRatio = 4 / 5,
  })  : image = PublicImage(url: ApiUrls.submissionThumb(submissionId), semanticLabel: title),
        awarded = awarded ?? awardName != null;

  /// Private artwork (own entries, staff views): thumbnail through the API with the JWT.
  ArtworkTile.private({
    super.key,
    required int submissionId,
    required this.title,
    this.artist,
    this.meta,
    this.awardName,
    this.awarded = false,
    this.onTap,
    this.aspectRatio = 4 / 5,
  }) : image = AuthImage(submissionId: submissionId, semanticLabel: title);

  final Widget image;
  final String title;
  final String? artist;
  final String? meta;
  final String? awardName;
  final bool awarded;
  final VoidCallback? onTap;
  final double? aspectRatio;

  @override
  Widget build(BuildContext context) {
    final content = Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      mainAxisSize: MainAxisSize.min,
      children: [
        ArtworkFrame(awarded: awarded, aspectRatio: aspectRatio, child: image),
        const SizedBox(height: AppSpacing.sm),
        WallLabel(title: title, artist: artist, meta: meta, awardName: awardName),
      ],
    );
    if (onTap == null) return content;
    return InkWell(onTap: onTap, borderRadius: AppRadius.smAll, child: content);
  }
}
