using System.Linq.Expressions;
using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Helpers;
using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.Services.Interfaces;
using InstituteOfFineArts.Web.ViewModels.Common;
using InstituteOfFineArts.Web.ViewModels.Staff.Submissions;
using Microsoft.EntityFrameworkCore;

namespace InstituteOfFineArts.Web.Services.Implementations;

/// <summary>
/// Read-only submission queries for staff (and later the manager): every query is AsNoTracking with a
/// projection into view models. Competition status always comes from CompetitionStatusResolver, so
/// "CLOSED" is right even before the nightly status job has updated the stored value.
/// </summary>
public class StaffSubmissionService : IStaffSubmissionService
{
    /// <summary>Rating used by the rating search when none is chosen.</summary>
    public const Rating DefaultSearchRating = Rating.BEST;

    /// <summary>Projection of one list row (shared by the list and the rating search).</summary>
    private static readonly Expression<Func<Submission, SubmissionListItemVm>> ListRow = s => new SubmissionListItemVm
    {
        Id = s.Id,
        Title = s.Title,
        StudentId = s.StudentId,
        StudentName = s.Student.Account.FullName,
        StudentCode = s.Student.StudentCode,
        CompetitionId = s.CompetitionId,
        CompetitionTitle = s.Competition.Title,
        CompetitionEndDate = s.Competition.EndDate,
        SubmittedAt = s.SubmittedAt,
        Rating = s.Evaluation != null ? (Rating?)s.Evaluation.Rating : null,
        AwardName = s.AwardIssued != null ? s.AwardIssued.CompetitionAward.AwardName : null,
        ExhibitionEntryCount = s.ExhibitionEntries.Count,
    };

    /// <summary>Database access.</summary>
    private readonly AppDbContext _db;

