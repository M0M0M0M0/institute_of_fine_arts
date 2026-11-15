import 'package:flutter/material.dart';

import '../../app/tokens.dart';
import '../../shared/shared.dart';
import 'gallery.dart';

/// Tile for one public artwork (framed picture + museum wall label).
ArtworkTile galleryTile(GalleryArtwork a, {required VoidCallback onTap, bool showCompetition = true}) =>
    ArtworkTile.public(
      submissionId: a.submissionId,
      title: a.title,
      artist: a.studentName,
      meta: showCompetition ? a.competitionTitle : null,
      awardName: a.isAwarded ? a.awardName : null,
      onTap: onTap,
    );

/// Builds a row of two tiles. Rows (not a fixed-height grid) keep the labels free from overflow at any text size.
Widget twoColumnRow(List<Widget> tiles, int rowIndex, {EdgeInsetsGeometry? padding}) {
  final first = rowIndex * 2;
  final second = first + 1;
  return Padding(
    padding: padding ?? const EdgeInsets.fromLTRB(AppSpacing.gutter, 0, AppSpacing.gutter, AppSpacing.lg),
    child: Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Expanded(child: tiles[first]),
        const SizedBox(width: AppSpacing.md),
        Expanded(child: second < tiles.length ? tiles[second] : const SizedBox.shrink()),
      ],
    ),
  );
}

int twoColumnRowCount(int tiles) => (tiles + 1) ~/ 2;

/// Non-lazy two column grid for short lists (home winners, competition winners).
class TwoColumnGrid extends StatelessWidget {
  const TwoColumnGrid({super.key, required this.tiles});

  final List<Widget> tiles;

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [for (var i = 0; i < twoColumnRowCount(tiles.length); i++) twoColumnRow(tiles, i)],
    );
  }
}
