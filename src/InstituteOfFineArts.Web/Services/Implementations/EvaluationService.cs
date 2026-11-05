using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Helpers;
using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.Services.Interfaces;
using InstituteOfFineArts.Web.ViewModels.Staff.Evaluations;
using Microsoft.EntityFrameworkCore;

namespace InstituteOfFineArts.Web.Services.Implementations;

/// <summary>
/// Evaluation of submissions (F-M07-03..05, F-M07-08, task BE1-10). Any staff member may evaluate any entry (A-07).
/// Rules: entries are evaluated only after the deadline, while the competition is CLOSED (BR-21); GRADED is read only;
/// at most one evaluation per submission, a new save overwrites the evaluator and the time (BR-22);
/// DISQUALIFIED needs a reason in Negative points, every other rating needs Positive points (BR-23);
/// an entry submitted after 23:59:59 of the end date can only be DISQUALIFIED (BR-14, TC-M07-04);
/// an evaluation is not deleted once the entry has an award or has been sent to an exhibition.
/// </summary>
public class EvaluationService : IEvaluationService
{
    /// <summary>Message when the competition is still UPCOMING / ONGOING (BR-21).</summary>
    public const string NotClosedMessage = "Entries can only be evaluated after the submission deadline.";

    /// <summary>Message when the competition results are published.</summary>
    public const string GradedReadOnlyMessage = "Results have been published; evaluations can no longer be changed.";

    /// <summary>Message when a late entry gets a rating other than DISQUALIFIED (BR-14).</summary>
    public const string LateOnlyDisqualifiedMessage = "This entry was submitted after the deadline and can only be rated DISQUALIFIED.";

    /// <summary>Message when a comment is longer than the form allows.</summary>
    public const string CommentTooLongMessage = "Each comment must be at most 2000 characters";

    /// <summary>Message when deleting the evaluation of an entry that has none.</summary>
    public const string NoEvaluationMessage = "This entry has not been evaluated yet.";

    /// <summary>Message when deleting the evaluation of an awarded entry.</summary>
    public const string AwardedDeleteMessage = "This entry has received an award, so its evaluation cannot be deleted.";

    /// <summary>Message when deleting the evaluation of an entry sent to an exhibition.</summary>
    public const string ExhibitedDeleteMessage = "This entry has been sent to an exhibition, so its evaluation cannot be deleted.";

    /// <summary>Message when another staff member created the evaluation at the same moment.</summary>
    public const string ConcurrentCreateMessage = "Another staff member has just evaluated this entry. Please review the evaluation and save again.";

    /// <summary>Database access.</summary>
    private readonly AppDbContext _db;

    /// <summary>Logs created / updated / deleted evaluations.</summary>
    private readonly ILogger<EvaluationService> _logger;

