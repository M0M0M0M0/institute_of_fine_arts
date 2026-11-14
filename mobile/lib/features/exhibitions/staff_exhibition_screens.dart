import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/api_client.dart';
import '../../core/async_view.dart';
import '../../core/format.dart';
import '../competitions/competition_widgets.dart';
import 'exhibition_widgets.dart';
import 'exhibitions.dart';

/// Tab "Triển lãm" của giáo viên: danh sách triển lãm + danh sách tranh gửi triển lãm (F-M09-01, 02, 09).
class StaffExhibitionsTab extends StatelessWidget {
  const StaffExhibitionsTab({super.key});

  @override
  Widget build(BuildContext context) {
    return const DefaultTabController(
      length: 2,
      child: Column(
        children: [
          TabBar(tabs: [Tab(text: 'Triển lãm'), Tab(text: 'Tranh gửi triển lãm')]),
          Expanded(child: TabBarView(children: [_StaffExhibitionList(), _StaffEntriesList()])),
        ],
      ),
    );
  }
}

Widget _emptyList(String text) => ListView(
      physics: const AlwaysScrollableScrollPhysics(),
      children: [Padding(padding: const EdgeInsets.all(32), child: Center(child: Text(text, textAlign: TextAlign.center)))],
    );

// ===================== Danh sách triển lãm =====================

class _StaffExhibitionList extends ConsumerStatefulWidget {
  const _StaffExhibitionList();

  @override
  ConsumerState<_StaffExhibitionList> createState() => _StaffExhibitionListState();
}

class _StaffExhibitionListState extends ConsumerState<_StaffExhibitionList> {
  String _q = '';
  ExhibitionStatus? _status;

  @override
  Widget build(BuildContext context) {
    final list = ref.watch(staffExhibitionsProvider);
    return Column(
      children: [
        Padding(
          padding: const EdgeInsets.fromLTRB(16, 12, 16, 0),
          child: Row(children: [
            Expanded(
              child: TextField(
                decoration: const InputDecoration(
                    hintText: 'Tìm theo tên, địa điểm, thành phố', prefixIcon: Icon(Icons.search), isDense: true),
                onChanged: (v) => setState(() => _q = v.trim().toLowerCase()),
              ),
            ),
            const SizedBox(width: 8),
            FilledButton.icon(
              onPressed: () => context.push('/staff/exhibitions/new'),
              icon: const Icon(Icons.add),
              label: const Text('Thêm'),
            ),
          ]),
        ),
        SingleChildScrollView(
          scrollDirection: Axis.horizontal,
          padding: const EdgeInsets.symmetric(horizontal: 12),
          child: Row(children: [
            Padding(
              padding: const EdgeInsets.all(4),
              child: ChoiceChip(label: const Text('Tất cả'), selected: _status == null, onSelected: (_) => setState(() => _status = null)),
            ),
            for (final s in ExhibitionStatus.values)
              Padding(
                padding: const EdgeInsets.all(4),
                child: ChoiceChip(label: Text(s.label), selected: _status == s, onSelected: (_) => setState(() => _status = s)),
              ),
          ]),
        ),
        Expanded(
          child: AsyncValueView(
            value: list,
            onRetry: () => ref.invalidate(staffExhibitionsProvider),
            data: (all) {
              // Lọc ở app vì số triển lãm ít; trạng thái do server tính sẵn
              final items = all
                  .where((e) => _status == null || e.status == _status)
                  .where((e) => _q.isEmpty || '${e.name} ${e.venue} ${e.city}'.toLowerCase().contains(_q))
                  .toList();
              return RefreshIndicator(
                onRefresh: () async {
                  ref.invalidate(staffExhibitionsProvider);
                  await ref.read(staffExhibitionsProvider.future);
                },
                child: items.isEmpty
                    ? _emptyList('Không có triển lãm phù hợp.')
                    : ListView.builder(
                        physics: const AlwaysScrollableScrollPhysics(),
                        padding: const EdgeInsets.all(16),
                        itemCount: items.length,
                        itemBuilder: (context, i) {
                          final e = items[i];
                          return Card(
                            child: InkWell(
                              onTap: () => context.push('/staff/exhibitions/${e.id}'),
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
                                    Text('${e.entryCount} tranh${e.pendingCount > 0 ? ' • ${e.pendingCount} chờ duyệt' : ''}',
                                        style: Theme.of(context).textTheme.bodySmall),
                                  ],
                                ),
                              ),
                            ),
                          );
                        },
                      ),
              );
            },
          ),
        ),
      ],
    );
  }
}

