using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Helpers;
using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.Services.Interfaces;
using InstituteOfFineArts.Web.ViewModels.Common;
using InstituteOfFineArts.Web.ViewModels.Student.Submissions;
using Microsoft.EntityFrameworkCore;

namespace InstituteOfFineArts.Web.Services.Implementations;

/// <summary>
/// A student's own competition entries (M06, tasks BE1-07..BE1-09). The deadline is always checked here on the
/// server with CompetitionStatusResolver (BR-13, BR-14), never only by hiding buttons. Files and rows are kept
/// consistent: a file written for a row that could not be saved is deleted again, and old files are deleted only
/// after the database change succeeded (BR-18).
/// </summary>
public class SubmissionService : ISubmissionService
{
    /// <summary>Message when the competition has not opened yet (TC-M06-11).</summary>
    public const string NotStartedMessage = "The competition has not started yet.";

    /// <summary>Message when the deadline has passed or results are published (BR-14, TC-M06-10, TC-M06-13).</summary>
    public const string DeadlinePassedMessage = "The submission deadline has passed.";

    /// <summary>Message for a second entry in the same competition (BR-15, docs/plan/04 TC-M06 note).</summary>
    public const string AlreadySubmittedMessage = "You have already submitted to this competition. Use Edit instead.";

    /// <summary>Database access.</summary>
    private readonly AppDbContext _db;

    /// <summary>Stores and deletes the image files.</summary>
    private readonly IFileStorageService _files;

    /// <summary>Logs submitted / changed / deleted entries.</summary>
    private readonly ILogger<SubmissionService> _logger;

    /// <summary>Receives the required services through Dependency Injection.</summary>
    public SubmissionService(AppDbContext db, IFileStorageService files, ILogger<SubmissionService> logger)
    {
        _db = db;
        _files = files;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<SubmissionFormVm> GetNewFormAsync(long competitionId, long studentProfileId)
    {
        var competition = await FindOpenCompetitionAsync(competitionId, studentProfileId);
        return new SubmissionFormVm
        {
            CompetitionId = competition.Id,
            CompetitionTitle = competition.Title,
            Deadline = competition.EndDate,
        };
    }

    /// <inheritdoc />
    public async Task<SubmissionSaveResult> CreateAsync(SubmissionFormVm form, long studentProfileId)
    {
        // 1. Competition open and no entry yet – checked at the moment of the request (BR-13..BR-15)
        var competition = await FindOpenCompetitionAsync(form.CompetitionId, studentProfileId);
        var textType = CheckFields(form);

        // 2. Store the JPEG (BR-16..BR-18); an invalid file throws on the Image field and nothing is written
        if (form.Image == null)
        {
            throw new BusinessException(JpegValidator.MissingMessage, nameof(SubmissionFormVm.Image));
        }
        var image = await _files.SaveSubmissionImageAsync(form.Image, competition.Id, nameof(SubmissionFormVm.Image));

        // 3. Insert the row; when that fails the stored files would be orphaned, so they are deleted again
        var submission = new Submission
        {
            CompetitionId = competition.Id,
            StudentId = studentProfileId,
            Title = form.Title.Trim(),
            Description = form.Description.Trim(),
            TextType = textType,
            TextContent = form.TextContent.Trim(),
            ImagePath = image.RelativePath,
            ThumbnailPath = image.ThumbnailPath,
            OriginalFileName = image.OriginalFileName,
            FileSize = image.FileSize,
            SubmittedAt = DateTimeHelper.Now,
        };
        _db.Submissions.Add(submission);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            _files.Delete(image.RelativePath, image.ThumbnailPath);
            _db.Entry(submission).State = EntityState.Detached;

            // Two submits at the same time: the unique index (BR-15) stopped the second one
            if (await HasSubmittedAsync(competition.Id, studentProfileId))
            {
                throw new BusinessException(AlreadySubmittedMessage);
            }
            throw;
        }
        catch
        {
            _files.Delete(image.RelativePath, image.ThumbnailPath);
            throw;
        }

        _logger.LogInformation("Submission {SubmissionId} created by student {StudentId} in competition {CompetitionId}",
            submission.Id, studentProfileId, competition.Id);
        return new SubmissionSaveResult(submission.Id, image.Warning);
    }

