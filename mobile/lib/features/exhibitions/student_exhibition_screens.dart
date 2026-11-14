import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/api_client.dart';
import '../../core/async_view.dart';
import '../../core/format.dart';
import '../competitions/competition_widgets.dart';
import 'exhibition_widgets.dart';
import 'exhibitions.dart';

/// Tab "Triển lãm" của học viên: triển lãm sắp tới + tranh đã gửi (F-M09-02, 03, 10).
class StudentExhibitionsTab extends StatelessWidget {
  const StudentExhibitionsTab({super.key});

  @override
  Widget build(BuildContext context) {
    return const DefaultTabController(
      length: 2,
      child: Column(
        children: [
          TabBar(tabs: [Tab(text: 'Triển lãm sắp tới'), Tab(text: 'Tranh đã gửi')]),
          Expanded(child: TabBarView(children: [_ExhibitionList(), _MyEntriesList()])),
        ],
      ),
    );
  }
}

/// Danh sách rỗng vẫn kéo để làm mới được.
Widget _emptyList(String text) => ListView(
      physics: const AlwaysScrollableScrollPhysics(),
      children: [Padding(padding: const EdgeInsets.all(32), child: Center(child: Text(text, textAlign: TextAlign.center)))],
    );

class _ExhibitionList extends ConsumerWidget {
  const _ExhibitionList();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final list = ref.watch(studentExhibitionsProvider);
    return AsyncValueView(
      value: list,
      onRetry: () => ref.invalidate(studentExhibitionsProvider),
      data: (items) => RefreshIndicator(
        onRefresh: () async {
          ref.invalidate(studentExhibitionsProvider);
          await ref.read(studentExhibitionsProvider.future);
        },
        child: items.isEmpty
            ? _emptyList('Chưa có triển lãm sắp tới.')
            : ListView.builder(
                physics: const AlwaysScrollableScrollPhysics(),
                padding: const EdgeInsets.all(16),
                itemCount: items.length,
                itemBuilder: (context, i) {
                  final e = items[i];
                  return Card(
                    child: InkWell(
                      onTap: () => context.push('/student/exhibitions/${e.id}'),
                      child: Padding(
                        padding: const EdgeInsets.all(12),
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Row(children: [
                              Expanded(child: Text(e.name, style: Theme.of(context).textTheme.titleMedium)),
                              ExhibitionStatusChip(e.status),
                            ]),
                            const SizedBox(height: 4),
                            Text('${e.venue} – ${e.city}'),
                            Text(dateRange(e.startDate, e.endDate)),
                            if (e.status == ExhibitionStatus.upcoming)
                              Text('Bắt đầu sau ${e.daysUntilStart} ngày', style: Theme.of(context).textTheme.bodySmall),
                          ],
                        ),
                      ),
                    ),
                  );
                },
              ),
      ),
    );
  }
}

class _MyEntriesList extends ConsumerWidget {
  const _MyEntriesList();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final list = ref.watch(myEntriesProvider);
    return AsyncValueView(
      value: list,
      onRetry: () => ref.invalidate(myEntriesProvider),
      data: (items) => RefreshIndicator(
        onRefresh: () async {
          ref.invalidate(myEntriesProvider);
          await ref.read(myEntriesProvider.future);
        },
        child: items.isEmpty
            ? _emptyList('Bạn chưa gửi tranh nào đến triển lãm.\nBài xếp loại BEST có thể gửi từ màn chi tiết triển lãm.')
            : ListView.builder(
                physics: const AlwaysScrollableScrollPhysics(),
                padding: const EdgeInsets.all(16),
                itemCount: items.length,
                itemBuilder: (context, i) => _EntryCard(entry: items[i]),
              ),
      ),
    );
  }
}

class _EntryCard extends StatelessWidget {
  const _EntryCard({required this.entry});

  final StudentEntry entry;

