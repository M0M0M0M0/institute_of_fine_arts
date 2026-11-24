namespace InstituteOfFineArts.Web.ViewModels.Common;

/// <summary>
/// Pictures of one entry for Views/Shared/_EntryImages.cshtml: the cover large (zoom), the others as thumbnails that
/// swap into the large frame. Without picture ids (entries older than submission_image) the entry's own image is shown.
/// </summary>
/// <param name="SubmissionId">Entry</param>
/// <param name="ImageIds">submission_image ids, cover first</param>
/// <param name="Alt">Alternative text (title – student)</param>
/// <param name="AllowDownload">Show "Download original" (staff, the jury, the owner)</param>
public record EntryImagesVm(long SubmissionId, IReadOnlyList<long> ImageIds, string Alt, bool AllowDownload);
