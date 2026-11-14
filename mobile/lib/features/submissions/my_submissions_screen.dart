import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/async_view.dart';
import '../../core/format.dart';
import 'submission_widgets.dart';
import 'submissions.dart';

/// Tab "Bài của tôi": bộ lọc + danh sách (F-M06-05).
class MySubmissionsTab extends ConsumerStatefulWidget {
  const MySubmissionsTab({super.key});

  @override
  ConsumerState<MySubmissionsTab> createState() => _MySubmissionsTabState();
}

class _MySubmissionsTabState extends ConsumerState<MySubmissionsTab> {
  // Ô tìm kiếm giữ chữ riêng; bộ lọc chỉ cập nhật khi bấm Enter
  late final TextEditingController _search;

  @override
  void initState() {
    super.initState();
    _search = TextEditingController(text: ref.read(submissionFilterProvider).q);
  }

  @override
  void dispose() {
    _search.dispose();
    super.dispose();
  }

  // Chọn khoảng ngày nộp
  Future<void> _pickRange(SubmissionFilter filter) async {
    final now = DateTime.now();
    final picked = await showDateRangePicker(
      context: context,
      firstDate: DateTime(now.year - 5),
      lastDate: DateTime(now.year + 1),
      initialDateRange:
          filter.from != null && filter.to != null ? DateTimeRange(start: filter.from!, end: filter.to!) : null,
    );
    if (picked == null) return;
    ref.read(submissionFilterProvider.notifier).set(filter.copyWith(from: () => picked.start, to: () => picked.end));
  }

