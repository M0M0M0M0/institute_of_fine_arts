import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/api_client.dart';
import '../../core/async_view.dart';
import '../../core/format.dart';
import '../competitions/competition_widgets.dart';
import 'exhibition_widgets.dart';
import 'exhibitions.dart';
import 'staff_exhibition_screens.dart' show askReason;

/// Chi tiết một tranh gửi triển lãm cho Staff: duyệt, ghi nhận bán, điều chỉnh, trả tiền (UC-07, bảng 4.8).
class StaffEntryDetailScreen extends ConsumerWidget {
  const StaffEntryDetailScreen({super.key, required this.id});

  final int id;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final detail = ref.watch(entryDetailProvider(id));
    return Scaffold(
      appBar: AppBar(title: const Text('Tranh gửi triển lãm')),
      body: AsyncValueView(
        value: detail,
        onRetry: () => ref.invalidate(entryDetailProvider(id)),
        data: (d) => _Body(key: ValueKey('${d.id}-${d.rowVersion}'), entry: d),
      ),
    );
  }
}

class _Body extends ConsumerStatefulWidget {
  const _Body({super.key, required this.entry});

  final EntryDetail entry;

  @override
  ConsumerState<_Body> createState() => _BodyState();
}

class _BodyState extends ConsumerState<_Body> {
  bool _busy = false;

  EntryDetail get _e => widget.entry;

