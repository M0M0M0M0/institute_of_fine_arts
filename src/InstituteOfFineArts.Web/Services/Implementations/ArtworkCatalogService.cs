using System.Text.RegularExpressions;
using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Helpers;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.Services.Interfaces;
using InstituteOfFineArts.Web.ViewModels.Common;
using InstituteOfFineArts.Web.ViewModels.Public;
using Microsoft.EntityFrameworkCore;

namespace InstituteOfFineArts.Web.Services.Implementations;

/// <summary>
/// Public browsing pages of FE101 with real data (see <see cref="IArtworkCatalogService"/>). The database has no
/// subject / style / medium column, so a category is matched on words in the painting's title and description.
/// </summary>
public class ArtworkCatalogService : IArtworkCatalogService
{
    /// <summary>Most published competitions shown under "Recent results" on the showcase page.</summary>
    private const int ShowcaseResults = 4;

    /// <summary>Number of most recently published competitions whose paintings are "new arrivals".</summary>
    private const int NewArrivalCompetitions = 3;

    /// <summary>Categories of the mega menu (intros written by FE101).</summary>
    private static readonly ArtworkCategory[] AllCategories =
    [
        new("subject", "Animals", "Animal and Wildlife",
            "Venture into the animal kingdom where wild horses race across the landscape, birds rest on flowering branches and dogs and cats keep their human friends company. A vivid and playful look into the secret lives of animals, painted by our students.",
            ["animal", "horse", "dog", "cat", "bird", "cow", "cattle", "sheep", "deer", "fox", "owl", "fish", "elephant", "lion", "swan", "duck", "kingfisher", "butterfl", "langur", "wildlife"]),
        new("subject", "Flora", "Flora",
            "Paintings that focus on flowers, plants, trees, leaves and other botanical subjects: floral arrangements, gardens, forests or detailed botanical studies, with an eye for natural forms, colours and textures.",
            // Plants as the subject (trees and leaves appear in most landscapes, so they are not keywords)
            ["flower", "floral", "bouquet", "blossom", "garden", "plant", "fruit", "still life", "vase", "botanical", "persimmon", "peony", "rose", "lotus"]),
        new("subject", "Landscape", "Landscape",
            "Paintings of natural environments and outdoor scenery – mountains, forests, rivers, lakes, oceans and the countryside – with an eye for atmosphere and the relationship between land and light.",
            ["landscape", "mountain", "valley", "river", "lake", "forest", "field", "hill", "meadow", "countryside", "coast", "sea", "ocean", "waterfall", "village", "rice"]),
        new("subject", "People", "People",
            "Paintings that feature human subjects: portraits, family groups, crowds and figures in everyday activities, historical events or cultural settings – faces, gestures and human interaction.",
            // People as the subject ("a man" / "figures" also appear as small details in landscapes)
            ["portrait", "woman", "women", "girl", "boy", "child", "family", "lady", "gentleman", "self-portrait", "mother", "father"]),
        new("style", "Abstract", "Abstract",
            "Abstract paintings use colours, lines, shapes and textures to build compositions that do not necessarily represent recognisable objects, emphasising expression and ideas over realistic representation.",
            ["abstract", "geometric", "shapes", "lines", "imaginative", "sketch"]),
        new("style", "Classical", "Classical",
            "Classical paintings follow traditional principles and historical techniques: balanced compositions, harmonious proportions and themes drawn from mythology, history, religion or everyday life.",
            ["classical", "histor", "allegor", "myth", "religio", "biblical", "ancient", "temple", "church", "battle", "pastoral"]),
        new("style", "Realism", "Realism",
            "Realist paintings represent people, objects, places and everyday situations as accurately as possible, with lifelike proportions, realistic detail and natural light.",
            ["realis", "lifelike", "detailed", "natural", "study", "studies", "everyday", "street", "market"]),
        new("medium", "OilPaintings", "Oil Paintings",
            "Oil paintings use pigments mixed with oil-based binders on canvas or wood. The medium is known for rich colours, smooth blending and layered textures, from thin glazes to thick, textured brushstrokes.",
            ["oil"]),
        new("medium", "WatercolorPaintings", "Watercolor Paintings",
            "Watercolor paintings use water-soluble pigments on paper. The medium is known for its transparency, delicate colour transitions and fluid effects such as washes, layering and wet-on-wet.",
            ["watercolor", "watercolour", "gouache", "wash"]),
        new("medium", "AcrylicPaintings", "Acrylic Paintings",
            "Acrylic paintings use pigments in an acrylic polymer binder. The medium dries fast, keeps strong colour and works in thin transparent layers or thick opaque ones.",
            ["acrylic"]),
    ];

