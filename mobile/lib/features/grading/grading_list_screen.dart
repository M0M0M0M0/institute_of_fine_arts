import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/async_view.dart';
import '../../core/format.dart';
import '../competitions/competition_widgets.dart';
import '../submissions/submission_widgets.dart';
import '../submissions/submissions.dart';
import 'grading.dart';

/// Danh sách bài của một cuộc thi để chấm, có lọc chưa/đã chấm, xếp loại, tìm tên/mã HV (F-M07-01, 06).
class GradingListScreen extends ConsumerStatefulWidget {
  const GradingListScreen({super.key, required this.competitionId});

  final int competitionId;

  @override
  ConsumerState<GradingListScreen> createState() => _GradingListScreenState();
}

class _GradingListScreenState extends ConsumerState<GradingListScreen> {
  GradingFilter _filter = const GradingFilter();
  final _search = TextEditingController();

  @override
  void dispose() {
    _search.dispose();
    super.dispose();
  }

  void _setFilter(GradingFilter value) => setState(() => _filter = value);

  @override
  Widget build(BuildContext context) {
    final key = (competitionId: widget.competitionId, filter: _filter);
    final list = ref.watch(gradingListProvider(key));

    return Scaffold(
      appBar: AppBar(title: Text(list.value?.competitionTitle ?? 'Danh sách bài')),
      body: Column(
        children: [
          // Tiến độ chỉ hiện khi đã có dữ liệu; đổi bộ lọc thì ẩn trong lúc tải lại (khóa provider đổi theo bộ lọc)
          if (list.value case final data?) _Progress(data: data),
          _Filters(
            filter: _filter,
            search: _search,
            onChanged: _setFilter,
          ),
          Expanded(
            child: AsyncValueView(
              value: list,
              onRetry: () => ref.invalidate(gradingListProvider(key)),
              data: (data) => RefreshIndicator(
                onRefresh: () async {
                  ref.invalidate(gradingListProvider(key));
                  await ref.read(gradingListProvider(key).future);
                },
                child: data.items.isEmpty
                    ? ListView(
                        physics: const AlwaysScrollableScrollPhysics(),
                        children: const [Padding(padding: EdgeInsets.all(32), child: Center(child: Text('Không có bài phù hợp.')))],
                      )
                    : ListView.builder(
                        physics: const AlwaysScrollableScrollPhysics(),
                        itemCount: data.items.length,
                        itemBuilder: (context, i) => _ItemTile(item: data.items[i]),
                      ),
              ),
            ),
          ),
        ],
      ),
    );
  }
}

/// Thanh tiến độ "Đã chấm x/y bài" của cả cuộc thi.
class _Progress extends StatelessWidget {
  const _Progress({required this.data});

  final GradingList data;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 12, 16, 0),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Expanded(child: Text('Đã chấm ${progressLabel(data.evaluated, data.total)} bài')),
              StatusChip(data.status),
            ],
          ),
          const SizedBox(height: 4),
          LinearProgressIndicator(value: data.total == 0 ? 0 : data.evaluated / data.total),
        ],
      ),
    );
  }
}

class _Filters extends StatelessWidget {
  const _Filters({required this.filter, required this.search, required this.onChanged});

  final GradingFilter filter;
  final TextEditingController search;
  final ValueChanged<GradingFilter> onChanged;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 8, 16, 0),
      child: Column(
        children: [
          // Chưa chấm / Đã chấm / Tất cả
          SegmentedButton<bool?>(
            showSelectedIcon: false,
            segments: const [
              ButtonSegment(value: null, label: Text('Tất cả')),
              ButtonSegment(value: false, label: Text('Chưa chấm')),
              ButtonSegment(value: true, label: Text('Đã chấm')),
            ],
            selected: {filter.graded},
            onSelectionChanged: (s) => onChanged(GradingFilter(graded: s.first, rating: filter.rating, q: filter.q)),
          ),
          const SizedBox(height: 8),
          Row(
            children: [
              Expanded(
                child: TextField(
                  controller: search,
                  decoration: const InputDecoration(
                    hintText: 'Tên, mã học viên hoặc tiêu đề',
                    prefixIcon: Icon(Icons.search),
                    isDense: true,
                    border: OutlineInputBorder(),
                  ),
                  textInputAction: TextInputAction.search,
                  onSubmitted: (v) => onChanged(GradingFilter(graded: filter.graded, rating: filter.rating, q: v)),
                ),
              ),
              const SizedBox(width: 8),
              DropdownButton<SubmissionRating?>(
                value: filter.rating,
                hint: const Text('Xếp loại'),
                onChanged: (v) => onChanged(GradingFilter(graded: filter.graded, rating: v, q: filter.q)),
                items: [
                  const DropdownMenuItem(value: null, child: Text('Mọi xếp loại')),
                  for (final r in SubmissionRating.values) DropdownMenuItem(value: r, child: Text(r.name.toUpperCase())),
                ],
              ),
            ],
          ),
        ],
      ),
    );
  }
}

class _ItemTile extends StatelessWidget {
  const _ItemTile({required this.item});

  final GradingItem item;

  @override
  Widget build(BuildContext context) {
    return ListTile(
      title: Text(item.title),
      subtitle: Text('${item.studentName} (${item.studentCode})\nNộp: ${formatDateTime(item.submittedAt)}'),
      isThreeLine: true,
      trailing: item.rating == null
          ? const Chip(label: Text('Chưa chấm'), visualDensity: VisualDensity.compact)
          : RatingChip(item.rating!),
      onTap: () => context.push('/staff/submissions/${item.id}/evaluate'),
    );
  }
}
