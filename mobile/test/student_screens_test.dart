import 'dart:convert';
import 'dart:typed_data';

import 'package:fine_arts_mobile/app/theme.dart';
import 'package:fine_arts_mobile/features/account/account_screen.dart';
import 'package:fine_arts_mobile/features/auth/auth.dart';
import 'package:fine_arts_mobile/features/competitions/competitions.dart';
import 'package:fine_arts_mobile/features/exhibitions/exhibitions.dart';
import 'package:fine_arts_mobile/features/exhibitions/student_exhibition_screens.dart';
import 'package:fine_arts_mobile/features/student/dashboard.dart';
import 'package:fine_arts_mobile/features/student/student_competitions_screen.dart';
import 'package:fine_arts_mobile/features/student/student_home_screen.dart';
import 'package:fine_arts_mobile/features/submissions/my_submissions_screen.dart';
import 'package:fine_arts_mobile/features/submissions/submission_detail_screen.dart';
import 'package:fine_arts_mobile/features/submissions/submission_form_screen.dart';
import 'package:fine_arts_mobile/features/submissions/submissions.dart';
import 'package:fine_arts_mobile/shared/shared.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_riverpod/misc.dart' show Override;
import 'package:flutter_test/flutter_test.dart';

// 1x1 transparent PNG, used as the picture of every private image.
final Uint8List _png = base64Decode(
    'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==');

class _FakeAuth extends AuthController {
  @override
  Future<AppUser?> build() async => const AppUser(
        accountId: 1,
        username: 'student01',
        fullName: 'Nguyen Van An',
        role: Role.student,
        email: 'an@example.com',
        code: 'HV001',
        className: 'Class 10A',
      );
}

CompetitionSummary _competition(int id, CompetitionStatus status, {String title = 'Spring Colours'}) =>
    CompetitionSummary(
      id: id,
      title: title,
      topic: null,
      startDate: DateTime(2026, 11, 1),
      endDate: DateTime(2026, 12, 20),
      status: status,
      daysLeft: 5,
      daysUntilStart: 3,
      submissionCount: 4,
    );

SubmissionListItem _item({
  int id = 7,
  int competitionId = 1,
  CompetitionStatus status = CompetitionStatus.ongoing,
  SubmissionRating? rating,
  bool canEdit = true,
  String title = 'Sunset',
}) =>
    SubmissionListItem(
      id: id,
      competitionId: competitionId,
      competitionTitle: 'Spring Colours',
      competitionStatus: status,
      title: title,
      submittedAt: DateTime(2026, 11, 10, 9, 30),
      updatedAt: null,
      canEdit: canEdit,
      rating: rating,
    );

SubmissionDetail _detail({bool evaluated = false, bool canEdit = true, bool awarded = false}) => SubmissionDetail(
      id: 7,
      competitionId: 1,
      competitionTitle: 'Spring Colours',
      competitionStatus: evaluated ? CompetitionStatus.graded : CompetitionStatus.ongoing,
      title: 'Sunset',
      description: 'A quiet evening',
      textType: TextType.poem,
      textContent: 'The sun goes down',
      publicConsent: true,
      originalFileName: 'sunset.jpg',
      submittedAt: DateTime(2026, 11, 10, 9, 30),
      updatedAt: null,
      canEdit: canEdit,
      evaluation: evaluated
          ? SubmissionEvaluation(
              rating: SubmissionRating.best,
              positivePoints: 'Harmonious colours',
              negativePoints: null,
              improvementAreas: 'Try more contrast',
              evaluatedAt: DateTime(2026, 11, 20),
            )
          : null,
      awards: awarded ? const [SubmissionAward(awardName: 'First Prize', rank: 1)] : const [],
    );

Future<void> _pump(WidgetTester tester, Widget home, {List<Override> overrides = const []}) async {
  tester.view.physicalSize = const Size(360, 740);
  tester.view.devicePixelRatio = 1;
  addTearDown(tester.view.reset);
  await tester.pumpWidget(ProviderScope(
    overrides: [
      authControllerProvider.overrideWith(_FakeAuth.new),
      authImageProvider.overrideWith((ref, key) => _png),
      ...overrides,
    ],
    child: MaterialApp(theme: buildTheme(), home: home),
  ));
  await tester.pumpAndSettle();
}