  void _toast(String text) => ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(text)));

  // 409 = dữ liệu đã đổi hoặc thao tác trùng (BR-52): báo rõ và tải lại bản mới nhất
  void _handleError(Object e) {
    final err = ApiException.from(e);
    _toast(err.message);
    if (err.statusCode == 409) refreshExhibitionData(ProviderScope.containerOf(context));
    setState(() => _busy = false);
  }

  // Chạy một thao tác ghi: khóa nút, báo kết quả, làm mới dữ liệu (form dựng lại theo RowVersion mới)
  Future<void> _run(Future<void> Function() action, String done) async {
    final container = ProviderScope.containerOf(context);
    setState(() => _busy = true);
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

  Future<void> _accept() => _run(
      () => ref.read(exhibitionRepositoryProvider).review(_e.id, accept: true), 'Đã duyệt đề xuất.');

  Future<void> _reject() async {
    final reason = await askReason(context, title: 'Từ chối đề xuất', label: 'Lý do từ chối *');
    if (reason == null || !mounted) return;
    await _run(() => ref.read(exhibitionRepositoryProvider).review(_e.id, accept: false, reason: reason), 'Đã từ chối đề xuất.');
  }

  Future<void> _sell() async {
    final body = await showDialog<Map<String, dynamic>>(
      context: context,
      builder: (context) => _SaleDialog(entry: _e, adjust: false),
    );
    if (body == null || !mounted) return;
    body['rowVersion'] = _e.rowVersion;
    await _run(() => ref.read(exhibitionRepositoryProvider).sell(_e.id, body), 'Đã ghi nhận bán.');
  }

  Future<void> _adjust() async {
    final body = await showDialog<Map<String, dynamic>>(
      context: context,
      builder: (context) => _e.adjustLimitedToPayment ? _PaymentNoteDialog(entry: _e) : _SaleDialog(entry: _e, adjust: true),
    );
    if (body == null || !mounted) return;
    body['rowVersion'] = _e.rowVersion;
    await _run(() => ref.read(exhibitionRepositoryProvider).adjustSale(_e.id, body), 'Đã điều chỉnh giao dịch.');
  }

  Future<void> _pay() async {
    final body = await showDialog<Map<String, dynamic>>(
      context: context,
      builder: (context) => _PayDialog(entry: _e),
    );
    if (body == null || !mounted) return;
    body['rowVersion'] = _e.rowVersion;
    await _run(() => ref.read(exhibitionRepositoryProvider).pay(_e.id, body), 'Đã ghi nhận trả tiền cho học viên.');
  }

  @override
  Widget build(BuildContext context) {
    final e = _e;
    final textTheme = Theme.of(context).textTheme;
    final scheme = Theme.of(context).colorScheme;
    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        Row(children: [
          Expanded(child: Text(e.submissionTitle, style: textTheme.headlineSmall)),
          EntryStatusChip(e.status, paid: e.paidToStudent),
        ]),
        const SizedBox(height: 8),
        InfoRow('Học viên', '${e.studentName} (${e.studentCode})'),
        InfoRow('Cuộc thi', e.competitionTitle),
        InfoRow('Triển lãm', e.exhibitionName),
        InfoRow('Thời gian', dateRange(e.exhibitionStartDate, e.exhibitionEndDate)),
        InfoRow('Trạng thái TL', e.exhibitionStatus.label),
        const SectionTitle('Giá'),
        InfoRow('Giá đề xuất', formatVnd(e.askingPrice)),
        if (e.priceSourceNote != null) InfoRow('Nguồn giá', e.priceSourceNote!),
        if (e.statusReason != null) NoticeCard(text: e.statusReason!, error: e.status == EntryStatus.rejected || e.status == EntryStatus.withdrawn),
        if (e.status == EntryStatus.sold) ...[
          const SectionTitle('Bán'),
          InfoRow('Giá bán', formatVnd(e.soldPrice ?? 0)),
          if (e.soldDate != null) InfoRow('Ngày bán', formatDate(e.soldDate!)),
          InfoRow('Khách mua', e.customerName ?? '—'),
          if (e.consentDate != null)
            InfoRow('Xác nhận của HV',
                '${formatDate(e.consentDate!)} – ${e.consentMethod ?? ''}${e.consentNote != null ? '\n${e.consentNote}' : ''}'),
          const SectionTitle('Trả tiền cho học viên'),
          InfoRow('Phải trả', formatVnd(e.soldPrice ?? 0)),
          if (e.paidToStudent) ...[
            InfoRow('Đã trả', formatVnd(e.paidAmount ?? 0)),
            if (e.paidDate != null) InfoRow('Ngày trả', formatDate(e.paidDate!)),
            InfoRow('Hình thức', e.paymentMethod ?? ''),
            if (e.paymentReference != null) InfoRow('Chứng từ', e.paymentReference!),
          ] else ...[
            if (e.payDueDate != null) InfoRow('Hạn trả', formatDate(e.payDueDate!)),
            if (e.overdueDays > 0)
              Padding(
                padding: const EdgeInsets.only(top: 4),
                child: Text('Quá hạn ${e.overdueDays} ngày', style: TextStyle(color: scheme.error, fontWeight: FontWeight.w600)),
              ),
            if (e.overdueDays == 0) const InfoRow('Trạng thái', 'Chưa trả tiền'),
          ],
        ],
        const SizedBox(height: 16),
        Wrap(spacing: 12, runSpacing: 8, children: [
          if (e.canReview) ...[
            FilledButton.icon(onPressed: _busy ? null : _accept, icon: const Icon(Icons.check), label: const Text('Duyệt')),
            FilledButton.tonalIcon(onPressed: _busy ? null : _reject, icon: const Icon(Icons.close), label: const Text('Từ chối')),
          ],
          if (e.canSell)
            FilledButton.icon(
              onPressed: _busy ? null : _sell,
              icon: const Icon(Icons.sell),
              label: Text(e.isLateSale ? 'Ghi nhận bán muộn' : 'Đã bán'),
            ),
          if (e.canPay)
            FilledButton.icon(onPressed: _busy ? null : _pay, icon: const Icon(Icons.payments), label: const Text('Đã trả tiền')),
          if (e.canAdjustSale)
            FilledButton.tonalIcon(
              onPressed: _busy ? null : _adjust,
              icon: const Icon(Icons.edit_note),
              label: Text(e.adjustLimitedToPayment ? 'Sửa chứng từ' : 'Điều chỉnh giao dịch'),
            ),
        ]),
        // Giải thích vì sao chưa có nút bán
        if (e.status == EntryStatus.accepted && !e.canSell)
          const Padding(
            padding: EdgeInsets.only(top: 8),
            child: Text('Chỉ ghi nhận bán khi triển lãm đã bắt đầu.'),
          ),
        if (e.status == EntryStatus.pending && !e.canReview)
          const Padding(
            padding: EdgeInsets.only(top: 8),
            child: Text('Triển lãm đã bắt đầu nên không còn duyệt được.'),
          ),
      ],
    );
  }
}

// ===================== Hộp thoại ghi nhận bán / điều chỉnh bán =====================

const _consentMethods = ['Tin nhắn', 'Email', 'Ký tên', 'Khác'];
const _newCustomer = -1; // giá trị đặc biệt trong danh sách khách: tạo khách mới

/// Ghi nhận bán (adjust = false) hoặc điều chỉnh giao dịch bán chưa trả tiền (adjust = true) (BR-44, BR-49).
class _SaleDialog extends ConsumerStatefulWidget {
  const _SaleDialog({required this.entry, required this.adjust});