  @override
  Widget build(BuildContext context) {
    final e = entry;
    return Card(
      child: InkWell(
        onTap: () => context.push('/student/exhibition-entries/${e.id}'),
        child: Padding(
          padding: const EdgeInsets.all(12),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(children: [
                Expanded(child: Text(e.submissionTitle, style: Theme.of(context).textTheme.titleMedium)),
                EntryStatusChip(e.status, paid: e.paidToStudent),
              ]),
              const SizedBox(height: 4),
              Text(e.exhibitionName),
              Text('Giá đề xuất: ${formatVnd(e.askingPrice)}'),
              if (e.soldPrice != null) Text('Giá khách trả: ${formatVnd(e.soldPrice!)}'),
            ],
          ),
        ),
      ),
    );
  }
}

// ===================== Chi tiết triển lãm =====================

/// Chi tiết một triển lãm cho học viên, có nút "Gửi tranh" khi còn sắp diễn ra (UC-07 bước 1).
class StudentExhibitionDetailScreen extends ConsumerWidget {
  const StudentExhibitionDetailScreen({super.key, required this.id});

  final int id;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final detail = ref.watch(studentExhibitionProvider(id));
    return Scaffold(
      appBar: AppBar(title: const Text('Triển lãm')),
      body: AsyncValueView(
        value: detail,
        onRetry: () => ref.invalidate(studentExhibitionProvider(id)),
        data: (e) => ListView(
          padding: const EdgeInsets.all(16),
          children: [
            Row(children: [
              Expanded(child: Text(e.name, style: Theme.of(context).textTheme.headlineSmall)),
              ExhibitionStatusChip(e.status),
            ]),
            const SizedBox(height: 8),
            InfoRow('Địa điểm', '${e.venue}, ${e.city}'),
            InfoRow('Thời gian', dateRange(e.startDate, e.endDate)),
            if (e.organizer != null) InfoRow('Ban tổ chức', e.organizer!),
            if (e.description != null) ...[const SectionTitle('Giới thiệu'), Text(e.description!)],
            if (e.status == ExhibitionStatus.cancelled && e.cancelReason != null)
              NoticeCard(text: 'Triển lãm đã hủy: ${e.cancelReason}', error: true),
            const SizedBox(height: 16),
            if (e.canSubmitEntries)
              FilledButton.icon(
                onPressed: () => context.push('/student/exhibitions/${e.id}/propose'),
                icon: const Icon(Icons.send),
                label: const Text('Gửi tranh'),
              )
            else
              const NoticeCard(text: 'Triển lãm đã bắt đầu hoặc đã hủy nên không nhận thêm tranh.'),
          ],
        ),
      ),
    );
  }
}

// ===================== Đề xuất gửi tranh =====================

/// Chọn bài BEST đủ điều kiện + nhập giá tối thiểu → entry PENDING (F-M09-04, BR-40, BR-41).
class ProposeEntryScreen extends ConsumerStatefulWidget {
  const ProposeEntryScreen({super.key, required this.exhibitionId});

  final int exhibitionId;

  @override
  ConsumerState<ProposeEntryScreen> createState() => _ProposeEntryScreenState();
}

class _ProposeEntryScreenState extends ConsumerState<ProposeEntryScreen> {
  final _price = TextEditingController();
  int? _submissionId;
  bool _busy = false;

  @override
  void dispose() {
    _price.dispose();
    super.dispose();
  }

