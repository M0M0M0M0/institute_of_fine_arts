import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../app/tokens.dart';
import '../../core/async_view.dart';
import '../../core/format.dart';
import '../../shared/shared.dart';
import '../submissions/submissions.dart';
import 'grading.dart';

/// M-T2 (2/2): the entries of one competition to grade, with thumbnails and filters
/// (graded / rating / search by student name, code or title).
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
      appBar: AppBar(title: Text(list.value?.competitionTitle ?? 'Entries')),
      body: Column(
        children: [
          // Progress only shows once data exists; it hides while a new filter loads (the provider key changes)
          if (list.value case final data?) _Progress(data: data),
          _Filters(filter: _filter, search: _search, onChanged: _setFilter),
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
                        children: const [
                          SizedBox(height: AppSpacing.xl),
                          EmptyState(title: 'No matching entries', message: 'Try another filter or search.'),
                        ],
                      )
                    : ListView.separated(
                        physics: const AlwaysScrollableScrollPhysics(),
                        padding: const EdgeInsets.all(AppSpacing.gutter),
                        itemCount: data.items.length,
                        separatorBuilder: (context, i) => const Divider(height: AppSpacing.lg),
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

/// Progress of the whole competition, independent of the filter.
class _Progress extends StatelessWidget {
  const _Progress({required this.data});

  final GradingList data;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(AppSpacing.gutter, AppSpacing.md, AppSpacing.gutter, 0),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.end,
        children: [
          Expanded(
            child: ProgressBarRow(
              label: 'Graded',
              value: data.evaluated,
              total: data.total,
              color: AppColors.statusOngoing,
            ),
          ),
          const SizedBox(width: AppSpacing.md),
          StatusChip(data.status),
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
    GradingFilter with_({bool? graded, bool clearGraded = false, SubmissionRating? rating, bool clearRating = false, String? q}) =>
        GradingFilter(
          graded: clearGraded ? null : (graded ?? filter.graded),
          rating: clearRating ? null : (rating ?? filter.rating),
          q: q ?? filter.q,
        );

    return Padding(
      padding: const EdgeInsets.fromLTRB(AppSpacing.gutter, AppSpacing.sm, AppSpacing.gutter, 0),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          TextField(
            controller: search,
            decoration: const InputDecoration(
              hintText: 'Student name, code or title',
              prefixIcon: Icon(Icons.search),
              isDense: true,
            ),
            textInputAction: TextInputAction.search,
            onSubmitted: (v) => onChanged(with_(q: v)),
          ),
          const SizedBox(height: AppSpacing.xs),
          SingleChildScrollView(
            scrollDirection: Axis.horizontal,
            child: Row(
              children: [
                _chip('All', filter.graded == null, () => onChanged(with_(clearGraded: true))),
                _chip('To grade', filter.graded == false, () => onChanged(with_(graded: false))),
                _chip('Graded', filter.graded == true, () => onChanged(with_(graded: true))),
              ],
            ),
          ),
          SingleChildScrollView(
            scrollDirection: Axis.horizontal,
            child: Row(
              children: [
                _chip('Any rating', filter.rating == null, () => onChanged(with_(clearRating: true))),
                for (final r in SubmissionRating.values)
                  _chip(ratingName(r), filter.rating == r, () => onChanged(with_(rating: r))),
              ],
            ),
          ),
        ],
      ),
    );
  }

  Widget _chip(String label, bool selected, VoidCallback onTap) => Padding(
        padding: const EdgeInsets.only(right: AppSpacing.sm),
        child: ChoiceChip(label: Text(label), selected: selected, onSelected: (_) => onTap()),
      );
}

class _ItemTile extends StatelessWidget {
  const _ItemTile({required this.item});

  final GradingItem item;

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    return InkWell(
      onTap: () => context.push('${gradingBase(context)}/submissions/${item.id}/evaluate'),
      child: Padding(
        padding: const EdgeInsets.symmetric(vertical: AppSpacing.xs),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            SizedBox(
              width: 84,
              child: ArtworkFrame(
                aspectRatio: 1,
                matWidth: 5,
                child: item.hasThumbnail
                    ? AuthImage(submissionId: item.id, semanticLabel: item.title)
                    : const ImagePlaceholder(),
              ),
            ),
            const SizedBox(width: AppSpacing.md),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    item.title,
                    maxLines: 2,
                    overflow: TextOverflow.ellipsis,
                    style: text.titleSmall!.copyWith(fontStyle: FontStyle.italic, fontWeight: FontWeight.w500),
                  ),
                  Text('${item.studentName} (${item.studentCode})', style: text.bodyMedium),
                  Text('Submitted ${formatDateTime(item.submittedAt)}', style: text.bodySmall),
                  const SizedBox(height: AppSpacing.xs),
                  item.rating == null
                      ? const StatusChip.custom(label: 'To grade', color: AppColors.statusUpcoming)
                      : StatusChip.rating(item.rating!),
                ],
              ),
            ),
            const Icon(Icons.chevron_right, color: AppColors.muted),
          ],
        ),
      ),
    );
  }
}
