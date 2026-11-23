import 'package:fine_arts_mobile/shared/shared.dart';
import 'package:flutter/material.dart';
import 'package:flutter_quill/flutter_quill.dart';
import 'package:flutter_test/flutter_test.dart';

// A plain MaterialApp on purpose: the field must work without the app registering the editor's texts
Widget _app(Widget child) => MaterialApp(
  home: Scaffold(
    body: SingleChildScrollView(
      child: Padding(padding: const EdgeInsets.all(16), child: child),
    ),
  ),
);

// Takes the widget tree down and lets the editor's pending timers run, so the test ends cleanly
Future<void> _unmount(WidgetTester tester) async {
  await tester.pumpWidget(const SizedBox());
  await tester.pump(const Duration(milliseconds: 50));
}

void main() {
  group('RichTextView', () {
    testWidgets('plain text from before the editor is shown as it is', (
      tester,
    ) async {
      await tester.pumpWidget(
        _app(const RichTextView('Just a plain description')),
      );
      expect(find.text('Just a plain description'), findsOneWidget);
    });

    testWidgets('HTML is drawn as text, tags are not shown', (tester) async {
      await tester.pumpWidget(
        _app(
          const RichTextView(
            '<p>Hello <strong>bold</strong> world</p><ul><li>one</li></ul>',
          ),
        ),
      );
      await tester.pumpAndSettle();
      expect(find.textContaining('Hello', findRichText: true), findsOneWidget);
      expect(find.textContaining('one', findRichText: true), findsOneWidget);
      expect(find.textContaining('<strong>', findRichText: true), findsNothing);
    });

    testWidgets('a script in the HTML does not run and no image is loaded', (
      tester,
    ) async {
      await tester.pumpWidget(
        _app(
          const RichTextView(
            '<p>Safe</p><script>throw 1</script><img src="http://x/y.png">',
          ),
        ),
      );
      await tester.pumpAndSettle();
      expect(tester.takeException(), isNull);
      expect(find.textContaining('Safe', findRichText: true), findsOneWidget);
      expect(find.byType(Image), findsNothing);
    });
  });

  group('RichTextFormField', () {
    testWidgets('shows the toolbar, the editor and the character counter', (
      tester,
    ) async {
      final controller = RichTextController();
      addTearDown(controller.dispose);
      await tester.pumpWidget(
        _app(
          RichTextFormField(
            controller: controller,
            label: 'Description *',
            requiredMessage: 'Please enter a description',
            placeholder: 'Write about your painting…',
          ),
        ),
      );
      await tester.pumpAndSettle();
      expect(find.byType(QuillSimpleToolbar), findsOneWidget);
      expect(find.byType(QuillEditor), findsOneWidget);
      expect(find.text('0/2000'), findsOneWidget);
      // Quill draws the placeholder as rich text
      expect(
        find.text('Write about your painting…', findRichText: true),
        findsOneWidget,
      );
      await _unmount(tester);
    });

    testWidgets(
      'formatted text becomes HTML and the counter counts the written characters',
      (tester) async {
        final controller = RichTextController();
        addTearDown(controller.dispose);
        await tester.pumpWidget(
          _app(
            RichTextFormField(
              controller: controller,
              label: 'Description *',
              requiredMessage: 'Required',
            ),
          ),
        );
        controller.quill.document.insert(0, 'Hello world');
        controller.quill.formatText(0, 5, Attribute.bold);
        await tester.pumpAndSettle();
        expect(controller.html, '<p><strong>Hello</strong> world</p>');
        expect(controller.plainText, 'Hello world');
        expect(find.text('11/2000'), findsOneWidget);
        await _unmount(tester);
      },
    );

    testWidgets('an existing description (HTML or plain) fills the editor', (
      tester,
    ) async {
      final html = RichTextController(
        stored: '<p>From the <em>server</em></p>',
      );
      final plain = RichTextController(stored: 'Old plain text');
      addTearDown(html.dispose);
      addTearDown(plain.dispose);
      expect(html.plainText, 'From the server');
      expect(html.html, '<p>From the <em>server</em></p>');
      expect(plain.plainText, 'Old plain text');
      expect(plain.html, '<p>Old plain text</p>');
    });

    testWidgets(
      'an empty description is refused with the message; text makes it valid',
      (tester) async {
        final controller = RichTextController();
        addTearDown(controller.dispose);
        final key = GlobalKey<FormState>();
        await tester.pumpWidget(
          _app(
            Form(
              key: key,
              child: RichTextFormField(
                controller: controller,
                label: 'Description *',
                requiredMessage: 'Please enter a description',
              ),
            ),
          ),
        );
        expect(key.currentState!.validate(), isFalse);
        await tester.pump();
        expect(find.text('Please enter a description'), findsOneWidget);

        controller.quill.document.insert(0, 'Something');
        await tester.pump();
        expect(key.currentState!.validate(), isTrue);
        await _unmount(tester);
      },
    );

    testWidgets(
      'a text whose HTML is longer than the limit is refused, even if the written text is short',
      (tester) async {
        final controller = RichTextController();
        addTearDown(controller.dispose);
        final key = GlobalKey<FormState>();
        await tester.pumpWidget(
          _app(
            Form(
              key: key,
              child: RichTextFormField(
                controller: controller,
                label: 'D',
                requiredMessage: 'Required',
                maxLength: 40,
              ),
            ),
          ),
        );
        // 20 characters written, all bold: the tags push the HTML over 40
        controller.quill.document.insert(0, 'abcdefghijklmnopqrst');
        controller.quill.formatText(0, 20, Attribute.bold);
        await tester.pump();
        expect(controller.plainText.length, 20);
        expect(controller.html.length, greaterThan(40));
        expect(key.currentState!.validate(), isFalse);
        await _unmount(tester);
      },
    );
  });
}
