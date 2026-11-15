import 'package:dio/dio.dart';
import 'package:fine_arts_mobile/app/theme.dart';
import 'package:fine_arts_mobile/core/api_client.dart';
import 'package:fine_arts_mobile/features/auth/auth.dart';
import 'package:fine_arts_mobile/features/auth/login_screen.dart';
import 'package:fine_arts_mobile/features/competitions/competition_detail_screen.dart';
import 'package:fine_arts_mobile/features/competitions/competitions.dart';
import 'package:fine_arts_mobile/features/competitions/competitions_screen.dart';
import 'package:fine_arts_mobile/features/gallery/artwork_screen.dart';
import 'package:fine_arts_mobile/features/gallery/gallery.dart';
import 'package:fine_arts_mobile/features/gallery/gallery_screen.dart';
import 'package:fine_arts_mobile/features/home/home_screen.dart';
import 'package:fine_arts_mobile/features/submissions/submissions.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_riverpod/misc.dart' show Override;
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';

// ---------- fakes ----------

class _FakeAuth extends AuthController {
  _FakeAuth(this._user, {this.loginError});

  final AppUser? _user;
  final String? loginError;

  @override
  Future<AppUser?> build() async => _user;

  @override
  Future<void> login(String username, String password) async {
    state = const AsyncLoading();
    state = AsyncError(ApiException(loginError ?? 'Invalid username or password.'), StackTrace.empty);
  }
}

class _FakeGallery extends GalleryRepository {
  _FakeGallery({this.total = 25}) : super(Dio());

  final int total;
  final requestedPages = <int>[];
  final requestedCategories = <String?>[];

  GalleryArtwork _art(int i) => GalleryArtwork(
        submissionId: i,
        title: 'Painting $i',
        studentName: 'Student $i',
        competitionId: 1,
        competitionTitle: 'Hometown Landscapes',
        awardName: i == 1 ? 'First Prize' : null,
      );

  @override
  Future<GalleryPage> page({String? category, int page = 1, int pageSize = 20}) async {
    requestedPages.add(page);
    requestedCategories.add(category);
    final start = (page - 1) * pageSize;
    final end = (start + pageSize).clamp(0, total);
    return GalleryPage(
      items: [for (var i = start + 1; i <= end; i++) _art(i)],
      page: page,
      pageSize: pageSize,
      totalCount: total,
      totalPages: (total / pageSize).ceil(),
    );
  }

  @override
  Future<List<GalleryCategory>> categories() async => const [
        GalleryCategory(group: 'subject', key: 'Animals', label: 'Animals'),
        GalleryCategory(group: 'subject', key: 'Landscape', label: 'Landscape'),
      ];

  @override
  Future<GalleryArtwork> artwork(int submissionId) async => _art(submissionId);
}

CompetitionSummary _comp(
  int id,
  String title,
  CompetitionStatus status, {
  DateTime? start,
  DateTime? end,
}) =>
    CompetitionSummary(
      id: id,
      title: title,
      topic: 'Topic $id',
      startDate: start ?? DateTime(2026, 12, 1),
      endDate: end ?? DateTime(2026, 12, 20),
      status: status,
      daysLeft: 4,
      daysUntilStart: 10,
      submissionCount: 3,
    );

CompetitionDetail _detail(CompetitionStatus status) => CompetitionDetail(
      summary: _comp(7, 'Spring Colours', status),
      description: 'Paint the spring.',
      conditions: 'One painting per student.',
      deadline: DateTime(2026, 12, 20, 23, 59),
      awards: const [
        CompetitionAward(name: 'First Prize', rank: 1, prizeValue: 300, quantity: 1, description: 'Certificate'),
      ],
    );

final _awards = [
  const AwardResult(
      awardName: 'First Prize', rank: 1, studentName: 'Linh Tran', paintingTitle: 'Rice Fields', prizeValue: 300, submissionId: 11),
];

const _student = AppUser(accountId: 1, username: 's', fullName: 'Linh Tran', role: Role.student);

