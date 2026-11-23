import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../app/tokens.dart';
import '../../core/api_client.dart';
import '../../core/async_view.dart';
import '../../core/format.dart';
import '../../shared/shared.dart';
import 'exhibition_widgets.dart';
import 'exhibitions.dart';

/// Staff "Exhibitions" tab: exhibitions + submitted artworks (F-M09-01, 02, 09).
/// [initialView] comes from the Home cards: `pending` (proposals to review) or `unpaid`
/// (sales to pay out) opens the artworks list with that filter.
class StaffExhibitionsTab extends StatefulWidget {
  const StaffExhibitionsTab({super.key, this.initialView});

  final String? initialView;

  @override
  State<StaffExhibitionsTab> createState() => _StaffExhibitionsTabState();
}

enum _StaffTab { exhibitions, artworks }

class _StaffExhibitionsTabState extends State<StaffExhibitionsTab> {
  // The Home cards open the artworks list straight away
  late _StaffTab _tab = (widget.initialView == 'pending' || widget.initialView == 'unpaid')
      ? _StaffTab.artworks
      : _StaffTab.exhibitions;

  @override
  Widget build(BuildContext context) {
    final width = MediaQuery.sizeOf(context).width;
    final initialView = widget.initialView;
    return Column(
      children: [
        Padding(
          padding: EdgeInsets.symmetric(horizontal: AppLayout.gutterFor(width)),
          child: SegmentTabs<_StaffTab>(
            values: _StaffTab.values,
            selected: _tab,
            expand: width < AppLayout.wide,
            labelOf: (t) => switch (t) {
              _StaffTab.exhibitions => 'Exhibitions',
              _StaffTab.artworks => 'Artworks',
            },
            onSelected: (t) => setState(() => _tab = t),
          ),
        ),
        Expanded(
          child: switch (_tab) {
            _StaffTab.exhibitions => const _StaffExhibitionList(),
            _StaffTab.artworks => _StaffEntriesList(
                initialStatus: initialView == 'pending'
                    ? EntryStatus.pending
                    : (initialView == 'unpaid' ? EntryStatus.sold : null),
                initialPaid: initialView == 'unpaid' ? false : null,
              ),
          },
        ),
      ],
    );
  }
}

Widget _emptyList(String title, {String? message}) => ListView(
      physics: const AlwaysScrollableScrollPhysics(),
      children: [const SizedBox(height: AppSpacing.xl), EmptyState(title: title, message: message)],
    );

// ===================== Exhibition list =====================

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
    final width = MediaQuery.sizeOf(context).width;
    final gutter = AppLayout.gutterFor(width);
    return Column(
      children: [
        Padding(
          padding: EdgeInsets.fromLTRB(gutter, AppSpacing.lg, gutter, AppSpacing.md),
          child: Row(children: [
            Expanded(
              child: TextField(
                decoration: const InputDecoration(
                    hintText: 'Search name, venue or city', prefixIcon: Icon(Icons.search), isDense: true),
                onChanged: (v) => setState(() => _q = v.trim().toLowerCase()),
              ),
            ),
            const SizedBox(width: AppSpacing.md),
            FilledButton.icon(
              style: FilledButton.styleFrom(minimumSize: const Size(0, 46)),
              onPressed: () => context.push('/staff/exhibitions/new'),
              icon: const Icon(Icons.add, size: 18),
              label: const Text('NEW'),
            ),
          ]),
        ),
        Padding(
          padding: EdgeInsets.symmetric(horizontal: gutter),
          // No "Cancelled" filter: this system has no cancelled exhibitions
          child: FilterTabs<ExhibitionStatus?>(
            values: [null, ...ExhibitionStatus.values.where((s) => s != ExhibitionStatus.cancelled)],
            selected: _status,
            labelOf: (s) => s?.label ?? 'All',
            onSelected: (s) => setState(() => _status = s),
          ),
        ),
        const SizedBox(height: AppSpacing.lg),
        Expanded(
          child: AsyncValueView(
            value: list,
            onRetry: () => ref.invalidate(staffExhibitionsProvider),
            data: (all) {
              // Filtered in the app: there are few exhibitions and the server already computes the status
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
                    ? _emptyList('No matching exhibitions')
                    : width >= AppLayout.wide
                        // Columns on wide screens
                        ? ListView(
                            physics: const AlwaysScrollableScrollPhysics(),
                            padding: const EdgeInsets.only(bottom: AppSpacing.xl),
                            children: [
                              AdaptiveTileGrid(
                                minTileWidth: 300,
                                maxColumns: 3,
                                spacing: AppSpacing.xl,
                                children: [for (final e in items) _StaffExhibitionCard(exhibition: e)],
                              ),
                            ],
                          )
                        : ListView.separated(
                            physics: const AlwaysScrollableScrollPhysics(),
                            padding: EdgeInsets.fromLTRB(gutter, 0, gutter, AppSpacing.xl),
                            itemCount: items.length,
                            separatorBuilder: (_, _) => const SizedBox(height: AppSpacing.md),
                            itemBuilder: (context, i) => _StaffExhibitionCard(exhibition: items[i]),
                          ),
              );
            },
          ),
        ),
      ],
    );
  }
}

