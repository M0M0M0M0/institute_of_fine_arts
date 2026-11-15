import 'package:fine_arts_mobile/app/theme.dart';
import 'package:fine_arts_mobile/app/tokens.dart';
import 'package:fine_arts_mobile/features/competitions/competitions.dart';
import 'package:fine_arts_mobile/shared/shared.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

Widget _app(Widget child) => ProviderScope(
      child: MaterialApp(theme: buildTheme(), home: Scaffold(body: SingleChildScrollView(child: child))),
    );

CompetitionSummary _competition({CompetitionStatus status = CompetitionStatus.ongoing, bool hasCover = false}) =>
    CompetitionSummary(
      id: 1,
      title: 'Spring Colours',
      topic: 'Landscape',
      startDate: DateTime(2026, 12, 1),
      endDate: DateTime(2026, 12, 20),
      status: status,
      daysLeft: 4,
      submissionCount: 12,
      hasCover: hasCover,
    );

void main() {
  group('theme', () {
    test('uses the web palette and fonts', () {
      final theme = buildTheme();
      expect(theme.colorScheme.primary, AppColors.ink);
      expect(theme.colorScheme.surface, AppColors.wall);
      expect(theme.colorScheme.error, AppColors.danger);
      expect(theme.textTheme.headlineMedium!.fontFamily, AppFonts.display);
      expect(theme.textTheme.titleLarge!.fontFamily, AppFonts.display);
      expect(theme.textTheme.bodyMedium!.fontFamily, AppFonts.body);
      expect(theme.textTheme.labelLarge!.fontFamily, AppFonts.body);
      expect(theme.textTheme.bodyMedium!.fontSize, greaterThanOrEqualTo(14));
    });

    testWidgets('app bar and navigation bar are navy', (tester) async {
      await tester.pumpWidget(MaterialApp(
        theme: buildTheme(),
        home: Scaffold(
          appBar: AppBar(title: const Text('Title')),
          bottomNavigationBar: NavigationBar(
            selectedIndex: 0,
            destinations: const [
              NavigationDestination(icon: Icon(Icons.home), label: 'Home'),
              NavigationDestination(icon: Icon(Icons.palette), label: 'Competitions'),
            ],
          ),
        ),
      ));
      expect(tester.widget<Material>(find.descendant(of: find.byType(AppBar), matching: find.byType(Material)).first).color,
          AppColors.ink);
      expect(find.text('Competitions'), findsOneWidget);
    });
  });

  group('StatusChip', () {
    testWidgets('shows English competition status labels', (tester) async {
      await tester.pumpWidget(_app(Column(children: [
        StatusChip(CompetitionStatus.upcoming),
        StatusChip(CompetitionStatus.ongoing),
        StatusChip(CompetitionStatus.closed),
        StatusChip(CompetitionStatus.graded),
      ])));
      expect(find.text('Upcoming'), findsOneWidget);
      expect(find.text('Open'), findsOneWidget);
      expect(find.text('Closed'), findsOneWidget);
      expect(find.text('Results published'), findsOneWidget);
    });

    test('status colours match the plan', () {
      expect(competitionStatusColor(CompetitionStatus.upcoming), const Color(0xFF2C5D8A));
      expect(competitionStatusColor(CompetitionStatus.ongoing), const Color(0xFF2F6B4F));
      expect(competitionStatusColor(CompetitionStatus.graded), AppColors.ink);
    });

    testWidgets('custom chip', (tester) async {
      await tester.pumpWidget(_app(const StatusChip.custom(label: 'Sold', color: AppColors.brassText)));
      expect(find.text('Sold'), findsOneWidget);
    });
  });

  group('artwork widgets', () {
    testWidgets('ArtworkFrame awarded uses the brass border', (tester) async {
      await tester.pumpWidget(_app(const SizedBox(
        width: 200,
        child: Column(children: [
          ArtworkFrame(awarded: true, child: SizedBox.expand()),
          ArtworkFrame(child: SizedBox.expand()),
        ]),
      )));
      final decorations = tester
          .widgetList<DecoratedBox>(find.descendant(of: find.byType(ArtworkFrame), matching: find.byType(DecoratedBox)))
          .map((d) => d.decoration)
          .whereType<BoxDecoration>()
          .where((d) => d.color == AppColors.stone)
          .toList();
      expect(decorations, hasLength(2));
      expect((decorations[0].border as Border).top.color, AppColors.brass);
      expect((decorations[1].border as Border).top.color, AppColors.line);
    });

    testWidgets('WallLabel shows title in italic, artist, award and meta', (tester) async {
      await tester.pumpWidget(_app(const WallLabel(
        title: 'Dawn',
        artist: 'An Nguyen',
        awardName: 'First Prize',
        meta: 'Spring Colours',
      )));
      expect(tester.widget<Text>(find.text('Dawn')).style!.fontStyle, FontStyle.italic);
      expect(find.text('An Nguyen'), findsOneWidget);
      expect(find.text('First Prize'), findsOneWidget);
      expect(find.text('Spring Colours'), findsOneWidget);
    });

    testWidgets('ArtworkTile is tappable', (tester) async {
      var taps = 0;
      await tester.pumpWidget(_app(SizedBox(
        width: 180,
        child: ArtworkTile(
          image: const ColoredBox(color: Colors.grey),
          title: 'Dawn',
          artist: 'An',
          onTap: () => taps++,
        ),
      )));
      await tester.tap(find.text('Dawn'));
      expect(taps, 1);
    });
  });

  group('images', () {
    testWidgets('PublicImage shows the error state when loading fails', (tester) async {
      await tester.pumpWidget(_app(const SizedBox(
        width: 200,
        height: 150,
        child: PublicImage(url: 'http://localhost:1/none.jpg', semanticLabel: 'x'),
      )));
      await tester.pump(const Duration(milliseconds: 200));
      await tester.pump();
      expect(find.byType(ImagePlaceholder), findsOneWidget);
    });
  });

  group('CompetitionCard', () {
    testWidgets('shows status, dates, countdown and entries', (tester) async {
      var tapped = false;
      await tester.pumpWidget(_app(CompetitionCard(competition: _competition(), onTap: () => tapped = true)));
      expect(find.text('Spring Colours'), findsOneWidget);
      expect(find.text('Open'), findsOneWidget);
      expect(find.text('4 days left'), findsOneWidget);
      expect(find.text('12 entries'), findsOneWidget);
      expect(find.textContaining('01 Dec 2026'), findsOneWidget);
      await tester.tap(find.text('Spring Colours'));
      expect(tapped, isTrue);
    });

    testWidgets('upcoming competition shows start countdown and no entries count', (tester) async {
      final c = CompetitionSummary(
        id: 2,
        title: 'Winter',
        topic: null,
        startDate: DateTime(2027, 1, 5),
        endDate: DateTime(2027, 1, 25),
        status: CompetitionStatus.upcoming,
        daysLeft: 0,
        daysUntilStart: 3,
        submissionCount: 0,
      );
      await tester.pumpWidget(_app(CompetitionCard(competition: c, onTap: () {})));
      expect(find.text('Starts in 3 days'), findsOneWidget);
      expect(find.textContaining('entries'), findsNothing);
    });
  });

  group('layout widgets', () {
    testWidgets('SectionHeader action only shows with a handler', (tester) async {
      var tapped = false;
      await tester.pumpWidget(_app(Column(children: [
        SectionHeader('Open now', actionLabel: 'See all', onAction: () => tapped = true),
        const SectionHeader('Coming soon', actionLabel: 'See all'),
      ])));
      expect(find.text('Open now'), findsOneWidget);
      expect(find.text('See all'), findsOneWidget);
      await tester.tap(find.text('See all'));
      expect(tapped, isTrue);
    });

    testWidgets('EmptyState shows title, message and action', (tester) async {
      var tapped = false;
      await tester.pumpWidget(MaterialApp(
        theme: buildTheme(),
        home: Scaffold(
          body: EmptyState(title: 'Nothing here', message: 'Try later', actionLabel: 'Reload', onAction: () => tapped = true),
        ),
      ));
      expect(find.text('Nothing here'), findsOneWidget);
      expect(find.text('Try later'), findsOneWidget);
      await tester.tap(find.text('Reload'));
      expect(tapped, isTrue);
    });

    testWidgets('ProgressBarRow shows the ratio and clamps the fraction', (tester) async {
      await tester.pumpWidget(_app(const Column(children: [
        ProgressBarRow(label: 'Graded', value: 3, total: 4),
        ProgressBarRow(label: 'Empty', value: 0, total: 0),
      ])));
      expect(find.text('3 / 4'), findsOneWidget);
      final bars = tester.widgetList<LinearProgressIndicator>(find.byType(LinearProgressIndicator)).toList();
      expect(bars[0].value, 0.75);
      expect(bars[1].value, 0);
    });

    testWidgets('HeroHeader shows title, subtitle and CTA', (tester) async {
      var tapped = false;
      await tester.pumpWidget(_app(HeroHeader(
        title: 'Institute of Fine Arts',
        subtitle: 'Where art begins',
        ctaLabel: 'Browse competitions',
        onCta: () => tapped = true,
      )));
      expect(find.text('Institute of Fine Arts'), findsOneWidget);
      expect(find.text('Where art begins'), findsOneWidget);
      await tester.tap(find.text('Browse competitions'));
      expect(tapped, isTrue);
    });
  });

  group('ArtworkViewer', () {
    testWidgets('shows the caption, zooms on double tap and closes', (tester) async {
      await tester.pumpWidget(ProviderScope(
        child: MaterialApp(
          theme: buildTheme(),
          home: Builder(
            builder: (context) => Scaffold(
              body: TextButton(
                onPressed: () => ArtworkViewer.open(
                  context,
                  const ArtworkViewer(image: ColoredBox(color: Colors.grey), title: 'Dawn', subtitle: 'An Nguyen'),
                ),
                child: const Text('open'),
              ),
            ),
          ),
        ),
      ));
      await tester.tap(find.text('open'));
      await tester.pumpAndSettle();
      expect(find.text('Dawn'), findsOneWidget);
      expect(find.text('An Nguyen'), findsOneWidget);

      final viewer = tester.widget<InteractiveViewer>(find.byType(InteractiveViewer));
      expect(viewer.transformationController!.value.getMaxScaleOnAxis(), 1);
      await tester.tapAt(const Offset(200, 300));
      await tester.pump(const Duration(milliseconds: 50));
      await tester.tapAt(const Offset(200, 300));
      await tester.pumpAndSettle();
      expect(viewer.transformationController!.value.getMaxScaleOnAxis(), greaterThan(2));

      await tester.tap(find.byTooltip('Close'));
      await tester.pumpAndSettle();
      expect(find.byType(ArtworkViewer), findsNothing);
    });
  });
}
