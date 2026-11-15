import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../app/tokens.dart';
import '../../core/api_client.dart';
import '../../core/async_view.dart';
import '../../core/file_download.dart';
import '../../core/format.dart';
import '../../shared/shared.dart';
import '../competitions/competitions.dart';
import '../student/dashboard.dart';
import 'submissions.dart';

/// M-S5 Entry detail: framed picture (tap to zoom), text, evaluation card once published,
/// awards, and Edit / Delete while the competition is open.
class SubmissionDetailScreen extends ConsumerWidget {
  const SubmissionDetailScreen({super.key, required this.id});

  final int id;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final detail = ref.watch(submissionDetailProvider(id));
    return Scaffold(
      appBar: AppBar(title: const Text('Entry')),
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

  // Delete after confirmation (UC-04)
  Future<void> _delete() async {
    final d = widget.detail;
    final ok = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Delete this entry?'),
        content: Text('"${d.title}" and its picture will be deleted permanently.'),
        actions: [
          TextButton(onPressed: () => Navigator.pop(context, false), child: const Text('Cancel')),
          FilledButton(
            style: FilledButton.styleFrom(backgroundColor: AppColors.danger),
            onPressed: () => Navigator.pop(context, true),
            child: const Text('Delete'),
          ),
        ],
      ),
    );
    if (ok != true || !mounted) return;

