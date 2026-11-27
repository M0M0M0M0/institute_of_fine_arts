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

enum _ExhibitionTab { exhibitions, proposals }

/// M-S6 Exhibitions tab for students: upcoming exhibitions + my proposals (F-M09-02, 03, 10).
class StudentExhibitionsTab extends StatefulWidget {
  const StudentExhibitionsTab({super.key});

  @override
  State<StudentExhibitionsTab> createState() => _StudentExhibitionsTabState();
}

class _StudentExhibitionsTabState extends State<StudentExhibitionsTab> {
  _ExhibitionTab _tab = _ExhibitionTab.exhibitions;

  @override
  Widget build(BuildContext context) {
    final width = MediaQuery.sizeOf(context).width;
    return Column(
      children: [
        Padding(
          padding: EdgeInsets.symmetric(horizontal: AppLayout.gutterFor(width)),
          child: SegmentTabs<_ExhibitionTab>(
            values: _ExhibitionTab.values,
            selected: _tab,
            expand: width < AppLayout.wide,
            labelOf: (t) => switch (t) {
              _ExhibitionTab.exhibitions => 'Exhibitions',
              _ExhibitionTab.proposals => 'My proposals',
            },
            onSelected: (t) => setState(() => _tab = t),
          ),
        ),
        const SizedBox(height: AppSpacing.md),
        Expanded(
          child: switch (_tab) {
            _ExhibitionTab.exhibitions => const _ExhibitionList(),
            _ExhibitionTab.proposals => const _MyEntriesList(),
          },
        ),
      ],
    );
  }
}

/// Empty list that can still be pulled to refresh.
Widget _emptyList(String title, {String? message, IconData icon = Icons.museum_outlined}) => ListView(
      physics: const AlwaysScrollableScrollPhysics(),
      children: [
        const SizedBox(height: AppSpacing.xxl),
        EmptyState(title: title, message: message, icon: icon),
      ],
    );

class _ExhibitionList extends ConsumerWidget {
  const _ExhibitionList();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final list = ref.watch(studentExhibitionsProvider);
    final width = MediaQuery.sizeOf(context).width;
    final gutter = AppLayout.gutterFor(width);
    return AsyncValueView(
      value: list,
      onRetry: () => ref.invalidate(studentExhibitionsProvider),
      placeholder: Placeholders.exhibitions,
      data: (items) => RefreshIndicator(
        onRefresh: () async {
          ref.invalidate(studentExhibitionsProvider);
          await ref.read(studentExhibitionsProvider.future);
        },
        child: items.isEmpty
            ? _emptyList('No upcoming exhibitions', message: 'Check back soon.')
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
                        children: [for (final e in items) _ExhibitionCard(exhibition: e)],
                      ),
                    ],
                  )
                : ListView.separated(
                    physics: const AlwaysScrollableScrollPhysics(),
                    padding: EdgeInsets.fromLTRB(gutter, 0, gutter, AppSpacing.xl),
                    itemCount: items.length,
                    separatorBuilder: (_, _) => const SizedBox(height: AppSpacing.md),
                    itemBuilder: (context, i) => StaggerIn(index: i, child: _ExhibitionCard(exhibition: items[i])),
                  ),
      ),
    );
  }
}

/// One exhibition as a wall label: a hairline above, then status, name, venue, dates and the countdown.
class _ExhibitionCard extends StatelessWidget {
  const _ExhibitionCard({required this.exhibition});

  final ExhibitionSummary exhibition;

  @override
  Widget build(BuildContext context) {
    final e = exhibition;
    final text = Theme.of(context).textTheme;
    return Pressable(
      pressedScale: 0.98,
      onTap: () => context.push('/student/exhibitions/${e.id}'),
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
              Text('${e.venue} – ${e.city}', style: text.bodyMedium),
              Text(dateRange(e.startDate, e.endDate), style: text.bodySmall),
              if (e.status == ExhibitionStatus.upcoming)
                Padding(
                  padding: const EdgeInsets.only(top: AppSpacing.xs),
                  child: Text(
                    e.daysUntilStart <= 0
                        ? 'Starts today'
                        : 'Starts in ${e.daysUntilStart} ${e.daysUntilStart == 1 ? 'day' : 'days'}',
                    style: text.labelMedium!.copyWith(color: AppColors.brassText),
                  ),
                ),
            ],
          ),
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
            ? _emptyList(
                'No proposals yet',
                message: 'Entries rated Best can be proposed from an exhibition page.',
                icon: Icons.image_outlined,
              )
            : ListView.separated(
                physics: const AlwaysScrollableScrollPhysics(),
                padding: EdgeInsets.all(AppLayout.gutterFor(MediaQuery.sizeOf(context).width)),
                itemCount: items.length,
                separatorBuilder: (_, _) => const Divider(height: 1),
                itemBuilder: (context, i) => _EntryRow(entry: items[i]),
              ),
      ),
    );
  }
}

