import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../app/tokens.dart';
import '../../shared/shared.dart';
import '../submissions/submissions.dart' show competitionAwardsProvider;
import 'gallery.dart';

/// Tile for one public artwork (framed picture + museum wall label).
///
/// [hero] makes the picture fly to the artwork screen; use it only where the artwork shows once on the screen
/// (the gallery grid), because hero tags must be unique.
Widget galleryTile(
  GalleryArtwork a, {
  required VoidCallback onTap,
  bool showCompetition = true,
  bool hero = false,
}) =>
    _GalleryTile(artwork: a, onTap: onTap, showCompetition: showCompetition, hero: hero);

/// The gallery list does not say which prize a work won, only its name. The rank (which sets the gold, silver or
/// bronze frame) comes from the competition's published awards, one cached request per competition. Until it
/// arrives (or if it fails) the frame is brass.
class _GalleryTile extends ConsumerWidget {
  const _GalleryTile({required this.artwork, required this.onTap, required this.showCompetition, required this.hero});

  final GalleryArtwork artwork;
  final VoidCallback onTap;
  final bool showCompetition;
  final bool hero;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final a = artwork;
    var rank = a.awardRank;
    if (a.isAwarded && rank == null) {
      final awards = ref.watch(competitionAwardsProvider(a.competitionId)).value;
      rank = awards?.where((r) => r.submissionId == a.submissionId).firstOrNull?.rank;
    }
    return ArtworkTile.public(
      submissionId: a.submissionId,
      title: a.title,
      artist: a.studentName,
      meta: showCompetition ? a.competitionTitle : null,
      awardName: a.isAwarded ? a.awardName : null,
      awardRank: a.isAwarded ? rank : null,
      onTap: onTap,
      hero: hero,
    );
  }
}

/// Builds one row of [columns] tiles. Rows (not a fixed-height grid) keep the labels free from overflow at any text size.
Widget tileRow(
  List<Widget> tiles,
  int rowIndex,
  int columns, {
  EdgeInsetsGeometry? padding,
  double gutter = AppSpacing.gutter,
}) {
  final first = rowIndex * columns;
  final gap = columns > 2 ? AppSpacing.lg : AppSpacing.md;
  return Padding(
    padding: padding ?? EdgeInsets.fromLTRB(gutter, 0, gutter, AppSpacing.xl),
    child: Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        for (var i = 0; i < columns; i++) ...[
          if (i > 0) SizedBox(width: gap),
          Expanded(child: first + i < tiles.length ? tiles[first + i] : const SizedBox.shrink()),
        ],
      ],
    ),
  );
}

int tileRowCount(int tiles, int columns) => (tiles + columns - 1) ~/ columns;

/// Non-lazy grid for short lists (home winners, competition winners): 2 columns on a phone, more on wide screens.
class TwoColumnGrid extends StatelessWidget {
  const TwoColumnGrid({super.key, required this.tiles});

  final List<Widget> tiles;

  @override
  Widget build(BuildContext context) {
    final width = MediaQuery.sizeOf(context).width;
    final columns = AppLayout.columnsFor(width);
    final gutter = AppLayout.gutterFor(width);
    return Column(
      children: [
        for (var i = 0; i < tileRowCount(tiles.length, columns); i++) tileRow(tiles, i, columns, gutter: gutter),
      ],
    );
  }
}
