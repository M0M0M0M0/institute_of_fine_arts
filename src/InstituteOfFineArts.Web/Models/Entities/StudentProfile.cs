using InstituteOfFineArts.Web.Models.Enums;

namespace InstituteOfFineArts.Web.Models.Entities;

/// <summary>
/// Admission details of a student (table student_profile).
/// </summary>
public class StudentProfile : BaseEntity
{
    /// <summary>Foreign key to the account (one-to-one).</summary>
    public long AccountId { get; set; }

    /// <summary>Student code, unique. Example: HV0001.</summary>
    public string StudentCode { get; set; } = string.Empty;

    /// <summary>Admission date.</summary>
    public DateOnly EnrollmentDate { get; set; }

    /// <summary>Course: Painting, Animation…</summary>
    public string CourseName { get; set; } = string.Empty;

    /// <summary>Class.</summary>
    public string? ClassName { get; set; }

    /// <summary>Study status.</summary>
    public EnrollmentStatus EnrollmentStatus { get; set; } = EnrollmentStatus.STUDYING;

    /// <summary>Remarks.</summary>
    public string? Notes { get; set; }

    /// <summary>Login account.</summary>
    public Account Account { get; set; } = null!;

    /// <summary>Submissions made by this student.</summary>
    public ICollection<Submission> Submissions { get; set; } = new List<Submission>();
}