  final EntryDetail entry;
  final bool adjust;

  @override
  ConsumerState<_SaleDialog> createState() => _SaleDialogState();
}

class _SaleDialogState extends ConsumerState<_SaleDialog> {
  late final _price = TextEditingController(text: (e.soldPrice ?? e.askingPrice).round().toString());
  final _custName = TextEditingController();
  final _custPhone = TextEditingController();
  final _custEmail = TextEditingController();
  final _consentNote = TextEditingController();
  final _reason = TextEditingController(); // lý do bán muộn hoặc lý do điều chỉnh
  late DateTime _soldDate = _initialDate();
  late DateTime? _consentDate = e.consentDate;
  late String? _consentMethod = e.consentMethod;
  late int? _customerId = e.customerId;
  String? _error;

  EntryDetail get e => widget.entry;

  // Ngày bán nằm trong khoảng triển lãm và không ở tương lai (BR-44)
  DateTime get _lastDate {
    final today = todayDate();
    return e.exhibitionEndDate.isBefore(today) ? e.exhibitionEndDate : today;
  }

  DateTime _initialDate() {
    if (e.soldDate != null) return e.soldDate!;
    final last = _lastDate;
    return last.isBefore(e.exhibitionStartDate) ? e.exhibitionStartDate : last;
  }

  @override
  void dispose() {
    for (final c in [_price, _custName, _custPhone, _custEmail, _consentNote, _reason]) {
      c.dispose();
    }
    super.dispose();
  }

  bool get _below {
    final p = parseMoney(_price.text);
    return p != null && needsConsent(p, e.askingPrice);
  }

  void _submit() {
    final price = parseMoney(_price.text);
    if (price == null) return setState(() => _error = 'Vui lòng nhập giá bán lớn hơn 0');
    if (_customerId == null) return setState(() => _error = 'Vui lòng chọn khách mua');
    if (_customerId == _newCustomer && _custName.text.trim().isEmpty) {
      return setState(() => _error = 'Vui lòng nhập họ tên khách mới');
    }
    if (_below && (_consentDate == null || _consentMethod == null)) {
      return setState(() => _error = 'Giá thấp hơn giá đề xuất: cần xác nhận của học viên (ngày và hình thức)');
    }
    if ((widget.adjust || e.isLateSale) && _reason.text.trim().isEmpty) {
      return setState(() => _error = widget.adjust ? 'Vui lòng nhập lý do điều chỉnh' : 'Bán muộn: vui lòng nhập lý do');
    }
    final body = <String, dynamic>{
      'soldPrice': price,
      'soldDate': apiDate(_soldDate),
      if (_customerId == _newCustomer)
        'newCustomer': {
          'fullName': _custName.text.trim(),
          'phone': _custPhone.text.trim(),
          'email': _custEmail.text.trim(),
        }
      else
        'customerId': _customerId,
      if (_below) ...{
        'consentDate': apiDate(_consentDate!),
        'consentMethod': _consentMethod,
        'consentNote': _consentNote.text.trim(),
      },
      if (widget.adjust) 'reason': _reason.text.trim() else if (e.isLateSale) 'lateReason': _reason.text.trim(),
    };
    Navigator.pop(context, body);
  }

