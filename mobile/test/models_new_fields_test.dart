import 'package:fine_arts_mobile/core/config.dart';
import 'package:fine_arts_mobile/features/auth/auth.dart';
import 'package:fine_arts_mobile/features/competitions/competitions.dart';
import 'package:fine_arts_mobile/features/gallery/gallery.dart';
import 'package:fine_arts_mobile/features/staff/dashboard.dart';
import 'package:fine_arts_mobile/features/student/dashboard.dart';
import 'package:fine_arts_mobile/features/submissions/submissions.dart';
import 'package:fine_arts_mobile/shared/api_urls.dart';
import 'package:flutter_test/flutter_test.dart';

Map<String, dynamic> _competition({Map<String, dynamic> extra = const {}, String status = 'Ongoing', int daysLeft = 5}) => {
      'id': 7,
      'title': 'Spring',
      'topic': null,
      'startDate': '2026-12-01T00:00:00',
      'endDate': '2026-12-20T00:00:00',
      'status': status,
      'daysLeft': daysLeft,
      'submissionCount': 3,
      ...extra,
    };

Map<String, dynamic> _submission({Map<String, dynamic> extra = const {}}) => {
      'id': 11,
      'competitionId': 7,
      'competitionTitle': 'Spring',
      'competitionStatus': 'Graded',
      'title': 'Dawn',
      'submittedAt': '2026-12-02T10:00:00',
      'updatedAt': null,
      'canEdit': false,
      'rating': 'Best',
      ...extra,
    };

