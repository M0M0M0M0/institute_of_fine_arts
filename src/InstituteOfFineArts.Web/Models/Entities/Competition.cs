using InstituteOfFineArts.Web.Models.Enums;

namespace InstituteOfFineArts.Web.Models.Entities;

/// <summary>
/// Painting competition (table competition).
/// </summary>
public class Competition : BaseEntity
{
    /// <summary>Competition code, unique. Example: CT2026-01.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Detailed description.</summary>
    public string? Description { get; set; }

    /// <summary>Rules / conditions.</summary>
    public string? Rules { get; set; }

    /// <summary>First day submissions are accepted (from 00:00).</summary>
    public DateOnly StartDate { get; set; }

    /// <summary>End date – the submission deadline is 23:59:59 on this day (BR-06).</summary>
    public DateOnly EndDate { get; set; }

    /// <summary>Status (BR-07, BR-08).</summary>
    public CompetitionStatus Status { get; set; } = CompetitionStatus.UPCOMING;

    /// <summary>Time the results were published.</summary>
    public DateTime? PublishedAt { get; set; }

    /// <summary>Id of the staff profile that published the results.</summary>
    public long? PublishedBy { get; set; }

    /// <summary>Relative path of the cover image (saved with File I/O).</summary>
    public string? CoverImagePath { get; set; }

    /// <summary>General remarks about the competition.</summary>
    public string? ResultRemarks { get; set; }

    /// <summary>Id of the staff profile that created the competition.</summary>
    public long CreatedBy { get; set; }

    /// <summary>Staff member who created the competition.</summary>
    public StaffProfile CreatedByStaff { get; set; } = null!;

    /// <summary>Staff member who published the results.</summary>
    public StaffProfile? PublishedByStaff { get; set; }

    /// <summary>Planned awards of the competition.</summary>
    public ICollection<CompetitionAward> Awards { get; set; } = new List<CompetitionAward>();

    /// <summary>Submissions.</summary>
    public ICollection<Submission> Submissions { get; set; } = new List<Submission>();
}
