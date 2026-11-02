using InstituteOfFineArts.Web.Models.Enums;

namespace InstituteOfFineArts.Web.Models.Entities;

/// <summary>
/// Painting sent to an exhibition and its sale details (table exhibition_entry).
/// </summary>
/// <remarks>The payment status (not sold / unpaid / paid) is derived from Status and PaidToStudent, it is not stored.</remarks>
public class ExhibitionEntry : BaseEntity
{
    /// <summary>Foreign key to the exhibition.</summary>
    public long ExhibitionId { get; set; }

    /// <summary>Foreign key to the submission (only BEST/BETTER entries of a graded competition – BR-40).</summary>
    public long SubmissionId { get; set; }

    /// <summary>Asking price set by the student (BR-41).</summary>
    public decimal AskingPrice { get; set; }

    /// <summary>Status.</summary>
    public EntryStatus Status { get; set; } = EntryStatus.PENDING;

    /// <summary>Rejection reason – required when REJECTED.</summary>
    public string? RejectReason { get; set; }

    /// <summary>Actual sale price.</summary>
    public decimal? SoldPrice { get; set; }

    /// <summary>Sale date.</summary>
    public DateOnly? SoldDate { get; set; }

    /// <summary>Foreign key to the buyer.</summary>
    public long? CustomerId { get; set; }

    /// <summary>Whether the sale money has been paid to the student (BR-45).</summary>
    public bool PaidToStudent { get; set; }

    /// <summary>Amount paid to the student.</summary>
    public decimal? PaidAmount { get; set; }

    /// <summary>Payment date.</summary>
    public DateOnly? PaidDate { get; set; }

    /// <summary>Id of the account that created the entry (student proposal or staff).</summary>
    public long ProposedBy { get; set; }

    /// <summary>Id of the staff profile that approved / rejected / recorded the sale.</summary>
    public long? ReviewedBy { get; set; }

    /// <summary>Notes.</summary>
    public string? Notes { get; set; }

    /// <summary>Exhibition.</summary>
    public Exhibition Exhibition { get; set; } = null!;

    /// <summary>Submission.</summary>
    public Submission Submission { get; set; } = null!;

    /// <summary>Buyer.</summary>
    public Customer? Customer { get; set; }

    /// <summary>Account that created the entry.</summary>
    public Account ProposedByAccount { get; set; } = null!;

    /// <summary>Reviewing staff member.</summary>
    public StaffProfile? ReviewedByStaff { get; set; }
}
