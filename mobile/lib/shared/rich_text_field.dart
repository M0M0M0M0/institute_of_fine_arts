import 'package:flutter/material.dart';
import 'package:flutter_quill/flutter_quill.dart';

import '../app/tokens.dart';
import '../core/rich_text.dart';

/// Holds the text of a [RichTextFormField]. Read [html] when the form is sent.
class RichTextController {
  /// [stored] is what the server has (HTML, or plain text from before the editor existed); null or empty for a new text.
  RichTextController({String? stored})
    : quill = QuillController(
        document: Document.fromDelta(deltaFromStored(stored)),
        selection: const TextSelection.collapsed(offset: 0),
      );

  final QuillController quill;

  /// The HTML to send; an empty string when there is no text.
  String get html => htmlFromDelta(quill.document.toDelta());

  /// The text without formatting (what the user sees and counts).
  String get plainText => quill.document.toPlainText().trim();

  void dispose() => quill.dispose();
}

/// A text field with a small formatting toolbar (bold, italic, underline, lists, undo / redo) that looks like the
/// other outlined fields of the app. The value is HTML, see [RichTextController.html].
///
/// Valid when there is some text and the HTML (tags included: the server counts them) is not longer than
/// [maxLength]. The counter shows the characters the user wrote.
class RichTextFormField extends FormField<String> {
  RichTextFormField({
    super.key,
    required RichTextController controller,
    required String label,
    required String requiredMessage,
    String? placeholder,
    int maxLength = 2000,
    // false: an empty text is fine (an optional comment)
    bool isRequired = true,
    // false: the text can be read and selected but not changed, and the toolbar is hidden
    bool enabled = true,
    double minHeight = 140,
    // true: for a page with several of these fields. The toolbar is one small row shown only on the field being
    // edited, so the other fields stay quiet boxes.
    bool compact = false,
  }) : super(
         initialValue: '',
         autovalidateMode: AutovalidateMode.onUserInteraction,
         validator: (_) {
           if (isRequired && controller.plainText.isEmpty) {
             return requiredMessage;
           }
           if (controller.html.length > maxLength) {
             return 'This text is too long once its formatting is counted. Please shorten it.';
           }
           return null;
         },
         builder: (state) => _RichTextBox(
           controller: controller,
           label: label,
           placeholder: placeholder,
           maxLength: maxLength,
           enabled: enabled,
           minHeight: minHeight,
           compact: compact,
           errorText: state.errorText,
           onChanged: () => state.didChange(controller.html),
         ),
       );
}

class _RichTextBox extends StatefulWidget {
  const _RichTextBox({
    required this.controller,
    required this.label,
    required this.placeholder,
    required this.maxLength,
    required this.enabled,
    required this.minHeight,
    required this.compact,
    required this.errorText,
    required this.onChanged,
  });

  final RichTextController controller;
  final String label;
  final String? placeholder;
  final int maxLength;
  final bool enabled;
  final double minHeight;
  final bool compact;
  final String? errorText;
  final VoidCallback onChanged;

  @override
  State<_RichTextBox> createState() => _RichTextBoxState();
}

class _RichTextBoxState extends State<_RichTextBox> {
  final _focus = FocusNode();
  final _scroll = ScrollController();

  QuillController get _quill => widget.controller.quill;

  @override
  void initState() {
    super.initState();
    _quill.readOnly = !widget.enabled;
    _quill.addListener(_onQuillChanged);
    _focus.addListener(_refresh);
  }

  @override
  void dispose() {
    _quill.removeListener(_onQuillChanged);
    _focus.removeListener(_refresh);
    _focus.dispose();
    _scroll.dispose();
    super.dispose();
  }

  @override
  void didUpdateWidget(_RichTextBox old) {
    super.didUpdateWidget(old);
    _quill.readOnly = !widget.enabled;
  }

  void _refresh() => setState(() {});

  void _onQuillChanged() {
    setState(() {});
    widget.onChanged();
  }

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    final length = widget.controller.plainText.length;
    final overLimit = length > widget.maxLength;
    // The toolbar looks up the editor's own texts; give them here so the field works wherever it is placed,
    // without the app having to register them
    return Localizations.override(
      context: context,
      delegates: const [FlutterQuillLocalizations.delegate],
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          InputDecorator(
            isFocused: _focus.hasFocus,
            isEmpty: false,
            decoration: InputDecoration(
              labelText: widget.label,
              errorText: widget.errorText,
              floatingLabelBehavior: FloatingLabelBehavior.always,
              contentPadding: const EdgeInsets.fromLTRB(
                AppSpacing.sm,
                AppSpacing.md,
                AppSpacing.sm,
                AppSpacing.sm,
              ),
            ),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                // Only what a description needs: bold, italic, underline, two kinds of list and undo / redo
                if (widget.enabled && (!widget.compact || _focus.hasFocus))
                  QuillSimpleToolbar(
                    controller: _quill,
                    config: QuillSimpleToolbarConfig(
                      // The buttons wrap onto a second row on a very narrow screen. (The single scrolling row reads
                      // its scroll position before the first layout and throws "Null check operator" on a phone.)
                      multiRowsDisplay: true,
                      toolbarIconAlignment: WrapAlignment.start,
                      showDividers: false,
                      showUndo: !widget.compact,
                      showRedo: !widget.compact,
                      buttonOptions: widget.compact
                          ? const QuillSimpleToolbarButtonOptions(
                              base: QuillToolbarBaseButtonOptions(
                                iconSize: 17,
                                iconButtonFactor: 1.5,
                              ),
                            )
                          : const QuillSimpleToolbarButtonOptions(),
                      showFontFamily: false,
                      showFontSize: false,
                      showStrikeThrough: false,
                      showInlineCode: false,
                      showColorButton: false,
                      showBackgroundColorButton: false,
                      showClearFormat: false,
                      showHeaderStyle: false,
                      showListCheck: false,
                      showCodeBlock: false,
                      showQuote: false,
                      showIndent: false,
                      showLink: false,
                      showSearchButton: false,
                      showSubscript: false,
                      showSuperscript: false,
                      color: Colors.transparent,
                    ),
                  ),
                if (widget.enabled && (!widget.compact || _focus.hasFocus))
                  const Divider(height: AppSpacing.sm),
                QuillEditor.basic(
                  controller: _quill,
                  focusNode: _focus,
                  scrollController: _scroll,
                  config: QuillEditorConfig(
                    placeholder: widget.placeholder,
                    minHeight: widget.minHeight,
                    maxHeight: 320,
                    padding: const EdgeInsets.only(
                      top: AppSpacing.xs,
                      bottom: AppSpacing.sm,
                    ),
                  ),
                ),
              ],
            ),
          ),
          Padding(
            padding: const EdgeInsets.only(
              top: AppSpacing.xs,
              right: AppSpacing.md,
            ),
            child: Text(
              '$length/${widget.maxLength}',
              textAlign: TextAlign.end,
              style: text.bodySmall!.copyWith(
                color: overLimit ? AppColors.danger : AppColors.muted,
              ),
            ),
          ),
        ],
      ),
    );
  }
}