// Pumps [home] inside a router that records pushes as plain text pages.
Future<GoRouter> _pump(
  WidgetTester tester,
  Widget home, {
  List<Override> overrides = const [],
  AppUser? user,
  String? loginError,
  Size size = const Size(412, 915),
}) async {
  tester.view.physicalSize = size;
  tester.view.devicePixelRatio = 1;
  addTearDown(tester.view.reset);
  final router = GoRouter(
    routes: [
      GoRoute(path: '/', builder: (context, state) => home),
      GoRoute(path: '/competitions', builder: (c, s) => const Scaffold(body: Text('page:competitions'))),
      GoRoute(path: '/gallery', builder: (c, s) => const Scaffold(body: Text('page:gallery'))),
      GoRoute(path: '/login', builder: (c, s) => const Scaffold(body: Text('page:login'))),
      GoRoute(path: '/competitions/:id', builder: (c, s) => Scaffold(body: Text('page:competition ${s.pathParameters['id']}'))),
      GoRoute(path: '/gallery/artworks/:id', builder: (c, s) => Scaffold(body: Text('page:artwork ${s.pathParameters['id']}'))),
      GoRoute(path: '/student/submissions/new', builder: (c, s) => Scaffold(body: Text('page:new ${s.uri.queryParameters['competitionId']}'))),
      GoRoute(path: '/student/submissions/:id', builder: (c, s) => Scaffold(body: Text('page:entry ${s.pathParameters['id']}'))),
    ],
  );
  addTearDown(router.dispose);
  await tester.pumpWidget(ProviderScope(
    overrides: [authControllerProvider.overrideWith(() => _FakeAuth(user, loginError: loginError)), ...overrides],
    child: MaterialApp.router(theme: buildTheme(), routerConfig: router),
  ));
  await tester.pumpAndSettle();
  return router;
}

HomeData _homeData({bool empty = false}) => HomeData(
      ongoing: empty ? [] : [_comp(1, 'Autumn Light', CompetitionStatus.ongoing)],
      upcoming: empty ? [] : [_comp(2, 'Winter Dreams', CompetitionStatus.upcoming)],
      latestGradedCompetition: empty ? null : 'Hometown Landscapes',
      latestWinners: empty
          ? []
          : const [
              Winner(
                  studentName: 'Linh Tran',
                  awardName: 'First Prize',
                  rank: 1,
                  paintingTitle: 'Rice Fields',
                  submissionId: 11,
                  hasThumbnail: true),
            ],
    );

