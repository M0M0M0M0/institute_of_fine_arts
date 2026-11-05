using InstituteOfFineArts.Web.Models.Enums;

namespace InstituteOfFineArts.Web.ViewModels.Staff.Submissions;

/// <summary>
/// Submission details for staff /staff/submissions/{id} (F-M07-02, F-M06-06): painting, texts, student,
/// competition, dates, evaluation, award and exhibition entries.
/// </summary>
public class SubmissionDetailsVm
{
    /// <summary>Submission id (image: /files/submissions/{id}).</summary>
    public long Id { get; set; }

    /// <summary>Title of the painting.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Kind of the accompanying text (poem, quotation, story, other).</summary>
    public TextType TextType { get; set; }

    /// <summary>The poem / quotation / story.</summary>
    public string TextContent { get; set; } = string.Empty;

    /// <summary>Description written by the student.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>File name chosen by the student (used for the download).</summary>
    public string OriginalFileName { get; set; } = string.Empty;

    /// <summary>File size in bytes.</summary>
    public long FileSize { get; set; }

    /// <summary>student_profile.id of the author.</summary>
    public long StudentId { get; set; }

    /// <summary>Full name of the author.</summary>
    public string StudentName { get; set; } = string.Empty;

    /// <summary>Student code of the author.</summary>
    public string StudentCode { get; set; } = string.Empty;

    /// <summary>Competition id.</summary>
    public long CompetitionId { get; set; }

    /// <summary>Competition code.</summary>
    public string CompetitionCode { get; set; } = string.Empty;

    /// <summary>Competition title.</summary>
    public string CompetitionTitle { get; set; } = string.Empty;

    /// <summary>Last day of the competition (deadline 23:59:59).</summary>
    public DateOnly CompetitionEndDate { get; set; }

    /// <summary>Competition status calculated for today (BR-07).</summary>
    public CompetitionStatus CompetitionStatus { get; set; }

    /// <summary>When the entry was submitted.</summary>
    public DateTime SubmittedAt { get; set; }

    /// <summary>Last change by the student, if any.</summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>True when submitted after the deadline (BR-14): shown as "Late entry".</summary>
    public bool IsLate { get; set; }

    /// <summary>The evaluation, or null when not evaluated yet.</summary>
    public EvaluationInfoVm? Evaluation { get; set; }

    /// <summary>The award given to this entry, or null.</summary>
    public AwardInfoVm? Award { get; set; }

    /// <summary>Exhibitions this painting was proposed to / shown in, newest first.</summary>
    public List<ExhibitionEntryInfoVm> ExhibitionEntries { get; set; } = new();

    /// <summary>
    /// "Evaluate" / "Edit evaluation" is shown: only while the competition is CLOSED (BR-21).
    /// The form itself lives at /staff/evaluations/{submissionId} (BE1-10).
    /// </summary>
    public bool CanEvaluate => CompetitionStatus == CompetitionStatus.CLOSED;
}

/// <summary>
/// Evaluation of one entry (rating and comments, BR-22, BR-23).
/// </summary>
public class EvaluationInfoVm
{
    /// <summary>Rating BEST … DISQUALIFIED.</summary>
    public Rating Rating { get; set; }

    /// <summary>Strong points.</summary>
    public string? PositivePoints { get; set; }

    /// <summary>Weak points (reason when DISQUALIFIED).</summary>
    public string? NegativePoints { get; set; }

    /// <summary>What to improve.</summary>
    public string? ImprovementAreas { get; set; }

    /// <summary>Full name of the staff member who evaluated last.</summary>
    public string EvaluatorName { get; set; } = string.Empty;

    /// <summary>When it was evaluated.</summary>
    public DateTime EvaluatedAt { get; set; }
}

/// <summary>
/// Award given to one entry.
/// </summary>
public class AwardInfoVm
{
    /// <summary>Award name, e.g. First Prize.</summary>
    public string AwardName { get; set; } = string.Empty;

    /// <summary>Rank (1 = best).</summary>
    public int Rank { get; set; }

    /// <summary>Prize value in USD.</summary>
    public decimal PrizeValue { get; set; }

    /// <summary>Day the award was given.</summary>
    public DateOnly IssuedDate { get; set; }

    /// <summary>Remarks of the jury.</summary>
    public string? Remarks { get; set; }
}

/// <summary>
/// One exhibition entry of a painting.
/// </summary>
public class ExhibitionEntryInfoVm
{
    /// <summary>Exhibition entry id.</summary>
    public long Id { get; set; }

    /// <summary>Exhibition name.</summary>
    public string ExhibitionName { get; set; } = string.Empty;

    /// <summary>City of the exhibition.</summary>
    public string City { get; set; } = string.Empty;

    /// <summary>First day of the exhibition.</summary>
    public DateOnly StartDate { get; set; }

    /// <summary>Last day of the exhibition.</summary>
    public DateOnly EndDate { get; set; }

    /// <summary>Price asked by the student (USD).</summary>
    public decimal AskingPrice { get; set; }

    /// <summary>PENDING / ACCEPTED / REJECTED / SOLD / UNSOLD.</summary>
    public EntryStatus Status { get; set; }

    /// <summary>Selling price when sold (USD).</summary>
    public decimal? SoldPrice { get; set; }
}

/// <summary>
/// Study history of one student /staff/students/{studentId}/history (F-M07-07): every entry with its
/// rating, comments, award and exhibition entries.
/// </summary>
public class StudentHistoryVm
{
    /// <summary>student_profile.id.</summary>
    public long StudentId { get; set; }

    /// <summary>Full name.</summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>Student code.</summary>
    public string StudentCode { get; set; } = string.Empty;

    /// <summary>Course name.</summary>
    public string CourseName { get; set; } = string.Empty;

    /// <summary>Class name.</summary>
    public string? ClassName { get; set; }

    /// <summary>All entries of the student, newest first.</summary>
    public List<StudentHistoryEntryVm> Entries { get; set; } = new();

    /// <summary>Number of evaluated entries.</summary>
    public int EvaluatedCount => Entries.Count(e => e.Evaluation != null);

    /// <summary>Number of awards received.</summary>
    public int AwardCount => Entries.Count(e => e.Award != null);
}

/// <summary>
/// One entry in a student's history.
/// </summary>
public class StudentHistoryEntryVm
{
    /// <summary>Submission id.</summary>
    public long Id { get; set; }

    /// <summary>Title of the painting.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Competition id.</summary>
    public long CompetitionId { get; set; }

    /// <summary>Competition title.</summary>
    public string CompetitionTitle { get; set; } = string.Empty;

    /// <summary>Last day of the competition (deadline 23:59:59).</summary>
    public DateOnly CompetitionEndDate { get; set; }

    /// <summary>Competition status calculated for today.</summary>
    public CompetitionStatus CompetitionStatus { get; set; }

    /// <summary>When the entry was submitted.</summary>
    public DateTime SubmittedAt { get; set; }

    /// <summary>True when submitted after the deadline (BR-14).</summary>
    public bool IsLate { get; set; }

    /// <summary>The evaluation, or null when not evaluated yet.</summary>
    public EvaluationInfoVm? Evaluation { get; set; }

    /// <summary>The award given to this entry, or null.</summary>
    public AwardInfoVm? Award { get; set; }

    /// <summary>Exhibition entries of this painting.</summary>
    public List<ExhibitionEntryInfoVm> ExhibitionEntries { get; set; } = new();
}