// ===================== Danh sách tranh gửi triển lãm =====================

class _StaffEntriesList extends ConsumerStatefulWidget {
  const _StaffEntriesList();

  @override
  ConsumerState<_StaffEntriesList> createState() => _StaffEntriesListState();
}

class _StaffEntriesListState extends ConsumerState<_StaffEntriesList> {
  EntryStatus? _status;
  bool? _paid;
  String _q = '';

  // Các bộ lọc nhanh: (nhãn, trạng thái, đã trả)
  static const _quick = <(String, EntryStatus?, bool?)>[
    ('Tất cả', null, null),
    ('Chờ duyệt', EntryStatus.pending, null),
    ('Đã duyệt', EntryStatus.accepted, null),
    ('Đã bán', EntryStatus.sold, null),
    ('Cần trả tiền', EntryStatus.sold, false),
    ('Chưa bán', EntryStatus.unsold, null),
  ];

  @override
  Widget build(BuildContext context) {
    final filter = EntryFilter(status: _status, paid: _paid, q: _q);
    final list = ref.watch(entriesProvider(filter));
    return Column(
      children: [
        Padding(
          padding: const EdgeInsets.fromLTRB(16, 12, 16, 0),
          child: TextField(
            decoration: const InputDecoration(hintText: 'Tìm theo học viên, mã HV, tên tranh', prefixIcon: Icon(Icons.search), isDense: true),
            onSubmitted: (v) => setState(() => _q = v.trim()),
            onChanged: (v) {
              if (v.trim().isEmpty && _q.isNotEmpty) setState(() => _q = '');
            },
          ),
        ),
        SingleChildScrollView(
          scrollDirection: Axis.horizontal,
          padding: const EdgeInsets.symmetric(horizontal: 12),
          child: Row(children: [
            for (final (label, status, paid) in _quick)
              Padding(
                padding: const EdgeInsets.all(4),
                child: ChoiceChip(
                  label: Text(label),
                  selected: _status == status && _paid == paid,
                  onSelected: (_) => setState(() {
                    _status = status;
                    _paid = paid;
                  }),
                ),
              ),
          ]),
        ),
        Expanded(
          child: AsyncValueView(
            value: list,
            onRetry: () => ref.invalidate(entriesProvider(filter)),
            data: (items) => RefreshIndicator(
              onRefresh: () async {
                ref.invalidate(entriesProvider(filter));
                await ref.read(entriesProvider(filter).future);
              },
              child: items.isEmpty
                  ? _emptyList('Không có tranh nào phù hợp.')
                  : ListView.builder(
                      physics: const AlwaysScrollableScrollPhysics(),
                      padding: const EdgeInsets.all(16),
                      itemCount: items.length,
                      itemBuilder: (context, i) => EntryListCard(item: items[i]),
                    ),
            ),
          ),
        ),
      ],
    );
  }
}

/// Thẻ một tranh gửi triển lãm trong danh sách của Staff.
class EntryListCard extends StatelessWidget {
  const EntryListCard({super.key, required this.item, this.showExhibition = true});

  final EntryListItem item;
  final bool showExhibition;

  @override
  Widget build(BuildContext context) {
    final e = item;
    return Card(
      child: InkWell(
        onTap: () => context.push('/staff/exhibition-entries/${e.id}'),
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
              Text('${e.studentName} (${e.studentCode})'),
              if (showExhibition) Text(e.exhibitionName),
              Text('Giá đề xuất: ${formatVnd(e.askingPrice)}'
                  '${e.soldPrice != null ? ' • Giá bán: ${formatVnd(e.soldPrice!)}' : ''}'),
            ],
          ),
        ),
      ),
    );
  }
}

// ===================== Chi tiết triển lãm =====================