    /// <summary>Receives the required services through Dependency Injection.</summary>
    public EvaluationService(AppDbContext db, ILogger<EvaluationService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<EvaluationPageVm> GetPageAsync(long submissionId)
    {
        // One read: the entry, its competition, student, evaluation and what blocks deleting it
        var data = await _db.Submissions.AsNoTracking()
            .Where(s => s.Id == submissionId)
            .Select(s => new
            {
                s.Id, s.Title, s.SubmittedAt, s.TextType, s.TextContent, s.Description,
                StudentName = s.Student.Account.FullName,
                s.Student.StudentCode,
                s.Competition,
                Evaluation = s.Evaluation == null ? null : new
                {
                    s.Evaluation.Rating, s.Evaluation.PositivePoints, s.Evaluation.NegativePoints, s.Evaluation.ImprovementAreas,
                    s.Evaluation.EvaluatedAt,
                    EvaluatedByName = s.Evaluation.EvaluatedByStaff.Account.FullName,
                },
                HasAward = s.AwardIssued != null,
                HasExhibitionEntry = s.ExhibitionEntries.Any(),
            })
            .FirstOrDefaultAsync()
            ?? throw new NotFoundException($"Submission {submissionId} was not found.");

        var status = CompetitionStatusResolver.Resolve(data.Competition);
        var isLate = CompetitionStatusResolver.IsLateSubmission(data.SubmittedAt, data.Competition.EndDate);
        var page = new EvaluationPageVm
        {
            SubmissionId = data.Id,
            Title = data.Title,
            StudentName = data.StudentName,
            StudentCode = data.StudentCode,
            CompetitionId = data.Competition.Id,
            CompetitionTitle = data.Competition.Title,
            CompetitionCode = data.Competition.Code,
            CompetitionStatus = status,
            EndDate = data.Competition.EndDate,
            SubmittedAt = data.SubmittedAt,
            IsLate = isLate,
            TextType = data.TextType,
            TextContent = data.TextContent,
            Description = data.Description,
            HasEvaluation = data.Evaluation != null,
            EvaluatedByName = data.Evaluation?.EvaluatedByName,
            EvaluatedAt = data.Evaluation?.EvaluatedAt,
            CanEvaluate = status == CompetitionStatus.CLOSED,
            ReadOnlyReason = EditBlockedReason(status),
            // A late entry can only be disqualified (BR-14); otherwise every rating is offered
            AllowedRatings = isLate ? [Rating.DISQUALIFIED] : Enum.GetValues<Rating>(),
        };

        if (data.Evaluation != null)
        {
            // Existing evaluation → the form shows it (editing overwrites it, BR-22)
            page.Form = new EvaluationFormVm
            {
                Rating = data.Evaluation.Rating,
                PositivePoints = data.Evaluation.PositivePoints,
                NegativePoints = data.Evaluation.NegativePoints,
                ImprovementAreas = data.Evaluation.ImprovementAreas,
            };
            page.DeleteBlockedReason = DeleteBlockedReason(status, data.HasAward, data.HasExhibitionEntry);
            page.CanDelete = page.DeleteBlockedReason == null;
        }
        else if (isLate)
        {
            // F-M07-08: a late entry is suggested DISQUALIFIED with the reason already written (staff may reword it)
            page.Form = new EvaluationFormVm
            {
                Rating = Rating.DISQUALIFIED,
                NegativePoints = $"Submitted after the submission deadline ({data.Competition.EndDate:dd MMM yyyy}, 23:59:59).",
            };
        }
        return page;
    }

    /// <inheritdoc />
    public async Task<bool> SaveAsync(long submissionId, long staffProfileId, EvaluationFormVm form)
    {
        // Tracked submission with its evaluation, so an existing evaluation is updated in place
        var submission = await _db.Submissions
            .Include(s => s.Competition)
            .Include(s => s.Evaluation)
            .FirstOrDefaultAsync(s => s.Id == submissionId)
            ?? throw new NotFoundException($"Submission {submissionId} was not found.");

        // BR-21: only after the deadline and before the results are published
        var status = CompetitionStatusResolver.Resolve(submission.Competition);
        if (EditBlockedReason(status) is { } reason)
        {
            throw new BusinessException(reason);
        }

        var (rating, positive, negative, improvement) = Validate(form);

        // BR-14: an entry submitted after the deadline can only be disqualified
        if (CompetitionStatusResolver.IsLateSubmission(submission.SubmittedAt, submission.Competition.EndDate) && rating != Rating.DISQUALIFIED)
        {
            throw new BusinessException(LateOnlyDisqualifiedMessage, nameof(EvaluationFormVm.Rating));
        }

        // BR-22: one evaluation per submission – create it the first time, overwrite it afterwards
        var evaluation = submission.Evaluation;
        var created = evaluation == null;
        if (evaluation == null)
        {
            evaluation = new Evaluation { SubmissionId = submission.Id };
            _db.Evaluations.Add(evaluation);
        }
        evaluation.Rating = rating;
        evaluation.PositivePoints = positive;
        evaluation.NegativePoints = negative;
        evaluation.ImprovementAreas = improvement;
        evaluation.EvaluatedBy = staffProfileId;
        evaluation.EvaluatedAt = DateTimeHelper.Now;

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException) when (created)
        {
            // Unique index UX_evaluation_submission: another staff member saved first → ask to review instead of failing
            throw new BusinessException(ConcurrentCreateMessage);
        }

        _logger.LogInformation("Submission {SubmissionId} {Action} as {Rating} by staff {StaffId}",
            submission.Id, created ? "evaluated" : "re-evaluated", rating, staffProfileId);
        return created;
    }