  @override
  Widget build(BuildContext context) {
    final customers = ref.watch(_customersProvider);
    return AlertDialog(
      title: Text(widget.adjust ? 'Điều chỉnh giao dịch bán' : (e.isLateSale ? 'Ghi nhận bán muộn' : 'Ghi nhận bán')),
      content: SizedBox(
        width: 420,
        child: SingleChildScrollView(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text('Giá đề xuất: ${formatVnd(e.askingPrice)}'),
              const SizedBox(height: 8),
              MoneyField(controller: _price, label: 'Giá bán *'),
              const SizedBox(height: 12),
              DateField(
                label: 'Ngày bán *',
                value: _soldDate,
                firstDate: e.exhibitionStartDate,
                lastDate: _lastDate.isBefore(e.exhibitionStartDate) ? e.exhibitionStartDate : _lastDate,
                onChanged: (d) => setState(() => _soldDate = d),
              ),
              const SizedBox(height: 12),
              switch (customers) {
                AsyncData(:final value) => DropdownButtonFormField<int>(
                    initialValue: value.any((c) => c.id == _customerId) || _customerId == _newCustomer ? _customerId : null,
                    isExpanded: true,
                    decoration: const InputDecoration(labelText: 'Khách mua *'),
                    items: [
                      for (final c in value) DropdownMenuItem(value: c.id, child: Text(c.fullName, overflow: TextOverflow.ellipsis)),
                      if (!widget.adjust) const DropdownMenuItem(value: _newCustomer, child: Text('+ Khách mới')),
                    ],
                    onChanged: (v) => setState(() => _customerId = v),
                  ),
                AsyncError(:final error) => Text(ApiException.from(error).message),
                _ => const LinearProgressIndicator(),
              },
              if (_customerId == _newCustomer) ...[
                const SizedBox(height: 8),
                TextField(controller: _custName, decoration: const InputDecoration(labelText: 'Họ tên khách *')),
                const SizedBox(height: 8),
                TextField(controller: _custPhone, keyboardType: TextInputType.phone, decoration: const InputDecoration(labelText: 'Số điện thoại')),
                const SizedBox(height: 8),
                TextField(controller: _custEmail, keyboardType: TextInputType.emailAddress, decoration: const InputDecoration(labelText: 'Email')),
              ],
              if (_below) ...[
                const Divider(height: 24),
                Text('Giá thấp hơn giá đề xuất – cần xác nhận của học viên (BR-44)',
                    style: TextStyle(color: Theme.of(context).colorScheme.error)),
                const SizedBox(height: 8),
                DateField(
                  label: 'Ngày học viên xác nhận *',
                  value: _consentDate,
                  firstDate: DateTime(todayDate().year - 1),
                  lastDate: todayDate(),
                  onChanged: (d) => setState(() => _consentDate = d),
                ),
                const SizedBox(height: 12),
                DropdownButtonFormField<String>(
                  initialValue: _consentMethod,
                  decoration: const InputDecoration(labelText: 'Hình thức xác nhận *'),
                  items: [for (final m in _consentMethods) DropdownMenuItem(value: m, child: Text(m))],
                  onChanged: (v) => setState(() => _consentMethod = v),
                ),
                const SizedBox(height: 8),
                TextField(controller: _consentNote, maxLength: 1000, decoration: const InputDecoration(labelText: 'Ghi chú bằng chứng')),
              ],
              if (widget.adjust || e.isLateSale) ...[
                const SizedBox(height: 8),
                TextField(
                  controller: _reason,
                  maxLength: 1000,
                  maxLines: 2,
                  decoration: InputDecoration(labelText: widget.adjust ? 'Lý do điều chỉnh *' : 'Lý do bán muộn *'),
                ),
              ],
              if (_error != null) Text(_error!, style: TextStyle(color: Theme.of(context).colorScheme.error)),
            ],
          ),
        ),
      ),
      actions: [
        TextButton(onPressed: () => Navigator.pop(context), child: const Text('Hủy')),
        FilledButton(onPressed: _submit, child: const Text('Lưu')),
      ],
    );
  }
}

/// Danh sách khách mua đang dùng cho hộp thoại bán.
final _customersProvider = FutureProvider.autoDispose<List<Customer>>(
  (ref) => ref.watch(exhibitionRepositoryProvider).customers(''),
);

/// Giao dịch đã trả tiền: chỉ sửa hình thức/chứng từ trả tiền, bắt buộc lý do (BR-49).
class _PaymentNoteDialog extends StatefulWidget {
  const _PaymentNoteDialog({required this.entry});

  final EntryDetail entry;

  @override
  State<_PaymentNoteDialog> createState() => _PaymentNoteDialogState();
}

class _PaymentNoteDialogState extends State<_PaymentNoteDialog> {
  late final _method = TextEditingController(text: widget.entry.paymentMethod ?? '');
  late final _reference = TextEditingController(text: widget.entry.paymentReference ?? '');
  final _reason = TextEditingController();
  String? _error;

  @override
  void dispose() {
    _method.dispose();
    _reference.dispose();
    _reason.dispose();
    super.dispose();
  }

  void _submit() {
    if (_method.text.trim().isEmpty) return setState(() => _error = 'Hình thức trả tiền không được để trống');
    if (_reason.text.trim().isEmpty) return setState(() => _error = 'Vui lòng nhập lý do điều chỉnh');
    Navigator.pop(context, {
      'paymentMethod': _method.text.trim(),
      'paymentReference': _reference.text.trim(),
      'reason': _reason.text.trim(),
    });
  }

