import 'dart:typed_data';

import 'package:file_picker/file_picker.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/api_client.dart';
import '../../core/async_view.dart';
import '../../core/format.dart';
import '../competitions/competitions.dart';
import 'submission_widgets.dart';
import 'submissions.dart';

/// Form nộp bài mới (có [competitionId]) hoặc sửa bài (có [submissionId]) – UC-03, UC-04.
class SubmissionFormScreen extends ConsumerWidget {
  const SubmissionFormScreen({super.key, this.competitionId, this.submissionId})
      : assert(competitionId != null || submissionId != null);

  final int? competitionId;
  final int? submissionId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final editing = submissionId != null;
    return Scaffold(
      appBar: AppBar(title: Text(editing ? 'Sửa bài dự thi' : 'Nộp bài dự thi')),
      body: editing
          // Sửa: tải chi tiết bài trước để điền sẵn form
          ? AsyncValueView(
              value: ref.watch(submissionDetailProvider(submissionId!)),
              onRetry: () => ref.invalidate(submissionDetailProvider(submissionId!)),
              data: (d) => _SubmissionForm(competitionId: d.competitionId, initial: d),
            )
          : _SubmissionForm(competitionId: competitionId!),
    );
  }
}

class _SubmissionForm extends ConsumerStatefulWidget {
  const _SubmissionForm({required this.competitionId, this.initial});

  final int competitionId;

  /// Có giá trị khi đang sửa bài.
  final SubmissionDetail? initial;

  @override
  ConsumerState<_SubmissionForm> createState() => _SubmissionFormState();
}

class _SubmissionFormState extends ConsumerState<_SubmissionForm> {
  final _formKey = GlobalKey<FormState>();
  late final TextEditingController _title;
  late final TextEditingController _description;
  late final TextEditingController _textContent;
  late TextType _textType;
  late bool _publicConsent;

  // Ảnh mới người dùng vừa chọn (null khi sửa mà không đổi ảnh)
  Uint8List? _imageBytes;
  String? _imageName;
  String? _imageError;
  String? _imageWarning; // cảnh báo ảnh nhỏ, không chặn nộp
  bool _submitting = false;

  bool get _editing => widget.initial != null;

  @override
  void initState() {
    super.initState();
    final i = widget.initial;
    // Điền sẵn khi sửa; nộp mới thì để trống, đồng ý công khai mặc định false
    _title = TextEditingController(text: i?.title ?? '');
    _description = TextEditingController(text: i?.description ?? '');
    _textContent = TextEditingController(text: i?.textContent ?? '');
    _textType = i?.textType ?? TextType.poem;
    _publicConsent = i?.publicConsent ?? false;
  }

  @override
  void dispose() {
    _title.dispose();
    _description.dispose();
    _textContent.dispose();
    super.dispose();
  }

  // Mở hộp chọn file (web: trình duyệt), kiểm tra JPEG ≤ 5 MB rồi đọc byte để xem trước
  Future<void> _pickImage() async {
    try {
      final file = await FilePicker.pickFile(
        type: FileType.custom,
        allowedExtensions: const ['jpg', 'jpeg'],
      );
      if (file == null) return;
      final bytes = await file.readAsBytes();
      final error = validateImageFile(file.name, bytes.length);
      // Đọc kích thước ảnh để cảnh báo nếu quá nhỏ (BR-17, chỉ nhắc, không chặn)
      String? warning;
      if (error == null) {
        try {
          final decoded = await decodeImageFromList(bytes);
          warning = smallImageWarning(decoded.width, decoded.height);
          decoded.dispose();
        } catch (_) {
          // Không giải mã được thì bỏ qua cảnh báo; server vẫn kiểm tra JPEG ở bước nộp
        }
      }
      if (!mounted) return;
      setState(() {
        _imageError = error;
        if (error == null) {
          _imageBytes = bytes;
          _imageName = file.name;
          _imageWarning = warning;
        }
      });
    } catch (e) {
      setState(() => _imageError = 'Không đọc được ảnh: $e');
    }
  }