    /// <summary>Database access.</summary>
    private readonly AppDbContext _db;

    /// <summary>Receives the DbContext through Dependency Injection.</summary>
    public ArtworkCatalogService(AppDbContext db) => _db = db;

    /// <inheritdoc />
    public IReadOnlyList<ArtworkCategory> Categories => AllCategories;

    /// <inheritdoc />
    public ArtworkCategory? FindCategory(string group, string key)
        => AllCategories.FirstOrDefault(c => c.Group.Equals(group, StringComparison.OrdinalIgnoreCase)
                                             && c.Key.Equals(key, StringComparison.OrdinalIgnoreCase));

    /// <inheritdoc />
    public async Task<ArtworkListVm> GetCategoryAsync(ArtworkCategory category, int page, int pageSize = 12)
    {
        // One word per keyword, matched at the start of a word ("oil" finds "oil painting", not "toil")
        var pattern = new Regex(@"\b(" + string.Join("|", category.Keywords.Select(k => Regex.Escape(k.Trim()))) + ")",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var matches = (await Rows(PublicSubmissions()).ToListAsync())
            .Where(r => pattern.IsMatch(r.Title) || (r.Description != null && pattern.IsMatch(r.Description)))
            .ToList();
        return new ArtworkListVm
        {
            Title = category.Title, Intro = category.Intro, Crumb = category.Label, Category = category,
            Items = Page(matches, page, pageSize),
            EmptyMessage = "No published paintings in this category yet.",
        };
    }

    /// <inheritdoc />
    public async Task<ArtworkListVm> GetNewArrivalsAsync(int page, int pageSize = 12)
    {
        var latest = await _db.Competitions.AsNoTracking()
            .Where(c => c.Status == CompetitionStatus.GRADED)
            .OrderByDescending(c => c.PublishedAt).ThenByDescending(c => c.Id)
            .Select(c => c.Id).Take(NewArrivalCompetitions).ToListAsync();
        var rows = await Rows(PublicSubmissions().Where(s => latest.Contains(s.CompetitionId))).ToListAsync();
        return new ArtworkListVm
        {
            Title = "New Art This Month",
            Intro = "The best paintings of our most recently published competitions, fresh from the judges' table.",
            Crumb = "New Arrivals", Items = Page(rows, page, pageSize),
            EmptyMessage = "No competition results have been published yet.",
        };
    }

    /// <inheritdoc />
    public async Task<ArtworkListVm> GetAllAsync(int page, int pageSize = 12)
        => new()
        {
            Title = "All Artworks",
            Intro = "Every painting that won an award or was rated best in one of the institute's competitions.",
            Crumb = "All Artworks", Items = Page(await Rows(PublicSubmissions()).ToListAsync(), page, pageSize),
        };

    /// <inheritdoc />
    public async Task<ArtworkListVm> GetCompetitionArtworksAsync(long competitionId, int page, int pageSize = 12)
    {
        var competition = await _db.Competitions.AsNoTracking().FirstOrDefaultAsync(c => c.Id == competitionId)
            ?? throw new NotFoundException($"Competition {competitionId} was not found.");
        var rows = await Rows(PublicSubmissions().Where(s => s.CompetitionId == competitionId)).ToListAsync();
        return new ArtworkListVm
        {
            Title = competition.Title,
            Intro = competition.Description,
            Crumb = "Artworks", CompetitionId = competition.Id, Items = Page(rows, page, pageSize),
            EmptyMessage = CompetitionStatusResolver.Resolve(competition) == CompetitionStatus.GRADED
                ? "No paintings of this competition are public."
                : "The paintings are shown here once the results are published.",
        };
    }

    /// <inheritdoc />
    public async Task<CompetitionShowcaseListVm> GetShowcaseAsync()
    {
        var today = DateTimeHelper.Today;
        var rows = await _db.Competitions.AsNoTracking()
            .Where(c => c.Status == CompetitionStatus.GRADED || c.EndDate >= today)
            .Select(c => new { Competition = c, HasCover = c.CoverImagePath != null, Count = c.Submissions.Count })
            .ToListAsync();

        // Status from the dates, like every other page (the stored status may lag until the nightly sync)
        var shown = rows.Select(r => (r.Competition, r.HasCover, r.Count, Status: CompetitionStatusResolver.Resolve(r.Competition, today)))
            .Select(x => (x.Competition, Vm: new CompetitionShowcaseVm(x.Competition.Id, x.Competition.Title, x.Competition.Description,
                x.Competition.StartDate, x.Competition.EndDate, x.Status, x.HasCover, x.Count)))
            .ToList();
        var open = shown.Where(x => x.Vm.Status is CompetitionStatus.ONGOING or CompetitionStatus.UPCOMING)
            .OrderBy(x => x.Vm.Status == CompetitionStatus.ONGOING ? 0 : 1).ThenBy(x => x.Vm.EndDate)
            .Select(x => x.Vm).ToList();
        var results = shown.Where(x => x.Vm.Status == CompetitionStatus.GRADED)
            .OrderByDescending(x => x.Competition.PublishedAt).ThenByDescending(x => x.Vm.Id).Take(ShowcaseResults)
            .Select(x => x.Vm).ToList();
        return new CompetitionShowcaseListVm { Open = open, Results = results };
    }

    /// <inheritdoc />
    public async Task<PagedResult<CompetitionShowcaseVm>> SearchShowcaseAsync(GalleryCompetitionSearchVm search, int pageSize = 6)
    {
        var today = DateTimeHelper.Today;
        if (search.Status is { } wanted && !GalleryCompetitionSearchVm.StatusesOf(search.Tab).Contains(wanted))
        {
            search.Tab = GalleryCompetitionSearchVm.TabOf(wanted);
        }
        var statuses = search.Status is { } only ? [only] : GalleryCompetitionSearchVm.StatusesOf(search.Tab);

        var query = _db.Competitions.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search.Keyword))
        {
            var keyword = search.Keyword.Trim();
            query = query.Where(c => c.Title.Contains(keyword) || c.Code.Contains(keyword));
        }

