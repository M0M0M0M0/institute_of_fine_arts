using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Helpers;
using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace InstituteOfFineArts.Web.Services.Implementations;

/// <summary>
/// Rules shared by the student proposal (StudentExhibitionEntryService) and the staff direct create
/// (ExhibitionEntryService), so "which painting may be sent to which exhibition" is decided in one place:
/// BR-40 (BEST/BETTER of a GRADED competition), BR-41 (asking price &gt; 0 USD), BR-42 (UPCOMING exhibition only),
/// BR-43 (one active PENDING/ACCEPTED entry per painting, a SOLD painting never again) and TC-M09-04
/// (the same painting is never sent twice to the same exhibition).
/// </summary>
public static class ExhibitionEntryRules
{
    /// <summary>Message when the exhibition has already started or finished (BR-42).</summary>
    public const string NotUpcomingMessage = "Artworks can only be sent to an upcoming exhibition (one that has not started yet).";

    /// <summary>Message when the painting is not BEST/BETTER of a competition with published results (BR-40, US-13 AC2).</summary>
    public const string NotEligibleMessage = "Only paintings rated BEST or BETTER in a competition with published results can be sent to an exhibition.";

    /// <summary>Message when the painting already has a PENDING or ACCEPTED entry (BR-43, TC-M09-04).</summary>
    public const string DuplicateMessage = "This painting is already in an exhibition.";

    /// <summary>Message when the painting was already sent to this exhibition, e.g. rejected before (TC-M09-04).</summary>
    public const string SameExhibitionMessage = "This painting has already been sent to this exhibition.";

    /// <summary>Message when the painting has been sold (BR-43, US-13 AC6).</summary>
    public const string SoldMessage = "This painting has already been sold and cannot be sent to another exhibition.";

    /// <summary>Message when the asking price is missing.</summary>
    public const string PriceRequiredMessage = "Please enter the asking price";

    /// <summary>Message when the asking price is 0 or negative (BR-41, TC-M09-03).</summary>
    public const string PriceMessage = "Asking price must be greater than 0 (USD)";

    /// <summary>Message when an amount has more than 2 decimal places (column decimal(18,2), BR-47).</summary>
    public const string DecimalsMessage = "Amounts can have at most 2 decimal places";

    /// <summary>Largest amount the column decimal(18,2) can hold.</summary>
    public const decimal MaxAmount = 9999999999999999.99m;

    /// <summary>
    /// Keeps only the paintings that may be sent to <paramref name="exhibitionId"/>: rated BEST or BETTER in a GRADED
    /// competition (BR-40), without an active or sold entry (BR-43) and never sent to this exhibition before (TC-M09-04).
    /// </summary>
    /// <param name="query">Submissions to filter</param>
    /// <param name="exhibitionId">Target exhibition</param>
    public static IQueryable<Submission> WhereEligible(this IQueryable<Submission> query, long exhibitionId)
        => query.Where(s => s.Competition.Status == CompetitionStatus.GRADED
                            && s.Evaluation != null
                            && (s.Evaluation.Rating == Rating.BEST || s.Evaluation.Rating == Rating.BETTER)
                            && !s.ExhibitionEntries.Any(e => e.Status == EntryStatus.PENDING
                                                             || e.Status == EntryStatus.ACCEPTED
                                                             || e.Status == EntryStatus.SOLD
                                                             || e.ExhibitionId == exhibitionId));

    /// <summary>Loads the exhibition (NotFoundException when unknown) and checks it has not started (BR-42).</summary>
    /// <param name="db">Database</param>
    /// <param name="exhibitionId">Exhibition id</param>
    public static async Task<Exhibition> FindUpcomingExhibitionAsync(AppDbContext db, long exhibitionId)
    {
        var exhibition = await db.Exhibitions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == exhibitionId)
                         ?? throw new NotFoundException($"Exhibition {exhibitionId} was not found.");

        // The stored status may be stale: always calculate it from the dates
        if (CompetitionStatusResolver.Resolve(exhibition) != ExhibitionStatus.UPCOMING)
        {
            throw new BusinessException(NotUpcomingMessage);
        }
        return exhibition;
    }

    /// <summary>
    /// Checks one painting against BR-40 and BR-43 at the moment of the request (the dropdown may be out of date).
    /// The submission must be loaded with Competition and Evaluation.
    /// </summary>
    /// <param name="db">Database</param>
    /// <param name="submission">Painting to send</param>
    /// <param name="exhibitionId">Target exhibition</param>
    /// <param name="field">Form field that gets the message</param>
    public static async Task EnsureCanSendAsync(AppDbContext db, Submission submission, long exhibitionId, string field)
    {
        // BR-40: BEST / BETTER of a competition whose results are published
        if (submission.Competition.Status != CompetitionStatus.GRADED
            || submission.Evaluation?.Rating is not (Rating.BEST or Rating.BETTER))
        {
            throw new BusinessException(NotEligibleMessage, field);
        }

        // BR-43: what happened to the painting in other exhibitions decides whether it can go again
        var statuses = await db.ExhibitionEntries.AsNoTracking()
            .Where(e => e.SubmissionId == submission.Id)
            .Select(e => new { e.Status, e.ExhibitionId })
            .ToListAsync();
        if (statuses.Any(e => e.Status == EntryStatus.SOLD))
        {
            throw new BusinessException(SoldMessage, field);
        }
        if (statuses.Any(e => e.Status is EntryStatus.PENDING or EntryStatus.ACCEPTED))
        {
            throw new BusinessException(DuplicateMessage, field);
        }

        // A rejected painting may go to another exhibition, but not again to the same one
        if (statuses.Any(e => e.ExhibitionId == exhibitionId))
        {
            throw new BusinessException(SameExhibitionMessage, field);
        }
    }

    /// <summary>
    /// Checks an amount again on the server (the form attributes can be skipped by a direct request):
    /// required, greater than 0, at most 2 decimals, fits decimal(18,2). Returns the amount.
    /// </summary>
    /// <param name="amount">Amount from the form</param>
    /// <param name="field">Form field that gets the message</param>
    /// <param name="requiredMessage">Message when it is missing</param>
    /// <param name="positiveMessage">Message when it is 0 or negative (or too large)</param>
    public static decimal CheckAmount(decimal? amount, string field, string requiredMessage, string positiveMessage)
    {
        if (amount is not { } value)
        {
            throw new BusinessException(requiredMessage, field);
        }
        if (value <= 0 || value > MaxAmount)
        {
            throw new BusinessException(positiveMessage, field);
        }
        if (decimal.Round(value, 2) != value)
        {
            throw new BusinessException(DecimalsMessage, field);
        }
        return value;
    }

    /// <summary>
    /// True when the unique index UX_entry_active_submission (BR-43) stopped the insert, i.e. another request
    /// created an active entry for the same painting at the same moment.
    /// </summary>
    /// <param name="db">Database</param>
    /// <param name="submissionId">Painting</param>
    public static Task<bool> HasActiveEntryAsync(AppDbContext db, long submissionId)
        => db.ExhibitionEntries.AnyAsync(e => e.SubmissionId == submissionId
                                              && (e.Status == EntryStatus.PENDING || e.Status == EntryStatus.ACCEPTED));

    /// <summary>Trims text; blank becomes null.</summary>
    /// <param name="value">Text from the form</param>
    public static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
