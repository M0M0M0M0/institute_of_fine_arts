using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Infrastructure.Api;
using InstituteOfFineArts.Web.Services.Interfaces;
using InstituteOfFineArts.Web.ViewModels.Public;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InstituteOfFineArts.Web.Controllers.Api;

/// <summary>
/// Public gallery for the mobile app (anonymous): GET /api/gallery, /api/gallery/categories, /api/gallery/artworks/{id}.
/// Same paintings as the web gallery (BR-20: BEST or awarded entries of GRADED competitions) from IArtworkCatalogService.
/// Pictures are loaded from /files/submissions/{id}/thumb and /files/submissions/{id}.
/// </summary>
[Route("api/gallery")]
[AllowAnonymous]
public class GalleryApiController : ApiControllerBase
{
    private const int MaxPageSize = 48;
    private const int DefaultPageSize = 12;

    private readonly IArtworkCatalogService _catalog;
    private readonly AppDbContext _db;

    public GalleryApiController(IArtworkCatalogService catalog, AppDbContext db)
    {
        _catalog = catalog;
        _db = db;
    }

    /// <summary>One painting of the gallery list.</summary>
    public sealed record GalleryItemDto(long SubmissionId, string Title, string StudentName, long CompetitionId, string CompetitionTitle,
        string? AwardName, bool HasThumbnail);

    /// <summary>A page of paintings.</summary>
    public sealed record GalleryPageDto(IReadOnlyList<GalleryItemDto> Items, int Page, int PageSize, int TotalCount, int TotalPages);

    /// <summary>Browse category for the filter.</summary>
    public sealed record GalleryCategoryDto(string Group, string Key, string Label);

    /// <summary>One public painting.</summary>
    public sealed record GalleryArtworkDto(long SubmissionId, string Title, string StudentName, long CompetitionId, string CompetitionTitle,
        string? CompetitionDescription, string? AwardName, int? AwardRank, bool IsBest, DateTime SubmittedAt, bool HasThumbnail);

    /// <summary>Public paintings, newest results first; category like "subject/Animals" (404 when unknown), none = all.</summary>
    [HttpGet("")]
    public async Task<ActionResult<GalleryPageDto>> List(string? category, int page = 1, int pageSize = DefaultPageSize)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        ArtworkListVm vm;
        if (string.IsNullOrWhiteSpace(category))
        {
            vm = await _catalog.GetAllAsync(page, pageSize);
        }
        else
        {
            var parts = category.Trim().Trim('/').Split('/', StringSplitOptions.TrimEntries);
            var found = parts.Length == 2 ? _catalog.FindCategory(parts[0], parts[1]) : null;
            if (found == null)
            {
                throw new NotFoundException($"Category '{category}' was not found.");
            }
            vm = await _catalog.GetCategoryAsync(found, page, pageSize);
        }

        var p = vm.Items;
        return new GalleryPageDto(
            p.Items.Select(a => new GalleryItemDto(a.SubmissionId, a.Title, a.StudentName, a.CompetitionId, a.CompetitionTitle, a.AwardName,
                a.HasThumbnail)).ToList(),
            p.Page, p.PageSize, p.TotalCount, p.TotalPages);
    }

    /// <summary>Categories for the filter: group ("subject", "style", "medium"), key and label; the category value is "group/key".</summary>
    [HttpGet("categories")]
    public ActionResult<List<GalleryCategoryDto>> Categories()
        => _catalog.Categories.Select(c => new GalleryCategoryDto(c.Group, c.Key, c.Label)).ToList();

    /// <summary>One public painting; 404 when it does not exist or is not public.</summary>
    [HttpGet("artworks/{id:long}")]
    public async Task<ActionResult<GalleryArtworkDto>> Artwork(long id)
    {
        var a = await _catalog.GetArtworkAsync(id);
        var hasThumb = await _db.Submissions.AsNoTracking().AnyAsync(s => s.Id == id && s.ThumbnailPath != null);
        return new GalleryArtworkDto(a.SubmissionId, a.Title, a.StudentName, a.CompetitionId, a.CompetitionTitle, a.CompetitionDescription,
            a.AwardName, a.AwardRank, a.IsBest, a.SubmittedAt, hasThumb);
    }
}