        // A few dozen competitions: status from the dates and the order by status are worked out in memory
        var rows = await query
            .Select(c => new { Competition = c, HasCover = c.CoverImagePath != null, Count = c.Submissions.Count })
            .ToListAsync();
        var listed = rows
            .Select(r => (r.Competition, Vm: new CompetitionShowcaseVm(r.Competition.Id, r.Competition.Title, r.Competition.Description,
                r.Competition.StartDate, r.Competition.EndDate, CompetitionStatusResolver.Resolve(r.Competition, today), r.HasCover, r.Count)))
            .Where(x => statuses.Contains(x.Vm.Status))
            .OrderBy(x => x.Vm.Status switch
            {
                CompetitionStatus.ONGOING => 0,
                CompetitionStatus.UPCOMING => 1,
                CompetitionStatus.CLOSED => 2,
                _ => 3,
            })
            // Within a status: nearest deadline / soonest start first; closed and graded newest first
            .ThenBy(x => x.Vm.Status switch
            {
                CompetitionStatus.ONGOING => x.Vm.EndDate.ToDateTime(TimeOnly.MinValue).Ticks,
                CompetitionStatus.UPCOMING => x.Vm.StartDate.ToDateTime(TimeOnly.MinValue).Ticks,
                CompetitionStatus.CLOSED => -x.Vm.EndDate.ToDateTime(TimeOnly.MinValue).Ticks,
                _ => -(x.Competition.PublishedAt ?? x.Vm.EndDate.ToDateTime(TimeOnly.MinValue)).Ticks,
            })
            .ThenByDescending(x => x.Vm.Id)
            .Select(x => x.Vm)
            .ToList();

