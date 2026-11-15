import 'dart:typed_data';

import 'package:file_picker/file_picker.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/api_client.dart';
import '../../core/async_view.dart';
import '../../app/tokens.dart';
import '../../core/format.dart';
import '../../shared/shared.dart';
import '../competitions/competitions.dart';
import '../student/dashboard.dart';
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
      appBar: AppBar(title: Text(editing ? 'Edit entry' : 'Submit entry')),
      body: editing
          // Sửa: tải chi tiết bài trước để điền sẵn form
          ? AsyncValueView(
              value: ref.watch(submissionDetailProvider(submissionId!)),
              onRetry: () =>
                  ref.invalidate(submissionDetailProvider(submissionId!)),
              data: (d) =>
                  _SubmissionForm(competitionId: d.competitionId, initial: d),
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
    // The institute does not record a publishing consent (BR-20 decides what is public), so it is always sent as true
    _publicConsent = true;
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
      setState(() => _imageError = 'Could not read the picture: $e');
    }
  }

  void _toast(String text) =>
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(text)));

  // Gửi form: nộp mới = POST, sửa = PUT
  Future<void> _submit() async {
    final formOk = _formKey.currentState!.validate();
    // A new entry needs a picture; show that error together with the field errors
    final missingImage = !_editing && _imageBytes == null;
    if (missingImage)
      setState(
        () => _imageError = _imageError ?? 'Please choose a JPEG picture',
      );
    if (!formOk || missingImage) return;

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
      refreshSubmissionData(
        ref,
        submissionId: saved.id,
        competitionId: saved.competitionId,
      );
      ref.invalidate(studentDashboardProvider);
      _toast(_editing ? 'Changes saved.' : 'Entry submitted.');
      if (_editing) {
        // Quay lại màn chi tiết (đã được làm mới)
        context.pop();
      } else {
        // Về tab "Bài của tôi" rồi mở chi tiết bài vừa nộp
        context.go('/student/entries');
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
    final competition = ref
        .watch(competitionDetailProvider(widget.competitionId))
        .value;
    final text = Theme.of(context).textTheme;

    return Form(
      key: _formKey,
      // Column in a scroll view (not a lazy ListView) so every field exists and gets validated
      child: SingleChildScrollView(
        padding: const EdgeInsets.fromLTRB(
          AppSpacing.gutter,
          AppSpacing.md,
          AppSpacing.gutter,
          AppSpacing.xl,
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            // Competition + deadline notice (F-M06-10)
            if (competition != null) _DeadlineBanner(detail: competition),
            const SizedBox(height: AppSpacing.md),
            // ---- Picture first: pick + immediate preview ----
            Text(
              _editing
                  ? 'Picture (only choose one to replace it)'
                  : 'Picture *',
              style: text.titleSmall,
            ),
            const SizedBox(height: AppSpacing.sm),
            if (_imageBytes != null)
              Semantics(
                label: 'Preview of the chosen picture',
                child: ArtworkFrame(
                  aspectRatio: null,
                  child: SizedBox(
                    height: 260,
                    child: Image.memory(_imageBytes!, fit: BoxFit.contain),
                  ),
                ),
              )
            else if (_editing)
              ArtworkFrame(
                aspectRatio: null,
                child: SizedBox(
                  height: 260,
                  child: AuthImage(
                    submissionId: widget.initial!.id,
                    thumb: false,
                    fit: BoxFit.contain,
                    semanticLabel: widget.initial!.title,
                  ),
                ),
              )
            else
              ArtworkFrame(
                aspectRatio: null,
                child: SizedBox(
                  height: 160,
                  child: ColoredBox(
                    color: AppColors.mist,
                    child: Center(
                      child: Column(
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          const Icon(
                            Icons.add_photo_alternate_outlined,
                            size: 40,
                            color: AppColors.muted,
                          ),
                          const SizedBox(height: AppSpacing.xs),
                          Text('No picture chosen yet', style: text.bodyMedium),
                        ],
                      ),
                    ),
                  ),
                ),
              ),
            const SizedBox(height: AppSpacing.sm),
            Align(
              alignment: Alignment.centerLeft,
              child: OutlinedButton.icon(
                onPressed: _submitting ? null : _pickImage,
                icon: const Icon(Icons.image_search_outlined),
                label: Text(
                  _imageName ??
                      (_editing ? 'Choose a new picture' : 'Choose a picture'),
                ),
              ),
            ),
            Padding(
              padding: const EdgeInsets.only(top: AppSpacing.xs),
              child: Text('JPEG only, up to 5 MB', style: text.bodySmall),
            ),
            if (_imageError != null)
              Padding(
                padding: const EdgeInsets.only(top: AppSpacing.xs),
                child: Text(
                  _imageError!,
                  style: text.bodySmall!.copyWith(color: AppColors.danger),
                ),
              ),
            if (_imageWarning != null && _imageError == null)
              Padding(
                padding: const EdgeInsets.only(top: AppSpacing.xs),
                child: Row(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    const Icon(
                      Icons.warning_amber,
                      size: 18,
                      color: AppColors.brassText,
                    ),
                    const SizedBox(width: 6),
                    Expanded(
                      child: Text(_imageWarning!, style: text.bodySmall),
                    ),
                  ],
                ),
              ),
            const SizedBox(height: AppSpacing.lg),
            TextFormField(
              controller: _title,
              maxLength: 150,
              textCapitalization: TextCapitalization.sentences,
              decoration: const InputDecoration(labelText: 'Title *'),
              validator: (v) => (v == null || v.trim().isEmpty)
                  ? 'Please enter a title'
                  : null,
            ),
            const SizedBox(height: AppSpacing.sm),
            TextFormField(
              controller: _description,
              maxLength: 2000,
              maxLines: 4,
              decoration: const InputDecoration(
                labelText:
                    'Description (why you made it, why it deserves to win) *',
                alignLabelWithHint: true,
              ),
              validator: (v) => (v == null || v.trim().isEmpty)
                  ? 'Please enter a description'
                  : null,
            ),
            const SizedBox(height: AppSpacing.md),
            Text('Accompanying text', style: text.titleSmall),
            const SizedBox(height: AppSpacing.sm),
            Wrap(
              spacing: AppSpacing.sm,
              runSpacing: AppSpacing.xs,
              children: [
                for (final t in TextType.values)
                  ChoiceChip(
                    label: Text(t.label),
                    selected: _textType == t,
                    onSelected: (_) => setState(() => _textType = t),
                  ),
              ],
            ),
            const SizedBox(height: AppSpacing.md),
            TextFormField(
              controller: _textContent,
              maxLength: 4000,
              maxLines: 6,
              decoration: InputDecoration(
                labelText: '${_textType.label} *',
                alignLabelWithHint: true,
              ),
              validator: (v) => (v == null || v.trim().isEmpty)
                  ? 'Please enter the accompanying text'
                  : null,
            ),
            const SizedBox(height: AppSpacing.lg),
            FilledButton.icon(
              onPressed: _submitting ? null : _submit,
              icon: _submitting
                  ? const SizedBox(
                      width: 16,
                      height: 16,
                      child: CircularProgressIndicator(strokeWidth: 2),
                    )
                  : const Icon(Icons.send_outlined),
              label: Text(_editing ? 'Save changes' : 'Submit entry'),
            ),
          ],
        ),
      ),
    );
  }
}

