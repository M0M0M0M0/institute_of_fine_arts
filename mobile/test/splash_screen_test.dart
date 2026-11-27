import 'package:fine_arts_mobile/app/splash_screen.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  testWidgets('splash covers the app, then fades away and leaves only the app', (tester) async {
    await tester.pumpWidget(const MaterialApp(home: ArtSplashScreen(child: Text('home page'))));

    // At the start the splash (logo + subtitle) sits over the app
    expect(find.text('INSTITUTE OF FINE ARTS'), findsOneWidget);
    expect(find.bySemanticsLabel('IFA'), findsOneWidget);
    expect(find.text('home page'), findsOneWidget); // the app is already built underneath

    // Still there before the fade-out ends, gone after it (no timer left pending)
    await tester.pump(const Duration(milliseconds: 2000));
    expect(find.text('INSTITUTE OF FINE ARTS'), findsOneWidget);
    await tester.pumpAndSettle();
    expect(find.text('INSTITUTE OF FINE ARTS'), findsNothing);
    expect(find.text('home page'), findsOneWidget);
  });

  testWidgets('a tap skips straight to the fade-out', (tester) async {
    await tester.pumpWidget(const MaterialApp(home: ArtSplashScreen(child: Text('home page'))));
    await tester.pump(const Duration(milliseconds: 300));
    await tester.tap(find.text('INSTITUTE OF FINE ARTS'), warnIfMissed: false);
    // Only the 0.65 s fade-out is left, not the remaining 2.5 s
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 700));
    await tester.pump();
    expect(find.text('INSTITUTE OF FINE ARTS'), findsNothing);
    expect(find.text('home page'), findsOneWidget);
  });

  testWidgets('with reduced motion the splash is not shown', (tester) async {
    await tester.pumpWidget(
      const MediaQuery(
        data: MediaQueryData(disableAnimations: true),
        child: MaterialApp(home: ArtSplashScreen(child: Text('home page'))),
      ),
    );
    expect(find.text('INSTITUTE OF FINE ARTS'), findsNothing);
    expect(find.text('home page'), findsOneWidget);
  });
}
