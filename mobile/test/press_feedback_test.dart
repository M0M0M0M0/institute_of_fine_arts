import 'package:fine_arts_mobile/app/tokens.dart';
import 'package:fine_arts_mobile/core/async_view.dart';
import 'package:fine_arts_mobile/shared/shared.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';

/// UX guidelines: press feedback starts on touch-down, haptic on tap, nothing moves with reduced motion,
/// lists appear one item after the other, touch targets are 48.
Widget _app(Widget child, {bool reduce = false}) => MediaQuery(
  data: MediaQueryData(disableAnimations: reduce),
  child: MaterialApp(
    home: Scaffold(body: Center(child: child)),
  ),
);

double _scale(WidgetTester tester) => tester.widget<AnimatedScale>(find.byType(AnimatedScale)).scale;

void main() {
  testWidgets('Pressable shrinks on touch-down, springs back on release and ticks once', (tester) async {
    final haptics = <String>[];
    tester.binding.defaultBinaryMessenger.setMockMethodCallHandler(SystemChannels.platform, (call) async {
      if (call.method == 'HapticFeedback.vibrate') {
        haptics.add(call.arguments as String);
      }
      return null;
    });
    var taps = 0;
    await tester.pumpWidget(_app(Pressable(onTap: () => taps++, child: const SizedBox(width: 120, height: 80))));

    final gesture = await tester.startGesture(tester.getCenter(find.byType(Pressable)));
    await tester.pump(AppMotion.press);
    expect(_scale(tester), 0.97);

    await gesture.up();
    await tester.pumpAndSettle();
    expect(_scale(tester), 1);
    expect(taps, 1);
    expect(haptics, ['HapticFeedbackType.selectionClick']);
  });

  testWidgets('Pressable does not shrink with reduced motion', (tester) async {
    await tester.pumpWidget(_app(Pressable(onTap: () {}, child: const SizedBox(width: 120, height: 80)), reduce: true));
    final gesture = await tester.startGesture(tester.getCenter(find.byType(Pressable)));
    await tester.pump();
    expect(_scale(tester), 1);
    await gesture.up();
  });

  testWidgets('StaggerIn: later items wait a little longer; reduced motion shows everything at once', (tester) async {
    Widget list() => Column(
      children: [for (var i = 0; i < 3; i++) StaggerIn(index: i, child: Text('item $i'))],
    );
    double opacity(int i) => tester
        .widget<FadeTransition>(find.ancestor(of: find.text('item $i'), matching: find.byType(FadeTransition)).first)
        .opacity
        .value;

    await tester.pumpWidget(_app(list()));
    await tester.pump(const Duration(milliseconds: 100));
    expect(opacity(0), greaterThan(opacity(2)));
    await tester.pumpAndSettle();
    expect(opacity(2), 1);

    await tester.pumpWidget(_app(list(), reduce: true));
    await tester.pumpWidget(_app(const SizedBox()));
    await tester.pumpWidget(_app(list(), reduce: true));
    expect(find.descendant(of: find.byType(StaggerIn), matching: find.byType(FadeTransition)), findsNothing);
  });

  test('touch targets are at least 48 x 48', () {
    expect(AppSpacing.touch, greaterThanOrEqualTo(48));
  });

  testWidgets('skeleton hands over to content as a fade through, not a quick swap', (tester) async {
    Widget state(bool loaded) => _app(
      SizedBox(
        width: 300,
        height: 400,
        child: StateSwitcher(
          child: loaded
              ? const Text('content', key: ValueKey('data'))
              : const Skeleton(key: ValueKey('loading'), child: Text('stand-in')),
        ),
      ),
    );
    double opacityOf(Finder f) =>
        tester.widget<FadeTransition>(find.ancestor(of: f, matching: find.byType(FadeTransition)).first).opacity.value;

    await tester.pumpWidget(state(false));
    await tester.pumpWidget(state(true));
    await tester.pump(const Duration(milliseconds: 100));
    // The skeleton is still fading out and the content has not started yet
    expect(find.byType(Skeleton), findsOneWidget);
    expect(opacityOf(find.text('content')), 0);

    await tester.pump(const Duration(milliseconds: 150));
    expect(find.byType(Skeleton), findsNothing);
    final midway = opacityOf(find.text('content'));
    expect(midway, inExclusiveRange(0, 1));

    await tester.pumpAndSettle();
    expect(opacityOf(find.text('content')), 1);
  });

  testWidgets('a skeleton takes exactly the size of the real content and fetches no picture', (tester) async {
    Widget card(Widget child) => _app(SizedBox(width: 320, child: child));
    final competition = Placeholders.competitions.first;
    final real = CompetitionCard(competition: competition, onTap: () {});

    await tester.pumpWidget(card(real));
    final realSize = tester.getSize(find.byType(CompetitionCard));

    await tester.pumpWidget(card(Skeleton(child: StaggerIn(index: 3, child: real))));
    expect(tester.getSize(find.byType(CompetitionCard)), realSize);
    // The cover is a grey block, not a network image; the item does not play its entrance
    expect(find.byType(Image), findsNothing);
    expect(find.byType(SkeletonBlock), findsOneWidget);
    expect(find.descendant(of: find.byType(StaggerIn), matching: find.byType(FadeTransition)), findsNothing);
  });

  testWidgets('pull to refresh keeps the content on screen instead of a skeleton', (tester) async {
    var loads = 0;
    final provider = FutureProvider<String>((ref) async {
      loads++;
      await Future<void>.delayed(const Duration(milliseconds: 300));
      return 'content $loads';
    });
    late WidgetRef widgetRef;
    await tester.pumpWidget(ProviderScope(
      child: _app(Consumer(builder: (context, ref, _) {
        widgetRef = ref;
        return SizedBox(
          width: 300,
          height: 300,
          child: AsyncValueView<String>(
            value: ref.watch(provider),
            onRetry: () {},
            placeholder: 'stand-in',
            data: (v) => Text(v),
          ),
        );
      })),
    ));
    await tester.pumpAndSettle();
    expect(find.text('content 1'), findsOneWidget);

    widgetRef.invalidate(provider);
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 100));
    // Still the old content while the new one loads; no skeleton
    expect(find.text('content 1'), findsOneWidget);
    expect(find.byType(Skeleton), findsNothing);
    await tester.pump(const Duration(milliseconds: 400));
    await tester.pumpAndSettle();
    expect(find.text('content 2'), findsOneWidget);
  });
}
