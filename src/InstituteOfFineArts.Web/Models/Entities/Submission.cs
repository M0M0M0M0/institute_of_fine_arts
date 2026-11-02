using InstituteOfFineArts.Web.Models.Enums;

namespace InstituteOfFineArts.Web.Models.Entities;

/// <summary>
/// A student's competition entry: JPEG image plus accompanying text (table submission).
/// </summary>
/// <remarks>There is no status column: the displayed status (submitted, waiting for grading, graded, awarded, disqualified) is derived from the competition, evaluation and award.</remarks>
public class Submission : BaseEntity
{
    /// <summary>Foreign key to the competition.</summary>
    public long CompetitionId { get; set; }

    /// <summary>Foreign key to the student profile.</summary>
    public long StudentId { get; set; }

    /// <summary>Painting title (≤ 150 characters).</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Relative path of the JPEG file inside the uploads folder.</summary>
    public string ImagePath { get; set; } = string.Empty;

    /// <summary>Relative path of the thumbnail.</summary>
    public string? ThumbnailPath { get; set; }

    /// <summary>Original file name – only for display/download, never used as a path.</summary>
    public string OriginalFileName { get; set; } = string.Empty;

    /// <summary>File size (bytes).</summary>
    public long FileSize { get; set; }

    /// <summary>Type of the accompanying text.</summary>
    public TextType TextType { get; set; }

    /// <summary>Poem / quotation / story (≤ 4000 characters).</summary>
    public string TextContent { get; set; } = string.Empty;

    /// <summary>Description: why it was painted, why it deserves to win… (≤ 2000 characters).</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Submission time.</summary>
    public DateTime SubmittedAt { get; set; }

    /// <summary>Competition.</summary>
    public Competition Competition { get; set; } = null!;

    /// <summary>Student.</summary>
    public StudentProfile Student { get; set; } = null!;

    /// <summary>Evaluation (at most one – BR-22).</summary>
    public Evaluation? Evaluation { get; set; }

    /// <summary>Award issued to this submission (at most one – BR-31).</summary>
    public AwardIssued? AwardIssued { get; set; }

    /// <summary>Times the painting was sent to an exhibition.</summary>
    public ICollection<ExhibitionEntry> ExhibitionEntries { get; set; } = new List<ExhibitionEntry>();
}
