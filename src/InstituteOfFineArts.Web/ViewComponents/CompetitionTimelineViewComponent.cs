using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InstituteOfFineArts.Web.ViewComponents;

/// <summary>
/// Timeline of a competition (step 7, BR-29): <c>@await Component.InvokeAsync("CompetitionTimeline", new { competitionId })</c>.
/// Used on the public competition pages and on the competition pages of every role. Nothing is shown for an unknown id.
/// </summary>
public class CompetitionTimelineViewComponent : ViewComponent
{
    /// <summary>Database access.</summary>
    private readonly AppDbContext _db;

    /// <summary>Receives the database context through Dependency Injection.</summary>
    public CompetitionTimelineViewComponent(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>Loads the competition with its milestones and renders the timeline.</summary>
    /// <param name="competitionId">Competition id</param>
    /// <param name="heading">Heading shown above the list (null = none)</param>
    public async Task<IViewComponentResult> InvokeAsync(long competitionId, string? heading = "Timeline")
    {
        var competition = await _db.Competitions.AsNoTracking()
            .Include(c => c.Milestones)
            .FirstOrDefaultAsync(c => c.Id == competitionId);
        if (competition == null)
        {
            return Content(string.Empty);
        }
        competition.Status = CompetitionStatusResolver.Resolve(competition);
        ViewData["Heading"] = heading;
        return View(CompetitionTimeline.Of(competition, DateTimeHelper.Today));
    }
}