/// One exhibition of the staff list as a wall label: a hairline above, then name, status, venue, dates and counts.
class _StaffExhibitionCard extends StatelessWidget {
  const _StaffExhibitionCard({required this.exhibition});

  final ExhibitionSummary exhibition;

  @override
  Widget build(BuildContext context) {
    final e = exhibition;
    final text = Theme.of(context).textTheme;
    return InkWell(
      onTap: () => context.push('/staff/exhibitions/${e.id}'),
      child: DecoratedBox(
        decoration: const BoxDecoration(border: Border(top: BorderSide(color: AppColors.line))),
        child: Padding(
          padding: const EdgeInsets.symmetric(vertical: AppSpacing.md),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              StatusChip.exhibition(e.status),
              const SizedBox(height: AppSpacing.sm),
              Text(e.name, style: text.titleMedium),
              const SizedBox(height: 2),
              Text('${e.venue}, ${e.city}', style: text.bodyMedium),
              Text(dateRange(e.startDate, e.endDate), style: text.bodySmall),
              const SizedBox(height: AppSpacing.xs),
              Text(
                '${e.entryCount} ${e.entryCount == 1 ? 'artwork' : 'artworks'}'
                '${e.pendingCount > 0 ? ' · ${e.pendingCount} to review' : ''}',
                style: text.labelMedium!.copyWith(color: e.pendingCount > 0 ? AppColors.brassText : AppColors.muted),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

// ===================== Submitted artworks list =====================

class _StaffEntriesList extends ConsumerStatefulWidget {
  const _StaffEntriesList({this.initialStatus, this.initialPaid});

  final EntryStatus? initialStatus;
  final bool? initialPaid;

  @override
  ConsumerState<_StaffEntriesList> createState() => _StaffEntriesListState();
}

class _StaffEntriesListState extends ConsumerState<_StaffEntriesList> {
  late EntryStatus? _status = widget.initialStatus;
  late bool? _paid = widget.initialPaid;
  String _q = '';

  // Quick filters: (label, status, paid)
  static const _quick = <(String, EntryStatus?, bool?)>[
    ('All', null, null),
    ('Pending review', EntryStatus.pending, null),
    ('Accepted', EntryStatus.accepted, null),
    ('Sold', EntryStatus.sold, null),
    ('To pay out', EntryStatus.sold, false),
    ('Unsold', EntryStatus.unsold, null),
  ];

  @override
  Widget build(BuildContext context) {
    final filter = EntryFilter(status: _status, paid: _paid, q: _q);
    final list = ref.watch(entriesProvider(filter));
    final width = MediaQuery.sizeOf(context).width;
    final gutter = AppLayout.gutterFor(width);
    // Index of the quick filter that matches the current status / paid pair (-1 when none does)
    final current = _quick.indexWhere((q) => q.$2 == _status && q.$3 == _paid);
    return Column(
      children: [
        Padding(
          padding: EdgeInsets.fromLTRB(gutter, AppSpacing.lg, gutter, AppSpacing.md),
          child: TextField(
            decoration: const InputDecoration(
                hintText: 'Search student, code or title', prefixIcon: Icon(Icons.search), isDense: true),
            onSubmitted: (v) => setState(() => _q = v.trim()),
            onChanged: (v) {
              if (v.trim().isEmpty && _q.isNotEmpty) setState(() => _q = '');
            },
          ),
        ),
        Padding(
          padding: EdgeInsets.symmetric(horizontal: gutter),
          child: FilterTabs<int>(
            values: List.generate(_quick.length, (i) => i),
            selected: current,
            labelOf: (i) => _quick[i].$1,
            onSelected: (i) => setState(() {
              _status = _quick[i].$2;
              _paid = _quick[i].$3;
            }),
          ),
        ),
        const SizedBox(height: AppSpacing.lg),
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
                  ? _emptyList('No matching artworks')
                  : width >= AppLayout.wide
                      // Two columns of artworks on wide screens
                      ? ListView(
                          physics: const AlwaysScrollableScrollPhysics(),
                          padding: const EdgeInsets.only(bottom: AppSpacing.xl),
                          children: [
                            AdaptiveTileGrid(
                              minTileWidth: 440,
                              maxColumns: 2,
                              spacing: AppSpacing.xl,
                              children: [for (final e in items) EntryListCard(item: e)],
                            ),
                          ],
                        )
                      : ListView.builder(
                          physics: const AlwaysScrollableScrollPhysics(),
                          padding: EdgeInsets.fromLTRB(gutter, 0, gutter, AppSpacing.xl),
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

/// One submitted artwork in the staff lists: thumbnail in its frame, student, price and status.
class EntryListCard extends StatelessWidget {
  const EntryListCard({super.key, required this.item, this.showExhibition = true});

  final EntryListItem item;
  final bool showExhibition;

  @override
  Widget build(BuildContext context) {
    final e = item;
    final text = Theme.of(context).textTheme;
    // No card chrome: a hairline above each artwork, like the rows of the student's own entries
    return InkWell(
      onTap: () => context.push('/staff/exhibition-entries/${e.id}'),
      child: DecoratedBox(
        decoration: const BoxDecoration(border: Border(top: BorderSide(color: AppColors.line))),
        child: Padding(
          padding: const EdgeInsets.symmetric(vertical: AppSpacing.md),
          child: Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              SizedBox(
                width: 84,
                child: ArtworkFrame(
                  aspectRatio: 1,
                  matWidth: 5,
                  awarded: e.status == EntryStatus.sold,
                  child: e.submissionId != null && e.hasThumbnail
                      ? AuthImage(submissionId: e.submissionId!, semanticLabel: e.submissionTitle)
                      : const ImagePlaceholder(),
                ),
              ),
              const SizedBox(width: AppSpacing.md),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      e.submissionTitle,
                      maxLines: 2,
                      overflow: TextOverflow.ellipsis,
                      style: text.titleSmall!.copyWith(fontStyle: FontStyle.italic, fontWeight: FontWeight.w500),
                    ),
                    Text('${e.studentName} (${e.studentCode})', style: text.bodyMedium),
                    if (showExhibition) Text(e.exhibitionName, style: text.bodySmall),
                    Text(
                      'Asking ${formatMoney(e.askingPrice)}'
                      '${e.soldPrice != null ? ' · sold ${formatMoney(e.soldPrice!)}' : ''}',
                      style: text.bodySmall,
                    ),
                    const SizedBox(height: AppSpacing.xs),
                    StatusChip.entry(e.status, paid: e.paidToStudent),
                  ],
                ),
              ),
              const Icon(Icons.chevron_right, color: AppColors.muted),
            ],
          ),
        ),
      ),
    );
  }
}

// ===================== Exhibition detail =====================

/// Staff exhibition detail: info, actions (edit / cancel / finish / delete) and its artworks (F-M09-01, 13).
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

  // Runs a write, reports the result and refreshes the data
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
        builder: (context) => ArtDialog(
          title: Text(title),
          content: Text(message),
          actions: [
            TextButton(onPressed: () => Navigator.pop(context, false), child: const Text('CLOSE')),
            FilledButton(onPressed: () => Navigator.pop(context, true), child: Text(ok.toUpperCase())),
          ],
        ),
      ) ??
      false;

  // Cancelling needs a reason (BR-42a)
  Future<void> _cancel() async {
    final reason = await askReason(context,
        title: 'Cancel exhibition',
        label: 'Reason for cancelling *',
        hint: 'Pending and accepted artworks become "Withdrawn".');
    if (reason == null || !mounted) return;
    await _run(() => ref.read(exhibitionRepositoryProvider).cancelExhibition(widget.id, reason), 'Exhibition cancelled.');
  }

  Future<void> _finish() async {
    if (!await _confirm('Finish exhibition?', 'Accepted artworks that are not sold become "Unsold".', 'Finish')) return;
    await _run(() => ref.read(exhibitionRepositoryProvider).finishExhibition(widget.id), 'Exhibition finished.');
  }

  Future<void> _delete() async {
    if (!await _confirm('Delete exhibition?', 'This exhibition has no artworks, so it can be deleted.', 'Delete')) return;
    if (!mounted) return;
    final container = ProviderScope.containerOf(context);
    setState(() => _busy = true);
    try {
      await ref.read(exhibitionRepositoryProvider).deleteExhibition(widget.id);
      if (!mounted) return;
      _toast('Exhibition deleted.');
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
    final text = Theme.of(context).textTheme;
    return Scaffold(
      appBar: AppBar(leading: const AppBackButton(), leadingWidth: 48, titleSpacing: 0, title: const Text('Exhibition')),
      body: AsyncValueView(
        value: detail,
        onRetry: () => ref.invalidate(staffExhibitionProvider(widget.id)),
        data: (e) => ListView(
          padding: readingPadding(context, top: AppSpacing.gutter, bottom: AppSpacing.gutter),
          children: [
            Row(crossAxisAlignment: CrossAxisAlignment.start, children: [
              Expanded(child: Text(e.name, style: text.headlineSmall)),
              const SizedBox(width: AppSpacing.sm),
              StatusChip.exhibition(e.status),
            ]),
            const SizedBox(height: AppSpacing.sm),
            InfoRow('Venue', '${e.venue}, ${e.city}'),
            InfoRow('Dates', dateRange(e.startDate, e.endDate)),
            if (e.organizer != null) InfoRow('Organizer', e.organizer!),
            InfoRow('Artworks',
                '${e.entryCount} (${e.pendingCount} to review, ${e.acceptedCount} accepted, ${e.soldCount} sold)'),
            if (e.description != null) ...[
              const SectionHeader('About', padding: EdgeInsets.fromLTRB(0, AppSpacing.md, 0, AppSpacing.xs)),
              Text(e.description!),
            ],
            if (e.status == ExhibitionStatus.cancelled && e.cancelReason != null)
              NoticeCard(text: 'Cancelled: ${e.cancelReason}', error: true),
            const SizedBox(height: AppSpacing.sm),
            Wrap(spacing: AppSpacing.md, runSpacing: AppSpacing.sm, children: [
              if (e.canSubmitEntries)
                FilledButton.icon(
                  onPressed: _busy ? null : () => context.push('/staff/exhibitions/${e.id}/add-entry'),
                  icon: const Icon(Icons.add_photo_alternate),
                  label: const Text('Add artwork'),
                ),
              if (e.canEdit)
                OutlinedButton.icon(
                  onPressed: _busy ? null : () => context.push('/staff/exhibitions/${e.id}/edit'),
                  icon: const Icon(Icons.edit),
                  label: const Text('Edit'),
                ),
              if (e.canFinish)
                OutlinedButton.icon(
                  onPressed: _busy ? null : _finish,
                  icon: const Icon(Icons.flag),
                  label: const Text('Finish exhibition'),
                ),
              if (e.canCancel)
                OutlinedButton.icon(
                  onPressed: _busy ? null : _cancel,
                  icon: const Icon(Icons.cancel),
                  label: const Text('Cancel exhibition'),
                ),
              if (e.canDelete)
                TextButton.icon(
                  onPressed: _busy ? null : _delete,
                  icon: const Icon(Icons.delete_outline),
                  label: const Text('Delete'),
                  style: TextButton.styleFrom(foregroundColor: AppColors.danger),
                ),
            ]),
            const SectionHeader('Submitted artworks', padding: EdgeInsets.fromLTRB(0, AppSpacing.lg, 0, AppSpacing.sm)),
            switch (entries) {
              AsyncData(:final value) => value.isEmpty
                  ? const EmptyState(title: 'No artworks yet')
                  : Column(children: [for (final it in value) EntryListCard(item: it, showExhibition: false)]),
              AsyncError(:final error) => Text(ApiException.from(error).message),
              _ => const Padding(padding: EdgeInsets.all(AppSpacing.sm), child: Center(child: CircularProgressIndicator())),
            },
          ],
        ),
      ),
    );
  }
}

// ===================== Create / edit exhibition =====================

/// Create or edit an exhibition (F-M09-01). [id] null = create.
class ExhibitionFormScreen extends ConsumerWidget {
  const ExhibitionFormScreen({super.key, this.id});

  final int? id;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return Scaffold(
      appBar: AppBar(leading: const AppBackButton(), leadingWidth: 48, titleSpacing: 0, title: Text(id == null ? 'New exhibition' : 'Edit exhibition')),
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

  /// Client-side check; the server decides in the end (BR-42).
  String? _validate() {
    if (_name.text.trim().isEmpty) return 'Please enter the exhibition name';
    if (_venue.text.trim().isEmpty) return 'Please enter the venue';
    if (_city.text.trim().isEmpty) return 'Please enter the city';
    if (_start == null || _end == null) return 'Please choose the start and end dates';
    if (_start!.isAfter(_end!)) return 'The start date must not be after the end date';
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
      _toast('Exhibition saved.');
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
      if (err.statusCode == 409) refreshExhibitionData(container); // data changed -> reload, the form is rebuilt
      setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final today = todayDate();
    return ListView(
      padding: readingPadding(context, top: AppSpacing.gutter, bottom: AppSpacing.gutter),
      children: [
        TextField(controller: _name, maxLength: 200, decoration: const InputDecoration(labelText: 'Exhibition name *')),
        const SizedBox(height: AppSpacing.sm),
        TextField(controller: _venue, maxLength: 200, decoration: const InputDecoration(labelText: 'Venue *')),
        const SizedBox(height: AppSpacing.sm),
        TextField(controller: _city, maxLength: 100, decoration: const InputDecoration(labelText: 'City (in Vietnam) *')),
        const SizedBox(height: AppSpacing.sm),
        TextField(controller: _organizer, maxLength: 200, decoration: const InputDecoration(labelText: 'Organizer')),
        const SizedBox(height: AppSpacing.sm),
        DateField(
          label: 'Start date *',
          value: _start,
          firstDate: widget.detail == null ? today : DateTime(today.year - 1),
          lastDate: DateTime(today.year + 5),
          onChanged: (d) => setState(() => _start = d),
        ),
        const SizedBox(height: AppSpacing.md),
        DateField(
          label: 'End date *',
          value: _end,
          firstDate: widget.detail == null ? today : DateTime(today.year - 1),
          lastDate: DateTime(today.year + 5),
          onChanged: (d) => setState(() => _end = d),
        ),
        const SizedBox(height: AppSpacing.md),
        TextField(
          controller: _description,
          maxLength: 4000,
          maxLines: 4,
          decoration: const InputDecoration(labelText: 'Description', alignLabelWithHint: true),
        ),
        const SizedBox(height: AppSpacing.sm),
        FilledButton.icon(
          onPressed: _busy ? null : _save,
          icon: _busy
              ? const SizedBox(width: 16, height: 16, child: CircularProgressIndicator(strokeWidth: 2))
              : const Icon(Icons.save),
          label: const Text('Save'),
        ),
      ],
    );
  }
}

// ===================== Staff adds an artwork directly =====================

/// Staff searches BEST entries and creates an ACCEPTED entry with the price the student reported, plus the price source (F-M09-05, BR-41).
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
    if (_submissionId == null) return _toast('Please choose an artwork');
    final priceError = validatePrice(_price.text, label: 'the price reported by the student');
    if (priceError != null) return _toast(priceError);
    if (_note.text.trim().isEmpty) return _toast('Please note where the student reported the price');

    final container = ProviderScope.containerOf(context);
    setState(() => _busy = true);
    try {
      final entry = await ref.read(exhibitionRepositoryProvider).createDirectEntry(widget.exhibitionId,
          submissionId: _submissionId!, askingPrice: parseMoney(_price.text)!, priceSourceNote: _note.text.trim());
      if (!mounted) return;
      _toast('Artwork added (accepted).');
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
    // The list depends on the search text; a plain FutureBuilder is enough since only this screen uses it
    final repo = ref.read(exhibitionRepositoryProvider);
    return Scaffold(
      appBar: AppBar(leading: const AppBackButton(), leadingWidth: 48, titleSpacing: 0, title: const Text('Add artwork')),
      body: ListView(
        padding: readingPadding(context, top: AppSpacing.gutter, bottom: AppSpacing.gutter),
        children: [
          TextField(
            controller: _search,
            decoration: const InputDecoration(
                hintText: 'Search BEST entries by student or title', prefixIcon: Icon(Icons.search)),
            onSubmitted: (v) => setState(() {
              _q = v.trim();
              _submissionId = null;
            }),
          ),
          const SizedBox(height: AppSpacing.sm),
          FutureBuilder<List<EligibleSubmission>>(
            key: ValueKey(_q),
            future: repo.staffEligible(_q),
            builder: (context, snap) {
              if (snap.hasError) return Text(ApiException.from(snap.error!).message);
              if (!snap.hasData) {
                return const Padding(padding: EdgeInsets.all(AppSpacing.md), child: Center(child: CircularProgressIndicator()));
              }
              final items = snap.data!;
              if (items.isEmpty) return const NoticeCard(text: 'No eligible BEST entries found.');
              return RadioGroup<int>(
                groupValue: _submissionId,
                onChanged: (v) => setState(() => _submissionId = v),
                child: Column(children: [
                  for (final s in items)
                    RadioListTile<int>(
                      contentPadding: EdgeInsets.zero,
                      value: s.id,
                      title: Text(s.title, maxLines: 2, overflow: TextOverflow.ellipsis),
                      subtitle: Text('${s.studentName} (${s.studentCode}) · ${s.competitionTitle}'),
                      secondary: SizedBox(
                        width: 56,
                        height: 56,
                        child: ArtworkFrame(
                          aspectRatio: null,
                          matWidth: 3,
                          child: s.hasThumbnail
                              ? AuthImage(submissionId: s.id, semanticLabel: s.title)
                              : const ImagePlaceholder(),
                        ),
                      ),
                    ),
                ]),
              );
            },
          ),
          const SizedBox(height: AppSpacing.md),
          MoneyField(controller: _price, label: 'Price reported by the student (minimum price)'),
          const SizedBox(height: AppSpacing.md),
          TextField(
            controller: _note,
            maxLength: 500,
            decoration: const InputDecoration(labelText: 'Price source *', hintText: 'e.g. reported by phone on …'),
          ),
          FilledButton.icon(
            onPressed: _busy ? null : _submit,
            icon: const Icon(Icons.add),
            label: const Text('Add artwork'),
          ),
        ],
      ),
    );
  }
}

// ===================== Shared dialog =====================

/// Dialog asking for one mandatory reason; returns the trimmed text or null when closed.
Future<String?> askReason(BuildContext context, {required String title, required String label, String? hint}) async {
  final controller = TextEditingController();
  final result = await showDialog<String>(
    context: context,
    builder: (context) {
      String? error;
      return StatefulBuilder(
        builder: (context, setState) => ArtDialog(
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
            TextButton(onPressed: () => Navigator.pop(context), child: const Text('CLOSE')),
            FilledButton(
              onPressed: () {
                final text = controller.text.trim();
                if (text.isEmpty) {
                  setState(() => error = 'Please enter a reason');
                  return;
                }
                Navigator.pop(context, text);
              },
              child: const Text('CONFIRM'),
            ),
          ],
        ),
      );
    },
  );
  return result;
}
