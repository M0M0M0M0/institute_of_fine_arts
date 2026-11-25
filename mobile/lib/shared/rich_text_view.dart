import 'package:flutter/material.dart';
import 'package:flutter_widget_from_html_core/flutter_widget_from_html_core.dart';

import '../core/rich_text.dart';

/// Shows a description written with the rich text editor (HTML) or before it existed (plain text).
///
/// The HTML is only drawn, never run: no scripts, no images, no styles from the text. Only the tags the editor
/// and TinyMCE produce (paragraphs, headings, bold, italic, underline, strike, lists, quotes, links, tables) have an
/// effect. Used for entry descriptions and a competition's Description / Rules (sanitized by the server, BR-50).
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
        'ul' || 'ol' || 'blockquote' || 'table' => {'margin': '0 0 0.6em 0'},
        // Headings inside a text: a little larger than the text, not page titles
        'h2' => {'font-size': '1.25em', 'font-weight': '600', 'margin': '0.8em 0 0.3em 0'},
        'h3' => {'font-size': '1.12em', 'font-weight': '600', 'margin': '0.8em 0 0.3em 0'},
        'h4' => {'font-size': '1em', 'font-weight': '600', 'margin': '0.8em 0 0.3em 0'},
        'td' || 'th' => {'border': '1px solid #c9d3d6', 'padding': '4px 8px'},
        _ => null,
      },
    );
  }
}