/// Chi tiết triển lãm cho Staff: thông tin, thao tác (sửa/hủy/kết thúc/xóa), danh sách tranh (F-M09-01, 13).
class StaffExhibitionDetailScreen extends ConsumerStatefulWidget {
  const StaffExhibitionDetailScreen({super.key, required this.id});

  final int id;

  @override
  ConsumerState<StaffExhibitionDetailScreen> createState() => _StaffExhibitionDetailScreenState();
}

class _StaffExhibitionDetailScreenState extends ConsumerState<StaffExhibitionDetailScreen> {
  bool _busy = false;

  void _toast(String text) => ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(text)));

  void _handleError(Object e) {
    final err = ApiException.from(e);
    _toast(err.message);
    if (err.statusCode == 409) refreshExhibitionData(ProviderScope.containerOf(context));
    setState(() => _busy = false);
  }

  // Chạy một thao tác ghi, báo kết quả và làm mới dữ liệu
  Future<void> _run(Future<void> Function() action, String done) async {
    setState(() => _busy = true);
    final container = ProviderScope.containerOf(context);
    try {
      await action();
      if (!mounted) return;
      _toast(done);
      setState(() => _busy = false);
      refreshExhibitionData(container);
    } catch (e) {
      if (mounted) _handleError(e);
    }
  }

  Future<bool> _confirm(String title, String message, String ok) async =>
      await showDialog<bool>(
        context: context,
        builder: (context) => AlertDialog(
          title: Text(title),
          content: Text(message),
          actions: [
            TextButton(onPressed: () => Navigator.pop(context, false), child: const Text('Đóng')),
            FilledButton(onPressed: () => Navigator.pop(context, true), child: Text(ok)),
          ],
        ),
      ) ??
      false;

  // Hủy triển lãm cần lý do (BR-42a)
  Future<void> _cancel() async {
    final reason = await askReason(context, title: 'Hủy triển lãm', label: 'Lý do hủy *',
        hint: 'Tranh đang chờ duyệt/đã duyệt sẽ chuyển sang "Đã rút".');
    if (reason == null || !mounted) return;
    await _run(() => ref.read(exhibitionRepositoryProvider).cancelExhibition(widget.id, reason), 'Đã hủy triển lãm.');
  }

  Future<void> _finish() async {
    if (!await _confirm('Kết thúc triển lãm?', 'Các tranh đã duyệt nhưng chưa bán sẽ chuyển sang "Chưa bán".', 'Kết thúc')) return;
    await _run(() => ref.read(exhibitionRepositoryProvider).finishExhibition(widget.id), 'Đã kết thúc triển lãm.');
  }

  Future<void> _delete() async {
    if (!await _confirm('Xóa triển lãm?', 'Triển lãm chưa có tranh nào nên có thể xóa.', 'Xóa')) return;
    if (!mounted) return;
    final container = ProviderScope.containerOf(context);
    setState(() => _busy = true);
    try {
      await ref.read(exhibitionRepositoryProvider).deleteExhibition(widget.id);
      if (!mounted) return;
      _toast('Đã xóa triển lãm.');
      context.pop();
      refreshExhibitionData(container);
    } catch (e) {
      if (mounted) _handleError(e);
    }
  }

  @override
  Widget build(BuildContext context) {
    final detail = ref.watch(staffExhibitionProvider(widget.id));
    final entries = ref.watch(entriesProvider(EntryFilter(exhibitionId: widget.id)));
    return Scaffold(
      appBar: AppBar(title: const Text('Triển lãm')),
      body: AsyncValueView(
        value: detail,
        onRetry: () => ref.invalidate(staffExhibitionProvider(widget.id)),
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
            InfoRow('Tranh', '${e.entryCount} (chờ duyệt ${e.pendingCount}, đã duyệt ${e.acceptedCount}, đã bán ${e.soldCount})'),
            if (e.description != null) ...[const SectionTitle('Mô tả'), Text(e.description!)],
            if (e.status == ExhibitionStatus.cancelled && e.cancelReason != null)
              NoticeCard(text: 'Đã hủy: ${e.cancelReason}', error: true),
            if (e.soldCount > 0) const NoticeCard(text: 'Đã có tranh bán nên không thể hủy hoặc đổi ngày triển lãm.'),
            const SizedBox(height: 8),
            Wrap(spacing: 12, runSpacing: 8, children: [
              if (e.canSubmitEntries)
                FilledButton.icon(
                  onPressed: _busy ? null : () => context.push('/staff/exhibitions/${e.id}/add-entry'),
                  icon: const Icon(Icons.add_photo_alternate),
                  label: const Text('Thêm tranh'),
                ),
              if (e.canEdit)
                FilledButton.tonalIcon(
                  onPressed: _busy ? null : () => context.push('/staff/exhibitions/${e.id}/edit'),
                  icon: const Icon(Icons.edit),
                  label: const Text('Sửa'),
                ),
              if (e.canFinish)
                FilledButton.tonalIcon(
                  onPressed: _busy ? null : _finish,
                  icon: const Icon(Icons.flag),
                  label: const Text('Kết thúc triển lãm'),
                ),
              if (e.canCancel)
                FilledButton.tonalIcon(
                  onPressed: _busy ? null : _cancel,
                  icon: const Icon(Icons.cancel),
                  label: const Text('Hủy triển lãm'),
                ),
              if (e.canDelete)
                FilledButton.tonalIcon(
                  onPressed: _busy ? null : _delete,
                  icon: const Icon(Icons.delete),
                  label: const Text('Xóa'),
                ),
            ]),
            const SectionTitle('Tranh gửi triển lãm'),
            switch (entries) {
              AsyncData(:final value) => value.isEmpty
                  ? const Padding(padding: EdgeInsets.all(8), child: Text('Chưa có tranh nào.'))
                  : Column(children: [for (final it in value) EntryListCard(item: it, showExhibition: false)]),
              AsyncError(:final error) => Text(ApiException.from(error).message),
              _ => const Padding(padding: EdgeInsets.all(8), child: Center(child: CircularProgressIndicator())),
            },
          ],
        ),
      ),
    );
  }
}