  @override
  Widget build(BuildContext context) {
    return AlertDialog(
      title: const Text('Sửa thông tin trả tiền'),
      content: SizedBox(
        width: 420,
        child: SingleChildScrollView(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              const Text('Đã trả tiền: không đổi giá/khách/ngày. Sai lệch số tiền xử lý ngoài hệ thống và ghi chú lại.'),
              const SizedBox(height: 8),
              TextField(controller: _method, decoration: const InputDecoration(labelText: 'Hình thức trả *')),
              const SizedBox(height: 8),
              TextField(controller: _reference, decoration: const InputDecoration(labelText: 'Số chứng từ / ghi chú')),
              const SizedBox(height: 8),
              TextField(controller: _reason, maxLength: 1000, maxLines: 2, decoration: const InputDecoration(labelText: 'Lý do điều chỉnh *')),
              if (_error != null) Text(_error!, style: TextStyle(color: Theme.of(context).colorScheme.error)),
            ],
          ),
        ),
      ),
      actions: [
        TextButton(onPressed: () => Navigator.pop(context), child: const Text('Hủy')),
        FilledButton(onPressed: _submit, child: const Text('Lưu')),
      ],
    );
  }
}

// ===================== Hộp thoại ghi nhận trả tiền =====================

const _payMethods = ['Chuyển khoản', 'Tiền mặt', 'Khác'];

/// Ghi nhận trả đủ một lần cho học viên: số tiền = giá bán (BR-45).
class _PayDialog extends StatefulWidget {
  const _PayDialog({required this.entry});

  final EntryDetail entry;

  @override
  State<_PayDialog> createState() => _PayDialogState();
}

class _PayDialogState extends State<_PayDialog> {
  late final _amount = TextEditingController(text: (widget.entry.soldPrice ?? 0).round().toString());
  final _reference = TextEditingController();
  late DateTime _date = _clampedToday();
  String? _method;
  String? _error;

  // Ngày trả mặc định hôm nay nhưng không trước ngày bán
  DateTime _clampedToday() {
    final today = todayDate();
    final sold = widget.entry.soldDate;
    return sold != null && today.isBefore(sold) ? sold : today;
  }

  @override
  void dispose() {
    _amount.dispose();
    _reference.dispose();
    super.dispose();
  }

  void _submit() {
    final amount = parseMoney(_amount.text);
    if (amount == null) return setState(() => _error = 'Vui lòng nhập số tiền trả');
    if (amount != widget.entry.soldPrice) return setState(() => _error = 'Số tiền trả phải bằng giá bán');
    if (_method == null) return setState(() => _error = 'Vui lòng chọn hình thức trả tiền');
    Navigator.pop(context, {
      'paidAmount': amount,
      'paidDate': apiDate(_date),
      'paymentMethod': _method,
      'paymentReference': _reference.text.trim(),
    });
  }

  @override
  Widget build(BuildContext context) {
    final sold = widget.entry.soldDate ?? DateTime(todayDate().year - 1);
    return AlertDialog(
      title: const Text('Ghi nhận trả tiền cho học viên'),
      content: SizedBox(
        width: 420,
        child: SingleChildScrollView(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Text('Phải trả học viên: ${formatVnd(widget.entry.soldPrice ?? 0)} (trả đủ một lần)'),
              const SizedBox(height: 8),
              MoneyField(controller: _amount, label: 'Số tiền trả *'),
              const SizedBox(height: 12),
              DateField(
                label: 'Ngày trả *',
                value: _date,
                firstDate: sold,
                lastDate: todayDate().isBefore(sold) ? sold : todayDate(),
                onChanged: (d) => setState(() => _date = d),
              ),
              const SizedBox(height: 12),
              DropdownButtonFormField<String>(
                initialValue: _method,
                decoration: const InputDecoration(labelText: 'Hình thức trả *'),
                items: [for (final m in _payMethods) DropdownMenuItem(value: m, child: Text(m))],
                onChanged: (v) => setState(() => _method = v),
              ),
              const SizedBox(height: 8),
              TextField(controller: _reference, maxLength: 100, decoration: const InputDecoration(labelText: 'Số chứng từ / ghi chú')),
              if (_error != null) Text(_error!, style: TextStyle(color: Theme.of(context).colorScheme.error)),
            ],
          ),
        ),
      ),
      actions: [
        TextButton(onPressed: () => Navigator.pop(context), child: const Text('Hủy')),
        FilledButton(onPressed: _submit, child: const Text('Ghi nhận')),
      ],
    );
  }
}
