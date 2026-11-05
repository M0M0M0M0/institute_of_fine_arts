using InstituteOfFineArts.Web.Models.Enums;

namespace InstituteOfFineArts.Web.ViewModels.Reports;

/// <summary>
/// Filters of RPT-04 "Exhibition artworks &amp; sales revenue" (F-M10-04, BA §7): exhibition, date range, entry status,
/// student. The date range selects the exhibitions whose period overlaps [From, To] (see ExhibitionReportService).
/// </summary>
public class ExhibitionSalesSearchVm
{
    /// <summary>Exhibition (null = every exhibition).</summary>
    public long? ExhibitionId { get; set; }

    /// <summary>First day of the period (inclusive).</summary>
    public DateOnly? From { get; set; }

    /// <summary>Last day of the period (inclusive).</summary>
    public DateOnly? To { get; set; }

    /// <summary>Entry status (null = every status).</summary>
    public EntryStatus? Status { get; set; }

    /// <summary>Part of the student's full name or student code.</summary>
    public string? Student { get; set; }
}

/// <summary>One row of RPT-04: a painting sent to an exhibition with its sale and payment details.</summary>
public class ExhibitionSalesRowVm
{
    /// <summary>Exhibition entry id.</summary>
    public long Id { get; set; }

    /// <summary>Exhibition name.</summary>
    public string ExhibitionName { get; set; } = string.Empty;

    /// <summary>Painting title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Student full name.</summary>
    public string StudentName { get; set; } = string.Empty;

    /// <summary>Student code.</summary>
    public string StudentCode { get; set; } = string.Empty;

    /// <summary>Rating of the painting (null = not evaluated).</summary>
    public Rating? Rating { get; set; }

    /// <summary>Asking price set by the student.</summary>
    public decimal AskingPrice { get; set; }

    /// <summary>Entry status.</summary>
    public EntryStatus Status { get; set; }

    /// <summary>Sale price (SOLD only).</summary>
    public decimal? SoldPrice { get; set; }

    /// <summary>Sale date (SOLD only).</summary>
    public DateOnly? SoldDate { get; set; }

    /// <summary>Buyer's full name (SOLD only).</summary>
    public string? CustomerName { get; set; }

    /// <summary>Whether the sale money has been paid to the student.</summary>
    public bool PaidToStudent { get; set; }

    /// <summary>Amount paid to the student.</summary>
    public decimal? PaidAmount { get; set; }

    /// <summary>Payment date.</summary>
    public DateOnly? PaidDate { get; set; }
}

/// <summary>Totals of RPT-04 over every matching entry (not only the displayed rows), TC-M10-05.</summary>
public class ExhibitionSalesTotalsVm
{
    /// <summary>Number of matching entries (every status).</summary>
    public int Entries { get; set; }

    /// <summary>Paintings actually sent to the exhibition: ACCEPTED, SOLD or UNSOLD (not PENDING / REJECTED).</summary>
    public int Sent { get; set; }

    /// <summary>Paintings sold.</summary>
    public int Sold { get; set; }

    /// <summary>Paintings sent but not sold: still on show (ACCEPTED) or UNSOLD.</summary>
    public int Unsold { get; set; }

    /// <summary>Total asking price of the sent paintings.</summary>
    public decimal TotalAskingPrice { get; set; }

    /// <summary>Total asking price of the sold paintings (base of the difference).</summary>
    public decimal SoldAskingPrice { get; set; }

    /// <summary>Total sale price (revenue) = sum of soldPrice.</summary>
    public decimal TotalSoldPrice { get; set; }

    /// <summary>Total sale price minus the asking price of the same sold paintings.</summary>
    public decimal Difference => TotalSoldPrice - SoldAskingPrice;

    /// <summary>Sold paintings already paid to the student.</summary>
    public int PaidCount { get; set; }

    /// <summary>Amount already paid to students.</summary>
    public decimal PaidAmount { get; set; }

    /// <summary>Sold paintings not paid to the student yet.</summary>
    public int UnpaidCount { get; set; }

    /// <summary>Sale price still owed to students.</summary>
    public decimal UnpaidAmount { get; set; }
}

/// <summary>Result of RPT-04: rows (capped) and totals over every matching entry.</summary>
public class ExhibitionSalesReportVm
{
    /// <summary>Displayed rows (at most ExhibitionReportService.MaxRows).</summary>
    public List<ExhibitionSalesRowVm> Rows { get; set; } = new();

    /// <summary>True when more entries matched than the displayed rows.</summary>
    public bool Truncated { get; set; }

    /// <summary>Totals.</summary>
    public ExhibitionSalesTotalsVm Totals { get; set; } = new();
}

/// <summary>
/// Filters of RPT-05 "Money owed to students" (F-M10-05, BA §7): exhibition, student, sold date range.
/// </summary>
public class StudentDebtSearchVm
{
    /// <summary>Exhibition (null = every exhibition).</summary>
    public long? ExhibitionId { get; set; }

    /// <summary>Part of the student's full name or student code.</summary>
    public string? Student { get; set; }

    /// <summary>First sale day (inclusive).</summary>
    public DateOnly? From { get; set; }

    /// <summary>Last sale day (inclusive).</summary>
    public DateOnly? To { get; set; }
}

/// <summary>One row of RPT-05: a sold painting whose money has not been paid to the student.</summary>
public class StudentDebtRowVm
{
    /// <summary>Exhibition entry id.</summary>
    public long Id { get; set; }

    /// <summary>Student full name.</summary>
    public string StudentName { get; set; } = string.Empty;

    /// <summary>Student code.</summary>
    public string StudentCode { get; set; } = string.Empty;

    /// <summary>Painting title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Exhibition name.</summary>
    public string ExhibitionName { get; set; } = string.Empty;

    /// <summary>Sale date.</summary>
    public DateOnly SoldDate { get; set; }

    /// <summary>Sale price = amount owed (BR-45: the student receives the whole price).</summary>
    public decimal SoldPrice { get; set; }

    /// <summary>Days since the sale (today − sold date).</summary>
    public int DaysUnpaid { get; set; }
}

/// <summary>Result of RPT-05: rows (capped) and totals over every matching entry.</summary>
public class StudentDebtReportVm
{
    /// <summary>Displayed rows, oldest sale first (at most ExhibitionReportService.MaxRows).</summary>
    public List<StudentDebtRowVm> Rows { get; set; } = new();

    /// <summary>True when more entries matched than the displayed rows.</summary>
    public bool Truncated { get; set; }

    /// <summary>Number of unpaid sold paintings.</summary>
    public int Count { get; set; }

    /// <summary>Total amount not paid to students yet.</summary>
    public decimal TotalUnpaid { get; set; }
}