// ===================== Form tạo / sửa triển lãm =====================

/// Tạo hoặc sửa triển lãm (F-M09-01). [id] null = tạo mới.
class ExhibitionFormScreen extends ConsumerWidget {
  const ExhibitionFormScreen({super.key, this.id});

  final int? id;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return Scaffold(
      appBar: AppBar(title: Text(id == null ? 'Thêm triển lãm' : 'Sửa triển lãm')),
      body: id == null
          ? const _ExhibitionForm()
          : AsyncValueView(
              value: ref.watch(staffExhibitionProvider(id!)),
              onRetry: () => ref.invalidate(staffExhibitionProvider(id!)),
              data: (e) => _ExhibitionForm(key: ValueKey('${e.id}-${e.rowVersion}'), detail: e),
            ),
    );
  }
}

class _ExhibitionForm extends ConsumerStatefulWidget {
  const _ExhibitionForm({super.key, this.detail});

  final ExhibitionDetail? detail;

  @override
  ConsumerState<_ExhibitionForm> createState() => _ExhibitionFormState();
}

class _ExhibitionFormState extends ConsumerState<_ExhibitionForm> {
  late final _name = TextEditingController(text: widget.detail?.name ?? '');
  late final _venue = TextEditingController(text: widget.detail?.venue ?? '');
  late final _city = TextEditingController(text: widget.detail?.city ?? '');
  late final _organizer = TextEditingController(text: widget.detail?.organizer ?? '');
  late final _description = TextEditingController(text: widget.detail?.description ?? '');
  late DateTime? _start = widget.detail?.startDate;
  late DateTime? _end = widget.detail?.endDate;
  bool _busy = false;

  @override
  void dispose() {
    for (final c in [_name, _venue, _city, _organizer, _description]) {
      c.dispose();
    }
    super.dispose();
  }