void main() {
  group('competition cover and countdown', () {
    test('hasCover defaults to false when the API does not send it', () {
      expect(CompetitionSummary.fromJson(_competition()).hasCover, isFalse);
    });

    test('hasCover is read when present, also through CompetitionDetail', () {
      expect(CompetitionSummary.fromJson(_competition(extra: {'hasCover': true})).hasCover, isTrue);
      final detail = CompetitionDetail.fromJson({
        ..._competition(extra: {'hasCover': true}),
        'description': null,
        'conditions': null,
        'deadline': '2026-12-20T00:00:00',
        'awards': [],
      });
      expect(detail.summary.hasCover, isTrue);
    });

    test('countdownLabel for open and upcoming competitions', () {
      expect(CompetitionSummary.fromJson(_competition(daysLeft: 5)).countdownLabel, '5 days left');
      expect(CompetitionSummary.fromJson(_competition(daysLeft: 1)).countdownLabel, '1 day left');
      expect(CompetitionSummary.fromJson(_competition(daysLeft: 0)).countdownLabel, 'Last day');
      expect(CompetitionSummary.fromJson(_competition(status: 'Closed')).countdownLabel, isNull);
      expect(
        CompetitionSummary.fromJson(_competition(status: 'Upcoming', extra: {'daysUntilStart': 1})).countdownLabel,
        'Starts in 1 day',
      );
    });
  });

  group('winner / award result / list item image fields', () {
    test('Winner defaults without submissionId', () {
      final w = Winner.fromJson({'studentName': 'An', 'awardName': 'First', 'rank': 1, 'paintingTitle': 'Dawn'});
      expect(w.submissionId, isNull);
      expect(w.hasThumbnail, isFalse);
    });

    test('Winner reads submissionId and hasThumbnail', () {
      final w = Winner.fromJson({
        'studentName': 'An',
        'awardName': 'First',
        'rank': 1,
        'paintingTitle': 'Dawn',
        'submissionId': 42,
        'hasThumbnail': true,
      });
      expect(w.submissionId, 42);
      expect(w.hasThumbnail, isTrue);
    });

    test('AwardResult defaults and new fields', () {
      final base = {'awardName': 'First', 'rank': 1, 'studentName': 'An', 'paintingTitle': 'Dawn', 'prizeValue': 100};
      expect(AwardResult.fromJson(base).submissionId, isNull);
      final withFields = AwardResult.fromJson({...base, 'submissionId': 9, 'hasThumbnail': true});
      expect(withFields.submissionId, 9);
      expect(withFields.hasThumbnail, isTrue);
    });

    test('SubmissionListItem.hasThumbnail defaults to true (a submission always has an image)', () {
      expect(SubmissionListItem.fromJson(_submission()).hasThumbnail, isTrue);
      expect(SubmissionListItem.fromJson(_submission(extra: {'hasThumbnail': false})).hasThumbnail, isFalse);
    });
  });

  group('AppUser', () {
    test('optional profile fields are null when missing', () {
      final u = AppUser.fromJson({'accountId': 1, 'username': 'hv01', 'fullName': 'Nguyen Van An', 'role': 'Student'});
      expect(u.email, isNull);
      expect(u.code, isNull);
      expect(u.className, isNull);
      expect(u.position, isNull);
      expect(u.firstName, 'An');
    });

    test('reads email, code, className and position', () {
      final u = AppUser.fromJson({
        'accountId': 1,
        'username': 'gv01',
        'fullName': 'Le Thi Binh',
        'role': 'Staff',
        'email': 'binh@example.com',
        'code': 'NV001',
        'position': 'Lecturer',
      });
      expect(u.email, 'binh@example.com');
      expect(u.code, 'NV001');
      expect(u.position, 'Lecturer');
    });
  });

  group('gallery', () {
    test('page parses items and paging', () {
      final page = GalleryPage.fromJson({
        'items': [
          {
            'submissionId': 5,
            'title': 'Dawn',
            'studentName': 'An',
            'competitionId': 7,
            'competitionTitle': 'Spring',
            'awardName': 'First',
            'hasThumbnail': true,
          },
          {'submissionId': 6, 'title': 'Dusk'},
        ],
        'page': 1,
        'pageSize': 2,
        'totalCount': 5,
        'totalPages': 3,
      });
      expect(page.items, hasLength(2));
      expect(page.items.first.isAwarded, isTrue);
      expect(page.items.last.awardName, isNull);
      expect(page.items.last.hasThumbnail, isFalse);
      expect(page.hasMore, isTrue);
    });

    test('empty / partial page falls back to defaults', () {
      final page = GalleryPage.fromJson({});
      expect(page.items, isEmpty);
      expect(page.hasMore, isFalse);
    });

    test('category apiValue is group/key', () {
      final c = GalleryCategory.fromJson({'group': 'subject', 'key': 'Animals', 'label': 'Animals'});
      expect(c.apiValue, 'subject/Animals');
    });
  });

  group('dashboards', () {
    test('student dashboard with missing fields is empty', () {
      final d = StudentDashboard.fromJson({});
      expect(d.isEmpty, isTrue);
      expect(d.pendingProposals, 0);
    });

    test('student dashboard parses lists and counters', () {
      final d = StudentDashboard.fromJson({
        'openToEnter': [_competition()],
        'editable': [_submission()],
        'newResults': [_submission(extra: {'id': 12})],
        'pendingProposals': 2,
        'unpaidSales': 1,
      });
      expect(d.openToEnter.single.id, 7);
      expect(d.editable.single.id, 11);
      expect(d.newResults.single.id, 12);
      expect(d.pendingProposals, 2);
      expect(d.unpaidSales, 1);
      expect(d.isEmpty, isFalse);
    });

    test('staff dashboard parses to-grade and to-publish items', () {
      final d = StaffDashboard.fromJson({
        'toGrade': [
          {'competitionId': 3, 'title': 'Autumn', 'endDate': '2026-10-01T00:00:00', 'total': 10, 'evaluated': 4},
        ],
        'toPublish': [
          {'competitionId': 4, 'title': 'Summer', 'emptySlots': 2},
        ],
        'pendingProposals': 1,
        'unpaidSales': 3,
      });
      expect(d.toGrade.single.remaining, 6);
      expect(d.toGrade.single.hasCover, isFalse);
      expect(d.toPublish.single.emptySlots, 2);
      expect(d.pendingProposals, 1);
      expect(d.unpaidSales, 3);
    });

    test('staff dashboard with no fields is empty', () {
      expect(StaffDashboard.fromJson({}).isEmpty, isTrue);
    });
  });

  test('ApiUrls build public image URLs from apiBaseUrl', () {
    expect(ApiUrls.competitionCover(3), '$apiBaseUrl/files/competitions/3/cover');
    expect(ApiUrls.submissionThumb(9), '$apiBaseUrl/files/submissions/9/thumb');
    expect(ApiUrls.submissionFull(9), '$apiBaseUrl/files/submissions/9');
    expect(ApiUrls.submission(9, thumb: false), ApiUrls.submissionFull(9));
  });
}
