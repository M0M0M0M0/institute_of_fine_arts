import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/api_client.dart';
import '../../core/async_view.dart';
import '../../core/file_download.dart';
import '../../core/format.dart';
import '../competitions/competition_widgets.dart';
import 'submission_widgets.dart';
import 'submissions.dart';

/// Chi tiết bài của tôi: ảnh, nội dung, kết quả chấm, giải; sửa/xóa/tải (F-M06-02..07).
class SubmissionDetailScreen extends ConsumerWidget {
  const SubmissionDetailScreen({super.key, required this.id});

  final int id;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final detail = ref.watch(submissionDetailProvider(id));
    return Scaffold(
      appBar: AppBar(title: const Text('Chi tiết bài dự thi')),
      body: AsyncValueView(
        value: detail,
        onRetry: () => ref.invalidate(submissionDetailProvider(id)),
        data: (d) => _DetailBody(detail: d),
      ),
    );
  }
}

class _DetailBody extends ConsumerStatefulWidget {
  const _DetailBody({required this.detail});

  final SubmissionDetail detail;

  @override
  ConsumerState<_DetailBody> createState() => _DetailBodyState();
}

class _DetailBodyState extends ConsumerState<_DetailBody> {
  bool _busy = false;

  void _toast(String text) => ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(text)));

  // Xóa bài sau khi xác nhận (UC-04)
  Future<void> _delete() async {
    final d = widget.detail;
    final ok = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Xóa bài dự thi?'),
        content: Text('Bài "${d.title}" và ảnh sẽ bị xóa vĩnh viễn.'),
        actions: [
          TextButton(onPressed: () => Navigator.pop(context, false), child: const Text('Hủy')),
          FilledButton(onPressed: () => Navigator.pop(context, true), child: const Text('Xóa')),
        ],
      ),
    );
    if (ok != true || !mounted) return;

    setState(() => _busy = true);
    try {
      await ref.read(submissionRepositoryProvider).delete(d.id);
      if (!mounted) return;
      refreshSubmissionData(ref, submissionId: d.id, competitionId: d.competitionId);
      _toast('Đã xóa bài dự thi.');
      // Về danh sách "Bài của tôi"
      context.go('/student?tab=1');
    } catch (e) {
      if (!mounted) return;
      _toast(ApiException.from(e).message);
      setState(() => _busy = false);
    }
  }

  // Tải file gốc về máy (F-M06-06)
  Future<void> _download() async {
    final d = widget.detail;
    setState(() => _busy = true);
    try {
      final bytes = await ref.read(submissionRepositoryProvider).imageBytes(d.id, download: true);
      final saved = await saveBytesAsFile(bytes, d.originalFileName);
      if (mounted) _toast(saved ? 'Đã tải ảnh về máy.' : 'Nền tảng này chưa hỗ trợ tải về (hãy chạy trên Chrome).');
    } catch (e) {
      if (mounted) _toast(ApiException.from(e).message);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final d = widget.detail;
    final textTheme = Theme.of(context).textTheme;
    final ev = d.evaluation;

    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        // Ảnh tải bằng Dio (có JWT)
        SubmissionImage(submissionId: d.id, height: 320),
        const SizedBox(height: 12),
        Row(
          children: [
            Expanded(child: Text(d.title, style: textTheme.headlineSmall)),
            if (ev != null) RatingChip(ev.rating),
          ],
        ),
        const SizedBox(height: 4),
        Text(d.competitionTitle, style: textTheme.bodyMedium),
        const SizedBox(height: 4),
        Text('Nộp: ${formatDateTime(d.submittedAt)}'
            '${d.updatedAt != null ? ' • Sửa: ${formatDateTime(d.updatedAt!)}' : ''}'),
        // Hết quyền sửa/xóa
        if (!d.canEdit)
          Text('Đã quá hạn nộp bài – không thể sửa hoặc xóa.',
              style: TextStyle(color: Theme.of(context).colorScheme.error)),
        const SectionTitle('Mô tả'),
        Text(d.description),
        SectionTitle(d.textType.label),
        Text(d.textContent),
        const SizedBox(height: 8),
        Text(d.publicConsent ? 'Đồng ý hiển thị công khai' : 'Không đồng ý hiển thị công khai',
            style: textTheme.bodySmall),
        // Kết quả chấm (chỉ khi đã có evaluation)
        if (ev != null) ...[
          const SectionTitle('Nhận xét của giáo viên'),
          _Feedback(label: 'Điểm mạnh', text: ev.positivePoints),
          _Feedback(label: 'Điểm yếu', text: ev.negativePoints),
          _Feedback(label: 'Cần cải thiện', text: ev.improvementAreas),
          Text('Chấm lúc ${formatDateTime(ev.evaluatedAt)}', style: textTheme.bodySmall),
        ],
        // Giải thưởng (chỉ khi có)
        if (d.awards.isNotEmpty) ...[
          const SectionTitle('Giải thưởng'),
          for (final a in d.awards)
            ListTile(
              contentPadding: EdgeInsets.zero,
              leading: const Icon(Icons.emoji_events),
              title: Text(a.awardName),
              subtitle: Text('Hạng ${a.rank}'),
            ),
        ],
        const SizedBox(height: 16),
        // Nút thao tác
        Wrap(
          spacing: 12,
          runSpacing: 8,
          children: [
            OutlinedButton.icon(
              onPressed: _busy ? null : _download,
              icon: const Icon(Icons.download),
              label: const Text('Tải về'),
            ),
            if (d.canEdit) ...[
              FilledButton.icon(
                onPressed: _busy ? null : () => context.push('/student/submissions/${d.id}/edit'),
                icon: const Icon(Icons.edit),
                label: const Text('Sửa'),
              ),
              FilledButton.tonalIcon(
                onPressed: _busy ? null : _delete,
                icon: const Icon(Icons.delete),
                label: const Text('Xóa'),
              ),
            ],
          ],
        ),
      ],
    );
  }
}

/// Một mục nhận xét; bỏ qua nếu giáo viên không nhập.
class _Feedback extends StatelessWidget {
  const _Feedback({required this.label, required this.text});

  final String label;
  final String? text;

  @override
  Widget build(BuildContext context) {
    if (text == null || text!.isEmpty) return const SizedBox.shrink();
    return Padding(
      padding: const EdgeInsets.only(bottom: 8),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(label, style: Theme.of(context).textTheme.titleSmall),
          Text(text!),
        ],
      ),
    );
  }
}
