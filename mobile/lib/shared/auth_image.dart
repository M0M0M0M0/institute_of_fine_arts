import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../app/motion.dart';
import '../core/api_client.dart';
import 'public_image.dart';

/// Key of [authImageProvider]: (submission id, thumbnail?).
typedef AuthImageKey = (int submissionId, bool thumb);

/// Bytes of a private submission image: `GET /api/submissions/{id}/image?thumb=true|false`
/// with the JWT, cached in memory by Riverpod (one entry per id + size).
/// Invalidate with `ref.invalidate(authImageProvider((id, true)))` after an image changes.
final authImageProvider = FutureProvider.family<Uint8List, AuthImageKey>((ref, key) {
  final dio = ref.watch(dioProvider);
  final (id, thumb) = key;
  return guardApi(() async {
    final res = await dio.get<List<int>>(
      '/api/submissions/$id/image',
      queryParameters: {'thumb': thumb},
      options: Options(responseType: ResponseType.bytes, receiveTimeout: const Duration(minutes: 2)),
    );
    return Uint8List.fromList(res.data!);
  });
});

/// Drops every cached private image (call when the signed-in account changes).
void resetAuthImages(Ref ref) => ref.invalidate(authImageProvider);

/// Image of a submission that needs a token (a student's own work, anything for staff).
/// Shows the thumbnail by default; pass `thumb: false` for the full-size image.
/// Like [PublicImage] it fills the box it is given.
class AuthImage extends ConsumerWidget {
  const AuthImage({
    super.key,
    required this.submissionId,
    this.thumb = true,
    this.fit = BoxFit.cover,
    this.aspectRatio,
    this.semanticLabel,
    this.borderRadius,
  });

  final int submissionId;
  final bool thumb;
  final BoxFit fit;
  final double? aspectRatio;
  final String? semanticLabel;
  final BorderRadius? borderRadius;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    // A loading skeleton fetches nothing: the picture's box stays grey
    final AsyncValue<Uint8List> bytes =
        SkeletonScope.of(context) ? const AsyncLoading() : ref.watch(authImageProvider((submissionId, thumb)));
    Widget child = switch (bytes) {
      AsyncData(:final value) => Image.memory(
          value,
          fit: fit,
          semanticLabel: semanticLabel,
          excludeFromSemantics: semanticLabel == null,
          frameBuilder: fadeInFrame,
          gaplessPlayback: true,
          errorBuilder: (context, error, stack) =>
              const ImagePlaceholder(icon: Icons.broken_image_outlined, label: 'Image unavailable'),
        ),
      AsyncError() => InkWell(
          onTap: () => ref.invalidate(authImageProvider((submissionId, thumb))),
          child: const ImagePlaceholder(icon: Icons.refresh, label: 'Tap to retry'),
        ),
      _ => const ImagePlaceholder(),
    };
    if (borderRadius != null) child = ClipRRect(borderRadius: borderRadius!, child: child);
    if (aspectRatio != null) child = AspectRatio(aspectRatio: aspectRatio!, child: child);
    return child;
  }
}