    /// <inheritdoc />
    public async Task DeleteAsync(long submissionId)
    {
        var submission = await _db.Submissions
            .Include(s => s.Competition)
            .Include(s => s.Evaluation)
            .FirstOrDefaultAsync(s => s.Id == submissionId)
            ?? throw new NotFoundException($"Submission {submissionId} was not found.");
        if (submission.Evaluation == null)
        {
            throw new BusinessException(NoEvaluationMessage);
        }

        // Not after publishing, and never under an award or an exhibition entry (they were chosen from this rating)
        var status = CompetitionStatusResolver.Resolve(submission.Competition);
        var hasAward = await _db.AwardsIssued.AnyAsync(a => a.SubmissionId == submissionId);
        var hasEntry = await _db.ExhibitionEntries.AnyAsync(e => e.SubmissionId == submissionId);
        if (DeleteBlockedReason(status, hasAward, hasEntry) is { } reason)
        {
            throw new BusinessException(reason);
        }

        _db.Evaluations.Remove(submission.Evaluation);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Evaluation of submission {SubmissionId} deleted", submissionId);
    }

    /// <summary>Checks the form again (no caller can skip BR-23) and returns the rating and the trimmed comments.</summary>
    private static (Rating Rating, string? Positive, string? Negative, string? Improvement) Validate(EvaluationFormVm form)
    {
        // Rating: required and one of the six values
        if (form.Rating is not { } rating || !Enum.IsDefined(rating))
        {
            throw new BusinessException(EvaluationFormVm.RatingRequiredMessage, nameof(EvaluationFormVm.Rating));
        }

        var positive = Clean(form.PositivePoints);
        var negative = Clean(form.NegativePoints);
        var improvement = Clean(form.ImprovementAreas);

        // Each comment fits the form limit
        CheckLength(positive, nameof(EvaluationFormVm.PositivePoints));
        CheckLength(negative, nameof(EvaluationFormVm.NegativePoints));
        CheckLength(improvement, nameof(EvaluationFormVm.ImprovementAreas));

        // BR-23: the reason of a disqualification is required; other ratings need the positive points
        if (rating == Rating.DISQUALIFIED && negative == null)
        {
            throw new BusinessException(EvaluationFormVm.DisqualifiedReasonMessage, nameof(EvaluationFormVm.NegativePoints));
        }
        if (rating != Rating.DISQUALIFIED && positive == null)
        {
            throw new BusinessException(EvaluationFormVm.PositivePointsMessage, nameof(EvaluationFormVm.PositivePoints));
        }
        return (rating, positive, negative, improvement);
    }

    /// <summary>Throws on the field when the comment is longer than the form allows.</summary>
    private static void CheckLength(string? value, string field)
    {
        if (value?.Length > EvaluationFormVm.MaxCommentLength)
        {
            throw new BusinessException(CommentTooLongMessage, field);
        }
    }

    /// <summary>Why the evaluation cannot be created / edited in this status (null = CLOSED, allowed).</summary>
    private static string? EditBlockedReason(CompetitionStatus status) => status switch
    {
        CompetitionStatus.CLOSED => null,
        CompetitionStatus.GRADED => GradedReadOnlyMessage,
        _ => NotClosedMessage,
    };

    /// <summary>Why an existing evaluation cannot be deleted (null = allowed).</summary>
    private static string? DeleteBlockedReason(CompetitionStatus status, bool hasAward, bool hasExhibitionEntry)
    {
        if (status == CompetitionStatus.GRADED)
        {
            return GradedReadOnlyMessage;
        }
        if (hasAward)
        {
            return AwardedDeleteMessage;
        }
        return hasExhibitionEntry ? ExhibitedDeleteMessage : null;
    }


    /// <summary>Trims text; blank becomes null.</summary>
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
