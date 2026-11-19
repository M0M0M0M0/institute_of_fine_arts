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
}
