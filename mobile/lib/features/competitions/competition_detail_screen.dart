import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/async_view.dart';
import '../../core/format.dart';
import '../auth/auth.dart';
import '../submissions/results_tab.dart';
import '../submissions/submissions.dart';
import 'competition_widgets.dart';
import 'competitions.dart';

/// Chi tiết cuộc thi: thông tin, thể lệ, giải thưởng (F-M02-05, F-M05-09).
class CompetitionDetailScreen extends ConsumerWidget {
  const CompetitionDetailScreen({super.key, required this.id});

  final int id;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final detail = ref.watch(competitionDetailProvider(id));
    final user = currentUserOf(ref.watch(authControllerProvider));

    return Scaffold(
      appBar: AppBar(title: const Text('Chi tiết cuộc thi')),
      body: AsyncValueView(
        value: detail,
        onRetry: () => ref.invalidate(competitionDetailProvider(id)),
        data: (d) => _Body(detail: d, user: user),
      ),
    );
  }
}

class _Body extends ConsumerWidget {
  const _Body({required this.detail, required this.user});

  final CompetitionDetail detail;
  final AppUser? user;

  // Nút "Nộp bài": khách → đăng nhập; học viên → sửa bài đã nộp hoặc mở form nộp mới (BR-15)
  void _onSubmit(BuildContext context, int? existingSubmissionId) {
    if (user == null) {
      context.go('/login');
      return;
    }
    if (existingSubmissionId != null) {
      context.push('/student/submissions/$existingSubmissionId/edit');
    } else {
      context.push('/student/submissions/new?competitionId=${detail.summary.id}');
    }
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final s = detail.summary;
    final textTheme = Theme.of(context).textTheme;
    final isStudent = user?.role == Role.student;
    final canSubmit = s.status == CompetitionStatus.ongoing && (user == null || isStudent);
    // Chỉ học viên mới hỏi API xem đã nộp bài chưa
    final mySubmission = isStudent && canSubmit ? ref.watch(mySubmissionIdProvider(s.id)) : null;
    final existingId = mySubmission?.value;
    final deadlineText = deadlineLabel(s.status, s.daysLeft);

    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        Row(
          children: [
            Expanded(child: Text(s.title, style: textTheme.headlineSmall)),
            StatusChip(s.status),
          ],
        ),
        if (s.topic != null) Padding(padding: const EdgeInsets.only(top: 4), child: Text(s.topic!)),
        const SizedBox(height: 12),
        Card(
          child: Padding(
            padding: const EdgeInsets.all(16),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text('Thời gian: ${formatDate(s.startDate)} – ${formatDate(s.endDate)}'),
                Text('Hạn nộp: ${formatDateTime(detail.deadline)}'),
                if (s.status == CompetitionStatus.ongoing) Text('${deadlineText ?? ''} • ${s.submissionCount} bài dự thi'),
                if (s.status != CompetitionStatus.ongoing && deadlineText != null) Text(deadlineText),
                // Cuộc thi sắp diễn ra: hiện đếm ngược đến ngày bắt đầu
                if (s.startCountdownLabel != null) Text(s.startCountdownLabel!),
              ],
            ),
          ),
        ),
        if (detail.description != null) ...[
          const SectionTitle('Mô tả'),
          Text(detail.description!),
        ],
        if (detail.conditions != null) ...[
          const SectionTitle('Thể lệ'),
          Text(detail.conditions!),
        ],
        const SectionTitle('Giải thưởng'),
        for (final a in detail.awards)
          ListTile(
            contentPadding: EdgeInsets.zero,
            leading: CircleAvatar(child: Text('${a.rank}')),
            title: Text('${a.name} • ${formatVnd(a.prizeValue)}'),
            subtitle: Text(['Số lượng: ${a.quantity}', if (a.description != null) a.description!].join(' • ')),
          ),
        // Cuộc thi đã công bố: hiện giải đã trao (F-M06-09)
        if (s.status == CompetitionStatus.graded) ...[
          const SectionTitle('Kết quả đạt giải'),
          AwardResultsList(competitionId: s.id),
        ],
        if (canSubmit) ...[
          const SizedBox(height: 16),
          FilledButton.icon(
            // Học viên: chờ biết đã nộp chưa rồi mới cho bấm
            onPressed: mySubmission?.isLoading == true ? null : () => _onSubmit(context, existingId),
            icon: Icon(existingId != null ? Icons.edit : Icons.upload),
            label: Text(user == null
                ? 'Đăng nhập để nộp bài'
                : existingId != null
                    ? 'Sửa bài của tôi'
                    : 'Nộp bài'),
          ),
        ],
      ],
    );
  }
}