void main() {
  group('Home (M-S1)', () {
    testWidgets('greets by time of day and shows the task cards', (tester) async {
      await _pump(
        tester,
        StudentHomeScreen(now: DateTime(2026, 11, 15, 9)),
        overrides: [
          studentDashboardProvider.overrideWith((ref) => StudentDashboard(
                openToEnter: [_competition(2, CompetitionStatus.ongoing, title: 'Autumn Light')],
                editable: [_item()],
                newResults: [_item(id: 8, status: CompetitionStatus.graded, rating: SubmissionRating.best, title: 'Harbor')],
                pendingProposals: 2,
                unpaidSales: 1,
              )),
          competitionListProvider.overrideWith((ref) => [_competition(1, CompetitionStatus.ongoing)]),
        ],
      );
      expect(find.text('Good morning, An'), findsOneWidget);
      expect(find.text('Open for entries'), findsOneWidget);
      expect(find.text('Autumn Light'), findsOneWidget);
      expect(find.text('Submit'), findsOneWidget);
      expect(find.text('Still editable'), findsOneWidget);
      expect(find.text('Sunset'), findsOneWidget);
      expect(find.text('Closes 20 Dec 2026'), findsOneWidget);
      await tester.scrollUntilVisible(find.text('New results'), 200, scrollable: find.byType(Scrollable).first);
      expect(find.text('Best'), findsOneWidget);
      await tester.scrollUntilVisible(find.text('2 proposals waiting for review'), 200, scrollable: find.byType(Scrollable).first);
      expect(find.text('1 sold work, payment not received yet'), findsOneWidget);
    });

    test('greeting changes with the hour', () {
      expect(greetingFor(DateTime(2026, 1, 1, 8)), 'Good morning');
      expect(greetingFor(DateTime(2026, 1, 1, 14)), 'Good afternoon');
      expect(greetingFor(DateTime(2026, 1, 1, 20)), 'Good evening');
    });

    testWidgets('shows "You\'re all caught up" when there is nothing to do', (tester) async {
      await _pump(
        tester,
        StudentHomeScreen(now: DateTime(2026, 11, 15, 20)),
        overrides: [
          studentDashboardProvider.overrideWith((ref) => const StudentDashboard()),
          competitionListProvider.overrideWith((ref) => const <CompetitionSummary>[]),
        ],
      );
      expect(find.text('Good evening, An'), findsOneWidget);
      expect(find.text("You're all caught up"), findsOneWidget);
    });
  });

  group('Competitions (M-S2)', () {
    testWidgets('segments filter the list; open competitions offer Submit or View my entry', (tester) async {
      await _pump(
        tester,
        const StudentCompetitionsScreen(),
        overrides: [
          competitionListProvider.overrideWith((ref) => [
                _competition(1, CompetitionStatus.ongoing, title: 'Entered One'),
                _competition(2, CompetitionStatus.ongoing, title: 'Fresh One'),
                _competition(3, CompetitionStatus.upcoming, title: 'Later One'),
                _competition(4, CompetitionStatus.graded, title: 'Done One'),
              ]),
          allMySubmissionsProvider.overrideWith((ref) => [_item()]),
        ],
      );
      expect(find.text('Entered One'), findsOneWidget);
      expect(find.text('Fresh One'), findsOneWidget);
      expect(find.text('Submit'), findsOneWidget);
      expect(find.text('View my entry'), findsOneWidget);
      expect(find.text('Later One'), findsNothing);

      await tester.tap(find.text('Upcoming'));
      await tester.pumpAndSettle();
      expect(find.text('Later One'), findsOneWidget);
      expect(find.text('Fresh One'), findsNothing);

      await tester.tap(find.text('Results'));
      await tester.pumpAndSettle();
      expect(find.text('Done One'), findsOneWidget);
    });
  });

  group('My entries (M-S4)', () {
    testWidgets('lists entries with their state and the award', (tester) async {
      await _pump(
        tester,
        const MySubmissionsScreen(),
        overrides: [
          mySubmissionsProvider.overrideWith((ref) => [
                _item(id: 1, title: 'Fresh'),
                _item(id: 2, title: 'Waiting', status: CompetitionStatus.closed, canEdit: false),
                _item(id: 3, competitionId: 9, title: 'Winner', status: CompetitionStatus.graded, rating: SubmissionRating.best, canEdit: false),
              ]),
          allMySubmissionsProvider.overrideWith((ref) => [
                _item(id: 3, competitionId: 9, title: 'Winner', status: CompetitionStatus.graded, rating: SubmissionRating.best),
              ]),
          competitionListProvider.overrideWith((ref) => const <CompetitionSummary>[]),
          competitionAwardsProvider.overrideWith((ref, id) => const [
                AwardResult(
                    awardName: 'First Prize', rank: 1, studentName: 'An', paintingTitle: 'Winner', prizeValue: 1000000, submissionId: 3),
              ]),
        ],
      );
      expect(find.text('Submitted'), findsOneWidget);
      expect(find.text('Waiting for results'), findsOneWidget);
      expect(find.text('Best'), findsWidgets);
      expect(find.text('First Prize'), findsWidgets);
      expect(find.text('Competition'), findsOneWidget); // filter label
      expect(find.text('Rating'), findsOneWidget);
      expect(find.text('Awards'), findsOneWidget); // merged results section
    });

    testWidgets('empty state invites the student to browse competitions', (tester) async {
      await _pump(
        tester,
        const MySubmissionsScreen(),
        overrides: [
          mySubmissionsProvider.overrideWith((ref) => const <SubmissionListItem>[]),
          allMySubmissionsProvider.overrideWith((ref) => const <SubmissionListItem>[]),
          competitionListProvider.overrideWith((ref) => const <CompetitionSummary>[]),
        ],
      );
      expect(find.text('No entries yet'), findsOneWidget);
      expect(find.text('Browse competitions'), findsOneWidget);
    });
  });

  group('Entry detail (M-S5)', () {
    testWidgets('open competition: Edit and Delete, no evaluation card', (tester) async {
      await _pump(tester, const SubmissionDetailScreen(id: 7),
          overrides: [submissionDetailProvider.overrideWith((ref, id) => _detail())]);
      expect(find.text('Sunset'), findsOneWidget);
      await tester.scrollUntilVisible(find.text('Edit'), 200, scrollable: find.byType(Scrollable).first);
      expect(find.text('Edit'), findsOneWidget);
      expect(find.text('Delete'), findsOneWidget);
      expect(find.text('Evaluation'), findsNothing);
      expect(find.text('Submitted'), findsWidgets);
    });

    testWidgets('published: evaluation card, award, no Edit / Delete', (tester) async {
      await _pump(tester, const SubmissionDetailScreen(id: 7),
          overrides: [submissionDetailProvider.overrideWith((ref, id) => _detail(evaluated: true, canEdit: false, awarded: true))]);
      await tester.scrollUntilVisible(find.text('Strengths'), 200, scrollable: find.byType(Scrollable).first);
      expect(find.text('Evaluation'), findsOneWidget);
      expect(find.text('Strengths'), findsOneWidget);
      expect(find.text('Harmonious colours'), findsOneWidget);
      expect(find.text('To improve'), findsOneWidget);
      await tester.scrollUntilVisible(find.text('Award'), 200, scrollable: find.byType(Scrollable).first);
      expect(find.text('First Prize'), findsWidgets);
      expect(find.text('Edit'), findsNothing);
      expect(find.text('Delete'), findsNothing);
    });

    testWidgets('tapping the picture opens the zoom viewer', (tester) async {
      await _pump(tester, const SubmissionDetailScreen(id: 7),
          overrides: [submissionDetailProvider.overrideWith((ref, id) => _detail())]);
      await tester.tap(find.byType(ArtworkFrame));
      await tester.pumpAndSettle();
      expect(find.byType(ArtworkViewer), findsOneWidget);
    });
  });

  group('Submit / edit entry (M-S3)', () {
    testWidgets('shows the pictures note, text type chips and field errors', (tester) async {
      await _pump(tester, const SubmissionFormScreen(competitionId: 1), overrides: [
        competitionDetailProvider.overrideWith((ref, id) => CompetitionDetail(
              summary: _competition(1, CompetitionStatus.ongoing),
              description: null,
              conditions: null,
              deadline: DateTime(2026, 12, 20, 23, 59),
              awards: const [],
            )),
      ]);
      expect(find.textContaining('JPG, PNG or WebP, up to 10 MB each'), findsOneWidget);
      expect(find.text('Choose pictures'), findsOneWidget);
      expect(find.text('Deadline: 20 Dec 2026 23:59'), findsOneWidget);
      await tester.scrollUntilVisible(find.widgetWithText(ChoiceChip, 'Story'), 200, scrollable: find.byType(Scrollable).first);
      await tester.tap(find.widgetWithText(ChoiceChip, 'Story'));
      await tester.pumpAndSettle();
      expect(tester.widget<ChoiceChip>(find.widgetWithText(ChoiceChip, 'Story')).selected, isTrue);

      await tester.ensureVisible(find.text('Submit entry').first);
      await tester.pumpAndSettle();
      await tester.tap(find.text('Submit entry').first);
      await tester.pumpAndSettle();
      await tester.drag(find.byType(Scrollable).first, const Offset(0, 3000));
      await tester.pumpAndSettle();
      expect(find.text('Please enter a title'), findsOneWidget);
      expect(find.text('Choose at least one picture (JPG, PNG or WebP)'), findsOneWidget);
    });
  });

  group('Exhibitions (M-S6)', () {
    testWidgets('upcoming exhibitions and my proposals', (tester) async {
      await _pump(
        tester,
        const Scaffold(body: StudentExhibitionsTab()),
        overrides: [
          studentExhibitionsProvider.overrideWith((ref) => [
                ExhibitionSummary(
                  id: 1,
                  name: 'Winter Salon',
                  venue: 'Main Hall',
                  city: 'Hanoi',
                  startDate: DateTime(2026, 12, 1),
                  endDate: DateTime(2026, 12, 10),
                  status: ExhibitionStatus.upcoming,
                  daysUntilStart: 16,
                  entryCount: 0,
                  pendingCount: 0,
                ),
              ]),
          myEntriesProvider.overrideWith((ref) => [
                StudentEntry(
                  id: 5,
                  exhibitionId: 1,
                  exhibitionName: 'Winter Salon',
                  venue: 'Main Hall',
                  city: 'Hanoi',
                  exhibitionStartDate: DateTime(2026, 12, 1),
                  exhibitionEndDate: DateTime(2026, 12, 10),
                  exhibitionStatus: ExhibitionStatus.upcoming,
                  submissionTitle: 'Harbor',
                  competitionTitle: 'Spring Colours',
                  askingPrice: 2000000,
                  status: EntryStatus.sold,
                  statusReason: null,
                  soldPrice: 2500000,
                  soldDate: DateTime(2026, 12, 5),
                  paidToStudent: true,
                  paidAmount: 2500000,
                  paidDate: DateTime(2026, 12, 6),
                  canEdit: false,
                  rowVersion: 'AAA',
                  submissionId: 3,
                ),
              ]),
        ],
      );
      expect(find.text('Winter Salon'), findsOneWidget);
      expect(find.text('Upcoming'), findsOneWidget);
      expect(find.text('Starts in 16 days'), findsOneWidget);
      await tester.tap(find.text('My proposals'));
      await tester.pumpAndSettle();
      expect(find.text('Harbor'), findsOneWidget);
      expect(find.text('Sold · paid'), findsOneWidget);
      expect(find.text('Asking price \$2,000,000'), findsOneWidget);
      expect(find.text('Sold for \$2,500,000'), findsOneWidget);
    });
  });

  group('Account (M-S7)', () {
    test('initials', () {
      expect(initialsOf('Nguyen Van An'), 'NA');
      expect(initialsOf('An'), 'A');
      expect(initialsOf('  '), '?');
    });

    testWidgets('shows avatar initials, code, email and class; Sign out', (tester) async {
      await _pump(tester, const AccountScreen());
      expect(find.text('NA'), findsOneWidget);
      expect(find.text('Nguyen Van An'), findsOneWidget);
      expect(find.text('Student code'), findsOneWidget);
      expect(find.text('HV001'), findsOneWidget);
      expect(find.text('an@example.com'), findsOneWidget);
      expect(find.text('Class 10A'), findsOneWidget);
      expect(find.text('Sign out'), findsOneWidget);
    });
  });
}
