using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Helpers;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.ViewModels.Common;
using Microsoft.EntityFrameworkCore;

namespace InstituteOfFineArts.Web.Services.Implementations;

/// <summary>
/// "Needs attention" cards of the Staff dashboard: entries the teachers have not graded yet, closed competitions,
/// exhibition proposals and sales not paid to the student. Each card links to the Staff list filtered on exactly the
/// counted rows; cards with a count of 0 are left out. Locked accounts are the Admin's business, so they are not here.
/// </summary>
public static class AttentionItems
{
    /// <summary>Builds the cards.</summary>
    /// <param name="db">Database</param>
    /// <param name="day">Today (competition statuses come from the dates)</param>
    public static async Task<List<AttentionItemVm>> BuildAsync(AppDbContext db, DateOnly day)
    {
        var closed = db.Competitions.AsNoTracking().WhereStatus(CompetitionStatus.CLOSED, day);
        var toGrade = await db.Submissions.CountAsync(s => s.Evaluation == null && closed.Any(c => c.Id == s.CompetitionId));
        var closedCount = await closed.CountAsync();
        var pending = await db.ExhibitionEntries.CountAsync(e => e.Status == EntryStatus.PENDING);
        var unpaid = await db.ExhibitionEntries.CountAsync(e => e.Status == EntryStatus.SOLD && !e.PaidToStudent);

        var items = new List<AttentionItemVm>
        {
            // Staff do not grade: this card follows the teachers' progress
            new(toGrade, toGrade == 1 ? "entry not graded yet" : "entries not graded yet", "Closed competitions, waiting for the teachers.",
                $"/staff/submissions?Evaluated=false&CompetitionStatus={CompetitionStatus.CLOSED}", "bi-pencil-square"),
            new(closedCount, closedCount == 1 ? "closed competition" : "closed competitions", "Waiting for grading or for the results to be published.",
                $"/staff/competitions?Status={CompetitionStatus.CLOSED}", "bi-hourglass-split"),
            new(pending, pending == 1 ? "exhibition proposal" : "exhibition proposals", "Waiting for a staff decision.",
                $"/staff/exhibition-entries?Status={EntryStatus.PENDING}", "bi-inbox"),
            new(unpaid, unpaid == 1 ? "sale not paid out" : "sales not paid out", "The student has not received the sale price yet.",
                $"/staff/exhibition-entries?Status={EntryStatus.SOLD}&Paid=false", "bi-cash-coin"),
        };
        return items.Where(i => i.Count > 0).ToList();
    }
}
