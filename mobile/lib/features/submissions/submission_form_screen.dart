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
      appBar: AppBar(leading: const AppBackButton(), leadingWidth: 48, titleSpacing: 0, title: Text(editing ? 'Edit entry' : 'Submit entry')),
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
  late final RichTextController _description;
  late final RichTextController _textContent;
  late TextType _textType;
  late bool _publicConsent;

  // Pictures (BR-18): new ones chosen on the device, current ones to remove, the cover
  final List<PickedPicture> _picked = [];
  int _coverIndex = 0; // among [_picked] (new entry)
  final Set<int> _remove = {}; // current picture ids (edit)
  int? _coverImageId; // current picture chosen as the cover (edit)
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
    // Rich text (BR-50): HTML like the web's editor; older entries have plain text, read as paragraphs
    _description = RichTextController(stored: i?.description);
    _textContent = RichTextController(stored: i?.textContent);
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

  /// Current pictures of the entry being edited (cover first).
  List<int> get _current => widget.initial?.imageIds ?? const [];

  /// Pictures the entry will have after saving.
  int get _count =>
      (_editing ? (_current.isEmpty ? 1 : _current.length) - _remove.length : 0) + _picked.length;

  // Opens the file chooser (web: the browser), checks each picture (JPG / PNG / WebP ≤ 10 MB) and keeps the bytes
  Future<void> _pickImages() async {
    try {
      final files = await FilePicker.pickFiles(
        type: FileType.custom,
        allowedExtensions: imageExtensions,
      );
      if (files.isEmpty) return;
      final errors = <String>[];
      final added = <PickedPicture>[];
      String? warning;
      for (final file in files) {
        if (_count + added.length >= maxImagesPerEntry) {
          errors.add('An entry has at most $maxImagesPerEntry pictures');
          break;
        }
        final bytes = await file.readAsBytes();
        final error = validateImageFile(file.name, bytes.length);
        if (error != null) {
          errors.add('${file.name}: $error');
          continue;
        }
        // Small pictures are accepted with a warning (BR-17)
        try {
          final decoded = await decodeImageFromList(bytes);
          warning ??= smallImageWarning(decoded.width, decoded.height);
          decoded.dispose();
        } catch (_) {
          // Not decodable here: the server checks the file anyway
        }
        added.add(PickedPicture(name: file.name, bytes: bytes));
      }
      if (!mounted) return;
      setState(() {
        _picked.addAll(added);
        _imageError = errors.isEmpty ? null : errors.join('\n');
        _imageWarning = warning ?? _imageWarning;
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
    final missingImage = _count < 1;
    if (missingImage) {
      setState(() => _imageError = 'Choose at least one picture (JPG, PNG or WebP)');
    }
    if (!formOk || missingImage) return;

    final input = SubmissionInput(
      title: _title.text.trim(),
      description: _description.html,
      textType: _textType,
      textContent: _textContent.html,
      publicConsent: _publicConsent,
      pictures: List.of(_picked),
      coverIndex: _coverIndex.clamp(0, _picked.isEmpty ? 0 : _picked.length - 1),
      removeImageIds: _remove.toList(),
      coverImageId: _coverImageId,
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
        padding: readingPadding(context),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            // Competition + deadline notice (F-M06-10)
            if (competition != null) _DeadlineBanner(detail: competition),
            const SizedBox(height: AppSpacing.md),
            // ---- Pictures first (BR-18: 1 to 5 of the same artwork) ----
            Text(_editing ? 'Pictures' : 'Pictures *', style: text.titleSmall),
            const SizedBox(height: AppSpacing.xs),
            Text(
              'The same artwork from other angles, in other light, or a close-up. JPG, PNG or WebP, up to 10 MB each, '
              'at most $maxImagesPerEntry pictures. Pick the cover.',
              style: text.bodySmall,
            ),
            const SizedBox(height: AppSpacing.sm),
            // Current pictures (edit): cover / remove
            if (_editing && _current.isEmpty)
              ArtworkFrame(
                aspectRatio: null,
                child: SizedBox(
                  height: 200,
                  child: AuthImage(submissionId: widget.initial!.id, thumb: false, fit: BoxFit.contain, semanticLabel: widget.initial!.title),
                ),
              ),
            for (final (i, id) in _current.indexed)
              _PictureRow(
                image: PictureImage(imageId: id),
                label: 'Picture ${i + 1}${i == 0 ? ' (current cover)' : ''}',
                removed: _remove.contains(id),
                cover: !_remove.contains(id) && (_coverImageId == null ? i == 0 : _coverImageId == id),
                onCover: _submitting || _remove.contains(id) ? null : () => setState(() => _coverImageId = id),
                onRemove: _submitting
                    ? null
                    : () => setState(() {
                          if (!_remove.remove(id)) _remove.add(id);
                          if (_coverImageId == id) _coverImageId = null;
                        }),
              ),
            // Newly chosen pictures
            for (final (i, p) in _picked.indexed)
              _PictureRow(
                image: Image.memory(p.bytes, fit: BoxFit.cover),
                label: p.name,
                removed: false,
                // On a new entry one of them is the cover; on edit a new picture can become the cover after saving
                cover: !_editing && i == _coverIndex,
                onCover: _editing || _submitting ? null : () => setState(() => _coverIndex = i),
                onRemove: _submitting
                    ? null
                    : () => setState(() {
                          _picked.removeAt(i);
                          if (_coverIndex >= _picked.length) _coverIndex = 0;
                        }),
              ),
            if (!_editing && _picked.isEmpty)
              ArtworkFrame(
                aspectRatio: null,
                child: SizedBox(
                  height: 140,
                  child: ColoredBox(
                    color: AppColors.mist,
                    child: Center(
                      child: Column(
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          const Icon(Icons.add_photo_alternate_outlined, size: 40, color: AppColors.muted),
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
                onPressed: _submitting || _count >= maxImagesPerEntry ? null : _pickImages,
                icon: const Icon(Icons.add_photo_alternate_outlined),
                label: Text(_count == 0 ? 'Choose pictures' : 'Add pictures ($_count/$maxImagesPerEntry)'),
              ),
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
                    const Icon(Icons.warning_amber, size: 18, color: AppColors.brassText),
                    const SizedBox(width: 6),
                    Expanded(child: Text(_imageWarning!, style: text.bodySmall)),
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
            RichTextFormField(
              controller: _description,
              label: 'Description *',
              placeholder: 'Why you made it, why it deserves to win…',
              requiredMessage: 'Please enter a description',
              maxLength: 2000,
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
            RichTextFormField(
              controller: _textContent,
              label: '${_textType.label} *',
              requiredMessage: 'Please enter the accompanying text',
              maxLength: 4000,
              minHeight: 180,
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

/// One picture on the form: thumbnail, name, "Cover" and "Remove" (or "Undo" for a current one marked for removal).
class _PictureRow extends StatelessWidget {
  const _PictureRow({
    required this.image,
    required this.label,
    required this.removed,
    required this.cover,
    required this.onCover,
    required this.onRemove,
  });

  final Widget image;
  final String label;
  final bool removed;
  final bool cover;
  final VoidCallback? onCover;
  final VoidCallback? onRemove;

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    return Padding(
      padding: const EdgeInsets.only(bottom: AppSpacing.sm),
      child: Row(
        children: [
          Opacity(
            opacity: removed ? 0.4 : 1,
            child: SizedBox(width: 64, height: 64, child: ClipRRect(borderRadius: AppRadius.mdAll, child: image)),
          ),
          const SizedBox(width: AppSpacing.sm),
          Expanded(
            child: Text(
              label,
              style: text.bodyMedium!.copyWith(decoration: removed ? TextDecoration.lineThrough : null),
              overflow: TextOverflow.ellipsis,
            ),
          ),
          if (onCover != null || cover)
            ChoiceChip(label: const Text('Cover'), selected: cover, onSelected: onCover == null ? null : (_) => onCover!()),
          IconButton(
            tooltip: removed ? 'Keep this picture' : 'Remove this picture',
            onPressed: onRemove,
            icon: Icon(removed ? Icons.undo : Icons.close),
          ),
        ],
      ),
    );
  }
}