  void _toast(String text) => ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(text)));

  /// Kiểm tra phía client; server vẫn quyết định cuối cùng (BR-42).
  String? _validate() {
    if (_name.text.trim().isEmpty) return 'Vui lòng nhập tên triển lãm';
    if (_venue.text.trim().isEmpty) return 'Vui lòng nhập địa điểm';
    if (_city.text.trim().isEmpty) return 'Vui lòng nhập thành phố';
    if (_start == null || _end == null) return 'Vui lòng chọn ngày bắt đầu và kết thúc';
    if (_start!.isAfter(_end!)) return 'Ngày bắt đầu phải trước hoặc bằng ngày kết thúc';
    return null;
  }

  Future<void> _save() async {
    final error = _validate();
    if (error != null) {
      _toast(error);
      return;
    }
    final container = ProviderScope.containerOf(context);
    final repo = ref.read(exhibitionRepositoryProvider);
    final input = ExhibitionInput(
      name: _name.text,
      venue: _venue.text,
      city: _city.text,
      organizer: _organizer.text,
      description: _description.text,
      startDate: _start!,
      endDate: _end!,
      rowVersion: widget.detail?.rowVersion,
    );
    setState(() => _busy = true);
    try {
      final saved = widget.detail == null
          ? await repo.createExhibition(input)
          : await repo.updateExhibition(widget.detail!.id, input);
      if (!mounted) return;
      _toast('Đã lưu triển lãm.');
      if (widget.detail == null) {
        context.pushReplacement('/staff/exhibitions/${saved.id}');
      } else {
        context.pop();
      }
      refreshExhibitionData(container);
    } catch (e) {
      if (!mounted) return;
      final err = ApiException.from(e);
      _toast(err.message);
      if (err.statusCode == 409) refreshExhibitionData(container); // dữ liệu đã đổi → tải lại, form dựng lại
      setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final today = todayDate();
    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        TextField(controller: _name, maxLength: 200, decoration: const InputDecoration(labelText: 'Tên triển lãm *')),
        const SizedBox(height: 8),
        TextField(controller: _venue, maxLength: 200, decoration: const InputDecoration(labelText: 'Địa điểm *')),
        const SizedBox(height: 8),
        TextField(controller: _city, maxLength: 100, decoration: const InputDecoration(labelText: 'Thành phố (trong nước) *')),
        const SizedBox(height: 8),
        TextField(controller: _organizer, maxLength: 200, decoration: const InputDecoration(labelText: 'Ban tổ chức')),
        const SizedBox(height: 8),
        DateField(
          label: 'Ngày bắt đầu *',
          value: _start,
          firstDate: widget.detail == null ? today : DateTime(today.year - 1),
          lastDate: DateTime(today.year + 5),
          onChanged: (d) => setState(() => _start = d),
        ),
        const SizedBox(height: 12),
        DateField(
          label: 'Ngày kết thúc *',
          value: _end,
          firstDate: widget.detail == null ? today : DateTime(today.year - 1),
          lastDate: DateTime(today.year + 5),
          onChanged: (d) => setState(() => _end = d),
        ),
        const SizedBox(height: 12),
        TextField(
          controller: _description,
          maxLength: 4000,
          maxLines: 4,
          decoration: const InputDecoration(labelText: 'Mô tả', alignLabelWithHint: true),
        ),
        const SizedBox(height: 8),
        FilledButton.icon(
          onPressed: _busy ? null : _save,
          icon: _busy
              ? const SizedBox(width: 16, height: 16, child: CircularProgressIndicator(strokeWidth: 2))
              : const Icon(Icons.save),
          label: const Text('Lưu'),
        ),
      ],
    );
  }
}

// ===================== Staff thêm tranh trực tiếp =====================

/// Staff tìm bài BEST và tạo entry ACCEPTED với giá học viên đã báo, kèm nguồn giá (F-M09-05, BR-41).
class DirectEntryScreen extends ConsumerStatefulWidget {
  const DirectEntryScreen({super.key, required this.exhibitionId});

  final int exhibitionId;

  @override
  ConsumerState<DirectEntryScreen> createState() => _DirectEntryScreenState();
}

class _DirectEntryScreenState extends ConsumerState<DirectEntryScreen> {
  final _search = TextEditingController();
  final _price = TextEditingController();
  final _note = TextEditingController();
  String _q = '';
  int? _submissionId;
  bool _busy = false;

  @override
  void dispose() {
    _search.dispose();
    _price.dispose();
    _note.dispose();
    super.dispose();
  }

