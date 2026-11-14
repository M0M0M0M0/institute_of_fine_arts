import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/api_client.dart';
import 'submissions.dart';

/// Nhãn xếp loại; màu theo mức (BEST nổi nhất, DISQUALIFIED màu lỗi).
class RatingChip extends StatelessWidget {
  const RatingChip(this.rating, {super.key});

  final SubmissionRating rating;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final color = switch (rating) {
      SubmissionRating.best => scheme.primary,
      SubmissionRating.better || SubmissionRating.good => scheme.tertiary,
      SubmissionRating.moderate || SubmissionRating.normal => scheme.outline,
      SubmissionRating.disqualified => scheme.error,
    };
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
      decoration: BoxDecoration(color: color.withValues(alpha: 0.12), borderRadius: BorderRadius.circular(12)),
      child: Text(rating.label, style: TextStyle(color: color, fontSize: 12, fontWeight: FontWeight.w600)),
    );
  }
}

/// Ảnh bài dự thi: tải byte bằng Dio (có JWT) rồi hiện bằng Image.memory.
class SubmissionImage extends ConsumerWidget {
  const SubmissionImage({super.key, required this.submissionId, this.height});

  final int submissionId;
  final double? height;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final image = ref.watch(submissionImageProvider(submissionId));
    return SizedBox(
      height: height,
      child: switch (image) {
        // Có byte → hiện ảnh, giữ nguyên tỉ lệ
        AsyncData(:final value) => Image.memory(value, fit: BoxFit.contain, width: double.infinity),
        // Lỗi (ví dụ 404 mất file) → báo và cho thử lại
        AsyncError(:final error) => Center(
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                const Icon(Icons.broken_image_outlined, size: 40),
                const SizedBox(height: 8),
                Text(ApiException.from(error).message, textAlign: TextAlign.center),
                TextButton(
                  onPressed: () => ref.invalidate(submissionImageProvider(submissionId)),
                  child: const Text('Thử lại'),
                ),
              ],
            ),
          ),
        _ => const Center(child: CircularProgressIndicator()),
      },
    );
  }
}