    /// <inheritdoc />
    public async Task<PagedResult<MySubmissionListItemVm>> SearchMineAsync(long studentProfileId, MySubmissionSearchVm search, int pageSize = 10)
    {
        var today = DateTimeHelper.Today;

        // Only the signed-in student's entries (BR-20)
        var query = _db.Submissions.AsNoTracking().Where(s => s.StudentId == studentProfileId);

        // Competition and part of the title
        if (search.CompetitionId is { } competitionId)
        {
            query = query.Where(s => s.CompetitionId == competitionId);
        }
        if (!string.IsNullOrWhiteSpace(search.Title))
        {
            var title = search.Title.Trim();
            query = query.Where(s => s.Title.Contains(title));
        }

        // Submitted date range: whole days, "To" included up to 23:59:59
        if (search.From is { } from)
        {
            var start = DateTimeHelper.StartOfDay(from);
            query = query.Where(s => s.SubmittedAt >= start);
        }
        if (search.To is { } to)
        {
            var end = DateTimeHelper.StartOfNextDay(to);
            query = query.Where(s => s.SubmittedAt < end);
        }

        // Rating: only published results (BR-24), so an unpublished evaluation is never revealed by the filter
        if (search.Rating is { } rating)
        {
            query = query.Where(s => s.Competition.Status == CompetitionStatus.GRADED
                                     && s.Evaluation != null && s.Evaluation.Rating == rating);
        }

        var rows = query
            .OrderByDescending(s => s.SubmittedAt).ThenByDescending(s => s.Id)
            .Select(s => new MySubmissionListItemVm
            {
                Id = s.Id,
                Title = s.Title,
                HasThumbnail = s.ThumbnailPath != null,
                CompetitionId = s.CompetitionId,
                CompetitionTitle = s.Competition.Title,
                StartDate = s.Competition.StartDate,
                EndDate = s.Competition.EndDate,
                CompetitionStatus = s.Competition.Status,
                SubmittedAt = s.SubmittedAt,
                Rating = s.Competition.Status == CompetitionStatus.GRADED && s.Evaluation != null
                    ? s.Evaluation.Rating
                    : null,
            });

        var page = await PagedResult<MySubmissionListItemVm>.CreateAsync(rows, search.Page, pageSize);

        // Status for today (the stored value may be stale) and whether the entry can still be changed
        foreach (var item in page.Items)
        {
            item.CompetitionStatus = CompetitionStatusResolver.Resolve(
                new Competition { Status = item.CompetitionStatus, StartDate = item.StartDate, EndDate = item.EndDate }, today);
            item.CanEdit = item.CompetitionStatus == CompetitionStatus.ONGOING;
        }
        return page;
    }

    /// <inheritdoc />
    public async Task<List<MyCompetitionOptionVm>> GetMyCompetitionsAsync(long studentProfileId)
        => await _db.Submissions.AsNoTracking()
            .Where(s => s.StudentId == studentProfileId)
            .OrderByDescending(s => s.Competition.StartDate)
            .Select(s => new MyCompetitionOptionVm(s.CompetitionId, s.Competition.Title))
            .ToListAsync();

    /// <inheritdoc />
    public async Task<MySubmissionDetailsVm> GetDetailsAsync(long id, long studentProfileId)
    {
        // Owner only: another student's id looks exactly like an unknown id (TC-M06-14)
        var s = await _db.Submissions.AsNoTracking()
            .Include(x => x.Competition)
            .Include(x => x.Evaluation)
            .FirstOrDefaultAsync(x => x.Id == id && x.StudentId == studentProfileId)
            ?? throw new NotFoundException($"Submission {id} was not found.");
        var status = CompetitionStatusResolver.Resolve(s.Competition);

        return new MySubmissionDetailsVm
        {
            Id = s.Id,
            Title = s.Title,
            OriginalFileName = s.OriginalFileName,
            FileSize = s.FileSize,
            TextType = s.TextType,
            TextContent = s.TextContent,
            Description = s.Description,
            SubmittedAt = s.SubmittedAt,
            UpdatedAt = s.UpdatedAt,
            CompetitionId = s.CompetitionId,
            CompetitionCode = s.Competition.Code,
            CompetitionTitle = s.Competition.Title,
            EndDate = s.Competition.EndDate,
            CompetitionStatus = status,
            CanEdit = status == CompetitionStatus.ONGOING,
            // BR-24: the evaluation stays hidden until the results are published
            Evaluation = status == CompetitionStatus.GRADED && s.Evaluation is { } e
                ? new MyEvaluationVm(e.Rating, e.PositivePoints, e.NegativePoints, e.ImprovementAreas, e.EvaluatedAt)
                : null,
        };
    }

    /// <inheritdoc />
    public async Task<SubmissionEditVm> GetForEditAsync(long id, long studentProfileId)
    {
        var s = await FindEditableAsync(id, studentProfileId, tracking: false);
        return new SubmissionEditVm
        {
            Id = s.Id,
            CompetitionId = s.CompetitionId,
            CompetitionTitle = s.Competition.Title,
            Deadline = s.Competition.EndDate,
            Title = s.Title,
            Description = s.Description,
            TextType = s.TextType,
            TextContent = s.TextContent,
            OriginalFileName = s.OriginalFileName,
        };
    }