/// Competition, deadline and the "5 days left" / "Submissions closed" label.
class _DeadlineBanner extends StatelessWidget {
  const _DeadlineBanner({required this.detail});

  final CompetitionDetail detail;

  @override
  Widget build(BuildContext context) {
    final s = detail.summary;
    final label = deadlineLabel(s.status, s.daysLeft);
    final expired =
        s.status == CompetitionStatus.closed ||
        s.status == CompetitionStatus.graded;
    final text = Theme.of(context).textTheme;
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(AppSpacing.md),
      decoration: BoxDecoration(
        color: expired
            ? AppColors.danger.withValues(alpha: 0.08)
            : AppColors.mist,
        borderRadius: AppRadius.mdAll,
        border: Border.all(
          color: expired
              ? AppColors.danger.withValues(alpha: 0.4)
              : AppColors.line,
        ),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(s.title, style: text.titleMedium),
          const SizedBox(height: 2),
          Text(
            'Deadline: ${formatDateTime(detail.deadline)}',
            style: text.bodyMedium,
          ),
          if (label != null)
            Text(
              label,
              style: text.bodyMedium!.copyWith(
                fontWeight: FontWeight.w600,
                color: expired ? AppColors.danger : AppColors.brassText,
              ),
            ),
        ],
      ),
    );
  }
}
