import 'dart:async';
import 'dart:typed_data';

import 'package:fine_arts_mobile/app/theme.dart';
import 'package:fine_arts_mobile/features/auth/auth.dart';
import 'package:fine_arts_mobile/features/awarding/awarding.dart';
import 'package:fine_arts_mobile/features/awarding/awarding_competitions_screen.dart';
import 'package:fine_arts_mobile/features/awarding/awarding_detail_screen.dart';
import 'package:fine_arts_mobile/features/competitions/competitions.dart';
import 'package:fine_arts_mobile/features/exhibitions/exhibitions.dart';
import 'package:fine_arts_mobile/features/exhibitions/staff_entry_screen.dart';
import 'package:fine_arts_mobile/features/exhibitions/staff_exhibition_screens.dart';
import 'package:fine_arts_mobile/features/grading/grading.dart';
import 'package:fine_arts_mobile/features/grading/grading_competitions_screen.dart';
import 'package:fine_arts_mobile/features/grading/grading_detail_screen.dart';
import 'package:fine_arts_mobile/features/grading/grading_list_screen.dart';
import 'package:fine_arts_mobile/features/staff/dashboard.dart';
import 'package:fine_arts_mobile/features/submissions/submissions.dart';
import 'package:fine_arts_mobile/features/staff/staff_home_screen.dart';
import 'package:fine_arts_mobile/shared/auth_image.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_riverpod/misc.dart' show Override;
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';

class _FakeAuth extends AuthController {
  @override
  Future<AppUser?> build() async =>
      const AppUser(accountId: 1, username: 'staff01', fullName: 'Tran Thi Lan', role: Role.staff);
}

/// Pictures never load in tests: the future stays pending, so the placeholder shows.
final _imageOverride = authImageProvider.overrideWith((ref, key) => Completer<Uint8List>().future);

Future<void> _pump(WidgetTester tester, Widget home, List<Override> overrides,
    {GoRouter? router, Size size = const Size(412, 3000)}) async {
  tester.view.physicalSize = size;
  tester.view.devicePixelRatio = 1;
  addTearDown(tester.view.reset);
  final scope = ProviderScope(
    overrides: [authControllerProvider.overrideWith(_FakeAuth.new), _imageOverride, ...overrides],
    child: router != null
        ? MaterialApp.router(theme: buildTheme(), routerConfig: router)
        : MaterialApp(theme: buildTheme(), home: home),
  );
  await tester.pumpWidget(scope);
  await tester.pump();
  await tester.pump();
}

GoRouter _router(Widget home) => GoRouter(routes: [
      GoRoute(path: '/', builder: (context, state) => home),
      GoRoute(path: '/staff/competitions/:id/submissions', builder: (c, s) => Text('GRADING ${s.pathParameters['id']}')),
      GoRoute(path: '/staff/awarding/:id', builder: (c, s) => Text('AWARDING ${s.pathParameters['id']}')),
      GoRoute(path: '/staff/exhibitions', builder: (c, s) => Text('EXHIBITIONS ${s.uri.query}')),
    ]);

Map<String, dynamic> _gradingDetail({bool canEvaluate = true, String? lockReason, Map<String, dynamic>? evaluation}) => {
      'id': 7,
      'competitionId': 2,
      'competitionTitle': 'Autumn Colours',
      'competitionStatus': 'Closed',
      'title': 'Still Sea',
      'description': 'Oil on canvas',
      'textType': 'Poem',
      'textContent': 'Waves at dusk',
      'studentName': 'Le Hoang Nam',
      'studentCode': 'HV001',
      'submittedAt': '2026-11-01T10:00:00',
      'updatedAt': null,
      'canEvaluate': canEvaluate,
      'canDeleteEvaluation': evaluation != null,
      'lockReason': lockReason,
      'evaluation': evaluation,
      'nextUnevaluatedId': 8,
      'total': 5,
      'evaluated': 2,
    };