  @override
  Widget build(BuildContext context) {
    final filter = ref.watch(submissionFilterProvider);
    final notifier = ref.read(submissionFilterProvider.notifier);
    final competitions = ref.watch(competitionListProvider).value ?? const [];
    final list = ref.watch(mySubmissionsProvider);

    return Column(
      children: [
        // ---- Khu vực bộ lọc ----
        Padding(
          padding: const EdgeInsets.fromLTRB(16, 12, 16, 0),
          child: Column(
            children: [
              TextField(
                controller: _search,
                textInputAction: TextInputAction.search,
                decoration: InputDecoration(
                  labelText: 'Tìm theo tiêu đề (Enter để tìm)',
                  prefixIcon: const Icon(Icons.search),
                  suffixIcon: IconButton(
                    tooltip: 'Xóa chữ tìm kiếm',
                    icon: const Icon(Icons.clear),
                    onPressed: () {
                      _search.clear();
                      notifier.set(filter.copyWith(q: ''));
                    },
                  ),
                  border: const OutlineInputBorder(),
                  isDense: true,
                ),
                onSubmitted: (v) => notifier.set(filter.copyWith(q: v)),
              ),
              const SizedBox(height: 8),
              Row(
                children: [
                  // Lọc theo cuộc thi
                  Expanded(
                    child: DropdownButtonFormField<int?>(
                      isExpanded: true,
                      initialValue: filter.competitionId,
                      decoration:
                          const InputDecoration(labelText: 'Cuộc thi', border: OutlineInputBorder(), isDense: true),
                      items: [
                        const DropdownMenuItem<int?>(value: null, child: Text('Tất cả')),
                        for (final c in competitions)
                          DropdownMenuItem<int?>(value: c.id, child: Text(c.title, overflow: TextOverflow.ellipsis)),
                      ],
                      onChanged: (v) => notifier.set(filter.copyWith(competitionId: () => v)),
                    ),
                  ),
                  const SizedBox(width: 8),
                  // Lọc theo xếp loại
                  Expanded(
                    child: DropdownButtonFormField<SubmissionRating?>(
                      isExpanded: true,
                      initialValue: filter.rating,
                      decoration:
                          const InputDecoration(labelText: 'Xếp loại', border: OutlineInputBorder(), isDense: true),
                      items: [
                        const DropdownMenuItem<SubmissionRating?>(value: null, child: Text('Tất cả')),
                        for (final r in SubmissionRating.values)
                          DropdownMenuItem<SubmissionRating?>(
                              value: r, child: Text(r.label, overflow: TextOverflow.ellipsis)),
                      ],
                      onChanged: (v) => notifier.set(filter.copyWith(rating: () => v)),
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 8),
              // Khoảng ngày nộp + nút xóa lọc
              Row(
                children: [
                  Flexible(
                    child: OutlinedButton.icon(
                      onPressed: () => _pickRange(filter),
                      icon: const Icon(Icons.date_range),
                      label: Text(
                        filter.from == null || filter.to == null
                            ? 'Khoảng ngày nộp'
                            : '${formatDate(filter.from!)} – ${formatDate(filter.to!)}',
                        overflow: TextOverflow.ellipsis,
                      ),
                    ),
                  ),
                  if (filter.from != null)
                    IconButton(
                      tooltip: 'Bỏ lọc ngày',
                      icon: const Icon(Icons.close),
                      onPressed: () => notifier.set(filter.copyWith(from: () => null, to: () => null)),
                    ),
                  const Spacer(),
                  if (filter.isActive)
                    TextButton(
                      onPressed: () {
                        _search.clear();
                        notifier.set(const SubmissionFilter());
                      },
                      child: const Text('Xóa bộ lọc'),
                    ),
                ],
              ),
            ],
          ),
        ),
        // ---- Danh sách ----
        Expanded(
          child: AsyncValueView(
            value: list,
            onRetry: () => ref.invalidate(mySubmissionsProvider),
            data: (items) => RefreshIndicator(
              onRefresh: () => ref.refresh(mySubmissionsProvider.future),
              child: items.isEmpty
                  // Trạng thái rỗng vẫn cuộn được để kéo làm mới
                  ? ListView(
                      physics: const AlwaysScrollableScrollPhysics(),
                      children: [
                        const SizedBox(height: 48),
                        Icon(Icons.image_not_supported_outlined,
                            size: 48, color: Theme.of(context).colorScheme.outline),
                        const SizedBox(height: 12),
                        Text(
                          filter.isActive
                              ? 'Không có bài nào khớp bộ lọc.'
                              : 'Bạn chưa nộp bài nào. Vào tab "Cuộc thi" để nộp bài.',
                          textAlign: TextAlign.center,
                        ),
                      ],
                    )
                  : ListView.builder(
                      physics: const AlwaysScrollableScrollPhysics(),
                      padding: const EdgeInsets.all(16),
                      itemCount: items.length,
                      itemBuilder: (context, i) => SubmissionTile(item: items[i]),
                    ),
            ),
          ),
        ),
      ],
    );
  }
}

/// Một thẻ bài trong danh sách; bấm để mở chi tiết.
class SubmissionTile extends StatelessWidget {
  const SubmissionTile({super.key, required this.item});

  final SubmissionListItem item;

  @override
  Widget build(BuildContext context) {
    final textTheme = Theme.of(context).textTheme;
    return Card(
      clipBehavior: Clip.antiAlias,
      child: InkWell(
        onTap: () => context.push('/student/submissions/${item.id}'),
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  Expanded(child: Text(item.title, style: textTheme.titleMedium)),
                  // Chỉ hiện xếp loại khi đã chấm
                  if (item.rating != null) RatingChip(item.rating!),
                ],
              ),
              const SizedBox(height: 4),
              Text(item.competitionTitle, style: textTheme.bodyMedium),
              const SizedBox(height: 4),
              Wrap(
                spacing: 12,
                children: [
                  Text('Nộp: ${formatDateTime(item.submittedAt)}', style: textTheme.bodySmall),
                  // Hết quyền sửa → nhắc đã quá hạn
                  if (!item.canEdit)
                    Text('Đã quá hạn',
                        style: textTheme.bodySmall?.copyWith(color: Theme.of(context).colorScheme.error)),
                ],
              ),
            ],
          ),
        ),
      ),
    );
  }
}
