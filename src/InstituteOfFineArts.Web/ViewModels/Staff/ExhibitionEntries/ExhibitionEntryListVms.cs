using System.ComponentModel.DataAnnotations;
using InstituteOfFineArts.Web.Models.Enums;

namespace InstituteOfFineArts.Web.ViewModels.Staff.ExhibitionEntries;

/// <summary>
/// Filters of the staff entry list /staff/exhibition-entries (F-M09-09, SCR-30): exhibition, status,
/// paid to the student, student name or code, page.
/// </summary>
public class EntrySearchVm
{
    /// <summary>Only entries of this exhibition (link "Artworks" of the exhibition page).</summary>
    [Display(Name = "Exhibition")]
    public long? ExhibitionId { get; set; }

    /// <summary>Only entries with this status.</summary>
    [Display(Name = "Status")]
    public EntryStatus? Status { get; set; }

    /// <summary>true = paid to the student, false = sold but not paid yet, null = all.</summary>
    [Display(Name = "Paid to student")]
    public bool? Paid { get; set; }

    /// <summary>Part of the student's full name or student code.</summary>
    [StringLength(100)]
    [Display(Name = "Student name or code")]
    public string? Student { get; set; }

    /// <summary>Page number (1-based).</summary>
    public int Page { get; set; } = 1;
}

/// <summary>An exhibition in a dropdown (filter or create form).</summary>
/// <param name="Id">Exhibition id</param>
/// <param name="Name">Exhibition name</param>
/// <param name="StartDate">First day</param>
/// <param name="EndDate">Last day</param>
public record EntryExhibitionOptionVm(long Id, string Name, DateOnly StartDate, DateOnly EndDate);

/// <summary>One row of the staff entry list.</summary>
public class EntryListItemVm
{
    /// <summary>Entry id.</summary>
    public long Id { get; set; }

    /// <summary>Submission id (thumbnail /files/submissions/{id}/thumb).</summary>
    public long SubmissionId { get; set; }

    /// <summary>Painting title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>True when a thumbnail exists.</summary>
    public bool HasThumbnail { get; set; }

    /// <summary>Full name of the student.</summary>
    public string StudentName { get; set; } = string.Empty;

    /// <summary>Student code.</summary>
    public string StudentCode { get; set; } = string.Empty;

    /// <summary>Exhibition id.</summary>
    public long ExhibitionId { get; set; }

    /// <summary>Exhibition name.</summary>
    public string ExhibitionName { get; set; } = string.Empty;

    /// <summary>Asking price (BR-41).</summary>
    public decimal AskingPrice { get; set; }

    /// <summary>Entry status.</summary>
    public EntryStatus Status { get; set; }

    /// <summary>Sale price, only when SOLD.</summary>
    public decimal? SoldPrice { get; set; }

    /// <summary>Whether the sale money has been paid to the student.</summary>
    public bool PaidToStudent { get; set; }

    /// <summary>Payment date.</summary>
    public DateOnly? PaidDate { get; set; }
}
