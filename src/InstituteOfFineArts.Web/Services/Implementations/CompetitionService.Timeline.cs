using InstituteOfFineArts.Web.Data.Configurations;
using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.ViewModels.Staff.Competitions;
using Microsoft.EntityFrameworkCore;

namespace InstituteOfFineArts.Web.Services.Implementations;

/// <summary>
/// Timeline of the competition form (step 7, 11/2026): milestones written by the staff, saved with the competition.
/// They are information only (BR-29): the submission period stays the start / end dates (BR-06, BR-07, BR-10) and
/// the results stay a staff action (BR-08), so a milestone never changes the status of the competition.
/// </summary>
public partial class CompetitionService
{
    /// <summary>Most milestones a competition may have.</summary>
    public const int MaxMilestones = 12;

    /// <summary>Errors of a milestone row.</summary>
    public const string MilestoneDateRequiredMessage = "Please enter the date of the milestone";
    public const string MilestoneTitleRequiredMessage = "Please enter a title for the milestone";
    public const string MilestoneTitleTooLongMessage = "A milestone title must be at most 100 characters";
    public const string MilestoneContentTooLongMessage = "A milestone description must be at most 1000 characters";
    public const string MilestoneDateRangeMessage = "The date must be within a year before the start or after the end of the competition";
    public const string TooManyMilestonesMessage = "A competition can have at most 12 milestones";

    /// <summary>A valid milestone row: position in the form, id (0 = new) and the cleaned values.</summary>
    private sealed record MilestoneRow(int Index, long Id, DateOnly Date, string Title, string? Content);

    /// <summary>Error key of one field of a milestone row, e.g. "Milestones[1].Date".</summary>
    private static string MilestoneField(int index, string field) => $"{nameof(CompetitionFormVm.Milestones)}[{index}].{field}";

    /// <summary>Milestone rows of the edit form, in date order.</summary>
    private Task<List<CompetitionMilestoneRowVm>> MilestoneRowsAsync(long competitionId)
        => _db.CompetitionMilestones.AsNoTracking()
            .Where(m => m.CompetitionId == competitionId)
            .OrderBy(m => m.Date).ThenBy(m => m.SortOrder).ThenBy(m => m.Id)
            .Select(m => new CompetitionMilestoneRowVm { Id = m.Id, Date = m.Date, Title = m.Title, Content = m.Content })
            .ToListAsync();

    /// <summary>Checks the kept rows (not removed, not blank): date near the competition, title, length, count.</summary>
    private static List<MilestoneRow> CheckMilestoneRows(IReadOnlyList<CompetitionMilestoneRowVm> rows, DateOnly start, DateOnly end)
    {
        var result = new List<MilestoneRow>();
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row.Remove || row.IsBlank) continue;
            if (row.Date is not { } date)
            {
                throw new BusinessException(MilestoneDateRequiredMessage, MilestoneField(i, nameof(CompetitionMilestoneRowVm.Date)));
            }
            if (date < start.AddYears(-1) || date > end.AddYears(1))
            {
                throw new BusinessException(MilestoneDateRangeMessage, MilestoneField(i, nameof(CompetitionMilestoneRowVm.Date)));
            }
            var title = Clean(row.Title);
            if (title == null)
            {
                throw new BusinessException(MilestoneTitleRequiredMessage, MilestoneField(i, nameof(CompetitionMilestoneRowVm.Title)));
            }
            if (title.Length > CompetitionMilestoneConfiguration.CompetitionMilestone_TitleMax)
            {
                throw new BusinessException(MilestoneTitleTooLongMessage, MilestoneField(i, nameof(CompetitionMilestoneRowVm.Title)));
            }
            var content = Clean(row.Content);
            if (content?.Length > CompetitionMilestoneConfiguration.CompetitionMilestone_ContentMax)
            {
                throw new BusinessException(MilestoneContentTooLongMessage, MilestoneField(i, nameof(CompetitionMilestoneRowVm.Content)));
            }
            result.Add(new MilestoneRow(i, row.Id, date, title, content));
        }
        if (result.Count > MaxMilestones)
        {
            throw new BusinessException(TooManyMilestonesMessage, MilestoneField(result[MaxMilestones].Index, nameof(CompetitionMilestoneRowVm.Title)));
        }
        return result;
    }

    /// <summary>Milestones of a new competition (added to it before its first save).</summary>
    private static void AddMilestones(Competition competition, IReadOnlyList<CompetitionMilestoneRowVm> rows)
    {
        foreach (var row in CheckMilestoneRows(rows, competition.StartDate, competition.EndDate))
        {
            competition.Milestones.Add(new CompetitionMilestone { Date = row.Date, Title = row.Title, Content = row.Content, SortOrder = row.Index });
        }
    }

    /// <summary>
    /// Applies the milestone rows of the edit form (saved by the caller's SaveChanges): new rows are added, existing
    /// ones updated, removed ones deleted. Milestones not on the form are left as they are.
    /// </summary>
    /// <returns>The changes for the history: "Milestone added: 30 Nov 2026 – Award ceremony"</returns>
    private async Task<List<string>> SyncMilestonesAsync(long competitionId, IReadOnlyList<CompetitionMilestoneRowVm> rows,
        DateOnly start, DateOnly end)
    {
        var changes = new List<string>();
        var existing = await _db.CompetitionMilestones.Where(m => m.CompetitionId == competitionId).ToListAsync();
        for (var i = 0; i < rows.Count; i++)
        {
            if (rows[i].Id != 0 && existing.All(m => m.Id != rows[i].Id))
            {
                throw new BusinessException("This milestone does not belong to the competition. Reload the page and try again.",
                    MilestoneField(i, nameof(CompetitionMilestoneRowVm.Title)));
            }
        }
        var kept = CheckMilestoneRows(rows, start, end);
        var untouched = existing.Count(m => rows.All(r => r.Id != m.Id));
        if (kept.Count + untouched > MaxMilestones)
        {
            throw new BusinessException(TooManyMilestonesMessage, MilestoneField(kept[^1].Index, nameof(CompetitionMilestoneRowVm.Title)));
        }

        string Label(DateOnly date, string title) => $"{date.ToString(ChangeDate)} – {title}";
        foreach (var row in rows.Where(r => r.Remove && r.Id != 0))
        {
            var m = existing.Single(x => x.Id == row.Id);
            _db.CompetitionMilestones.Remove(m);
            changes.Add($"Milestone removed: {Label(m.Date, m.Title)}");
        }
        foreach (var row in kept)
        {
            if (row.Id == 0)
            {
                _db.CompetitionMilestones.Add(new CompetitionMilestone
                {
                    CompetitionId = competitionId, Date = row.Date, Title = row.Title, Content = row.Content, SortOrder = row.Index,
                });
                changes.Add($"Milestone added: {Label(row.Date, row.Title)}");
                continue;
            }
            var m = existing.Single(x => x.Id == row.Id);
            var before = Label(m.Date, m.Title);
            var after = Label(row.Date, row.Title);
            if (before != after)
            {
                changes.Add($"Milestone changed: {before} → {after}");
            }
            else if (m.Content != row.Content)
            {
                changes.Add($"Milestone changed: {after} (description)");
            }
            m.Date = row.Date;
            m.Title = row.Title;
            m.Content = row.Content;
            m.SortOrder = row.Index;
        }
        return changes;
    }
}