    /// <inheritdoc />
    public async Task<SubmissionSaveResult> UpdateAsync(long id, SubmissionEditVm form, long studentProfileId)
    {
        // Owner + still ONGOING at the moment of the request (BR-13, BR-14, BR-20)
        var s = await FindEditableAsync(id, studentProfileId, tracking: true);
        var textType = CheckFields(form);

        s.Title = form.Title.Trim();
        s.Description = form.Description.Trim();
        s.TextType = textType;
        s.TextContent = form.TextContent.Trim();

        // New image (optional): write the new file first; the old files are deleted only after saving (BR-18)
        var oldImage = s.ImagePath;
        var oldThumbnail = s.ThumbnailPath;
        StoredImage? image = null;
        if (form.NewImage is { Length: > 0 })
        {
            image = await _files.SaveSubmissionImageAsync(form.NewImage, s.CompetitionId, nameof(SubmissionEditVm.NewImage));
            s.ImagePath = image.RelativePath;
            s.ThumbnailPath = image.ThumbnailPath;
            s.OriginalFileName = image.OriginalFileName;
            s.FileSize = image.FileSize;
        }

        try
        {
            await _db.SaveChangesAsync();
        }
        catch
        {
            // The row still points to the old files: remove the new ones so they are not orphaned
            if (image != null)
            {
                _files.Delete(image.RelativePath, image.ThumbnailPath);
            }
            throw;
        }

        if (image != null)
        {
            _files.Delete(oldImage, oldThumbnail);
        }
        _logger.LogInformation("Submission {SubmissionId} updated by student {StudentId} (image replaced: {Replaced})",
            s.Id, studentProfileId, image != null);
        return new SubmissionSaveResult(s.Id, image?.Warning);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(long id, long studentProfileId)
    {
        var s = await FindEditableAsync(id, studentProfileId, tracking: true);

        // Row first, files after the delete succeeded (BR-18)
        _db.Submissions.Remove(s);
        await _db.SaveChangesAsync();
        _files.Delete(s.ImagePath, s.ThumbnailPath);
        _logger.LogInformation("Submission {SubmissionId} deleted by student {StudentId}", s.Id, studentProfileId);
    }

    /// <summary>
    /// Loads a competition that accepts a new entry from this student: it exists, is ONGOING (BR-13, BR-14)
    /// and the student has no entry in it yet (BR-15).
    /// </summary>
    private async Task<Competition> FindOpenCompetitionAsync(long competitionId, long studentProfileId)
    {
        var competition = await _db.Competitions.AsNoTracking().FirstOrDefaultAsync(c => c.Id == competitionId)
                          ?? throw new NotFoundException($"Competition {competitionId} was not found.");
        EnsureOpen(competition);
        if (await HasSubmittedAsync(competitionId, studentProfileId))
        {
            throw new BusinessException(AlreadySubmittedMessage);
        }
        return competition;
    }

    /// <summary>
    /// Loads an own entry with its competition and checks it can still be changed. Other students' entries throw
    /// NotFoundException (TC-M06-14); after the deadline BusinessException (TC-M06-13).
    /// </summary>
    private async Task<Submission> FindEditableAsync(long id, long studentProfileId, bool tracking)
    {
        var query = _db.Submissions.Include(x => x.Competition).AsQueryable();
        if (!tracking)
        {
            query = query.AsNoTracking();
        }
        var s = await query.FirstOrDefaultAsync(x => x.Id == id && x.StudentId == studentProfileId)
                ?? throw new NotFoundException($"Submission {id} was not found.");
        EnsureOpen(s.Competition);
        return s;
    }

    /// <summary>True when the student already has an entry in the competition (BR-15).</summary>
    private Task<bool> HasSubmittedAsync(long competitionId, long studentProfileId)
        => _db.Submissions.AnyAsync(s => s.CompetitionId == competitionId && s.StudentId == studentProfileId);

    /// <summary>Entries can be submitted, edited and deleted only while the competition is ONGOING (BR-13, BR-14).</summary>
    private static void EnsureOpen(Competition competition)
    {
        if (CompetitionStatusResolver.IsOpenForSubmission(competition))
        {
            return;
        }

        // Before the start: not open yet; closed or graded: the deadline has passed
        throw new BusinessException(CompetitionStatusResolver.Resolve(competition) == CompetitionStatus.UPCOMING
            ? NotStartedMessage
            : DeadlinePassedMessage);
    }

    /// <summary>
    /// Checks the BR-19 fields again on the server (the form attributes can be skipped by a direct request) and
    /// returns the text type.
    /// </summary>
    private static TextType CheckFields(SubmissionFieldsVm form)
    {
        // Required texts within the column lengths
        CheckText(form.Title, SubmissionFieldsVm.TitleMaxLength, "Please enter a title", "Title must be at most 150 characters",
            nameof(SubmissionFieldsVm.Title));
        CheckText(form.Description, SubmissionFieldsVm.DescriptionMaxLength, "Please enter a description",
            "Description must be at most 2000 characters", nameof(SubmissionFieldsVm.Description));
        CheckText(form.TextContent, SubmissionFieldsVm.TextMaxLength, "Please enter the poem, quotation or story",
            "The text must be at most 4000 characters", nameof(SubmissionFieldsVm.TextContent));

        // Text type: one of POEM / QUOTATION / STORY / OTHER
        if (form.TextType is not { } type || !Enum.IsDefined(type))
        {
            throw new BusinessException("Please choose the text type", nameof(SubmissionFieldsVm.TextType));
        }
        return type;
    }

    /// <summary>Throws BusinessException on <paramref name="field"/> when the text is blank or too long.</summary>
    private static void CheckText(string? value, int maxLength, string requiredMessage, string tooLongMessage, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BusinessException(requiredMessage, field);
        }
        if (value.Trim().Length > maxLength)
        {
            throw new BusinessException(tooLongMessage, field);
        }
    }
}
