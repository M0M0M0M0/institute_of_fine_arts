namespace InstituteOfFineArts.Web.Models.Entities;

/// <summary>
/// One picture of an entry (table submission_image). An entry is still one artwork (BR-15); it may have up to 5
/// pictures of that artwork – another angle, another light, a close-up. One of them is the cover: it is copied to the
/// image columns of the submission, so lists, the gallery and the reports keep using one picture.
/// </summary>
public class SubmissionImage : BaseEntity
{
    /// <summary>Entry.</summary>
    public long SubmissionId { get; set; }

    /// <summary>Path of the original under the upload folder.</summary>
    public string ImagePath { get; set; } = string.Empty;

    /// <summary>Path of the 400 px JPEG thumbnail (null when it could not be made).</summary>
    public string? ThumbnailPath { get; set; }

    /// <summary>File name chosen by the student.</summary>
    public string OriginalFileName { get; set; } = string.Empty;

    /// <summary>Size in bytes.</summary>
    public long FileSize { get; set; }

    /// <summary>image/jpeg, image/png or image/webp.</summary>
    public string ContentType { get; set; } = "image/jpeg";

    /// <summary>Order in the entry (0 first).</summary>
    public int SortOrder { get; set; }

    /// <summary>The cover picture of the entry (exactly one per entry).</summary>
    public bool IsCover { get; set; }

    /// <summary>Entry.</summary>
    public Submission Submission { get; set; } = null!;
}