  void _toast(String text) => ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(text)));

  Future<void> _submit() async {
    if (_submissionId == null) {
      _toast('Vui lòng chọn bài muốn gửi');
      return;
    }
    final error = validatePrice(_price.text, label: 'giá đề xuất');
    if (error != null) {
      _toast(error);
      return;
    }
    final container = ProviderScope.containerOf(context);
    setState(() => _busy = true);
    try {
      final entry = await ref.read(exhibitionRepositoryProvider).propose(
            exhibitionId: widget.exhibitionId,
            submissionId: _submissionId!,
            askingPrice: parseMoney(_price.text)!,
          );
      if (!mounted) return;
      _toast('Đã gửi đề xuất, vui lòng chờ giáo viên duyệt.');
      context.pushReplacement('/student/exhibition-entries/${entry.id}');
      refreshExhibitionData(container);
    } catch (e) {
      if (!mounted) return;
      final err = ApiException.from(e);
      _toast(err.message);
      // 409 "Đã có đề xuất": làm mới danh sách bài đủ điều kiện
      if (err.statusCode == 409) ref.invalidate(studentEligibleProvider);
      setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final eligible = ref.watch(studentEligibleProvider);
    return Scaffold(
      appBar: AppBar(title: const Text('Gửi tranh đến triển lãm')),
      body: AsyncValueView(
        value: eligible,
        onRetry: () => ref.invalidate(studentEligibleProvider),
        data: (items) => ListView(
          padding: const EdgeInsets.all(16),
          children: [
            Text('Chọn bài (chỉ bài xếp loại BEST của cuộc thi đã công bố)', style: Theme.of(context).textTheme.titleMedium),
            const SizedBox(height: 8),
            if (items.isEmpty)
              const NoticeCard(text: 'Bạn chưa có bài nào đủ điều kiện (hoặc bài đã được gửi/đã bán).')
            else
              RadioGroup<int>(
                groupValue: _submissionId,
                onChanged: (v) => setState(() => _submissionId = v),
                child: Column(
                  children: [
                    for (final s in items)
                      RadioListTile<int>(
                        value: s.id,
                        title: Text(s.title),
                        subtitle: Text(s.competitionTitle),
                      ),
                  ],
                ),
              ),
            const SizedBox(height: 16),
            MoneyField(controller: _price, label: 'Giá đề xuất (giá tối thiểu bạn chấp nhận)'),
            const SizedBox(height: 4),
            Text('Nếu bán thấp hơn giá này, giáo viên phải có xác nhận của bạn.', style: Theme.of(context).textTheme.bodySmall),
            const SizedBox(height: 16),
            FilledButton.icon(
              onPressed: (_busy || items.isEmpty) ? null : _submit,
              icon: _busy
                  ? const SizedBox(width: 16, height: 16, child: CircularProgressIndicator(strokeWidth: 2))
                  : const Icon(Icons.send),
              label: const Text('Gửi đề xuất'),
            ),
          ],
        ),
      ),
    );
  }
}

// ===================== Chi tiết tranh đã gửi =====================

/// Trạng thái tranh đã gửi: giá đề xuất, đã bán?, giá khách trả, đã nhận tiền? (F-M09-10); sửa giá/rút khi PENDING.
class StudentEntryDetailScreen extends ConsumerWidget {
  const StudentEntryDetailScreen({super.key, required this.id});

  final int id;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final detail = ref.watch(myEntryProvider(id));
    return Scaffold(
      appBar: AppBar(title: const Text('Tranh đã gửi')),
      body: AsyncValueView(
        value: detail,
        onRetry: () => ref.invalidate(myEntryProvider(id)),
        // Key theo RowVersion: tải lại sau xung đột thì form dựng lại với giá mới
        data: (e) => _EntryBody(key: ValueKey('${e.id}-${e.rowVersion}'), entry: e),
      ),
    );
  }
}

class _EntryBody extends ConsumerStatefulWidget {
  const _EntryBody({super.key, required this.entry});

  final StudentEntry entry;

  @override
  ConsumerState<_EntryBody> createState() => _EntryBodyState();
}

class _EntryBodyState extends ConsumerState<_EntryBody> {
  late final TextEditingController _price =
      TextEditingController(text: widget.entry.askingPrice.round().toString());
  bool _busy = false;

  StudentEntry get _e => widget.entry;

  @override
  void dispose() {
    _price.dispose();
    super.dispose();
  }