Map<String, dynamic> _awardingDetail({bool published = false, bool canMarkDelivered = true}) => {
      'id': 7,
      'title': 'Autumn Colours',
      'status': published ? 'Graded' : 'Closed',
      'submissionCount': 5,
      'evaluatedCount': 5,
      'pendingEvaluations': 0,
      'emptySlots': 2,
      'canAward': !published,
      'canPublish': !published,
      'publishBlockReason': null,
      'isPublished': published,
      'publishedAt': published ? '2026-11-12T08:00:00' : null,
      'adjustedAt': null,
      'canMarkDelivered': canMarkDelivered,
      'canAdjustEvaluation': false,
      'awards': [
        {
          'id': 1,
          'name': 'First Prize',
          'rank': 1,
          'prizeValue': 2000000.0,
          'quantity': 3,
          'description': null,
          'activeCount': 1,
          'remaining': 2,
          'issued': [
            {
              'id': 9,
              'submissionId': 11,
              'submissionTitle': 'Still Sea',
              'studentName': 'Le Hoang Nam',
              'studentCode': 'HV001',
              'rating': 'Best',
              'issuedDate': '2026-11-10',
              'remarks': null,
              'prizeValue': 2000000.0,
              'status': 'Active',
              'revokedReason': null,
              'isDelivered': false,
              'deliveredAt': null,
              'rowVersion': 'AAAAAAAAB9E=',
              'hasThumbnail': true,
            },
          ],
        },
      ],
    };

EntryListItem _entry({EntryStatus status = EntryStatus.pending, bool paid = false}) => EntryListItem(
      id: 3,
      exhibitionId: 1,
      exhibitionName: 'Winter Show',
      submissionTitle: 'Still Sea',
      studentName: 'Le Hoang Nam',
      studentCode: 'HV001',
      askingPrice: 2000000,
      status: status,
      paidToStudent: paid,
      soldPrice: status == EntryStatus.sold ? 2000000 : null,
      statusReason: null,
      submissionId: 11,
    );

Map<String, dynamic> _entryDetail({String status = 'Pending', bool canReview = true}) => {
      'id': 3,
      'submissionId': 11,
      'exhibitionId': 1,
      'exhibitionName': 'Winter Show',
      'exhibitionStatus': 'Upcoming',
      'exhibitionStartDate': '2026-12-01',
      'exhibitionEndDate': '2026-12-20',
      'submissionTitle': 'Still Sea',
      'studentName': 'Le Hoang Nam',
      'studentCode': 'HV001',
      'competitionTitle': 'Autumn Colours',
      'askingPrice': 2000000,
      'priceSourceNote': null,
      'status': status,
      'statusReason': null,
      'soldPrice': null,
      'soldDate': null,
      'customerId': null,
      'customerName': null,
      'consentDate': null,
      'consentMethod': null,
      'consentNote': null,
      'paidToStudent': false,
      'paidAmount': null,
      'paidDate': null,
      'paymentMethod': null,
      'paymentReference': null,
      'payDueDate': null,
      'overdueDays': 0,
      'canReview': canReview,
      'canSell': false,
      'isLateSale': false,
      'canAdjustSale': false,
      'adjustLimitedToPayment': false,
      'canPay': false,
      'rowVersion': 'AAAA',
    };

