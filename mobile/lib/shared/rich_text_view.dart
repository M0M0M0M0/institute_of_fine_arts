import 'package:flutter/material.dart';
import 'package:flutter_widget_from_html_core/flutter_widget_from_html_core.dart';

import '../core/rich_text.dart';

/// Shows a description written with the rich text editor (HTML) or before it existed (plain text).
///
/// The HTML is only drawn, never run: no scripts, no images, no styles from the text. Only the tags the editor
/// and TinyMCE produce (paragraphs, bold, italic, underline, lists, links) have an effect.
class RichTextView extends StatelessWidget {
  const RichTextView(this.stored, {super.key, this.style});

  final String stored;
  final TextStyle? style;

  @override
  Widget build(BuildContext context) {
    final textStyle = style ?? Theme.of(context).textTheme.bodyMedium;
    if (!looksLikeHtml(stored)) return Text(stored, style: textStyle);
    return HtmlWidget(
      stored,
      textStyle: textStyle,
      // The renderer would load a picture from any `<img src="...">`; a description is text only, so a picture
      // (which could come from anywhere on the internet) is not drawn
      customWidgetBuilder: (element) => switch (element.localName) {
        'img' ||
        'video' ||
        'audio' ||
        'iframe' ||
        'object' ||
        'embed' => const SizedBox.shrink(),
        _ => null,
      },
      // Paragraphs close together, like in the editor
      customStylesBuilder: (element) => switch (element.localName) {
        'p' => {'margin': '0 0 0.6em 0'},
        'ul' || 'ol' => {'margin': '0 0 0.6em 0'},
        _ => null,
      },
    );
  }
}