  void _toast(String text) => ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(text)));

  // 409 = Staff vừa xử lý đề xuất (BR-52): báo rõ và tải lại bản mới nhất
  void _handleError(Object e) {
    final err = ApiException.from(e);
    _toast(err.message);
    if (err.statusCode == 409) ref.invalidate(myEntryProvider(_e.id));
    setState(() => _busy = false);
  }

  Future<void> _savePrice() async {
    final error = validatePrice(_price.text, label: 'giá đề xuất');
    if (error != null) {
      _toast(error);
      return;
    }
    final container = ProviderScope.containerOf(context);
    setState(() => _busy = true);
    try {
      await ref.read(exhibitionRepositoryProvider).updatePrice(_e.id, parseMoney(_price.text)!, _e.rowVersion);
      if (!mounted) return;
      _toast('Đã cập nhật giá đề xuất.');
      refreshExhibitionData(container);
    } catch (e) {
      if (mounted) _handleError(e);
    }
  }

  Future<void> _withdraw() async {
    final ok = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Rút đề xuất?'),
        content: const Text('Đề xuất sẽ bị xóa. Bạn có thể gửi lại sau.'),
        actions: [
          TextButton(onPressed: () => Navigator.pop(context, false), child: const Text('Giữ lại')),
          FilledButton(onPressed: () => Navigator.pop(context, true), child: const Text('Rút')),
        ],
      ),
    );
    if (ok != true || !mounted) return;
    final container = ProviderScope.containerOf(context);
    setState(() => _busy = true);
    try {
      await ref.read(exhibitionRepositoryProvider).withdraw(_e.id);
      if (!mounted) return;
      _toast('Đã rút đề xuất.');
      context.pop();
      refreshExhibitionData(container);
    } catch (e) {
      if (mounted) _handleError(e);
    }
  }

  @override
  Widget build(BuildContext context) {
    final e = _e;
    final textTheme = Theme.of(context).textTheme;
    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        Row(children: [
          Expanded(child: Text(e.submissionTitle, style: textTheme.headlineSmall)),
          EntryStatusChip(e.status, paid: e.paidToStudent),
        ]),
        const SizedBox(height: 8),
        InfoRow('Cuộc thi', e.competitionTitle),
        InfoRow('Triển lãm', e.exhibitionName),
        InfoRow('Địa điểm', '${e.venue}, ${e.city}'),
        InfoRow('Thời gian', dateRange(e.exhibitionStartDate, e.exhibitionEndDate)),
        const Divider(height: 24),
        InfoRow('Giá đề xuất', formatVnd(e.askingPrice)),
        if (e.status == EntryStatus.sold) ...[
          InfoRow('Giá khách trả', formatVnd(e.soldPrice ?? 0)),
          if (e.soldDate != null) InfoRow('Ngày bán', formatDate(e.soldDate!)),
          InfoRow('Nhận tiền', e.paidToStudent ? 'Đã nhận ${formatVnd(e.paidAmount ?? 0)}'
              '${e.paidDate != null ? ' ngày ${formatDate(e.paidDate!)}' : ''}' : 'Chưa nhận tiền'),
        ],
        if (e.statusReason != null &&
            (e.status == EntryStatus.rejected || e.status == EntryStatus.withdrawn || e.status == EntryStatus.unsold))
          NoticeCard(text: e.statusReason!, error: e.status != EntryStatus.unsold),
        if (e.status == EntryStatus.pending)
          const NoticeCard(text: 'Đang chờ giáo viên duyệt. Bạn có thể sửa giá hoặc rút đề xuất.'),
        if (e.canEdit) ...[
          const Divider(height: 24),
          MoneyField(controller: _price, label: 'Giá đề xuất', enabled: !_busy),
          const SizedBox(height: 12),
          Wrap(spacing: 12, runSpacing: 8, children: [
            FilledButton.icon(
              onPressed: _busy ? null : _savePrice,
              icon: const Icon(Icons.save),
              label: const Text('Lưu giá'),
            ),
            FilledButton.tonalIcon(
              onPressed: _busy ? null : _withdraw,
              icon: const Icon(Icons.undo),
              label: const Text('Rút đề xuất'),
            ),
          ]),
        ],
      ],
    );
  }
}