void main() {
  group('M-T1 Home', () {
    testWidgets('shows greeting, grading progress, publish card and exhibition counts; cards navigate', (tester) async {
      const data = StaffDashboard(
        toGrade: [ToGradeItem(competitionId: 5, title: 'Autumn Colours', endDate: null, total: 10, evaluated: 7)],
        toPublish: [ToPublishItem(competitionId: 6, title: 'Spring Colours', emptySlots: 2)],
        pendingProposals: 3,
        unpaidSales: 1,
      );
      final router = _router(const StaffHomeScreen());
      await _pump(tester, const SizedBox(), [staffDashboardProvider.overrideWith((ref) async => data)], router: router);

      expect(find.textContaining('Lan'), findsOneWidget); // greeting uses the first name
      // Grading is the teachers' work: staff follow the progress, the card does not open the grading screen
      expect(find.text('Being graded'), findsOneWidget);
      expect(find.text('3 left to grade'), findsOneWidget);
      expect(find.text('7 / 10'), findsOneWidget);
      expect(find.text('Ready to publish'), findsOneWidget);
      expect(find.text('2 award slots still empty'), findsOneWidget);
      expect(find.text('Proposals waiting for review'), findsOneWidget);
      expect(find.text('Sale to pay out to the student'), findsOneWidget);

      await tester.tap(find.text('Autumn Colours'));
      await tester.pumpAndSettle();
      expect(find.text('GRADING 5'), findsNothing);

      await tester.tap(find.text('Spring Colours'));
      await tester.pumpAndSettle();
      expect(find.text('AWARDING 6'), findsOneWidget);
      router.pop();
      await tester.pumpAndSettle();

      await tester.tap(find.text('Proposals waiting for review'));
      await tester.pumpAndSettle();
      expect(find.text('EXHIBITIONS show=pending'), findsOneWidget);
    });

    testWidgets('empty dashboard shows "Nothing waiting"', (tester) async {
      await _pump(tester, const StaffHomeScreen(), [staffDashboardProvider.overrideWith((ref) async => const StaffDashboard())]);
      expect(find.text('Nothing waiting'), findsOneWidget);
      expect(find.text('You are all caught up.'), findsOneWidget);
    });
  });

  group('M-T2 Grading', () {
    testWidgets('competition cards show progress (no rule note)', (tester) async {
      final items = [
        GradingCompetition.fromJson({
          'id': 1,
          'title': 'Autumn Colours',
          'endDate': '2026-11-01T00:00:00',
          'status': 'Closed',
          'submissionCount': 10,
          'evaluatedCount': 4,
        }),
        GradingCompetition.fromJson({
          'id': 2,
          'title': 'Winter Light',
          'endDate': '2026-12-30T00:00:00',
          'status': 'Ongoing',
          'submissionCount': 3,
          'evaluatedCount': 0,
        }),
      ];
      await _pump(tester, const Scaffold(body: GradingCompetitionsTab()),
          [gradingCompetitionsProvider.overrideWith((ref) async => items)]);
      expect(find.text('Autumn Colours'), findsOneWidget);
      expect(find.text('4 / 10'), findsOneWidget);
      expect(find.text('Graded'), findsNWidgets(2));
      expect(find.text('Grading opens after the submission deadline.'), findsNothing);
    });

    testWidgets('entry list shows thumbnails, rating chips and filters', (tester) async {
      final list = GradingList(
        competitionId: 1,
        competitionTitle: 'Autumn Colours',
        status: statusFromJson('Closed'),
        total: 2,
        evaluated: 1,
        items: [
          GradingItem(
              id: 7,
              title: 'Still Sea',
              studentName: 'Le Hoang Nam',
              studentCode: 'HV001',
              submittedAt: DateTime(2026, 11, 1),
              rating: null),
          GradingItem(
              id: 8,
              title: 'Red Roofs',
              studentName: 'Pham Thu',
              studentCode: 'HV002',
              submittedAt: DateTime(2026, 11, 2),
              rating: ratingFromJson('Best')),
        ],
      );
      await _pump(tester, const GradingListScreen(competitionId: 1), [
        gradingListProvider.overrideWith((ref, key) async => list),
      ]);
      expect(find.text('Autumn Colours'), findsWidgets);
      expect(find.text('Still Sea'), findsOneWidget);
      expect(find.text('To grade'), findsNWidgets(2)); // filter chip + the entry chip
      expect(find.text('Best'), findsNWidgets(2)); // rating filter chip + the entry chip
      expect(find.text('Student name, code or title'), findsOneWidget);
    });
  });

  group('M-T3 Grade entry', () {
    testWidgets('shows entry info, rating chips and the three comment fields', (tester) async {
      await _pump(tester, const GradingDetailScreen(id: 7), [
        gradingDetailProvider.overrideWith((ref, id) async => GradingDetail.fromJson(_gradingDetail())),
      ]);
      expect(find.text('Still Sea'), findsOneWidget);
      expect(find.text('Le Hoang Nam (HV001)'), findsOneWidget);
      expect(find.text('Poem'), findsOneWidget);
      expect(find.byType(ChoiceChip), findsNWidgets(6));
      expect(find.text('Strengths *'), findsOneWidget);
      expect(find.text('Weaknesses'), findsOneWidget);
      expect(find.text('Areas to improve'), findsOneWidget);
      expect(find.text('Save & next'), findsOneWidget);
      expect(find.text('Delete evaluation'), findsNothing);
    });

    testWidgets('Disqualified turns the weaknesses field into the required reason', (tester) async {
      await _pump(tester, const GradingDetailScreen(id: 7), [
        gradingDetailProvider.overrideWith((ref, id) async => GradingDetail.fromJson(_gradingDetail())),
      ]);
      await tester.ensureVisible(find.text('Disqualified'));
      await tester.tap(find.text('Disqualified'));
      await tester.pump();
      expect(find.text('Reason for disqualification *'), findsOneWidget);
      expect(find.text('Strengths'), findsOneWidget);
    });

    testWidgets('not open for grading: no form, no rule banner, just "Not graded yet"', (tester) async {
      await _pump(tester, const GradingDetailScreen(id: 7), [
        gradingDetailProvider.overrideWith(
            (ref, id) async => GradingDetail.fromJson(_gradingDetail(canEvaluate: false, lockReason: 'Submissions are still open'))),
      ]);
      expect(find.text('Submissions are still open'), findsNothing);
      expect(find.text('Save & next'), findsNothing);
      await tester.ensureVisible(find.text('Not graded yet.'));
      expect(find.text('Not graded yet.'), findsOneWidget);
    });

    testWidgets('existing evaluation is prefilled and can be deleted', (tester) async {
      final evaluation = {
        'rating': 'Better',
        'positivePoints': 'Lovely colours',
        'negativePoints': null,
        'improvementAreas': null,
        'evaluatedByName': 'Tran Thi Lan',
        'evaluatedAt': '2026-11-05T09:00:00',
        'rowVersion': 'AAAA',
      };
      await _pump(tester, const GradingDetailScreen(id: 7), [
        gradingDetailProvider.overrideWith((ref, id) async => GradingDetail.fromJson(_gradingDetail(evaluation: evaluation))),
      ]);
      expect(find.text('Lovely colours'), findsOneWidget);
      expect(find.textContaining('Graded by Tran Thi Lan'), findsOneWidget);
      expect(find.text('Delete evaluation'), findsOneWidget);
    });
  });

  group('M-T4 Awards', () {
    testWidgets('competition card shows slot progress (no ungraded warning)', (tester) async {
      final items = [
        AwardingCompetition.fromJson({
          'id': 1,
          'title': 'Autumn Colours',
          'endDate': '2026-11-01T00:00:00',
          'status': 'Closed',
          'submissionCount': 5,
          'evaluatedCount': 3,
          'totalSlots': 6,
          'issuedSlots': 2,
        }),
      ];
      await _pump(tester, const Scaffold(body: AwardingCompetitionsTab()),
          [awardingCompetitionsProvider.overrideWith((ref) async => items)]);
      expect(find.text('2 / 6'), findsOneWidget);
      expect(find.textContaining('2 entries are not graded yet'), findsNothing);
    });

    testWidgets('before publishing: checklist, publish button and award slot with edit/delete', (tester) async {
      await _pump(tester, const AwardingDetailScreen(competitionId: 7), [
        awardingDetailProvider.overrideWith((ref, id) async => AwardingDetail.fromJson(_awardingDetail())),
      ]);
      expect(find.text('Before publishing'), findsOneWidget);
      expect(find.text('All 5 entries are graded'), findsOneWidget);
      expect(find.textContaining('2 award slots still empty'), findsOneWidget);
      expect(find.text('Publish results'), findsOneWidget);
      expect(find.text('First Prize · \$2,000,000'), findsOneWidget);
      expect(find.text('Still Sea'), findsOneWidget);

      await tester.tap(find.byTooltip('Award actions'));
      await tester.pumpAndSettle();
      expect(find.text('Edit'), findsOneWidget);
      expect(find.text('Delete'), findsOneWidget);
    });

    testWidgets('after publishing: unsupported actions are hidden, revoke stays', (tester) async {
      await _pump(tester, const AwardingDetailScreen(competitionId: 7), [
        awardingDetailProvider
            .overrideWith((ref, id) async => AwardingDetail.fromJson(_awardingDetail(published: true, canMarkDelivered: false))),
      ]);
      expect(find.textContaining('Results are published'), findsOneWidget);
      await tester.tap(find.byTooltip('Award actions'));
      await tester.pumpAndSettle();
      expect(find.text('Revoke award'), findsOneWidget);
      expect(find.text('Mark as delivered'), findsNothing);
      expect(find.text('Adjust evaluation'), findsNothing);
    });
  });

  group('M-T5 Exhibitions', () {
    final exhibitions = [
      ExhibitionSummary.fromJson({
        'id': 1,
        'name': 'Winter Show',
        'venue': 'Hall A',
        'city': 'Hanoi',
        'startDate': '2026-12-01',
        'endDate': '2026-12-20',
        'status': 'Upcoming',
        'daysUntilStart': 16,
        'entryCount': 4,
        'pendingCount': 2,
      }),
    ];

    testWidgets('exhibition cards show venue, dates and pending count', (tester) async {
      await _pump(tester, const Scaffold(body: StaffExhibitionsTab()), [
        staffExhibitionsProvider.overrideWith((ref) async => exhibitions),
        entriesProvider.overrideWith((ref, f) async => [_entry()]),
      ]);
      expect(find.text('Winter Show'), findsOneWidget);
      expect(find.text('Hall A, Hanoi'), findsOneWidget);
      expect(find.text('4 artworks · 2 to review'), findsOneWidget);
      expect(find.text('Upcoming'), findsWidgets);
    });

    testWidgets('initialView=unpaid opens the artworks tab filtered to sales to pay out', (tester) async {
      EntryFilter? seen;
      await _pump(tester, const Scaffold(body: StaffExhibitionsTab(initialView: 'unpaid')), [
        staffExhibitionsProvider.overrideWith((ref) async => exhibitions),
        entriesProvider.overrideWith((ref, f) async {
          seen = f;
          return [_entry(status: EntryStatus.sold)];
        }),
      ]);
      await tester.pumpAndSettle();
      expect(seen?.status, EntryStatus.sold);
      expect(seen?.paid, false);
      expect(find.text('Still Sea'), findsOneWidget);
      expect(find.text('Sold'), findsWidgets);
      final chip = tester.widget<ChoiceChip>(find.widgetWithText(ChoiceChip, 'To pay out'));
      expect(chip.selected, isTrue);
    });

    testWidgets('entry detail shows the framed picture area and review buttons', (tester) async {
      await _pump(tester, const StaffEntryDetailScreen(id: 3), [
        entryDetailProvider.overrideWith((ref, id) async => EntryDetail.fromJson(_entryDetail())),
      ]);
      expect(find.text('Still Sea'), findsOneWidget);
      expect(find.text('Pending review'), findsOneWidget);
      expect(find.text('Asking price'), findsOneWidget);
      expect(find.text('Accept'), findsOneWidget);
      expect(find.text('Reject'), findsOneWidget);
    });
  });

  group('small phone (360x640): no overflow', () {
    const small = Size(360, 640);

    testWidgets('Home', (tester) async {
      await _pump(tester, const StaffHomeScreen(), [
        staffDashboardProvider.overrideWith((ref) async => const StaffDashboard(
              toGrade: [ToGradeItem(competitionId: 5, title: 'A very long competition title for a small screen', endDate: null, total: 10, evaluated: 7)],
              toPublish: [ToPublishItem(competitionId: 6, title: 'Spring Colours', emptySlots: 1)],
              pendingProposals: 3,
              unpaidSales: 1,
            )),
      ], size: small);
      expect(tester.takeException(), isNull);
    });

    testWidgets('Grade entry', (tester) async {
      await _pump(tester, const GradingDetailScreen(id: 7), [
        gradingDetailProvider.overrideWith((ref, id) async => GradingDetail.fromJson(_gradingDetail())),
      ], size: small);
      expect(tester.takeException(), isNull);
    });

    testWidgets('Awards detail', (tester) async {
      await _pump(tester, const AwardingDetailScreen(competitionId: 7), [
        awardingDetailProvider.overrideWith((ref, id) async => AwardingDetail.fromJson(_awardingDetail())),
      ], size: small);
      expect(tester.takeException(), isNull);
    });

    testWidgets('Entry detail', (tester) async {
      await _pump(tester, const StaffEntryDetailScreen(id: 3), [
        entryDetailProvider.overrideWith((ref, id) async => EntryDetail.fromJson(_entryDetail())),
      ], size: small);
      expect(tester.takeException(), isNull);
    });
  });
}