/// Framed thumbnail of a proposed work (stone placeholder when the id is unknown).
Widget _entryThumb(StudentEntry e, {double matWidth = 5}) => ArtworkFrame(
      aspectRatio: 1,
      matWidth: matWidth,
      awarded: e.status == EntryStatus.sold,
      child: (e.submissionId != null && e.hasThumbnail)
          ? AuthImage(submissionId: e.submissionId!, semanticLabel: e.submissionTitle)
          : const ImagePlaceholder(),
    );

class _EntryRow extends StatelessWidget {
  const _EntryRow({required this.entry});

  final StudentEntry entry;

  @override
  Widget build(BuildContext context) {
    final e = entry;
    final text = Theme.of(context).textTheme;
    return Pressable(
      pressedScale: 0.98,
      onTap: () => context.push('/student/exhibition-entries/${e.id}'),
      child: Padding(
        padding: const EdgeInsets.symmetric(vertical: AppSpacing.sm),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            SizedBox(width: 88, child: _entryThumb(e)),
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
                  Text(e.exhibitionName, maxLines: 1, overflow: TextOverflow.ellipsis, style: text.bodyMedium),
                  const SizedBox(height: AppSpacing.xs),
                  StatusChip.entry(e.status, paid: e.paidToStudent),
                  const SizedBox(height: AppSpacing.xs),
                  Text('Asking price ${formatMoney(e.askingPrice)}', style: text.bodySmall),
                  if (e.soldPrice != null) Text('Sold for ${formatMoney(e.soldPrice!)}', style: text.bodySmall),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

// ===================== Exhibition detail =====================

/// One exhibition for a student, with "Propose a work" while it is still upcoming (UC-07 step 1).
class StudentExhibitionDetailScreen extends ConsumerWidget {
  const StudentExhibitionDetailScreen({super.key, required this.id});

  final int id;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final detail = ref.watch(studentExhibitionProvider(id));
    final text = Theme.of(context).textTheme;
    return Scaffold(
      appBar: AppBar(leading: const AppBackButton(), leadingWidth: 48, titleSpacing: 0, title: const Text('Exhibition')),
      body: AsyncValueView(
        value: detail,
        onRetry: () => ref.invalidate(studentExhibitionProvider(id)),
        data: (e) => ListView(
          padding: readingPadding(context, top: AppSpacing.gutter, bottom: AppSpacing.gutter),
          children: [
            Align(alignment: Alignment.centerLeft, child: StatusChip.exhibition(e.status)),
            const SizedBox(height: AppSpacing.sm),
            Text(e.name, style: text.headlineSmall),
            const SizedBox(height: AppSpacing.md),
            InfoRow('Venue', '${e.venue}, ${e.city}'),
            InfoRow('Dates', dateRange(e.startDate, e.endDate)),
            if (e.organizer != null) InfoRow('Organizer', e.organizer!),
            if (e.description != null) ...[
              const SectionHeader('About', padding: EdgeInsets.only(top: AppSpacing.lg, bottom: AppSpacing.xs)),
              RichTextView(e.description!),
            ],
            if (e.status == ExhibitionStatus.cancelled && e.cancelReason != null)
              NoticeCard(text: 'This exhibition was cancelled: ${e.cancelReason}', error: true),
            const SizedBox(height: AppSpacing.md),
            if (e.canSubmitEntries)
              FilledButton.icon(
                onPressed: () => context.push('/student/exhibitions/${e.id}/propose'),
                icon: const Icon(Icons.send_outlined),
                label: const Text('Propose a work'),
              ),
          ],
        ),
      ),
    );
  }
}

// ===================== Propose a work =====================

/// Pick an eligible BEST entry and enter the minimum price -> entry PENDING (F-M09-04, BR-40, BR-41).
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
      _toast('Please choose the entry you want to propose');
      return;
    }
    final error = validatePrice(_price.text, label: 'an asking price');
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
      _toast('Proposal sent. A teacher will review it.');
      context.pushReplacement('/student/exhibition-entries/${entry.id}');
      refreshExhibitionData(container);
    } catch (e) {
      if (!mounted) return;
      final err = ApiException.from(e);
      _toast(err.message);
      // 409 "already proposed": reload the eligible entries
      if (err.statusCode == 409) ref.invalidate(studentEligibleProvider);
      setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final eligible = ref.watch(studentEligibleProvider);
    final text = Theme.of(context).textTheme;
    return Scaffold(
      appBar: AppBar(leading: const AppBackButton(), leadingWidth: 48, titleSpacing: 0, title: const Text('Propose a work')),
      body: AsyncValueView(
        value: eligible,
        onRetry: () => ref.invalidate(studentEligibleProvider),
        data: (items) => ListView(
          padding: readingPadding(context, top: AppSpacing.gutter, bottom: AppSpacing.gutter),
          children: [
            Text('Choose an entry', style: text.titleMedium),
            Text('Only entries rated Best in a published competition can be proposed.', style: text.bodySmall),
            const SizedBox(height: AppSpacing.sm),
            if (items.isEmpty)
              const NoticeCard(text: 'You have no eligible entries (or they were already proposed or sold).')
            else
              RadioGroup<int>(
                groupValue: _submissionId,
                onChanged: (v) => setState(() => _submissionId = v),
                child: Column(
                  children: [
                    for (final s in items)
                      RadioListTile<int>(
                        value: s.id,
                        contentPadding: EdgeInsets.zero,
                        secondary: SizedBox(
                          width: 64,
                          child: ArtworkFrame(
                            aspectRatio: 1,
                            matWidth: 3,
                            child: s.hasThumbnail
                                ? AuthImage(submissionId: s.id, semanticLabel: s.title)
                                : const ImagePlaceholder(),
                          ),
                        ),
                        title: Text(s.title, style: const TextStyle(fontStyle: FontStyle.italic)),
                        subtitle: Text(s.competitionTitle),
                      ),
                  ],
                ),
              ),
            const SizedBox(height: AppSpacing.md),
            MoneyField(controller: _price, label: 'Asking price (the minimum you accept)'),
            const SizedBox(height: AppSpacing.xs),
            Text('If the work sells below this price, staff need your consent.', style: text.bodySmall),
            const SizedBox(height: AppSpacing.md),
            FilledButton.icon(
              onPressed: (_busy || items.isEmpty) ? null : _submit,
              icon: _busy
                  ? const SizedBox(width: 16, height: 16, child: CircularProgressIndicator(strokeWidth: 2))
                  : const Icon(Icons.send_outlined),
              label: const Text('Send proposal'),
            ),
          ],
        ),
      ),
    );
  }
}

