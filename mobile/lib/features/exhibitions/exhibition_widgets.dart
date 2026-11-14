import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import '../../core/format.dart';
import 'exhibitions.dart';

/// Nhãn trạng thái triển lãm có màu.
class ExhibitionStatusChip extends StatelessWidget {
  const ExhibitionStatusChip(this.status, {super.key});

  final ExhibitionStatus status;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final color = switch (status) {
      ExhibitionStatus.upcoming => scheme.primary,
      ExhibitionStatus.ongoing => scheme.tertiary,
      ExhibitionStatus.finished => scheme.outline,
      ExhibitionStatus.cancelled => scheme.error,
    };
    return _Pill(text: status.label, color: color);
  }
}

/// Nhãn trạng thái tranh gửi triển lãm có màu; SOLD + đã trả hiện "Đã bán – đã trả tiền".
class EntryStatusChip extends StatelessWidget {
  const EntryStatusChip(this.status, {super.key, this.paid = false});

  final EntryStatus status;
  final bool paid;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final color = switch (status) {
      EntryStatus.pending => scheme.secondary,
      EntryStatus.accepted => scheme.primary,
      EntryStatus.sold => scheme.tertiary,
      EntryStatus.rejected || EntryStatus.withdrawn => scheme.error,
      EntryStatus.unsold => scheme.outline,
    };
    return _Pill(text: entryStatusText(status, paid), color: color);
  }
}

class _Pill extends StatelessWidget {
  const _Pill({required this.text, required this.color});

  final String text;
  final Color color;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
      decoration: BoxDecoration(color: color.withValues(alpha: 0.12), borderRadius: BorderRadius.circular(12)),
      child: Text(text, style: TextStyle(color: color, fontSize: 12, fontWeight: FontWeight.w600)),
    );
  }
}

/// Một dòng "nhãn: giá trị" trong màn chi tiết.
class InfoRow extends StatelessWidget {
  const InfoRow(this.label, this.value, {super.key});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 2),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          SizedBox(width: 130, child: Text(label, style: Theme.of(context).textTheme.bodySmall)),
          Expanded(child: Text(value)),
        ],
      ),
    );
  }
}

/// Hộp thông báo/giải thích (vd lý do bị khóa, lý do từ chối).
class NoticeCard extends StatelessWidget {
  const NoticeCard({super.key, required this.text, this.icon = Icons.info_outline, this.error = false});

  final String text;
  final IconData icon;
  final bool error;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    return Card(
      color: error ? scheme.errorContainer : scheme.surfaceContainerHighest,
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Row(
          children: [
            Icon(icon),
            const SizedBox(width: 8),
            Expanded(child: Text(text)),
          ],
        ),
      ),
    );
  }
}

/// Ô tiền: chỉ nhận chữ số, gợi ý hiển thị "x.xxx.xxx ₫" ngay dưới ô (BR-47: VNĐ, không âm).
class MoneyField extends StatefulWidget {
  const MoneyField({super.key, required this.controller, required this.label, this.enabled = true});

  final TextEditingController controller;
  final String label;
  final bool enabled;

  @override
  State<MoneyField> createState() => _MoneyFieldState();
}

class _MoneyFieldState extends State<MoneyField> {
  @override
  void initState() {
    super.initState();
    widget.controller.addListener(_onChange);
  }

  @override
  void dispose() {
    widget.controller.removeListener(_onChange);
    super.dispose();
  }

  void _onChange() => setState(() {});

  @override
  Widget build(BuildContext context) {
    final value = parseMoney(widget.controller.text);
    return TextField(
      controller: widget.controller,
      enabled: widget.enabled,
      keyboardType: TextInputType.number,
      inputFormatters: [FilteringTextInputFormatter.digitsOnly],
      decoration: InputDecoration(
        labelText: widget.label,
        helperText: value == null ? null : formatVnd(value),
        suffixText: '₫',
      ),
    );
  }
}

/// Ô chọn ngày bằng hộp thoại lịch; hiển thị dd/MM/yyyy (NFR-17).
class DateField extends StatelessWidget {
  const DateField({
    super.key,
    required this.label,
    required this.value,
    required this.onChanged,
    required this.firstDate,
    required this.lastDate,
    this.enabled = true,
  });

  final String label;
  final DateTime? value;
  final ValueChanged<DateTime> onChanged;
  final DateTime firstDate;
  final DateTime lastDate;
  final bool enabled;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: !enabled
          ? null
          : () async {
              final initial = value ?? DateTime.now();
              final picked = await showDatePicker(
                context: context,
                initialDate: initial.isBefore(firstDate)
                    ? firstDate
                    : (initial.isAfter(lastDate) ? lastDate : initial),
                firstDate: firstDate,
                lastDate: lastDate,
              );
              if (picked != null) onChanged(picked);
            },
      child: InputDecorator(
        decoration: InputDecoration(labelText: label, suffixIcon: const Icon(Icons.calendar_today, size: 18)),
        child: Text(value == null ? 'Chọn ngày' : formatDate(value!)),
      ),
    );
  }
}

/// Ngày hôm nay (bỏ phần giờ) để so sánh với ngày của triển lãm.
DateTime todayDate() {
  final n = DateTime.now();
  return DateTime(n.year, n.month, n.day);
}

/// Khoảng ngày triển lãm dạng "dd/MM/yyyy – dd/MM/yyyy".
String dateRange(DateTime start, DateTime end) => '${formatDate(start)} – ${formatDate(end)}';
