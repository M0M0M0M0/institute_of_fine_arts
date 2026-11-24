import 'package:fine_arts_mobile/core/rich_text.dart';
import 'package:flutter_quill/flutter_quill.dart';
import 'package:flutter_test/flutter_test.dart';

String _roundTrip(String html) => htmlFromDelta(deltaFromStored(html));

void main() {
  group('rich text conversion', () {
    test(
      'plain text written before the editor is read as one paragraph per line',
      () {
        final delta = deltaFromStored('First line\nSecond line');
        expect(
          Document.fromDelta(delta).toPlainText(),
          'First line\nSecond line\n',
        );
        expect(looksLikeHtml('First line\nSecond line'), isFalse);
      },
    );

    test(
      'a description that merely mentions a less-than sign is still plain text',
      () {
        expect(looksLikeHtml('I used 3 < 4 colours & a brush'), isFalse);
      },
    );

    test('empty and null give an empty document and no HTML', () {
      expect(htmlFromDelta(deltaFromStored(null)), '');
      expect(htmlFromDelta(deltaFromStored('   ')), '');
      expect(plainTextOf(null), '');
    });

    test('bold, italic, underline and lists survive a round trip', () {
      const html =
          '<p>Hello <strong>bold</strong>, <em>italic</em> and <u>underlined</u>.</p>'
          '<ul><li>one</li><li>two</li></ul><ol><li>first</li><li>second</li></ol>';
      final back = _roundTrip(html);
      expect(back, contains('<strong>bold</strong>'));
      expect(back, contains('<em>italic</em>'));
      expect(back, contains('<u>underlined</u>'));
      expect(back, contains('<ul><li>one</li><li>two</li></ul>'));
      expect(back, contains('<ol><li>first</li><li>second</li></ol>'));
    });

    test('neighbouring paragraphs stay separate paragraphs', () {
      expect(_roundTrip('<p>One</p><p>Two</p>'), '<p>One</p><p>Two</p>');
    });

    test('TinyMCE puts line breaks between the tags: they are not text', () {
      const tinymce =
          '<p>One</p>\n<p>Two <strong>bold</strong></p>\n<ul>\n<li>a</li>\n<li>b</li>\n</ul>';
      expect(plainTextOf(tinymce), 'One\nTwo bold\na\nb');
      expect(
        _roundTrip(tinymce),
        '<p>One</p><p>Two <strong>bold</strong></p><ul><li>a</li><li>b</li></ul>',
      );
    });

    test('an empty line (TinyMCE writes &nbsp;) is one empty paragraph and does not multiply when saved again', () {
      final once = _roundTrip('<p>A</p><p>&nbsp;</p><p>B</p>');
      expect(
        Document.fromDelta(deltaFromStored(once)).toPlainText(),
        'A\n\nB\n',
      );
      expect(_roundTrip(once), once);
      expect(_roundTrip(_roundTrip(once)), once);
    });

    test('a line break inside a paragraph starts a new line, and a trailing one adds nothing', () {
      expect(plainTextOf('<p>One<br>Two</p>'), 'One\nTwo');
      expect(plainTextOf('<p>One<br></p>'), 'One');
    });

    test('nested lists keep their level', () {
      final back = _roundTrip(
        '<ul><li>top<ul><li>inner</li></ul></li><li>next</li></ul>',
      );
      expect(plainTextOf(back), 'top\ninner\nnext');
      expect(back, contains('inner'));
    });

    test('headings keep their text as bold, links keep their address', () {
      final back = _roundTrip(
        '<h2>Title</h2><p>See <a href="https://example.com/x">the link</a></p>',
      );
      expect(back, contains('<strong>Title</strong>'));
      expect(back, contains('href="https://example.com/x"'));
      expect(plainTextOf(back), 'Title\nSee the link');
    });

    test('tags that are not supported are dropped but their text stays; scripts never become text of a tag', () {
      final back = _roundTrip(
        '<p>Hi <span style="color:red">there</span></p><script>alert(1)</script>',
      );
      expect(plainTextOf(back), contains('Hi there'));
      expect(back, isNot(contains('<script')));
      expect(back, isNot(contains('<span')));
    });

    test('plain text turns into paragraphs when stored as HTML, and special characters are escaped', () {
      final back = htmlFromDelta(deltaFromStored('Tom & Jerry <3\nSecond'));
      expect(back, contains('&amp;'));
      expect(back, isNot(contains('<3')));
      expect(back, contains('<p>'));
      expect(plainTextOf(back), 'Tom & Jerry <3\nSecond');
    });

    test('plainTextOf strips the formatting', () {
      expect(plainTextOf('<p>Hello <strong>world</strong></p>'), 'Hello world');
    });
  });
}