// ===================== Proposal detail =====================

/// A proposal: asking price, sold?, sale price, paid? (F-M09-10); edit price / withdraw while PENDING.
class StudentEntryDetailScreen extends ConsumerWidget {
  const StudentEntryDetailScreen({super.key, required this.id});

  final int id;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final detail = ref.watch(myEntryProvider(id));
    return Scaffold(
      appBar: AppBar(leading: const AppBackButton(), leadingWidth: 48, titleSpacing: 0, title: const Text('Proposal')),
      body: AsyncValueView(
        value: detail,
        onRetry: () => ref.invalidate(myEntryProvider(id)),
        // Keyed by RowVersion: after a conflict reload the form is rebuilt with the new price
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

  // 409 = staff just handled the proposal (BR-52): say so and reload the latest version
  void _handleError(Object e) {
    final err = ApiException.from(e);
    _toast(err.message);
    if (err.statusCode == 409) ref.invalidate(myEntryProvider(_e.id));
    setState(() => _busy = false);
  }

  Future<void> _savePrice() async {
    final error = validatePrice(_price.text, label: 'an asking price');
    if (error != null) {
      _toast(error);
      return;
    }
    final container = ProviderScope.containerOf(context);
    setState(() => _busy = true);
    try {
      await ref.read(exhibitionRepositoryProvider).updatePrice(_e.id, parseMoney(_price.text)!, _e.rowVersion);
      if (!mounted) return;
      _toast('Asking price updated.');
      refreshExhibitionData(container);
    } catch (e) {
      if (mounted) _handleError(e);
    }
  }

  Future<void> _withdraw() async {
    final ok = await showDialog<bool>(
      context: context,
      builder: (context) => ArtDialog(
        title: const Text('Withdraw this proposal?'),
        content: const Text('The proposal will be removed. You can propose the work again later.'),
        actions: [
          TextButton(onPressed: () => Navigator.pop(context, false), child: const Text('KEEP IT')),
          FilledButton(onPressed: () => Navigator.pop(context, true), child: const Text('WITHDRAW')),
        ],
      ),
    );
    if (ok != true || !mounted) return;
    final container = ProviderScope.containerOf(context);
    setState(() => _busy = true);
    try {
      await ref.read(exhibitionRepositoryProvider).withdraw(_e.id);
      if (!mounted) return;
      _toast('Proposal withdrawn.');
      context.pop();
      refreshExhibitionData(container);
    } catch (e) {
      if (mounted) _handleError(e);
    }
  }

  @override
  Widget build(BuildContext context) {
    final e = _e;
    final text = Theme.of(context).textTheme;
    return ListView(
      padding: const EdgeInsets.all(AppSpacing.gutter),
      children: [
        if (e.submissionId != null && e.hasThumbnail) ...[
          Center(
            child: SizedBox(
              width: 260,
              child: GestureDetector(
                onTap: () => ArtworkViewer.open(
                  context,
                  ArtworkViewer.private(submissionId: e.submissionId!, title: e.submissionTitle, subtitle: e.competitionTitle),
                ),
                child: ArtworkFrame(
                  awarded: e.status == EntryStatus.sold,
                  aspectRatio: 1,
                  child: AuthImage(
                    submissionId: e.submissionId!,
                    thumb: false,
                    fit: BoxFit.contain,
                    semanticLabel: e.submissionTitle,
                  ),
                ),
              ),
            ),
          ),
          const SizedBox(height: AppSpacing.md),
        ],
        Text(e.submissionTitle, style: text.headlineSmall!.copyWith(fontStyle: FontStyle.italic)),
        const SizedBox(height: AppSpacing.sm),
        Align(alignment: Alignment.centerLeft, child: StatusChip.entry(e.status, paid: e.paidToStudent)),
        const SizedBox(height: AppSpacing.md),
        InfoRow('Competition', e.competitionTitle),
        InfoRow('Exhibition', e.exhibitionName),
        InfoRow('Venue', '${e.venue}, ${e.city}'),
        InfoRow('Dates', dateRange(e.exhibitionStartDate, e.exhibitionEndDate)),
        const Divider(height: 24),
        InfoRow('Asking price', formatMoney(e.askingPrice)),
        if (e.status == EntryStatus.sold) ...[
          InfoRow('Sold for', formatMoney(e.soldPrice ?? 0)),
          if (e.soldDate != null) InfoRow('Sold on', formatDate(e.soldDate!)),
          InfoRow(
            'Payment',
            e.paidToStudent
                ? 'Received ${formatMoney(e.paidAmount ?? 0)}${e.paidDate != null ? ' on ${formatDate(e.paidDate!)}' : ''}'
                : 'Not received yet',
          ),
        ],
        if (e.statusReason != null &&
            (e.status == EntryStatus.rejected || e.status == EntryStatus.withdrawn || e.status == EntryStatus.unsold))
          NoticeCard(text: e.statusReason!, error: e.status != EntryStatus.unsold),
        if (e.status == EntryStatus.pending)
          const NoticeCard(text: 'Waiting for a teacher to review. You can change the price or withdraw.'),
        if (e.canEdit) ...[
          const Divider(height: 24),
          MoneyField(controller: _price, label: 'Asking price', enabled: !_busy),
          const SizedBox(height: AppSpacing.md),
          Wrap(spacing: AppSpacing.sm, runSpacing: AppSpacing.sm, children: [
            FilledButton.icon(
              onPressed: _busy ? null : _savePrice,
              icon: const Icon(Icons.save_outlined),
              label: const Text('Save price'),
            ),
            OutlinedButton.icon(
              onPressed: _busy ? null : _withdraw,
              icon: const Icon(Icons.undo),
              label: const Text('Withdraw'),
            ),
          ]),
        ],
      ],
    );
  }
}
