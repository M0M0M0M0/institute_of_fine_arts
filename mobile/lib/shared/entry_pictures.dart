import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../app/tokens.dart';
import '../core/api_client.dart';
import 'artwork.dart';
import 'artwork_viewer.dart';
import 'natural_image.dart';
import 'public_image.dart';

/// Key of [pictureImageProvider]: (submission_image id, thumbnail?).
typedef PictureKey = (int imageId, bool thumb);

/// Bytes of one picture of an entry (BR-18, 11/2026): `GET /api/submission-images/{id}?thumb=true|false` with the JWT.
/// Same viewing rules as the entry's own image.
final pictureImageProvider = FutureProvider.family<Uint8List, PictureKey>((ref, key) {
  final dio = ref.watch(dioProvider);
  final (id, thumb) = key;
  return guardApi(() async {
    final res = await dio.get<List<int>>(
      '/api/submission-images/$id',
      queryParameters: {'thumb': thumb},
      options: Options(responseType: ResponseType.bytes, receiveTimeout: const Duration(minutes: 2)),
    );
    return Uint8List.fromList(res.data!);
  });
});

/// One picture of an entry that needs a token; fills the box it is given.
class PictureImage extends ConsumerWidget {
  const PictureImage({super.key, required this.imageId, this.thumb = true, this.fit = BoxFit.cover, this.semanticLabel});

  final int imageId;
  final bool thumb;
  final BoxFit fit;
  final String? semanticLabel;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final bytes = ref.watch(pictureImageProvider((imageId, thumb)));
    return switch (bytes) {
      AsyncData(:final value) => Image.memory(
          value,
          fit: fit,
          semanticLabel: semanticLabel,
          excludeFromSemantics: semanticLabel == null,
          gaplessPlayback: true,
          errorBuilder: (context, error, stack) =>
              const ImagePlaceholder(icon: Icons.broken_image_outlined, label: 'Image unavailable'),
        ),
      AsyncError() => InkWell(
          onTap: () => ref.invalidate(pictureImageProvider((imageId, thumb))),
          child: const ImagePlaceholder(icon: Icons.refresh, label: 'Tap to retry'),
        ),
      _ => const ImagePlaceholder(),
    };
  }
}

/// The pictures of a private entry (1 to 5 of the same artwork): the selected one large in its frame (tap to zoom),
/// the others as thumbnails below. Without picture ids (older entries) the entry's own image is shown.
class EntryPictures extends StatefulWidget {
  const EntryPictures({
    super.key,
    required this.submissionId,
    required this.imageIds,
    required this.title,
    this.subtitle,
    this.awarded = false,
    this.onSelected,
  });

  final int submissionId;
  final List<int> imageIds;
  final String title;
  final String? subtitle;
  final bool awarded;

  /// Called with the picture id shown large (e.g. so "Download original" follows it).
  final ValueChanged<int>? onSelected;

  @override
  State<EntryPictures> createState() => _EntryPicturesState();
}

class _EntryPicturesState extends State<EntryPictures> {
  int _index = 0;

  @override
  Widget build(BuildContext context) {
    final ids = widget.imageIds;
    final current = ids.isEmpty ? null : ids[_index.clamp(0, ids.length - 1)];

    final large = current == null
        ? NaturalArtworkImage.private(submissionId: widget.submissionId, semanticLabel: widget.title)
        : SizedBox(
            height: 360,
            width: double.infinity,
            child: PictureImage(imageId: current, thumb: false, fit: BoxFit.contain, semanticLabel: widget.title),
          );
    final viewer = current == null
        ? ArtworkViewer.private(submissionId: widget.submissionId, title: widget.title, subtitle: widget.subtitle)
        : ArtworkViewer(
            image: PictureImage(imageId: current, thumb: false, fit: BoxFit.contain, semanticLabel: widget.title),
            title: widget.title,
            subtitle: widget.subtitle,
          );

    return Column(
      children: [
        Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 440),
            child: Semantics(
              button: true,
              label: 'Open ${widget.title} full screen',
              child: InkWell(
                onTap: () => ArtworkViewer.open(context, viewer),
                child: ArtworkFrame(awarded: widget.awarded, aspectRatio: null, child: large),
              ),
            ),
          ),
        ),
        if (ids.length > 1) ...[
          const SizedBox(height: AppSpacing.sm),
          Wrap(
            spacing: AppSpacing.sm,
            runSpacing: AppSpacing.sm,
            alignment: WrapAlignment.center,
            children: [
              for (var i = 0; i < ids.length; i++)
                Semantics(
                  button: true,
                  selected: i == _index,
                  label: 'Show picture ${i + 1} of ${ids.length}',
                  child: InkWell(
                    onTap: () {
                      setState(() => _index = i);
                      widget.onSelected?.call(ids[i]);
                    },
                    child: Container(
                      width: 60,
                      height: 60,
                      decoration: BoxDecoration(
                        border: Border.all(color: i == _index ? AppColors.ink : Colors.transparent, width: 2),
                      ),
                      child: PictureImage(imageId: ids[i]),
                    ),
                  ),
                ),
            ],
          ),
        ],
      ],
    );
  }
}