    /// <summary>Receives the database context through Dependency Injection.</summary>
    public StaffSubmissionService(AppDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<PagedResult<SubmissionListItemVm>> SearchAsync(SubmissionSearchVm search, int pageSize = 10, long? judgeId = null)
    {
        var query = _db.Submissions.AsNoTracking().AsQueryable();

        // Teacher view: entries of the competitions they judge, once the submission period is over (no leak before the deadline)
        if (judgeId is { } judge)
        {
            var today = DateTimeHelper.Today;
            query = query.Where(s => (s.Competition.Status == CompetitionStatus.GRADED || s.Competition.EndDate < today)
                                     && s.Competition.Judges.Any(j => j.JudgeId == judge));
        }

        // Competition
        if (search.CompetitionId is { } competitionId)
        {
            query = query.Where(s => s.CompetitionId == competitionId);
        }

        // Status of the competition (same date rule as everywhere else, evaluated by SQL Server)
        if (search.CompetitionStatus is { } competitionStatus)
        {
            var competitions = _db.Competitions.WhereStatus(competitionStatus, DateTimeHelper.Today);
            query = query.Where(s => competitions.Any(c => c.Id == s.CompetitionId));
        }

        // Evaluated / not evaluated: for a judge, graded by them (the result needs the whole jury)
        if (search.Evaluated is { } evaluated)
        {
            if (judgeId is { } me)
            {
                query = evaluated
                    ? query.Where(s => s.JudgeEvaluations.Any(j => j.JudgeId == me))
                    : query.Where(s => !s.JudgeEvaluations.Any(j => j.JudgeId == me));
            }
            else
            {
                query = evaluated ? query.Where(s => s.Evaluation != null) : query.Where(s => s.Evaluation == null);
            }
        }

        // Rating (implies evaluated)
        if (search.Rating is { } rating)
        {
            query = query.Where(s => s.Evaluation != null && s.Evaluation.Rating == rating);
        }

        // Student: part of the full name or the student code
        if (!string.IsNullOrWhiteSpace(search.Student))
        {
            var keyword = search.Student.Trim();
            query = query.Where(s => s.Student.Account.FullName.Contains(keyword) || s.Student.StudentCode.Contains(keyword));
        }

        // Date range, both ends included: from 00:00 of From to 23:59:59 of To (TC-M10-02)
        if (search.From is { } from)
        {
            var start = DateTimeHelper.StartOfDay(from);
            query = query.Where(s => s.SubmittedAt >= start);
        }
        if (search.To is { } to)
        {
            var endExclusive = DateTimeHelper.StartOfNextDay(to);
            query = query.Where(s => s.SubmittedAt < endExclusive);
        }

        var rows = query.OrderByDescending(s => s.SubmittedAt).ThenByDescending(s => s.Id).Select(ListRow);
        var page = WithLateFlags(await PagedResult<SubmissionListItemVm>.CreateAsync(rows, search.Page, pageSize));
        if (judgeId is { } judgeProfile)
        {
            // Which of the listed entries the judge has graded
            var ids = page.Items.Select(i => i.Id).ToList();
            var mine = (await _db.JudgeEvaluations.AsNoTracking()
                .Where(j => j.JudgeId == judgeProfile && ids.Contains(j.SubmissionId))
                .Select(j => j.SubmissionId).ToListAsync()).ToHashSet();
            foreach (var item in page.Items)
            {
                item.GradedByMe = mine.Contains(item.Id);
            }
        }
        return page;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CompetitionOptionVm>> GetCompetitionOptionsAsync(long? judgeId = null)
    {
        var today = DateTimeHelper.Today;
        return await _db.Competitions.AsNoTracking()
            .Where(c => judgeId == null
                        || ((c.Status == CompetitionStatus.GRADED || c.EndDate < today) && c.Judges.Any(j => j.JudgeId == judgeId)))
            .OrderByDescending(c => c.StartDate).ThenByDescending(c => c.Id)
            .Select(c => new CompetitionOptionVm(c.Id, c.Code, c.Title))
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<EvaluationProgressVm?> GetProgressAsync(long competitionId)
    {
        // One query: the competition with both counts
        var row = await _db.Competitions.AsNoTracking()
            .Where(c => c.Id == competitionId)
            .Select(c => new
            {
                c.Id, c.Title, c.Status, c.StartDate, c.EndDate,
                Total = c.Submissions.Count,
                Evaluated = c.Submissions.Count(s => s.Evaluation != null),
            })
            .FirstOrDefaultAsync();
        if (row == null)
        {
            return null;
        }
        var status = CompetitionStatusResolver.Resolve(new Competition { Status = row.Status, StartDate = row.StartDate, EndDate = row.EndDate });
        return new EvaluationProgressVm(row.Id, row.Title, status, row.Evaluated, row.Total);
    }

    /// <inheritdoc />
    public async Task<SubmissionDetailsVm> GetDetailsAsync(long id, long? judgeId = null)
    {
        var row = await _db.Submissions.AsNoTracking()
            .Where(s => s.Id == id)
            .Select(s => new
            {
                s.Competition.Status,
                s.Competition.StartDate,
                Vm = new SubmissionDetailsVm
                {
                    Id = s.Id,
                    Title = s.Title,
                    TextType = s.TextType,
                    TextContent = s.TextContent,
                    Description = s.Description,
                    OriginalFileName = s.OriginalFileName,
                    FileSize = s.FileSize,
                    ImageIds = s.Images.OrderByDescending(i => i.IsCover).ThenBy(i => i.SortOrder).ThenBy(i => i.Id)
                        .Select(i => i.Id).ToList(),
                    StudentId = s.StudentId,
                    StudentName = s.Student.Account.FullName,
                    StudentCode = s.Student.StudentCode,
                    CompetitionId = s.CompetitionId,
                    CompetitionCode = s.Competition.Code,
                    CompetitionTitle = s.Competition.Title,
                    CompetitionEndDate = s.Competition.EndDate,
                    SubmittedAt = s.SubmittedAt,
                    UpdatedAt = s.UpdatedAt,
                    Evaluation = s.Evaluation == null ? null : new EvaluationInfoVm
                    {
                        Rating = s.Evaluation.Rating,
                        PositivePoints = s.Evaluation.PositivePoints,
                        NegativePoints = s.Evaluation.NegativePoints,
                        ImprovementAreas = s.Evaluation.ImprovementAreas,
                        EvaluatorName = s.Evaluation.EvaluatedByStaff.Account.FullName,
                        EvaluatedAt = s.Evaluation.EvaluatedAt,
                    },
                    Award = s.AwardIssued == null ? null : new AwardInfoVm
                    {
                        AwardName = s.AwardIssued.CompetitionAward.AwardName,
                        Rank = s.AwardIssued.CompetitionAward.Rank,
                        PrizeValue = s.AwardIssued.CompetitionAward.PrizeValue,
                        IssuedDate = s.AwardIssued.IssuedDate,
                        Remarks = s.AwardIssued.Remarks,
                    },
                    ExhibitionEntries = s.ExhibitionEntries
                        .OrderByDescending(e => e.Exhibition.StartDate).ThenByDescending(e => e.Id)
                        .Select(e => new ExhibitionEntryInfoVm
                        {
                            Id = e.Id,
                            ExhibitionName = e.Exhibition.Name,
                            City = e.Exhibition.City,
                            StartDate = e.Exhibition.StartDate,
                            EndDate = e.Exhibition.EndDate,
                            AskingPrice = e.AskingPrice,
                            Status = e.Status,
                            SoldPrice = e.SoldPrice,
                        })
                        .ToList(),
                },
            })
            .FirstOrDefaultAsync()
            ?? throw new NotFoundException($"Submission {id} was not found.");

        // Status for today (decides whether the Evaluate button is shown) and the late flag
        var vm = row.Vm;
        vm.CompetitionStatus = CompetitionStatusResolver.Resolve(
            new Competition { Status = row.Status, StartDate = row.StartDate, EndDate = vm.CompetitionEndDate });
        vm.IsLate = CompetitionStatusResolver.IsLateSubmission(vm.SubmittedAt, vm.CompetitionEndDate);

        // Teacher view: only entries of a competition they judge, and never while it still takes entries
        if (judgeId is { } judge
            && (vm.CompetitionStatus is CompetitionStatus.UPCOMING or CompetitionStatus.ONGOING
                || !await _db.CompetitionJudges.AnyAsync(j => j.CompetitionId == vm.CompetitionId && j.JudgeId == judge)))
        {
            throw new NotFoundException($"Submission {id} was not found.");
        }
        return vm;
    }

    /// <inheritdoc />
    public async Task<PagedResult<SubmissionListItemVm>> SearchByRatingAsync(RatingSearchVm search, int pageSize = 10)
    {
        var rating = search.Rating ?? DefaultSearchRating;
        var today = DateTimeHelper.Today;

        // Only CLOSED or GRADED competitions (same rule as CompetitionStatusResolver, written as a query)
        var query = _db.Submissions.AsNoTracking()
            .Where(s => s.Evaluation != null && s.Evaluation.Rating == rating)
            .Where(s => s.Competition.Status == CompetitionStatus.GRADED || s.Competition.EndDate < today);
        if (search.CompetitionId is { } competitionId)
        {
            query = query.Where(s => s.CompetitionId == competitionId);
        }

        // Newest competition first, then by submission time
        var rows = query
            .OrderByDescending(s => s.Competition.StartDate).ThenByDescending(s => s.CompetitionId)
            .ThenBy(s => s.SubmittedAt).ThenBy(s => s.Id)
            .Select(ListRow);
        return WithLateFlags(await PagedResult<SubmissionListItemVm>.CreateAsync(rows, search.Page, pageSize));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CompetitionOptionVm>> GetEvaluatedCompetitionOptionsAsync()
    {
        var today = DateTimeHelper.Today;
        return await _db.Competitions.AsNoTracking()
            .Where(c => c.Status == CompetitionStatus.GRADED || c.EndDate < today)
            .OrderByDescending(c => c.StartDate).ThenByDescending(c => c.Id)
            .Select(c => new CompetitionOptionVm(c.Id, c.Code, c.Title))
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<StudentHistoryVm> GetStudentHistoryAsync(long studentId)
    {
        var vm = await _db.StudentProfiles.AsNoTracking()
            .Where(p => p.Id == studentId)
            .Select(p => new StudentHistoryVm
            {
                StudentId = p.Id,
                FullName = p.Account.FullName,
                StudentCode = p.StudentCode,
                CourseName = p.CourseName,
                ClassName = p.ClassName,
            })
            .FirstOrDefaultAsync()
            ?? throw new NotFoundException($"Student {studentId} was not found.");

        // Every entry of this student only, newest first
        var rows = await _db.Submissions.AsNoTracking()
            .Where(s => s.StudentId == studentId)
            .OrderByDescending(s => s.SubmittedAt).ThenByDescending(s => s.Id)
            .Select(s => new
            {
                s.Competition.Status,
                s.Competition.StartDate,
                Vm = new StudentHistoryEntryVm
                {
                    Id = s.Id,
                    Title = s.Title,
                    CompetitionId = s.CompetitionId,
                    CompetitionTitle = s.Competition.Title,
                    CompetitionEndDate = s.Competition.EndDate,
                    SubmittedAt = s.SubmittedAt,
                    Evaluation = s.Evaluation == null ? null : new EvaluationInfoVm
                    {
                        Rating = s.Evaluation.Rating,
                        PositivePoints = s.Evaluation.PositivePoints,
                        NegativePoints = s.Evaluation.NegativePoints,
                        ImprovementAreas = s.Evaluation.ImprovementAreas,
                        EvaluatorName = s.Evaluation.EvaluatedByStaff.Account.FullName,
                        EvaluatedAt = s.Evaluation.EvaluatedAt,
                    },
                    Award = s.AwardIssued == null ? null : new AwardInfoVm
                    {
                        AwardName = s.AwardIssued.CompetitionAward.AwardName,
                        Rank = s.AwardIssued.CompetitionAward.Rank,
                        PrizeValue = s.AwardIssued.CompetitionAward.PrizeValue,
                        IssuedDate = s.AwardIssued.IssuedDate,
                        Remarks = s.AwardIssued.Remarks,
                    },
                    ExhibitionEntries = s.ExhibitionEntries
                        .OrderByDescending(e => e.Exhibition.StartDate).ThenByDescending(e => e.Id)
                        .Select(e => new ExhibitionEntryInfoVm
                        {
                            Id = e.Id,
                            ExhibitionName = e.Exhibition.Name,
                            City = e.Exhibition.City,
                            StartDate = e.Exhibition.StartDate,
                            EndDate = e.Exhibition.EndDate,
                            AskingPrice = e.AskingPrice,
                            Status = e.Status,
                            SoldPrice = e.SoldPrice,
                        })
                        .ToList(),
                },
            })
            .ToListAsync();

        // Status for today and the late flag of each entry
        foreach (var row in rows)
        {
            row.Vm.CompetitionStatus = CompetitionStatusResolver.Resolve(
                new Competition { Status = row.Status, StartDate = row.StartDate, EndDate = row.Vm.CompetitionEndDate });
            row.Vm.IsLate = CompetitionStatusResolver.IsLateSubmission(row.Vm.SubmittedAt, row.Vm.CompetitionEndDate);
            vm.Entries.Add(row.Vm);
        }
        return vm;
    }

    /// <summary>Sets the late flag of every row on the page (calculated in memory from the deadline).</summary>
    private static PagedResult<SubmissionListItemVm> WithLateFlags(PagedResult<SubmissionListItemVm> page)
    {
        foreach (var item in page.Items)
        {
            item.IsLate = CompetitionStatusResolver.IsLateSubmission(item.SubmittedAt, item.CompetitionEndDate);
        }
        return page;
    }
}