void main() {
  group('Home (M-G1)', () {
    testWidgets('shows hero, sections and opens a winner', (tester) async {
      final gallery = _FakeGallery();
      final router = await _pump(tester, const HomeScreen(), overrides: [
        homeProvider.overrideWith((ref) async => _homeData()),
        galleryRepositoryProvider.overrideWithValue(gallery),
      ]);
      expect(find.text('Institute of Fine Arts'), findsOneWidget);
      expect(find.text('Browse competitions'), findsOneWidget);
      expect(find.text('Open now'), findsOneWidget);
      expect(find.text('Autumn Light'), findsOneWidget);
      expect(find.text('Coming soon'), findsOneWidget);

      await tester.scrollUntilVisible(find.text('Rice Fields'), 300, scrollable: find.byType(Scrollable).first);
      expect(find.text('Latest winners'), findsOneWidget);
      expect(find.text('First Prize'), findsWidgets);

      await tester.ensureVisible(find.text('Rice Fields'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Rice Fields'));
      await tester.pumpAndSettle();
      expect(router.state.uri.toString(), '/gallery/artworks/11');
    });

    testWidgets('CTA goes to competitions; gallery strip links to the gallery', (tester) async {
      final router = await _pump(tester, const HomeScreen(), overrides: [
        homeProvider.overrideWith((ref) async => _homeData()),
        galleryRepositoryProvider.overrideWithValue(_FakeGallery()),
      ]);
      await tester.tap(find.text('Browse competitions'));
      await tester.pumpAndSettle();
      expect(router.state.uri.toString(), '/competitions');
      router.go('/');
      await tester.pumpAndSettle();
      await tester.scrollUntilVisible(find.text('See the gallery'), 300, scrollable: find.byType(Scrollable).first);
      expect(find.text('From the gallery'), findsOneWidget);
    });

    testWidgets('error state offers a retry', (tester) async {
      await _pump(tester, const HomeScreen(), overrides: [
        homeProvider.overrideWith((ref) async => throw ApiException('Server is down')),
        galleryRepositoryProvider.overrideWithValue(_FakeGallery()),
      ]);
      expect(find.text('Server is down'), findsOneWidget);
      expect(find.text('Try again'), findsOneWidget);
    });

    testWidgets('empty state when nothing exists', (tester) async {
      await _pump(tester, const HomeScreen(), overrides: [
        homeProvider.overrideWith((ref) async => _homeData(empty: true)),
        galleryRepositoryProvider.overrideWithValue(_FakeGallery(total: 0)),
      ]);
      expect(find.text('Nothing on show yet'), findsOneWidget);
    });
  });

  group('Competitions (M-G2)', () {
    testWidgets('segments filter by status', (tester) async {
      final router = await _pump(tester, const CompetitionsScreen(), overrides: [
        competitionListProvider.overrideWith((ref) async => [
              _comp(1, 'Open One', CompetitionStatus.ongoing),
              _comp(2, 'Soon One', CompetitionStatus.upcoming),
              _comp(3, 'Done One', CompetitionStatus.graded),
              _comp(4, 'Closed One', CompetitionStatus.closed),
            ]),
      ]);
      expect(find.text('Open One'), findsOneWidget);
      expect(find.text('Soon One'), findsNothing);

      await tester.tap(find.text('Upcoming'));
      await tester.pumpAndSettle();
      expect(find.text('Soon One'), findsOneWidget);
      expect(find.text('Open One'), findsNothing);

      await tester.tap(find.text('Results'));
      await tester.pumpAndSettle();
      expect(find.text('Done One'), findsOneWidget);
      expect(find.text('Closed One'), findsNothing);

      await tester.tap(find.text('Done One'));
      await tester.pumpAndSettle();
      expect(router.state.uri.toString(), '/competitions/3');
    });

    testWidgets('empty segment shows an empty state', (tester) async {
      await _pump(tester, const CompetitionsScreen(), overrides: [
        competitionListProvider.overrideWith((ref) async => <CompetitionSummary>[]),
      ]);
      expect(find.text('No competition is open right now'), findsOneWidget);
    });
  });

  group('Competition detail (M-G3)', () {
    List<Override> detailOverrides(CompetitionStatus status, {int? mine}) => [
          competitionDetailProvider(7).overrideWith((ref) async => _detail(status)),
          competitionAwardsProvider(7).overrideWith((ref) async => _awards),
          mySubmissionIdProvider(7).overrideWith((ref) async => mine),
        ];

    testWidgets('content: overview, rules, prizes', (tester) async {
      await _pump(tester, const CompetitionDetailScreen(id: 7), overrides: detailOverrides(CompetitionStatus.ongoing));
      expect(find.text('Spring Colours'), findsOneWidget);
      expect(find.text('Overview'), findsOneWidget);
      expect(find.text('Paint the spring.'), findsOneWidget);
      await tester.scrollUntilVisible(find.text('Prizes'), 300, scrollable: find.byType(Scrollable).first);
      expect(find.text('Rules'), findsOneWidget);
      expect(find.text('First Prize'), findsOneWidget);
    });

    testWidgets('guest, open: sign in to enter', (tester) async {
      final router =
          await _pump(tester, const CompetitionDetailScreen(id: 7), overrides: detailOverrides(CompetitionStatus.ongoing));
      await tester.tap(find.text('Sign in to enter'));
      await tester.pumpAndSettle();
      expect(router.state.uri.toString(), '/login');
    });

    testWidgets('student, open, no entry: submit your entry', (tester) async {
      final router = await _pump(tester, const CompetitionDetailScreen(id: 7),
          user: _student, overrides: detailOverrides(CompetitionStatus.ongoing));
      await tester.tap(find.text('Submit your entry'));
      await tester.pumpAndSettle();
      expect(router.state.uri.toString(), '/student/submissions/new?competitionId=7');
    });

    testWidgets('student, open, has entry: view my entry', (tester) async {
      final router = await _pump(tester, const CompetitionDetailScreen(id: 7),
          user: _student, overrides: detailOverrides(CompetitionStatus.ongoing, mine: 55));
      await tester.tap(find.text('View my entry'));
      await tester.pumpAndSettle();
      expect(router.state.uri.toString(), '/student/submissions/55');
    });

    testWidgets('upcoming: disabled "Opens on"', (tester) async {
      await _pump(tester, const CompetitionDetailScreen(id: 7), overrides: detailOverrides(CompetitionStatus.upcoming));
      final button = find.widgetWithText(FilledButton, 'Opens on 01 Dec 2026');
      expect(button, findsOneWidget);
      expect(tester.widget<FilledButton>(button).onPressed, isNull);
    });

    testWidgets('closed: submissions closed', (tester) async {
      await _pump(tester, const CompetitionDetailScreen(id: 7), overrides: detailOverrides(CompetitionStatus.closed));
      expect(find.widgetWithText(FilledButton, 'Submissions closed'), findsOneWidget);
    });

    testWidgets('graded: winners grid and "View all artworks"', (tester) async {
      final router =
          await _pump(tester, const CompetitionDetailScreen(id: 7), overrides: detailOverrides(CompetitionStatus.graded));
      await tester.tap(find.text('View all artworks'));
      await tester.pumpAndSettle();
      expect(find.text('Winners'), findsOneWidget);
      expect(find.text('Rice Fields'), findsOneWidget);
      await tester.tap(find.text('Rice Fields'));
      await tester.pumpAndSettle();
      expect(router.state.uri.toString(), '/gallery/artworks/11');
    });

    testWidgets('load error shows retry', (tester) async {
      await _pump(tester, const CompetitionDetailScreen(id: 7), overrides: [
        competitionDetailProvider(7).overrideWith((ref) async => throw ApiException('Not found')),
      ]);
      expect(find.text('Not found'), findsOneWidget);
      expect(find.text('Try again'), findsOneWidget);
    });
  });

  group('Gallery (M-G4)', () {
    testWidgets('chips, grid, load more on scroll, category filter', (tester) async {
      final gallery = _FakeGallery(total: 45);
      final router = await _pump(tester, const GalleryScreen(), overrides: [galleryRepositoryProvider.overrideWithValue(gallery)]);
      expect(find.text('All'), findsOneWidget);
      expect(find.text('Animals'), findsOneWidget);
      expect(find.text('Painting 1'), findsOneWidget);
      expect(gallery.requestedPages, [1]);

      await tester.drag(find.byType(CustomScrollView), const Offset(0, -6000));
      await tester.pumpAndSettle();
      expect(gallery.requestedPages.contains(2), isTrue);

      await tester.tap(find.text('Animals'));
      await tester.pumpAndSettle();
      expect(gallery.requestedCategories.last, 'subject/Animals');

      await tester.tap(find.text('Painting 1'));
      await tester.pumpAndSettle();
      expect(router.state.uri.toString(), '/gallery/artworks/1');
    });

    testWidgets('empty gallery', (tester) async {
      await _pump(tester, const GalleryScreen(), overrides: [galleryRepositoryProvider.overrideWithValue(_FakeGallery(total: 0))]);
      expect(find.text('No artworks here yet'), findsOneWidget);
    });
  });

  group('Artwork (M-G5)', () {
    testWidgets('shows label, award and competition link', (tester) async {
      final router = await _pump(tester, const ArtworkScreen(id: 1), overrides: [
        galleryRepositoryProvider.overrideWithValue(_FakeGallery()),
      ]);
      expect(find.text('Painting 1'), findsOneWidget);
      expect(find.text('Student 1'), findsOneWidget);
      expect(find.text('First Prize'), findsWidgets);
      await tester.tap(find.text('Hometown Landscapes'));
      await tester.pumpAndSettle();
      expect(router.state.uri.toString(), '/competitions/1');
    });

    testWidgets('tap opens the zoom viewer', (tester) async {
      await _pump(tester, const ArtworkScreen(id: 2), overrides: [
        galleryRepositoryProvider.overrideWithValue(_FakeGallery()),
      ]);
      await tester.tap(find.byIcon(Icons.zoom_in));
      await tester.pumpAndSettle();
      expect(find.byTooltip('Close'), findsOneWidget);
    });
  });

  group('Sign in (M-G6)', () {
    testWidgets('validates empty fields in English', (tester) async {
      await _pump(tester, const LoginScreen());
      await tester.tap(find.widgetWithText(FilledButton, 'Sign in'));
      await tester.pump();
      expect(find.text('Enter your username'), findsOneWidget);
      expect(find.text('Enter your password'), findsOneWidget);
    });

    testWidgets('shows the API error', (tester) async {
      await _pump(tester, const LoginScreen(), loginError: 'This account is locked.');
      await tester.enterText(find.widgetWithText(TextFormField, 'Username'), 'linh');
      await tester.enterText(find.widgetWithText(TextFormField, 'Password'), 'secret');
      await tester.tap(find.widgetWithText(FilledButton, 'Sign in'));
      await tester.pumpAndSettle();
      expect(find.text('This account is locked.'), findsOneWidget);
    });
  });
}
