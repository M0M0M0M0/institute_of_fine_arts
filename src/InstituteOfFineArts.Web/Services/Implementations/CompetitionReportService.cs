using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Helpers;
using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.Services.Interfaces;
using InstituteOfFineArts.Web.ViewModels.Reports;
using Microsoft.EntityFrameworkCore;

namespace InstituteOfFineArts.Web.Services.Implementations;

/// <summary>
/// Competition reports RPT-01, RPT-02, RPT-03 and RPT-06 (task BE2-12 part 1; F-M10-01/02/03/06; BA doc §7).
/// Every query is AsNoTracking with a projection. Submitted date ranges include both days: from 00:00 of From to
/// 23:59:59 of To (TC-M10-02); From after To is refused (TC-M10-03). Totals are calculated on the whole result,
/// the rows are capped at <see cref="MaxRows"/> so a printed report stays reasonable.
/// Decision: an "active student" is a student whose account is not locked (account.active = 1); RPT-02 counts
/// participation as the distinct active students with an entry divided by the active students, so it never
/// exceeds 100% and matches the BR-12 list of active students who have not submitted.
/// </summary>
public class CompetitionReportService : ICompetitionReportService
{
    /// <summary>Maximum number of rows a report shows (a warning is displayed above it).</summary>
    public const int MaxRows = 1000;

    /// <summary>Maximum length of the comment summary in RPT-06.</summary>
    public const int CommentSummaryLength = 100;

    /// <summary>Message when From is after To (TC-M10-03).</summary>
    public const string InvalidDateRangeMessage = "Invalid date range: the From date must be on or before the To date.";

    /// <summary>Message when RPT-01 is run without a (valid) competition.</summary>
    public const string ChooseCompetitionMessage = "Please choose a competition.";

    /// <summary>Message when RPT-06 is run without a (valid) student.</summary>
    public const string ChooseStudentMessage = "Please choose a student.";

    /// <summary>Database access.</summary>
    private readonly AppDbContext _db;

