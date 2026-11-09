namespace InstituteOfFineArts.Web.ViewModels.Reports;

/// <summary>File format of a report export (F-M14-01): Excel workbook or PDF document.</summary>
public enum ReportExportFormat
{
    /// <summary>Excel workbook (.xlsx, ClosedXML).</summary>
    Xlsx,

    /// <summary>PDF document (QuestPDF).</summary>
    Pdf,
}

/// <summary>How the values of one export column are written and formatted.</summary>
public enum ReportExportKind
{
    /// <summary>Plain text.</summary>
    Text,

    /// <summary>Whole number (counts, ranks).</summary>
    Number,

    /// <summary>USD amount, format <c>$#,##0.00</c> (BR-47).</summary>
    Money,

    /// <summary>Percentage already multiplied by 100 (e.g. 33.3), shown as <c>33.3%</c>.</summary>
    Percent,

    /// <summary>Date (DateOnly), shown as <c>dd MMM yyyy</c>.</summary>
    Date,

    /// <summary>Date and time (DateTime), shown as <c>dd MMM yyyy HH:mm</c>.</summary>
    DateTime,
}

/// <summary>One column of an exported table.</summary>
/// <param name="Header">Column header (same text as the HTML report)</param>
/// <param name="Kind">Value type and format</param>
public record ReportExportColumn(string Header, ReportExportKind Kind = ReportExportKind.Text);

/// <summary>One table of an exported report: header, rows and an optional totals row (the HTML table footer).</summary>
public class ReportExportTable
{
    /// <summary>Heading shown above the table (null = none, the first table of most reports).</summary>
    public string? Heading { get; set; }

    /// <summary>Columns in display order.</summary>
    public List<ReportExportColumn> Columns { get; set; } = new();

    /// <summary>Rows; each value matches the column at the same index (null = empty cell).</summary>
    public List<object?[]> Rows { get; set; } = new();

    /// <summary>Totals row (bold, under the rows), or null when the HTML table has no totals row.</summary>
    public object?[]? TotalsRow { get; set; }

    /// <summary>Text written instead of the table when it has no row (e.g. "No data").</summary>
    public string EmptyText { get; set; } = "No data";
}

/// <summary>One line of the totals block of a report (the "Totals" table of the HTML page).</summary>
/// <param name="Label">Total name</param>
/// <param name="Value">Value</param>
/// <param name="Kind">Number or Money</param>
public record ReportExportTotal(string Label, decimal Value, ReportExportKind Kind = ReportExportKind.Number);

/// <summary>
/// Format-independent content of an exported report: title, filter summary, tables and totals. Built from the same
/// result view model the HTML report shows, then written to Excel or PDF by ReportExportService.
/// </summary>
public class ReportExportDocument
{
    /// <summary>Report code, e.g. RPT-01 (also the start of the file name).</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Report title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Time the report was produced (institute time zone).</summary>
    public DateTime ProducedAt { get; set; }

    /// <summary>Filters as "Name: value" lines.</summary>
    public List<string> Filters { get; set; } = new();

    /// <summary>Warning written under the filters, e.g. when the rows were capped (null = none).</summary>
    public string? Note { get; set; }

    /// <summary>Tables in display order.</summary>
    public List<ReportExportTable> Tables { get; set; } = new();

    /// <summary>Totals block (empty when the totals are a row of a table).</summary>
    public List<ReportExportTotal> Totals { get; set; } = new();
}

/// <summary>An exported file ready to be returned by a controller.</summary>
/// <param name="Content">File bytes</param>
/// <param name="ContentType">MIME type</param>
/// <param name="FileName">Download name, e.g. RPT-01_2026-11-09.xlsx</param>
public record ReportExportFileVm(byte[] Content, string ContentType, string FileName);