  void _toast(String text) => ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(text)));

  Future<void> _submit() async {
    if (_submissionId == null) return _toast('Vui lòng chọn bài');
    final priceError = validatePrice(_price.text, label: 'giá học viên đã báo');
    if (priceError != null) return _toast(priceError);
    if (_note.text.trim().isEmpty) return _toast('Vui lòng ghi nguồn giá học viên đã báo');

    final container = ProviderScope.containerOf(context);
    setState(() => _busy = true);
    try {
      final entry = await ref.read(exhibitionRepositoryProvider).createDirectEntry(widget.exhibitionId,
          submissionId: _submissionId!, askingPrice: parseMoney(_price.text)!, priceSourceNote: _note.text.trim());
      if (!mounted) return;
      _toast('Đã thêm tranh (đã duyệt).');
      context.pushReplacement('/staff/exhibition-entries/${entry.id}');
      refreshExhibitionData(container);
    } catch (e) {
      if (!mounted) return;
      _toast(ApiException.from(e).message);
      setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    // Danh sách theo từ khóa tìm; FutureBuilder đơn giản vì chỉ dùng ở màn này
    final repo = ref.read(exhibitionRepositoryProvider);
    return Scaffold(
      appBar: AppBar(title: const Text('Thêm tranh vào triển lãm')),
      body: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          TextField(
            controller: _search,
            decoration: const InputDecoration(hintText: 'Tìm bài BEST theo học viên hoặc tên tranh', prefixIcon: Icon(Icons.search)),
            onSubmitted: (v) => setState(() {
              _q = v.trim();
              _submissionId = null;
            }),
          ),
          const SizedBox(height: 8),
          FutureBuilder<List<EligibleSubmission>>(
            key: ValueKey(_q),
            future: repo.staffEligible(_q),
            builder: (context, snap) {
              if (snap.hasError) return Text(ApiException.from(snap.error!).message);
              if (!snap.hasData) return const Padding(padding: EdgeInsets.all(16), child: Center(child: CircularProgressIndicator()));
              final items = snap.data!;
              if (items.isEmpty) return const NoticeCard(text: 'Không có bài BEST nào đủ điều kiện.');
              return RadioGroup<int>(
                groupValue: _submissionId,
                onChanged: (v) => setState(() => _submissionId = v),
                child: Column(children: [
                  for (final s in items)
                    RadioListTile<int>(
                      value: s.id,
                      title: Text(s.title),
                      subtitle: Text('${s.studentName} (${s.studentCode}) • ${s.competitionTitle}'),
                    ),
                ]),
              );
            },
          ),
          const SizedBox(height: 12),
          MoneyField(controller: _price, label: 'Giá học viên đã báo (giá tối thiểu)'),
          const SizedBox(height: 12),
          TextField(
            controller: _note,
            maxLength: 500,
            decoration: const InputDecoration(labelText: 'Nguồn giá *', hintText: 'Vd: học viên báo qua điện thoại ngày …'),
          ),
          FilledButton.icon(
            onPressed: _busy ? null : _submit,
            icon: const Icon(Icons.add),
            label: const Text('Thêm tranh'),
          ),
        ],
      ),
    );
  }
}

// ===================== Hộp thoại dùng chung =====================

/// Hộp thoại nhập một lý do bắt buộc; trả về chuỗi đã cắt khoảng trắng hoặc null nếu đóng.
Future<String?> askReason(BuildContext context, {required String title, required String label, String? hint}) async {
  final controller = TextEditingController();
  final result = await showDialog<String>(
    context: context,
    builder: (context) {
      String? error;
      return StatefulBuilder(
        builder: (context, setState) => AlertDialog(
          title: Text(title),
          content: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              if (hint != null) Padding(padding: const EdgeInsets.only(bottom: 8), child: Text(hint)),
              TextField(
                controller: controller,
                maxLength: 1000,
                maxLines: 3,
                autofocus: true,
                decoration: InputDecoration(labelText: label, errorText: error),
              ),
            ],
          ),
          actions: [
            TextButton(onPressed: () => Navigator.pop(context), child: const Text('Đóng')),
            FilledButton(
              onPressed: () {
                final text = controller.text.trim();
                if (text.isEmpty) {
                  setState(() => error = 'Vui lòng nhập lý do');
                  return;
                }
                Navigator.pop(context, text);
              },
              child: const Text('Xác nhận'),
            ),
          ],
        ),
      );
    },
  );
  return result;
}
