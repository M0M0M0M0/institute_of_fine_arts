using InstituteOfFineArts.Web.Models.Enums;

namespace InstituteOfFineArts.Web.ViewModels.Staff.ExhibitionEntries;

/// <summary>
/// Staff details page of an exhibition entry /staff/exhibition-entries/{id} (SCR-30, SCR-31): painting, student,
/// exhibition, price, status, sale, payment, plus the action forms that are allowed in the current state
/// (state diagram §4.7c). The Can* flags only hide buttons; ExhibitionEntryService checks every rule again.
/// </summary>
public class EntryDetailsVm
{
    /// <summary>Entry id.</summary>
    public long Id { get; set; }

    /// <summary>Submission id.</summary>
    public long SubmissionId { get; set; }

    /// <summary>Painting title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Competition of the painting.</summary>
    public string CompetitionTitle { get; set; } = string.Empty;

    /// <summary>Rating of the painting.</summary>
    public Rating? Rating { get; set; }

    /// <summary>Full name of the student.</summary>
    public string StudentName { get; set; } = string.Empty;

    /// <summary>Student code.</summary>
    public string StudentCode { get; set; } = string.Empty;

    /// <summary>Exhibition id.</summary>
    public long ExhibitionId { get; set; }

    /// <summary>Exhibition name.</summary>
    public string ExhibitionName { get; set; } = string.Empty;

    /// <summary>City of the exhibition.</summary>
    public string City { get; set; } = string.Empty;

    /// <summary>First day of the exhibition.</summary>
    public DateOnly StartDate { get; set; }

    /// <summary>Last day of the exhibition.</summary>
    public DateOnly EndDate { get; set; }

    /// <summary>Exhibition status calculated for today.</summary>
    public ExhibitionStatus ExhibitionStatus { get; set; }

    /// <summary>Asking price (BR-41).</summary>
    public decimal AskingPrice { get; set; }

    /// <summary>Entry status.</summary>
    public EntryStatus Status { get; set; }

    /// <summary>Reason when REJECTED.</summary>
    public string? RejectReason { get; set; }

    /// <summary>Notes (for a staff direct create: where the price comes from, BR-41).</summary>
    public string? Notes { get; set; }

    /// <summary>Who created the entry (full name of the account).</summary>
    public string ProposedByName { get; set; } = string.Empty;

    /// <summary>Role of the account that created the entry (STUDENT = proposal, STAFF = direct create).</summary>
    public Role ProposedByRole { get; set; }

    /// <summary>Staff member who last reviewed / recorded the sale or payment.</summary>
    public string? ReviewedByName { get; set; }

    /// <summary>When the entry was created.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Sale price.</summary>
    public decimal? SoldPrice { get; set; }

    /// <summary>Sale date.</summary>
    public DateOnly? SoldDate { get; set; }

    /// <summary>Buyer name.</summary>
    public string? CustomerName { get; set; }

    /// <summary>Buyer phone.</summary>
    public string? CustomerPhone { get; set; }

    /// <summary>Buyer email.</summary>
    public string? CustomerEmail { get; set; }

    /// <summary>Whether the sale money has been paid to the student.</summary>
    public bool PaidToStudent { get; set; }

    /// <summary>Amount paid to the student.</summary>
    public decimal? PaidAmount { get; set; }

    /// <summary>Payment date.</summary>
    public DateOnly? PaidDate { get; set; }

    /// <summary>Accept / reject allowed: PENDING (accept also needs an UPCOMING exhibition, BR-42).</summary>
    public bool CanAccept { get; set; }

    /// <summary>Reject allowed: PENDING.</summary>
    public bool CanReject { get; set; }

    /// <summary>Sale can be recorded: ACCEPTED and the exhibition has started (BR-44).</summary>
    public bool CanSell { get; set; }

    /// <summary>UNSOLD can be set: ACCEPTED and the exhibition has finished.</summary>
    public bool CanMarkUnsold { get; set; }

    /// <summary>Payment can be recorded: SOLD and not paid yet (BR-45).</summary>
    public bool CanPay { get; set; }

    /// <summary>Delete allowed: not SOLD/UNSOLD and the exhibition has not started.</summary>
    public bool CanDelete { get; set; }

    /// <summary>Customers for the buyer dropdown of the sale form.</summary>
    public List<EntryCustomerOptionVm> Customers { get; set; } = new();

    /// <summary>Reject form (values posted back after an error).</summary>
    public RejectFormVm RejectForm { get; set; } = new();

    /// <summary>Sale form (default sold price = asking price, BR-44).</summary>
    public SoldFormVm SoldForm { get; set; } = new();

    /// <summary>Payment form (default amount = sold price, BR-45).</summary>
    public PaidFormVm PaidForm { get; set; } = new();
}