    setState(() => _busy = true);
    try {
      await ref.read(submissionRepositoryProvider).delete(d.id);
      if (!mounted) return;
      refreshSubmissionData(ref, submissionId: d.id, competitionId: d.competitionId);
      ref.invalidate(studentDashboardProvider);
      _toast('Entry deleted.');
      context.go('/student/entries');
    } catch (e) {
      if (!mounted) return;
      _toast(ApiException.from(e).message);
      setState(() => _busy = false);
    }
  }

  // Save the original file to the device (F-M06-06)
  Future<void> _download() async {
    final d = widget.detail;
    setState(() => _busy = true);
    try {
      final bytes = await ref.read(submissionRepositoryProvider).imageBytes(d.id, download: true);
      final saved = await saveBytesAsFile(bytes, d.originalFileName);
      if (mounted) _toast(saved ? 'Picture saved to your device.' : 'Saving is not supported on this platform (use Chrome).');
    } catch (e) {
      if (mounted) _toast(ApiException.from(e).message);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final d = widget.detail;
    final text = Theme.of(context).textTheme;
    final ev = d.evaluation;
    final awarded = d.awards.isNotEmpty;

    return ListView(
      padding: const EdgeInsets.fromLTRB(AppSpacing.gutter, AppSpacing.md, AppSpacing.gutter, AppSpacing.xl),
      children: [
        // The picture, framed; tap to zoom
        Semantics(
          button: true,
          label: 'Zoom the picture "${d.title}"',
          child: GestureDetector(
            onTap: () => ArtworkViewer.open(
              context,
              ArtworkViewer.private(submissionId: d.id, title: d.title, subtitle: d.competitionTitle),
            ),
            child: ArtworkFrame(
              awarded: awarded,
              aspectRatio: null,
              // The frame hugs the painting at its own proportions
              child: Stack(
                children: [
                  NaturalArtworkImage.private(submissionId: d.id, semanticLabel: d.title),
                  const Positioned(
                    right: AppSpacing.sm,
                    bottom: AppSpacing.sm,
                    child: Icon(Icons.zoom_out_map, size: 20, color: AppColors.muted),
                  ),
                ],
              ),
            ),
          ),
        ),
        const SizedBox(height: AppSpacing.md),
        Text(d.title, style: text.headlineSmall!.copyWith(fontStyle: FontStyle.italic)),
        const SizedBox(height: 2),
        Text(d.competitionTitle, style: text.bodyLarge),
        const SizedBox(height: AppSpacing.sm),
        Wrap(
          spacing: AppSpacing.sm,
          runSpacing: AppSpacing.xs,
          children: [
            if (ev != null)
              StatusChip.rating(ev.rating)
            else
              StatusChip.custom(
                label: d.competitionStatus == CompetitionStatus.ongoing ? 'Submitted' : 'Waiting for results',
                color: d.competitionStatus == CompetitionStatus.ongoing
                    ? AppColors.statusOngoing
                    : AppColors.statusClosed,
              ),
            for (final a in d.awards)
              StatusChip.custom(label: a.awardName, color: AppColors.brassText, icon: Icons.emoji_events_outlined),
          ],
        ),
        const SizedBox(height: AppSpacing.xs),
        Text(
          'Submitted ${formatDateTime(d.submittedAt)}'
          '${d.updatedAt != null ? ' · Edited ${formatDateTime(d.updatedAt!)}' : ''}',
          style: text.bodySmall,
        ),
        const SectionHeader('Description', padding: EdgeInsets.only(top: AppSpacing.lg, bottom: AppSpacing.xs)),
        Text(d.description, style: text.bodyLarge),
        SectionHeader(d.textType.label, padding: const EdgeInsets.only(top: AppSpacing.lg, bottom: AppSpacing.xs)),
        Container(
          width: double.infinity,
          padding: const EdgeInsets.all(AppSpacing.md),
          decoration: BoxDecoration(
            color: AppColors.mist,
            borderRadius: AppRadius.mdAll,
          ),
          child: Text(d.textContent, style: text.bodyLarge!.copyWith(fontStyle: FontStyle.italic)),
        ),
        // Evaluation card (only once there is an evaluation)
        if (ev != null) ...[
          const SectionHeader('Evaluation', padding: EdgeInsets.only(top: AppSpacing.lg, bottom: AppSpacing.sm)),
          Card(
            child: Padding(
              padding: const EdgeInsets.all(AppSpacing.md),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(children: [
                    Text('Rating', style: text.labelLarge),
                    const SizedBox(width: AppSpacing.sm),
                    StatusChip.rating(ev.rating),
                  ]),
                  _Feedback(label: 'Strengths', text: ev.positivePoints),
                  _Feedback(label: 'Weaknesses', text: ev.negativePoints),
                  _Feedback(label: 'To improve', text: ev.improvementAreas),
                  const SizedBox(height: AppSpacing.sm),
                  Text('Evaluated ${formatDateTime(ev.evaluatedAt)}', style: text.bodySmall),
                ],
              ),
            ),
          ),
        ],
        if (awarded) ...[
          const SectionHeader('Award', padding: EdgeInsets.only(top: AppSpacing.lg, bottom: AppSpacing.xs)),
          for (final a in d.awards)
            ListTile(
              contentPadding: EdgeInsets.zero,
              leading: const Icon(Icons.emoji_events_outlined, color: AppColors.brass),
              title: Text(a.awardName),
              subtitle: Text('Rank ${a.rank}'),
            ),
        ],
        const SizedBox(height: AppSpacing.lg),
        Wrap(
          spacing: AppSpacing.sm,
          runSpacing: AppSpacing.sm,
          children: [
            if (d.canEdit) ...[
              FilledButton.icon(
                onPressed: _busy ? null : () => context.push('/student/submissions/${d.id}/edit'),
                icon: const Icon(Icons.edit_outlined),
                label: const Text('Edit'),
              ),
              OutlinedButton.icon(
                style: OutlinedButton.styleFrom(
                  foregroundColor: AppColors.danger,
                  side: const BorderSide(color: AppColors.danger),
                ),
                onPressed: _busy ? null : _delete,
                icon: const Icon(Icons.delete_outline),
                label: const Text('Delete'),
              ),
            ],
            TextButton.icon(
              onPressed: _busy ? null : _download,
              icon: const Icon(Icons.download_outlined),
              label: const Text('Download'),
            ),
          ],
        ),
      ],
    );
  }
}

/// One feedback item; skipped when the teacher left it empty.
class _Feedback extends StatelessWidget {
  const _Feedback({required this.label, required this.text});

  final String label;
  final String? text;

  @override
  Widget build(BuildContext context) {
    if (text == null || text!.isEmpty) return const SizedBox.shrink();
    return Padding(
      padding: const EdgeInsets.only(top: AppSpacing.md),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(label, style: Theme.of(context).textTheme.titleSmall),
          const SizedBox(height: 2),
          Text(text!),
        ],
      ),
    );
  }
}
