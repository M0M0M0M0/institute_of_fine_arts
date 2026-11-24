import 'package:flutter/material.dart';

import '../app/motion.dart';
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
    this.awardRank,
    this.aspectRatio = 4 / 5,
    this.matWidth = 12,
  });

  final Widget child;
  final bool awarded;

  /// Prize rank (1, 2, 3 …): colours the awarded frame gold, silver or bronze. Without it an awarded frame is brass.
  final int? awardRank;
  final double? aspectRatio;

  /// Width of the stone mat on each side.
  final double matWidth;

  @override
  Widget build(BuildContext context) {
    Widget art = child;
    if (aspectRatio != null) art = AspectRatio(aspectRatio: aspectRatio!, child: art);
    final medal = AppColors.medalFor(awardRank);
    return DecoratedBox(
      decoration: BoxDecoration(
        color: AppColors.stone,
        border: Border.all(color: awarded ? medal : AppColors.line, width: awarded ? 3 : 1),
        // A soft shadow lifts the frame off the wall
        boxShadow: awarded
            ? [BoxShadow(color: medal.withValues(alpha: 0.25), blurRadius: 18, offset: const Offset(0, 6))]
            : const [BoxShadow(color: Color(0x1A000000), blurRadius: 14, offset: Offset(0, 5))],
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
    this.awardRank,
    this.centered = false,
    this.maxTitleLines = 2,
  });

  final String title;
  final String? artist;

  /// Third line, e.g. the competition title or a year.
  final String? meta;

  /// Shown above the meta line when the work is awarded, in the medal colour of [awardRank] (brass by default).
  final String? awardName;
  final int? awardRank;
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
          style: text.titleSmall!.copyWith(fontStyle: FontStyle.italic, fontWeight: FontWeight.w400),
        ),
        if (artist != null && artist!.isNotEmpty) ...[
          const SizedBox(height: 2),
          Text(
            artist!.toUpperCase(),
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            textAlign: centered ? TextAlign.center : TextAlign.start,
            style: text.labelSmall!.copyWith(color: AppColors.muted, letterSpacing: 1.2),
          ),
        ],
        if (awardName != null && awardName!.isNotEmpty)
          Text(
            awardName!,
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: text.labelMedium!.copyWith(color: AppColors.medalText(awardRank), fontWeight: FontWeight.w600),
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
class ArtworkTile extends StatefulWidget {
  const ArtworkTile({
    super.key,
    required this.image,
    required this.title,
    this.artist,
    this.meta,
    this.awardName,
    this.awardRank,
    this.awarded = false,
    this.onTap,
    this.aspectRatio = 4 / 5,
    this.heroSubmissionId,
  });

  /// Public artwork (published results, gallery): thumbnail from `/files/submissions/{id}/thumb`.
  /// [awarded] defaults to `awardName != null`.
  ///
  /// With [hero] the picture flies to the artwork screen. Only turn it on where the submission appears once on the
  /// screen (the gallery grid): two heroes with the same tag on one screen throw an error.
  ArtworkTile.public({
    super.key,
    required int submissionId,
    required this.title,
    this.artist,
    this.meta,
    this.awardName,
    this.awardRank,
    bool? awarded,
    this.onTap,
    // Square mat: landscape and portrait paintings both sit in it whole, without wide empty bands
    this.aspectRatio = 1,
    bool hero = false,
  })  // `contain`: the whole painting sits inside the mat at its own proportions (thumbnails keep them), never cropped
      : image = PublicImage(url: ApiUrls.submissionThumb(submissionId), semanticLabel: title, fit: BoxFit.contain),
        awarded = awarded ?? awardName != null,
        heroSubmissionId = hero ? submissionId : null;

  /// Private artwork (own entries, staff views): thumbnail through the API with the JWT.
  ArtworkTile.private({
    super.key,
    required int submissionId,
    required this.title,
    this.artist,
    this.meta,
    this.awardName,
    this.awardRank,
    this.awarded = false,
    this.onTap,
    this.aspectRatio = 4 / 5,
  })  : image = AuthImage(submissionId: submissionId, semanticLabel: title),
        heroSubmissionId = null;

  final Widget image;
  final String title;
  final String? artist;
  final String? meta;
  final String? awardName;

  /// Prize rank (1 gold, 2 silver, 3 bronze) for the frame and the award line.
  final int? awardRank;
  final bool awarded;
  final VoidCallback? onTap;
  final double? aspectRatio;

  /// Submission id used for the hero tag, or null for no hero.
  final int? heroSubmissionId;

  @override
  State<ArtworkTile> createState() => _ArtworkTileState();
}

class _ArtworkTileState extends State<ArtworkTile> {
  bool _hover = false;

  @override
  Widget build(BuildContext context) {
    // The hero wraps the picture only, so the stone mat stays put while the picture flies
    final heroId = widget.heroSubmissionId;
    final picture = heroId == null ? widget.image : ArtworkHero(submissionId: heroId, child: widget.image);
    final content = Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      mainAxisSize: MainAxisSize.min,
      children: [
        // With a mouse the frame rises a little off the wall
        AnimatedSlide(
          offset: _hover ? const Offset(0, -0.012) : Offset.zero,
          duration: motionDuration(context, AppMotion.fast),
          curve: AppMotion.curve,
          child: ArtworkFrame(
            awarded: widget.awarded,
            awardRank: widget.awardRank,
            aspectRatio: widget.aspectRatio,
            child: picture,
          ),
        ),
        const SizedBox(height: AppSpacing.md),
        WallLabel(
          title: widget.title,
          artist: widget.artist,
          meta: widget.meta,
          awardName: widget.awardName,
          awardRank: widget.awardRank,
        ),
      ],
    );
    if (widget.onTap == null) return content;
    return InkWell(
      onTap: widget.onTap,
      onHover: (hover) => setState(() => _hover = hover),
      hoverColor: Colors.transparent,
      borderRadius: AppRadius.smAll,
      child: content,
    );
  }
}