        var size = Math.Max(1, pageSize);
        var page = Math.Clamp(search.Page, 1, Math.Max(1, (int)Math.Ceiling(listed.Count / (double)size)));
        return new PagedResult<CompetitionShowcaseVm>
        {
            Items = listed.Skip((page - 1) * size).Take(size).ToList(),
            Page = page,
            PageSize = size,
            TotalCount = listed.Count,
        };
    }

    /// <inheritdoc />
    public async Task<ArtworkDetailVm> GetArtworkAsync(long submissionId)
        => await PublicSubmissions()
               .Where(s => s.Id == submissionId)
               .Select(s => new ArtworkDetailVm(
                   s.Id, s.Title, s.Student.Account.FullName, s.CompetitionId, s.Competition.Title, s.Competition.Description,
                   s.AwardIssued != null ? s.AwardIssued.CompetitionAward.AwardName : null,
                   s.AwardIssued != null ? s.AwardIssued.CompetitionAward.Rank : null,
                   s.Evaluation != null && s.Evaluation.Rating == Rating.BEST, s.SubmittedAt))
               .FirstOrDefaultAsync()
           ?? throw new NotFoundException($"Artwork {submissionId} was not found or is not public.");

    /// <summary>Public paintings (BR-20, same rule as SubmissionImagePolicy.IsPublic): BEST or awarded, competition GRADED.</summary>
    private IQueryable<Models.Entities.Submission> PublicSubmissions()
        => _db.Submissions.AsNoTracking()
            .Where(s => s.Competition.Status == CompetitionStatus.GRADED
                        && ((s.Evaluation != null && s.Evaluation.Rating == Rating.BEST) || s.AwardIssued != null));

    /// <summary>One public painting with what the lists need, newest results first, then award rank.</summary>
    private sealed record Row(long Id, string Title, string? Description, string StudentName, long CompetitionId,
        string CompetitionTitle, string? AwardName, int Rank, bool HasThumbnail, DateTime? PublishedAt);

    private static IQueryable<Row> Rows(IQueryable<Models.Entities.Submission> source)
        => source
            .OrderByDescending(s => s.Competition.PublishedAt).ThenByDescending(s => s.CompetitionId)
            .ThenBy(s => s.AwardIssued != null ? s.AwardIssued.CompetitionAward.Rank : int.MaxValue).ThenBy(s => s.Id)
            .Select(s => new Row(s.Id, s.Title, s.Description, s.Student.Account.FullName, s.CompetitionId, s.Competition.Title,
                s.AwardIssued != null ? s.AwardIssued.CompetitionAward.AwardName : null,
                s.AwardIssued != null ? s.AwardIssued.CompetitionAward.Rank : int.MaxValue,
                s.ThumbnailPath != null, s.Competition.PublishedAt));

    /// <summary>A page of cards (the public set is small, so it is filtered and paged in memory).</summary>
    private static PagedResult<ArtworkCardVm> Page(List<Row> rows, int page, int pageSize)
    {
        var total = rows.Count;
        var pages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        page = Math.Clamp(page, 1, pages);
        return new PagedResult<ArtworkCardVm>
        {
            Items = rows.Skip((page - 1) * pageSize).Take(pageSize)
                .Select(r => new ArtworkCardVm(r.Id, r.Title, r.StudentName, r.CompetitionId, r.CompetitionTitle, r.AwardName, r.HasThumbnail))
                .ToList(),
            Page = page, PageSize = pageSize, TotalCount = total,
        };
    }
}
