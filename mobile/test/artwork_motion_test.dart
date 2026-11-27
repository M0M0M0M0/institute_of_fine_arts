import 'package:fine_arts_mobile/app/theme.dart';
import 'package:fine_arts_mobile/app/tokens.dart';
import 'package:fine_arts_mobile/shared/shared.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

Widget _withMotion({required bool reduce, required Widget child}) => MediaQuery(
      data: MediaQueryData(disableAnimations: reduce),
      child: child,
    );

/// Tiles sit in a narrow grid column on a phone, not across the whole screen.
Widget _app(Widget child) => ProviderScope(
      child: MaterialApp(
        theme: buildTheme(),
        home: Scaffold(
          body: SingleChildScrollView(child: Align(alignment: Alignment.topLeft, child: SizedBox(width: 160, child: child))),
        ),
      ),
    );

Future<void> _openViewer(WidgetTester tester, ArtworkViewer viewer) async {
  await tester.pumpWidget(ProviderScope(
    child: MaterialApp(
      theme: buildTheme(),
      home: Builder(
        builder: (context) => Scaffold(
          body: TextButton(onPressed: () => ArtworkViewer.open(context, viewer), child: const Text('open')),
        ),
      ),
    ),
  ));
  await tester.tap(find.text('open'));
  await tester.pumpAndSettle();
}

void main() {
  group('motionDuration', () {
    testWidgets('keeps the duration normally', (tester) async {
      late Duration result;
      await tester.pumpWidget(_withMotion(
        reduce: false,
        child: Builder(builder: (context) {
          result = motionDuration(context, AppMotion.med);
          return const SizedBox();
        }),
      ));
      expect(result, AppMotion.med);
    });

    testWidgets('is zero when the OS asks to reduce motion', (tester) async {
      late Duration result;
      await tester.pumpWidget(_withMotion(
        reduce: true,
        child: Builder(builder: (context) {
          result = motionDuration(context, AppMotion.med);
          return const SizedBox();
        }),
      ));
      expect(result, Duration.zero);
    });
  });

  group('ArtworkTile hero', () {
    testWidgets('has a hero tagged artwork-<id> when hero is on', (tester) async {
      await tester.pumpWidget(_app(ArtworkTile.public(submissionId: 5, title: 'Dawn', hero: true)));
      final hero = tester.widget<Hero>(find.byType(Hero));
      expect(hero.tag, 'artwork-5');
      expect(hero.tag, artworkHeroTag(5));
    });

    testWidgets('has no hero by default (Home shows the same artwork twice)', (tester) async {
      await tester.pumpWidget(_app(ArtworkTile.public(submissionId: 5, title: 'Dawn')));
      expect(find.byType(Hero), findsNothing);
    });

    testWidgets('different artworks get different tags on one screen', (tester) async {
      await tester.pumpWidget(_app(Column(children: [
        ArtworkTile.public(submissionId: 1, title: 'One', hero: true),
        ArtworkTile.public(submissionId: 2, title: 'Two', hero: true),
      ])));
      final tags = tester.widgetList<Hero>(find.byType(Hero)).map((h) => h.tag).toList();
      expect(tags, ['artwork-1', 'artwork-2']);
    });
  });

  group('ArtworkViewer background', () {
    const backdrop = ColoredBox(color: AppColors.stone);
    const image = ColoredBox(color: AppColors.mist);

    testWidgets('is blurred by default when a backdrop is given', (tester) async {
      await _openViewer(tester, const ArtworkViewer(image: image, backdrop: backdrop, title: 'Dawn'));
      expect(find.byType(ImageFiltered), findsOneWidget);
      expect(find.text('Dawn'), findsOneWidget);
    });

    testWidgets('blur can be turned off', (tester) async {
      await _openViewer(
        tester,
        const ArtworkViewer(image: image, backdrop: backdrop, blurBackground: false),
      );
      expect(find.byType(ImageFiltered), findsNothing);
    });

    testWidgets('without a backdrop the background is plain navy ink', (tester) async {
      await _openViewer(tester, const ArtworkViewer(image: image));
      expect(find.byType(ImageFiltered), findsNothing);
      expect(tester.widget<Scaffold>(find.byType(Scaffold).last).backgroundColor, AppColors.ink);
    });

    testWidgets('the public viewer builds its own backdrop', (tester) async {
      await _openViewer(tester, ArtworkViewer.public(submissionId: 7, title: 'Dawn'));
      expect(find.byType(ImageFiltered), findsOneWidget);
    });
  });
}
