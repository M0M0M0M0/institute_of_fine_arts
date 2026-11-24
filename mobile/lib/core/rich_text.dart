import 'package:flutter_quill/flutter_quill.dart';
import 'package:flutter_quill/quill_delta.dart';
import 'package:html/dom.dart' as dom;
import 'package:html/parser.dart' as html_parser;
import 'package:vsc_quill_delta_to_html/vsc_quill_delta_to_html.dart';

/// Rich text for the entry description.
///
/// The editor works on a Quill [Delta]; what is sent to and stored by the server is simple HTML, the same kind
/// the web's TinyMCE editor produces: paragraphs `<p>`, `<strong>`, `<em>`, `<u>`, `<ul>` / `<ol>` with `<li>`.
/// Descriptions written before the editor existed are plain text; they are read as one paragraph per line.

// An HTML tag the editor can produce (or TinyMCE's default output has); plain text does not contain one
final _knownTag = RegExp(
  r'</?(p|br|strong|b|em|i|u|s|ul|ol|li|h[1-6]|blockquote|a|table|tr|td|th)\b',
  caseSensitive: false,
);

/// Whether [stored] is HTML (as opposed to plain text written before the rich text editor).
bool looksLikeHtml(String stored) => _knownTag.hasMatch(stored);

/// A Delta for the editor from what the server stored: HTML, plain text, or nothing.
Delta deltaFromStored(String? stored) {
  final text = stored?.trim() ?? '';
  if (text.isEmpty) return Delta()..insert('\n');
  if (!looksLikeHtml(text)) {
    return Delta()..insert(text.endsWith('\n') ? text : '$text\n');
  }
  return _deltaFromHtml(text);
}

/// The HTML to store for the document of [delta]; an empty string when the document has no text.
String htmlFromDelta(Delta delta) {
  if (Document.fromDelta(delta).toPlainText().trim().isEmpty) return '';
  final ops = [
    for (final op in delta.toJson()) Map<String, dynamic>.from(op as Map),
  ];
  final converter = QuillDeltaToHtmlConverter(
    ops,
    // One `<p>` per paragraph, like TinyMCE (not one `<p>` with `<br/>` between the lines)
    ConverterOptions(multiLineParagraph: false),
  );
  return converter.convert().trim();
}

/// The text of [stored] without any formatting, e.g. to count characters or to show a short preview.
String plainTextOf(String? stored) =>
    Document.fromDelta(deltaFromStored(stored)).toPlainText().trim();

// ---- HTML -> Delta ----
// A small reader for the tags the editor knows. (The ready-made converters merge neighbouring `<p>` into one
// paragraph, which would flatten a multi-paragraph text written in TinyMCE.)

const _blockTags = {
  'p',
  'div',
  'h1',
  'h2',
  'h3',
  'h4',
  'h5',
  'h6',
  'blockquote',
  'pre',
};
const _nbsp = ' ';

Delta _deltaFromHtml(String source) {
  final delta = Delta();
  final fragment = html_parser.parseFragment(source);
  _readBlocks(fragment.nodes, delta, indent: 0);
  // A Delta of a document always ends with a newline
  final last = delta.isEmpty ? null : delta.last.data;
  if (last is! String || !last.endsWith('\n')) delta.insert('\n');
  return delta;
}

/// Reads sibling nodes: block elements become paragraphs / list items; loose inline content is gathered into
/// a paragraph of its own.
void _readBlocks(List<dom.Node> nodes, Delta delta, {required int indent}) {
  final loose = <dom.Node>[];
  void flushLoose() {
    if (loose.isEmpty) return;
    final hasContent = loose.any(
      (n) => n is dom.Element || (n.text ?? '').trim().isNotEmpty,
    );
    if (hasContent) _paragraph(loose, delta, const {});
    loose.clear();
  }

  for (final node in nodes) {
    if (node is dom.Element) {
      final tag = node.localName;
      if (_blockTags.contains(tag)) {
        flushLoose();
        // Headings keep their weight as bold text: the editor has no heading style
        final bold = tag != null && tag.length == 2 && tag.startsWith('h');
        _paragraph(node.nodes, delta, bold ? const {'bold': true} : const {});
        continue;
      }
      if (tag == 'ul' || tag == 'ol') {
        flushLoose();
        _list(node, delta, indent: indent);
        continue;
      }
    }
    loose.add(node);
  }
  flushLoose();
}

void _list(dom.Element list, Delta delta, {required int indent}) {
  final kind = list.localName == 'ol' ? 'ordered' : 'bullet';
  for (final item in list.children.where((e) => e.localName == 'li')) {
    // The item's own text first, then its nested lists one level deeper
    final own = item.nodes
        .where(
          (n) =>
              !(n is dom.Element &&
                  (n.localName == 'ul' || n.localName == 'ol')),
        )
        .toList();
    _inline(own, delta, const {});
    delta.insert('\n', {'list': kind, if (indent > 0) 'indent': indent});
    for (final nested in item.children.where(
      (e) => e.localName == 'ul' || e.localName == 'ol',
    )) {
      _list(nested, delta, indent: indent + 1);
    }
  }
}

/// One paragraph: its inline content and the closing newline. A paragraph that holds only a line break or a
/// non-breaking space (TinyMCE's empty line) is an empty paragraph.
void _paragraph(List<dom.Node> nodes, Delta delta, Map<String, dynamic> attrs) {
  final probe = Delta();
  _inline(nodes, probe, attrs);
  var ops = probe.toList();
  // A trailing line break closes the paragraph by itself; it must not add one more
  if (ops.isNotEmpty && ops.last.data == '\n' && ops.last.attributes == null) {
    ops = ops.sublist(0, ops.length - 1);
  }
  final text = ops.map((o) => o.data is String ? o.data as String : '').join();
  if (text.replaceAll(_nbsp, '').trim().isEmpty && !text.contains('\n')) {
    delta.insert('\n');
    return;
  }
  for (final op in ops) {
    delta.insert(op.data, op.attributes);
  }
  delta.insert('\n');
}

void _inline(List<dom.Node> nodes, Delta delta, Map<String, dynamic> attrs) {
  for (final node in nodes) {
    if (node is dom.Text) {
      // HTML collapses runs of white space (including the line breaks between tags) into one space
      final text = node.text.replaceAll(RegExp(r'[ \t\r\n]+'), ' ');
      if (text.isNotEmpty) {
        delta.insert(
          text,
          attrs.isEmpty ? null : Map<String, dynamic>.of(attrs),
        );
      }
    } else if (node is dom.Element) {
      switch (node.localName) {
        case 'br':
          delta.insert('\n');
        case 'strong' || 'b':
          _inline(node.nodes, delta, {...attrs, 'bold': true});
        case 'em' || 'i':
          _inline(node.nodes, delta, {...attrs, 'italic': true});
        case 'u' || 'ins':
          _inline(node.nodes, delta, {...attrs, 'underline': true});
        case 's' || 'strike' || 'del':
          _inline(node.nodes, delta, {...attrs, 'strike': true});
        case 'a':
          final href = node.attributes['href'];
          _inline(node.nodes, delta, {
            ...attrs,
            if (href != null && href.isNotEmpty) 'link': href,
          });
        default:
          // Any other tag: keep its text, drop the tag
          _inline(node.nodes, delta, attrs);
      }
    }
  }
}
