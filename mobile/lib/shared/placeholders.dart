import '../features/awarding/awarding.dart';
import '../features/competitions/competitions.dart';
import '../features/exhibitions/exhibitions.dart';
import '../features/gallery/gallery.dart';
import '../features/grading/grading.dart';
import '../features/submissions/submissions.dart';

/// Stand-in data for loading skeletons (`AsyncValueView.placeholder`, `Skeleton`): a few typical items with texts
/// of everyday length, so the skeleton has the shape the real list will have. Never shown as text: a skeleton
/// draws only grey bars where these texts sit. Lists hold every status, so a screen that filters by status still
/// finds items for its current tab.
abstract final class Placeholders {
  static final _today = DateTime.now();
  static DateTime _days(int n) => DateTime(_today.year, _today.month, _today.day).add(Duration(days: n));

  static const _titles = ['Spring Landscapes', 'Portraits of the City', 'Colours of the Sea'];

  static final competitions = [
    for (final status in CompetitionStatus.values)
      for (var i = 0; i < _titles.length; i++)
        CompetitionSummary(
          id: -1,
          title: _titles[i],
          topic: 'Watercolour and oil',
          startDate: _days(-10),
          endDate: _days(12),
          status: status,
          daysLeft: 12,
          daysUntilStart: 3,
          submissionCount: 12,
          hasCover: true,
        ),
  ];

  static final gradingCompetitions = [
    for (final title in _titles)
      GradingCompetition(
        id: -1,
        title: title,
        endDate: _days(12),
        status: CompetitionStatus.ongoing,
        submissionCount: 12,
        evaluatedCount: 6,
        hasCover: true,
      ),
  ];

  static final awardingCompetitions = [
    for (final title in _titles)
      AwardingCompetition(
        id: -1,
        title: title,
        endDate: _days(-2),
        status: CompetitionStatus.closed,
        submissionCount: 12,
        evaluatedCount: 12,
        totalSlots: 3,
        issuedSlots: 1,
        adjustedAt: null,
        hasCover: true,
      ),
  ];

  static final gradingList = GradingList(
    competitionId: -1,
    competitionTitle: _titles.first,
    status: CompetitionStatus.ongoing,
    total: 12,
    evaluated: 6,
    items: [
      for (var i = 0; i < 6; i++)
        GradingItem(
          id: -1,
          title: 'Evening by the river',
          studentName: 'Nguyen Van An',
          studentCode: 'SV0001',
          submittedAt: _days(-3),
          rating: i.isEven ? SubmissionRating.good : null,
          hasThumbnail: true,
          gradedByMe: i.isEven,
        ),
    ],
  );

  static final mySubmissions = [
    for (var i = 0; i < 4; i++)
      SubmissionListItem(
        id: -1,
        competitionId: -1,
        competitionTitle: _titles[i % _titles.length],
        competitionStatus: CompetitionStatus.ongoing,
        title: 'Evening by the river',
        submittedAt: _days(-3),
        updatedAt: null,
        canEdit: true,
        rating: null,
        hasThumbnail: true,
      ),
  ];

  static final exhibitions = [
    for (final status in ExhibitionStatus.values)
      for (var i = 0; i < 2; i++)
        ExhibitionSummary(
          id: -1,
          name: 'Young Artists Spring Show',
          venue: 'City Art Museum',
          city: 'Ha Noi',
          startDate: _days(5),
          endDate: _days(20),
          status: status,
          daysUntilStart: 5,
          entryCount: 8,
          pendingCount: 2,
        ),
  ];

  static final gallery = GalleryListState(
    items: [
      for (var i = 0; i < 6; i++)
        GalleryArtwork(
          submissionId: -1 - i,
          title: 'Evening by the river',
          studentName: 'Nguyen Van An',
          competitionId: -1,
          competitionTitle: _titles[i % _titles.length],
          awardName: null,
          awardRank: null,
          hasThumbnail: true,
          competitionDescription: null,
          submittedAt: _days(-3),
        ),
    ],
    totalCount: 6,
    hasMore: false,
  );
}