    /// <summary>Receives the database context through Dependency Injection.</summary>
    public CompetitionReportService(AppDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<Rpt01ResultVm> GetSubmissionsByRatingAsync(Rpt01FilterVm filter, int maxRows = MaxRows)
    {
        // The competition is required (the report of the assignment is always about one competition)
        if (filter.CompetitionId is not { } competitionId)
        {
            throw new BusinessException(ChooseCompetitionMessage, nameof(Rpt01FilterVm.CompetitionId));
        }
        CheckDateRange(filter.From, filter.To);
        var competition = await _db.Competitions.AsNoTracking()
            .Where(c => c.Id == competitionId)
            .Select(c => new { c.Code, c.Title })
            .FirstOrDefaultAsync()
            ?? throw new BusinessException(ChooseCompetitionMessage, nameof(Rpt01FilterVm.CompetitionId));

        var query = WhereSubmittedBetween(_db.Submissions.AsNoTracking().Where(s => s.CompetitionId == competitionId), filter.From, filter.To);

        // Class (exact name from the dropdown)
        if (!string.IsNullOrWhiteSpace(filter.ClassName))
        {
            var className = filter.ClassName.Trim();
            query = query.Where(s => s.Student.ClassName == className);
        }

        // Ratings: known rating names and/or "Not graded"; unknown values are ignored, nothing chosen = every entry
        var ratings = Enum.GetValues<Rating>().Where(r => filter.Ratings.Contains(r.ToString())).ToList();
        var notGraded = filter.Ratings.Contains(Rpt01FilterVm.NotGraded);
        if (ratings.Count > 0 || notGraded)
        {
            query = query.Where(s => (s.Evaluation != null && ratings.Contains(s.Evaluation.Rating)) || (notGraded && s.Evaluation == null));
        }

        // Totals of the whole result: one small column (the rating of each matching entry)
        var matchingRatings = await query.Select(s => s.Evaluation != null ? (Rating?)s.Evaluation.Rating : null).ToListAsync();

        // Rows in submission order, capped
        var rows = await query
            .OrderBy(s => s.SubmittedAt).ThenBy(s => s.Id)
            .Take(maxRows)
            .Select(s => new Rpt01RowVm
            {
                SubmissionId = s.Id,
                StudentCode = s.Student.StudentCode,
                StudentName = s.Student.Account.FullName,
                ClassName = s.Student.ClassName,
                Title = s.Title,
                SubmittedAt = s.SubmittedAt,
                UpdatedAt = s.UpdatedAt,
                Rating = s.Evaluation != null ? (Rating?)s.Evaluation.Rating : null,
                EvaluatorName = s.Evaluation != null ? s.Evaluation.EvaluatedByStaff.Account.FullName : null,
                EvaluatedAt = s.Evaluation != null ? (DateTime?)s.Evaluation.EvaluatedAt : null,
                AwardName = s.AwardIssued != null ? s.AwardIssued.CompetitionAward.AwardName : null,
            })
            .ToListAsync();
        Number(rows, (row, no) => row.No = no);

        return new Rpt01ResultVm
        {
            CompetitionCode = competition.Code,
            CompetitionTitle = competition.Title,
            Rows = rows,
            TotalCount = matchingRatings.Count,
            CountByRating = CountPerRating(matchingRatings),
            NotGradedCount = matchingRatings.Count(r => r == null),
        };
    }

    /// <inheritdoc />
    public async Task<Rpt02ResultVm> GetParticipationAsync(Rpt02FilterVm filter, DateOnly? today = null, int maxRows = MaxRows)
    {
        CheckDateRange(filter.From, filter.To);
        var day = today ?? DateTimeHelper.Today;

        // Competitions whose period overlaps the chosen period, with the actual status
        var competitionQuery = _db.Competitions.AsNoTracking().WhereStatus(filter.Status, day);
        if (filter.From is { } from)
        {
            competitionQuery = competitionQuery.Where(c => c.EndDate >= from);
        }
        if (filter.To is { } to)
        {
            competitionQuery = competitionQuery.Where(c => c.StartDate <= to);
        }
        var competitions = await competitionQuery
            .OrderByDescending(c => c.StartDate).ThenByDescending(c => c.Id)
            .Select(c => new Competition { Id = c.Id, Code = c.Code, Title = c.Title, StartDate = c.StartDate, EndDate = c.EndDate, Status = c.Status })
            .ToListAsync();

        // Active students today (same number in every row)
        var activeStudents = await _db.StudentProfiles.CountAsync(p => p.Account.Active);

        // One small row per entry of these competitions; grouped in memory (a few hundred rows at most)
        var ids = competitions.Select(c => c.Id).ToList();
        var entries = await _db.Submissions.AsNoTracking()
            .Where(s => ids.Contains(s.CompetitionId))
            .Select(s => new
            {
                s.CompetitionId,
                s.StudentId,
                s.Student.Account.Active,
                Rating = s.Evaluation != null ? (Rating?)s.Evaluation.Rating : null,
            })
            .ToListAsync();
        var byCompetition = entries.ToLookup(e => e.CompetitionId);

        var rows = competitions.Select(c =>
        {
            var list = byCompetition[c.Id].ToList();
            var participants = list.Where(e => e.Active).Select(e => e.StudentId).Distinct().Count();
            return new Rpt02RowVm
            {
                CompetitionId = c.Id,
                Code = c.Code,
                Title = c.Title,
                StartDate = c.StartDate,
                EndDate = c.EndDate,
                Status = CompetitionStatusResolver.Resolve(c, day),
                ActiveStudents = activeStudents,
                Submissions = list.Count,
                Participants = participants,
                ParticipationPercent = Percent(participants, activeStudents),
                CountByRating = CountPerRating(list.Select(e => e.Rating)),
                NotGraded = list.Count(e => e.Rating == null),
            };
        }).ToList();

        // Totals row over every matching competition
        var allRatings = entries.Select(e => e.Rating).ToList();
        var totalParticipants = rows.Sum(r => r.Participants);
        var result = new Rpt02ResultVm
        {
            ActiveStudents = activeStudents,
            Rows = rows.Take(maxRows).ToList(),
            TotalCount = rows.Count,
            TotalSubmissions = entries.Count,
            TotalParticipants = totalParticipants,
            TotalParticipationPercent = Percent(totalParticipants, activeStudents * rows.Count),
            TotalByRating = CountPerRating(allRatings),
            TotalNotGraded = allRatings.Count(r => r == null),
        };

        // BR-12: active students without an entry in the chosen competition
        if (filter.NotSubmittedCompetitionId is { } notSubmittedId)
        {
            result.NotSubmitted = await GetNotSubmittedAsync(notSubmittedId);
        }
        return result;
    }

    /// <inheritdoc />
    public async Task<Rpt03ResultVm> GetAwardsIssuedAsync(Rpt03FilterVm filter, int maxRows = MaxRows)
    {
        CheckDateRange(filter.From, filter.To);
        var query = _db.AwardsIssued.AsNoTracking().AsQueryable();

        // Competition
        if (filter.CompetitionId is { } competitionId)
        {
            query = query.Where(a => a.CompetitionAward.CompetitionId == competitionId);
        }

        // Issued date range, both days included (issued_date is a date column)
        if (filter.From is { } from)
        {
            query = query.Where(a => a.IssuedDate >= from);
        }
        if (filter.To is { } to)
        {
            query = query.Where(a => a.IssuedDate <= to);
        }

        // Student: part of the full name or the student code
        if (!string.IsNullOrWhiteSpace(filter.Student))
        {
            var keyword = filter.Student.Trim();
            query = query.Where(a => a.Submission.Student.Account.FullName.Contains(keyword)
                || a.Submission.Student.StudentCode.Contains(keyword));
        }

        // Totals of the whole result (Financial: total prize value)
        var totalCount = await query.CountAsync();
        var totalValue = totalCount == 0 ? 0m : await query.SumAsync(a => a.CompetitionAward.PrizeValue);

        // Newest first, then by competition and rank
        var rows = await query
            .OrderByDescending(a => a.IssuedDate).ThenBy(a => a.CompetitionAward.CompetitionId)
            .ThenBy(a => a.CompetitionAward.Rank).ThenBy(a => a.Id)
            .Take(maxRows)
            .Select(a => new Rpt03RowVm
            {
                CompetitionCode = a.CompetitionAward.Competition.Code,
                CompetitionTitle = a.CompetitionAward.Competition.Title,
                AwardName = a.CompetitionAward.AwardName,
                Rank = a.CompetitionAward.Rank,
                PrizeValue = a.CompetitionAward.PrizeValue,
                StudentCode = a.Submission.Student.StudentCode,
                StudentName = a.Submission.Student.Account.FullName,
                SubmissionId = a.SubmissionId,
                SubmissionTitle = a.Submission.Title,
                IssuedDate = a.IssuedDate,
                IssuedByName = a.IssuedByStaff.Account.FullName,
                Remarks = a.Remarks,
            })
            .ToListAsync();
        Number(rows, (row, no) => row.No = no);

        return new Rpt03ResultVm { Rows = rows, TotalCount = totalCount, TotalPrizeValue = totalValue };
    }

    /// <inheritdoc />
    public async Task<Rpt06ResultVm> GetStudentProfileAsync(Rpt06FilterVm filter, int maxRows = MaxRows)
    {
        // The student is required
        if (filter.StudentId is not { } studentId)
        {
            throw new BusinessException(ChooseStudentMessage, nameof(Rpt06FilterVm.StudentId));
        }
        CheckDateRange(filter.From, filter.To);
        var vm = await _db.StudentProfiles.AsNoTracking()
            .Where(p => p.Id == studentId)
            .Select(p => new Rpt06ResultVm
            {
                StudentCode = p.StudentCode,
                FullName = p.Account.FullName,
                ClassName = p.ClassName,
                CourseName = p.CourseName,
            })
            .FirstOrDefaultAsync()
            ?? throw new BusinessException(ChooseStudentMessage, nameof(Rpt06FilterVm.StudentId));

        var query = WhereSubmittedBetween(_db.Submissions.AsNoTracking().Where(s => s.StudentId == studentId), filter.From, filter.To);

        // Achievement totals over every matching entry
        vm.TotalCount = await query.CountAsync();
        vm.CompetitionsJoined = await query.Select(s => s.CompetitionId).Distinct().CountAsync();
        vm.BestCount = await query.CountAsync(s => s.Evaluation != null && s.Evaluation.Rating == Rating.BEST);
        var awarded = query.Where(s => s.AwardIssued != null);
        vm.AwardCount = await awarded.CountAsync();
        vm.TotalPrizeValue = vm.AwardCount == 0 ? 0m : await awarded.SumAsync(s => s.AwardIssued!.CompetitionAward.PrizeValue);

        // Total sales: sold prices of the entries of these paintings that were sold at an exhibition
        var submissionIds = query.Select(s => s.Id);
        var sold = _db.ExhibitionEntries.AsNoTracking()
            .Where(e => submissionIds.Contains(e.SubmissionId) && e.Status == EntryStatus.SOLD && e.SoldPrice != null);
        vm.TotalSales = await sold.AnyAsync() ? await sold.SumAsync(e => e.SoldPrice!.Value) : 0m;

        // Rows, oldest first, capped
        var rows = await query
            .OrderBy(s => s.SubmittedAt).ThenBy(s => s.Id)
            .Take(maxRows)
            .Select(s => new Rpt06RowVm
            {
                SubmissionId = s.Id,
                CompetitionCode = s.Competition.Code,
                CompetitionTitle = s.Competition.Title,
                Title = s.Title,
                SubmittedAt = s.SubmittedAt,
                Rating = s.Evaluation != null ? (Rating?)s.Evaluation.Rating : null,
                CommentSummary = s.Evaluation != null ? s.Evaluation.PositivePoints : null,
                AwardName = s.AwardIssued != null ? s.AwardIssued.CompetitionAward.AwardName : null,
                PrizeValue = s.AwardIssued != null ? (decimal?)s.AwardIssued.CompetitionAward.PrizeValue : null,
                Exhibitions = s.ExhibitionEntries
                    .OrderBy(e => e.Exhibition.StartDate).ThenBy(e => e.Id)
                    .Select(e => new Rpt06ExhibitionVm { ExhibitionName = e.Exhibition.Name, Status = e.Status, SoldPrice = e.SoldPrice })
                    .ToList(),
            })
            .ToListAsync();

        // Row numbers and the short comment (cut in memory: SQL would need a CASE per row for the "…")
        Number(rows, (row, no) => row.No = no);
        foreach (var row in rows)
        {
            row.CommentSummary = Summarize(row.CommentSummary);
        }
        vm.Rows = rows;
        return vm;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetClassOptionsAsync()
        => await _db.StudentProfiles.AsNoTracking()
            .Where(p => p.ClassName != null && p.ClassName != "")
            .Select(p => p.ClassName!)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync();

    /// <inheritdoc />
    public async Task<IReadOnlyList<StudentOptionVm>> GetStudentOptionsAsync()
        => await _db.StudentProfiles.AsNoTracking()
            .OrderBy(p => p.StudentCode)
            .Select(p => new StudentOptionVm(p.Id, p.StudentCode, p.Account.FullName))
            .ToListAsync();

    /// <summary>BR-12 list: active students without an entry in the competition, ordered by student code.</summary>
    /// <param name="competitionId">Competition id</param>
    private async Task<Rpt02NotSubmittedVm> GetNotSubmittedAsync(long competitionId)
    {
        var vm = await _db.Competitions.AsNoTracking()
            .Where(c => c.Id == competitionId)
            .Select(c => new Rpt02NotSubmittedVm { CompetitionId = c.Id, Code = c.Code, Title = c.Title })
            .FirstOrDefaultAsync()
            ?? throw new BusinessException(ChooseCompetitionMessage, nameof(Rpt02FilterVm.NotSubmittedCompetitionId));

        var students = await _db.StudentProfiles.AsNoTracking()
            .Where(p => p.Account.Active && !p.Submissions.Any(s => s.CompetitionId == competitionId))
            .OrderBy(p => p.StudentCode)
            .Select(p => new Rpt02StudentRowVm
            {
                StudentId = p.Id,
                StudentCode = p.StudentCode,
                FullName = p.Account.FullName,
                ClassName = p.ClassName,
                CourseName = p.CourseName,
                Email = p.Account.Email,
            })
            .ToListAsync();
        Number(students, (row, no) => row.No = no);
        vm.Students = students;
        return vm;
    }

    /// <summary>Refuses a date range whose From is after its To (TC-M10-03).</summary>
    /// <param name="from">From day</param>
    /// <param name="to">To day</param>
    private static void CheckDateRange(DateOnly? from, DateOnly? to)
    {
        if (from is { } f && to is { } t && f > t)
        {
            throw new BusinessException(InvalidDateRangeMessage, "From");
        }
    }

    /// <summary>Submitted from 00:00 of <paramref name="from"/> to 23:59:59 of <paramref name="to"/> (TC-M10-02).</summary>
    /// <param name="query">Entries to filter</param>
    /// <param name="from">From day (null = no lower bound)</param>
    /// <param name="to">To day (null = no upper bound)</param>
    private static IQueryable<Submission> WhereSubmittedBetween(IQueryable<Submission> query, DateOnly? from, DateOnly? to)
    {
        if (from is { } f)
        {
            var start = DateTimeHelper.StartOfDay(f);
            query = query.Where(s => s.SubmittedAt >= start);
        }
        if (to is { } t)
        {
            var endExclusive = DateTimeHelper.StartOfNextDay(t);
            query = query.Where(s => s.SubmittedAt < endExclusive);
        }
        return query;
    }

    /// <summary>Count per rating with every rating present (0 when none); null ratings (not graded) are skipped.</summary>
    /// <param name="ratings">Rating of each entry</param>
    private static IReadOnlyDictionary<Rating, int> CountPerRating(IEnumerable<Rating?> ratings)
    {
        var list = ratings.ToList();
        return Enum.GetValues<Rating>().ToDictionary(r => r, r => list.Count(x => x == r));
    }

    /// <summary>part / whole in percent with 1 decimal; 0 when the whole is 0.</summary>
    private static decimal Percent(int part, int whole)
        => whole == 0 ? 0m : Math.Round(part * 100m / whole, 1, MidpointRounding.AwayFromZero);

    /// <summary>Cuts a comment to <see cref="CommentSummaryLength"/> characters with "…".</summary>
    /// <param name="text">Comment, may be null</param>
    private static string? Summarize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }
        var trimmed = text.Trim();
        return trimmed.Length <= CommentSummaryLength ? trimmed : trimmed[..CommentSummaryLength].TrimEnd() + "…";
    }

    /// <summary>Sets the row numbers 1, 2, 3, … (STT column).</summary>
    private static void Number<T>(IEnumerable<T> rows, Action<T, int> setNo)
    {
        var no = 0;
        foreach (var row in rows)
        {
            setNo(row, ++no);
        }
    }
}