  void _toast(String text) => ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(text)));

  // Gửi form: nộp mới = POST, sửa = PUT
  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) return;
    // Nộp mới bắt buộc có ảnh
    if (!_editing && _imageBytes == null) {
      setState(() => _imageError = _imageError ?? 'Vui lòng chọn ảnh JPEG');
      return;
    }

    final input = SubmissionInput(
      title: _title.text.trim(),
      description: _description.text.trim(),
      textType: _textType,
      textContent: _textContent.text.trim(),
      publicConsent: _publicConsent,
      imageBytes: _imageBytes,
      imageName: _imageName,
    );

    setState(() => _submitting = true);
    final repo = ref.read(submissionRepositoryProvider);
    try {
      final saved = _editing
          ? await repo.update(widget.initial!.id, input)
          : await repo.create(widget.competitionId, input);
      if (!mounted) return;
      // Làm mới danh sách, chi tiết, số bài ở trang chủ
      refreshSubmissionData(ref, submissionId: saved.id, competitionId: saved.competitionId);
      _toast(_editing ? 'Đã lưu thay đổi.' : 'Nộp bài thành công.');
      if (_editing) {
        // Quay lại màn chi tiết (đã được làm mới)
        context.pop();
      } else {
        // Về tab "Bài của tôi" rồi mở chi tiết bài vừa nộp
        context.go('/student?tab=1');
        context.push('/student/submissions/${saved.id}');
      }
    } catch (e) {
      if (!mounted) return;
      final err = ApiException.from(e);
      setState(() => _submitting = false);
      // 409: đã có bài trong cuộc thi này (BR-15) → chuyển sang sửa bài đó
      final existing = err.submissionId;
      if (err.statusCode == 409 && existing != null && !_editing) {
        _toast(err.message);
        context.pushReplacement('/student/submissions/$existing/edit');
        return;
      }
      _toast(err.message);
    }
  }

  @override
  Widget build(BuildContext context) {
    final competition = ref.watch(competitionDetailProvider(widget.competitionId)).value;
    final textTheme = Theme.of(context).textTheme;

    return Form(
      key: _formKey,
      child: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          // Thông tin cuộc thi + cảnh báo hạn nộp (F-M06-10)
          if (competition != null) _DeadlineBanner(detail: competition),
          const SizedBox(height: 12),
          TextFormField(
            controller: _title,
            maxLength: 150,
            decoration: const InputDecoration(labelText: 'Tiêu đề *', border: OutlineInputBorder()),
            validator: (v) => (v == null || v.trim().isEmpty) ? 'Vui lòng nhập tiêu đề' : null,
          ),
          const SizedBox(height: 8),
          TextFormField(
            controller: _description,
            maxLength: 2000,
            maxLines: 4,
            decoration: const InputDecoration(
              labelText: 'Mô tả (vì sao vẽ, vì sao xứng đáng) *',
              alignLabelWithHint: true,
              border: OutlineInputBorder(),
            ),
            validator: (v) => (v == null || v.trim().isEmpty) ? 'Vui lòng nhập mô tả' : null,
          ),
          const SizedBox(height: 8),
          // Loại phần chữ đi kèm
          DropdownButtonFormField<TextType>(
            initialValue: _textType,
            decoration: const InputDecoration(labelText: 'Loại phần chữ', border: OutlineInputBorder()),
            items: [for (final t in TextType.values) DropdownMenuItem(value: t, child: Text(t.label))],
            onChanged: (v) => setState(() => _textType = v ?? _textType),
          ),
          const SizedBox(height: 16),
          TextFormField(
            controller: _textContent,
            maxLength: 4000,
            maxLines: 6,
            decoration: InputDecoration(
              labelText: '${_textType.label} *',
              alignLabelWithHint: true,
              border: const OutlineInputBorder(),
            ),
            validator: (v) => (v == null || v.trim().isEmpty) ? 'Vui lòng nhập phần chữ đi kèm' : null,
          ),
          CheckboxListTile(
            contentPadding: EdgeInsets.zero,
            controlAffinity: ListTileControlAffinity.leading,
            value: _publicConsent,
            onChanged: (v) => setState(() => _publicConsent = v ?? false),
            title: const Text('Đồng ý hiển thị công khai bài dự thi'),
          ),
          const SizedBox(height: 8),
          // ---- Chọn ảnh + xem trước ----
          Text(_editing ? 'Ảnh (chỉ chọn nếu muốn thay ảnh)' : 'Ảnh JPEG (≤ 5 MB) *', style: textTheme.titleSmall),
          const SizedBox(height: 8),
          if (_imageBytes != null)
            // Xem trước ảnh vừa chọn từ byte
            Image.memory(_imageBytes!, height: 240, fit: BoxFit.contain)
          else if (_editing)
            // Chưa chọn ảnh mới → hiện ảnh hiện tại
            SubmissionImage(submissionId: widget.initial!.id, height: 240),
          const SizedBox(height: 8),
          Align(
            alignment: Alignment.centerLeft,
            child: OutlinedButton.icon(
              onPressed: _submitting ? null : _pickImage,
              icon: const Icon(Icons.image_search),
              label: Text(_imageName ?? (_editing ? 'Chọn ảnh mới' : 'Chọn ảnh')),
            ),
          ),
          if (_imageError != null)
            Padding(
              padding: const EdgeInsets.only(top: 4),
              child: Text(_imageError!, style: TextStyle(color: Theme.of(context).colorScheme.error)),
            ),
          if (_imageWarning != null && _imageError == null)
            Padding(
              padding: const EdgeInsets.only(top: 4),
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Icon(Icons.warning_amber, size: 18, color: Colors.orange),
                  const SizedBox(width: 6),
                  Expanded(child: Text(_imageWarning!, style: textTheme.bodySmall)),
                ],
              ),
            ),
          const SizedBox(height: 24),
          FilledButton.icon(
            onPressed: _submitting ? null : _submit,
            icon: _submitting
                ? const SizedBox(width: 16, height: 16, child: CircularProgressIndicator(strokeWidth: 2))
                : const Icon(Icons.send),
            label: Text(_editing ? 'Lưu thay đổi' : 'Nộp bài'),
          ),
        ],
      ),
    );
  }
}

/// Khung hiển thị cuộc thi, hạn nộp và nhãn "Còn X ngày" / "Đã hết hạn nộp bài".
class _DeadlineBanner extends StatelessWidget {
  const _DeadlineBanner({required this.detail});

  final CompetitionDetail detail;

  @override
  Widget build(BuildContext context) {
    final s = detail.summary;
    final label = deadlineLabel(s.status, s.daysLeft);
    final expired = s.status == CompetitionStatus.closed || s.status == CompetitionStatus.graded;
    final scheme = Theme.of(context).colorScheme;
    return Card(
      color: expired ? scheme.errorContainer : scheme.secondaryContainer,
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(s.title, style: Theme.of(context).textTheme.titleMedium),
            Text('Hạn nộp: ${formatDateTime(detail.deadline)}'),
            if (label != null) Text(label, style: const TextStyle(fontWeight: FontWeight.w600)),
          ],
        ),
      ),
    );
  }
}
