using System.Globalization;
using ClosedXML.Excel;
using InstituteOfFineArts.Web.Helpers;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.Services.Interfaces;
using InstituteOfFineArts.Web.ViewModels.Reports;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace InstituteOfFineArts.Web.Services.Implementations;

/// <summary>
/// Excel / PDF export of RPT-01..07 (task BE2-OPT 1; F-M14-01). The Build methods copy the columns, totals and
/// filter summary of each HTML report (Views/Reports, Views/FinanceReports) from the result view model the report
/// service returned, so an export always equals the page. Render writes the document with ClosedXML (header row,
/// $#,##0.00 money cells, totals rows, filters on top, frozen header, auto-fit columns) or QuestPDF (A4 landscape,
/// title, filters, tables, totals, page numbers). Numbers and dates use en-US whatever the UI culture is.
/// </summary>
public class ReportExportService : IReportExportService
{
    /// <summary>Excel / PDF number format of USD amounts (BR-47).</summary>
    public const string MoneyFormat = "$#,##0.00";

    /// <summary>Message when the format query value is neither xlsx nor pdf.</summary>
    public const string UnknownFormatMessage = "Choose an export format: Excel (xlsx) or PDF.";

    /// <summary>Start of the error shown on the HTML report when the export was refused (invalid filters).</summary>
    public const string ExportFailedMessage = "The report was not exported: ";

    /// <summary>Fallback message of an invalid filter without its own text.</summary>
    public const string InvalidFiltersMessage = "Invalid filters.";

    /// <summary>MIME type of an Excel workbook.</summary>
    public const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    /// <summary>MIME type of a PDF file.</summary>
    public const string PdfContentType = "application/pdf";

    /// <summary>Widest auto-fitted Excel column (characters), so a long remark does not make a huge column.</summary>
    private const double MaxColumnWidth = 60;

    /// <summary>Culture of every formatted number / date (the UI is English, NFR-11).</summary>
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");

    /// <summary>Logger.</summary>
    private readonly ILogger<ReportExportService> _logger;

    /// <summary>Receives the logger through Dependency Injection.</summary>
    public ReportExportService(ILogger<ReportExportService> logger)
    {
        _logger = logger;
    }

    /// <summary>Reads the <c>format</c> query value (xlsx / pdf, any case).</summary>
    /// <param name="value">Query value</param>
    /// <param name="format">Parsed format</param>
    public static bool TryParseFormat(string? value, out ReportExportFormat format)
    {
        // Only the two documented values; "Xlsx"/"PDF" are accepted too
        switch (value?.Trim().ToLowerInvariant())
        {
            case "xlsx":
                format = ReportExportFormat.Xlsx;
                return true;
            case "pdf":
                format = ReportExportFormat.Pdf;
                return true;
            default:
                format = default;
                return false;
        }
    }

    /// <summary>
    /// URL of the HTML report with the same filters as an export request (the <c>format</c> value removed), used to
    /// send the user back to the page when an export is refused and by the Export buttons in the other direction.
    /// </summary>
    /// <param name="path">Report path, e.g. /reports/rpt-01</param>
    /// <param name="query">Query of the current request</param>
    /// <param name="format">Format to add (null = none, the HTML page)</param>
    public static string HtmlReportUrl(string path, IQueryCollection query, string? format = null)
    {
        // Keep every filter as entered (repeated keys like Ratings too), drop the old format
        var pairs = query.Where(q => !string.Equals(q.Key, "format", StringComparison.OrdinalIgnoreCase)).ToList();
        if (format != null)
        {
            pairs.Add(new KeyValuePair<string, Microsoft.Extensions.Primitives.StringValues>("format", format));
        }
        return path + QueryString.Create(pairs).ToUriComponent();
    }

    // ------------------------------------------------------------------ Build: one method per report

    /// <inheritdoc />
    public ReportExportDocument BuildRpt01(Rpt01ResultVm result, Rpt01FilterVm filter)
    {
        var doc = NewDocument("RPT-01");

        // Same criteria line as the HTML page
        var ratingTexts = Enum.GetValues<Rating>().Select(r => (Value: r.ToString(), Text: r.ToString()))
            .Append((Value: Rpt01FilterVm.NotGraded, Text: "Not graded"))
            .Where(c => filter.Ratings.Contains(c.Value)).Select(c => c.Text).ToList();
        doc.Filters.Add($"Competition: {result.CompetitionCode} – {result.CompetitionTitle}");
        doc.Filters.Add($"Submitted from {DateOr(filter.From, "the start")} to {DateOr(filter.To, "now")}");
        doc.Filters.Add($"Ratings: {(ratingTexts.Count == 0 ? "all" : string.Join(", ", ratingTexts))}");
        doc.Filters.Add($"Class: {(string.IsNullOrWhiteSpace(filter.ClassName) ? "all" : filter.ClassName)}");
        doc.Note = TruncatedNote(result.IsTruncated, result.TotalCount);

        doc.Tables.Add(new ReportExportTable
        {
            Columns =
            {
                new("No.", ReportExportKind.Number), new("Student code"), new("Full name"), new("Class"), new("Painting title"),
                new("Submitted", ReportExportKind.DateTime), new("Last modified", ReportExportKind.DateTime), new("Rating"),
                new("Evaluated by"), new("Evaluated on", ReportExportKind.Date), new("Award"),
            },
            Rows = result.Rows.Select(r => new object?[]
            {
                r.No, r.StudentCode, r.StudentName, r.ClassName, r.Title, r.SubmittedAt, r.UpdatedAt,
                r.Rating?.ToString() ?? "Not graded", r.EvaluatorName, r.EvaluatedAt,
                r.AwardName == null ? "No" : "Yes – " + r.AwardName,
            }).ToList(),
        });

        // Totals of §7: entries and the number per rating
        doc.Totals.Add(new("Total submissions", result.TotalCount));
        doc.Totals.AddRange(Enum.GetValues<Rating>().Select(r => new ReportExportTotal(r.ToString(), result.CountByRating[r])));
        doc.Totals.Add(new("Not graded", result.NotGradedCount));
        return doc;
    }

    /// <inheritdoc />
    public ReportExportDocument BuildRpt02(Rpt02ResultVm result, Rpt02FilterVm filter)
    {
        var doc = NewDocument("RPT-02");
        var ratings = Enum.GetValues<Rating>();
        doc.Filters.Add($"Period {DateOr(filter.From, "any")} to {DateOr(filter.To, "any")}");
        doc.Filters.Add($"Status: {filter.Status?.ToString() ?? "all"}");
        doc.Filters.Add($"Active students today: {result.ActiveStudents}");
        doc.Note = TruncatedNote(result.IsTruncated, result.TotalCount);

        // One row per competition + the totals row of the HTML table footer
        var columns = new List<ReportExportColumn>
        {
            new("Competition"), new("Start – End"), new("Status"), new("Active students", ReportExportKind.Number),
            new("Submissions", ReportExportKind.Number), new("Participation", ReportExportKind.Percent),
        };
        columns.AddRange(ratings.Select(r => new ReportExportColumn(r.ToString(), ReportExportKind.Number)));
        columns.Add(new("Not graded", ReportExportKind.Number));
        columns.Add(new("Not submitted", ReportExportKind.Number));
        doc.Tables.Add(new ReportExportTable
        {
            Columns = columns,
            Rows = result.Rows.Select(r => new object?[]
                {
                    $"{r.Code} – {r.Title}", $"{FormatDate(r.StartDate)} – {FormatDate(r.EndDate)}", r.Status.ToString(),
                    r.ActiveStudents, r.Submissions, r.ParticipationPercent,
                }
                .Concat(ratings.Select(x => (object?)r.CountByRating[x]))
                .Concat(new object?[] { r.NotGraded, r.NotSubmitted }).ToArray()).ToList(),
            TotalsRow = result.TotalCount == 0
                ? null
                : new object?[] { $"Total ({result.TotalCount} competitions)", null, null, null, result.TotalSubmissions, result.TotalParticipationPercent }
                    .Concat(ratings.Select(x => (object?)result.TotalByRating[x]))
                    .Concat(new object?[] { result.TotalNotGraded, null }).ToArray(),
        });

        // BR-12 list of the chosen competition
        if (result.NotSubmitted is { } missing)
        {
            doc.Tables.Add(new ReportExportTable
            {
                Heading = $"Students who have not submitted – {missing.Code} – {missing.Title}",
                Columns =
                {
                    new("No.", ReportExportKind.Number), new("Student code"), new("Full name"), new("Class"), new("Course"), new("Email"),
                },
                Rows = missing.Students.Select(s => new object?[] { s.No, s.StudentCode, s.FullName, s.ClassName, s.CourseName, s.Email }).ToList(),
                TotalsRow = missing.Students.Count == 0 ? null : new object?[] { "Total", $"{missing.Students.Count} student(s)", null, null, null, null },
                EmptyText = "Every active student has submitted.",
            });
        }
        return doc;
    }

    /// <inheritdoc />
    public ReportExportDocument BuildRpt03(Rpt03ResultVm result, Rpt03FilterVm filter, string? competitionLabel)
    {
        var doc = NewDocument("RPT-03");
        doc.Filters.Add($"Competition: {competitionLabel ?? "all"}");
        doc.Filters.Add($"Issued {DateOr(filter.From, "any")} to {DateOr(filter.To, "any")}");
        doc.Filters.Add($"Student: {(string.IsNullOrWhiteSpace(filter.Student) ? "all" : filter.Student)}");
        doc.Note = TruncatedNote(result.IsTruncated, result.TotalCount);

        doc.Tables.Add(new ReportExportTable
        {
            Columns =
            {
                new("No.", ReportExportKind.Number), new("Competition"), new("Award"), new("Rank", ReportExportKind.Number),
                new("Prize value", ReportExportKind.Money), new("Student"), new("Painting"), new("Issued on", ReportExportKind.Date),
                new("Issued by"), new("Remarks"),
            },
            Rows = result.Rows.Select(r => new object?[]
            {
                r.No, $"{r.CompetitionCode} – {r.CompetitionTitle}", r.AwardName, r.Rank, r.PrizeValue,
                $"{r.StudentCode} – {r.StudentName}", r.SubmissionTitle, r.IssuedDate, r.IssuedByName, r.Remarks,
            }).ToList(),
        });

        // Totals of §7: number of awards and total prize value
        doc.Totals.Add(new("Total awards", result.TotalCount));
        doc.Totals.Add(new("Total prize value", result.TotalPrizeValue, ReportExportKind.Money));
        return doc;
    }

    /// <inheritdoc />
    public ReportExportDocument BuildRpt04(ExhibitionSalesReportVm result, ExhibitionSalesSearchVm search, string? exhibitionName)
    {
        var doc = NewDocument("RPT-04");
        doc.Filters.Add($"Exhibition: {exhibitionName ?? "All exhibitions"}");
        doc.Filters.Add($"Exhibition period {DateOr(search.From, "…")} – {DateOr(search.To, "…")}");
        doc.Filters.Add($"Entry status: {search.Status?.ToString() ?? "all"}");
        doc.Filters.Add($"Student: {(string.IsNullOrWhiteSpace(search.Student) ? "all" : search.Student)}");
        if (result.Truncated)
        {
            doc.Note = $"Only the first {ExhibitionReportService.MaxRows} rows are shown; the totals include every matching painting.";
        }

        doc.Tables.Add(new ReportExportTable
        {
            Columns =
            {
                new("#", ReportExportKind.Number), new("Exhibition"), new("Painting"), new("Student"), new("Student code"),
                new("Rating"), new("Asking price", ReportExportKind.Money), new("Status"), new("Sold price", ReportExportKind.Money),
                new("Sold date", ReportExportKind.Date), new("Customer"), new("Paid to student"),
                new("Amount paid", ReportExportKind.Money), new("Paid date", ReportExportKind.Date),
            },
            Rows = result.Rows.Select((r, i) => new object?[]
            {
                i + 1, r.ExhibitionName, r.Title, r.StudentName, r.StudentCode, r.Rating?.ToString() ?? "–", r.AskingPrice,
                r.Status.ToString(), r.SoldPrice, r.SoldDate, r.CustomerName ?? "–",
                r.Status == EntryStatus.SOLD ? (r.PaidToStudent ? "Yes" : "No") : "–", r.PaidAmount, r.PaidDate,
            }).ToList(),
        });

        // Totals of §7 over every matching painting (TC-M10-05); the page shows none when nothing matched
        var t = result.Totals;
        if (t.Entries > 0)
        {
            doc.Totals.AddRange(new ReportExportTotal[]
            {
                new("Matching entries", t.Entries),
                new("Paintings sent (accepted, sold, unsold)", t.Sent),
                new("Sold", t.Sold),
                new("Not sold (on show or unsold)", t.Unsold),
                new("Total asking price (sent paintings)", t.TotalAskingPrice, ReportExportKind.Money),
                new("Total sold price (revenue)", t.TotalSoldPrice, ReportExportKind.Money),
                new("Asking price of the sold paintings", t.SoldAskingPrice, ReportExportKind.Money),
                new("Difference (sold price - asking price of the sold paintings)", t.Difference, ReportExportKind.Money),
                new("Paintings paid to students", t.PaidCount),
                new("Amount paid to students", t.PaidAmount, ReportExportKind.Money),
                new("Paintings not paid to students yet", t.UnpaidCount),
                new("Amount not paid to students yet", t.UnpaidAmount, ReportExportKind.Money),
            });
        }
        return doc;
    }

    /// <inheritdoc />
    public ReportExportDocument BuildRpt05(StudentDebtReportVm result, StudentDebtSearchVm search, string? exhibitionName)
    {
        var doc = NewDocument("RPT-05");
        doc.Filters.Add($"Exhibition: {exhibitionName ?? "All exhibitions"}");
        doc.Filters.Add($"Sold {DateOr(search.From, "…")} – {DateOr(search.To, "…")}");
        doc.Filters.Add($"Student: {(string.IsNullOrWhiteSpace(search.Student) ? "all" : search.Student)}");
        if (result.Truncated)
        {
            doc.Note = $"Only the first {ExhibitionReportService.MaxRows} rows are shown; the total includes every matching painting.";
        }

        // Rows oldest sale first + the "Total unpaid" footer of the page
        doc.Tables.Add(new ReportExportTable
        {
            Columns =
            {
                new("#", ReportExportKind.Number), new("Student"), new("Student code"), new("Painting"), new("Exhibition"),
                new("Sold date", ReportExportKind.Date), new("Sold price", ReportExportKind.Money), new("Days unpaid", ReportExportKind.Number),
            },
            Rows = result.Rows.Select((r, i) => new object?[]
            {
                i + 1, r.StudentName, r.StudentCode, r.Title, r.ExhibitionName, r.SoldDate, r.SoldPrice, r.DaysUnpaid,
            }).ToList(),
            TotalsRow = result.Count == 0
                ? null
                : new object?[] { $"Total unpaid ({result.Count} painting(s))", null, null, null, null, null, result.TotalUnpaid, null },
        });
        return doc;
    }

    /// <inheritdoc />
    public ReportExportDocument BuildRpt06(Rpt06ResultVm result, Rpt06FilterVm filter)
    {
        var doc = NewDocument("RPT-06");
        doc.Filters.Add($"Student: {result.StudentCode} – {result.FullName}");
        doc.Filters.Add($"Class {result.ClassName ?? "–"} · Course {result.CourseName}");
        doc.Filters.Add($"Submitted from {DateOr(filter.From, "the start")} to {DateOr(filter.To, "now")}");
        doc.Note = TruncatedNote(result.IsTruncated, result.TotalCount);

        doc.Tables.Add(new ReportExportTable
        {
            Columns =
            {
                new("No.", ReportExportKind.Number), new("Competition"), new("Painting"), new("Submitted", ReportExportKind.DateTime),
                new("Rating"), new("Comment summary"), new("Award"), new("Prize value", ReportExportKind.Money), new("Exhibition / sold"),
            },
            Rows = result.Rows.Select(r => new object?[]
            {
                r.No, $"{r.CompetitionCode} – {r.CompetitionTitle}", r.Title, r.SubmittedAt, r.Rating?.ToString() ?? "Not graded",
                r.CommentSummary, r.AwardName, r.PrizeValue,
                // One "exhibition – status (sold $x)" item per exhibition entry, as on the page
                string.Join("; ", r.Exhibitions.Select(e => $"{e.ExhibitionName} – {e.Status}"
                    + (e.Status == EntryStatus.SOLD && e.SoldPrice is { } price ? $" (sold {FormatMoney(price)})" : string.Empty))),
            }).ToList(),
        });

        // Totals of §7
        doc.Totals.AddRange(new ReportExportTotal[]
        {
            new("Entries", result.TotalCount),
            new("Competitions joined", result.CompetitionsJoined),
            new("BEST ratings", result.BestCount),
            new("Awards", result.AwardCount),
            new("Total prize value", result.TotalPrizeValue, ReportExportKind.Money),
            new("Total sales of paintings", result.TotalSales, ReportExportKind.Money),
        });
        return doc;
    }

    /// <inheritdoc />
    public ReportExportDocument BuildRpt07(AccountStatisticsReportVm result, AccountStatisticsSearchVm search)
    {
        var doc = NewDocument("RPT-07");
        doc.Filters.Add($"Role: {search.Role?.ToString() ?? "all"}");
        doc.Filters.Add($"Account status: {search.Active switch { true => "active", false => "inactive", _ => "all" }}");
        doc.Filters.Add($"Class / course: {(string.IsNullOrWhiteSpace(search.ClassOrCourse) ? "all" : search.ClassOrCourse)}");

        // Accounts per role + totals row
        doc.Tables.Add(new ReportExportTable
        {
            Heading = "Accounts per role",
            Columns =
            {
                new("Role"), new("Active", ReportExportKind.Number), new("Inactive", ReportExportKind.Number), new("Total", ReportExportKind.Number),
            },
            Rows = result.TotalAccounts == 0
                ? new List<object?[]>()
                : result.Roles.Select(r => new object?[] { r.Role.ToString(), r.Active, r.Inactive, r.Total }).ToList(),
            TotalsRow = result.TotalAccounts == 0 ? null : new object?[] { "Total", result.TotalActive, result.TotalInactive, result.TotalAccounts },
        });

        // Students per course / class: only when students are part of the selection (as on the page)
        if (result.ShowStudents)
        {
            var statuses = Enum.GetValues<EnrollmentStatus>();
            var columns = new List<ReportExportColumn> { new("Course"), new("Class") };
            columns.AddRange(statuses.Select(s => new ReportExportColumn(s.ToString(), ReportExportKind.Number)));
            columns.Add(new("Total", ReportExportKind.Number));
            var totals = result.StudentsByStatus;
            doc.Tables.Add(new ReportExportTable
            {
                Heading = "Students per course and class",
                Columns = columns,
                Rows = result.Classes.Select(c => new object?[] { c.CourseName, c.ClassName ?? "–" }
                    .Concat(statuses.Select(s => (object?)c.ByStatus[s]))
                    .Append(c.Total).ToArray()).ToList(),
                TotalsRow = result.Classes.Count == 0
                    ? null
                    : new object?[] { "Total", null }.Concat(statuses.Select(s => (object?)totals[s])).Append(result.TotalStudents).ToArray(),
            });
        }
        return doc;
    }

    // ------------------------------------------------------------------ Render

    /// <inheritdoc />
    public ReportExportFileVm Render(ReportExportDocument document, ReportExportFormat format)
    {
        // File name: report code + day of production, e.g. RPT-01_2026-11-09.xlsx
        var day = document.ProducedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var file = format == ReportExportFormat.Pdf
            ? new ReportExportFileVm(RenderPdf(document), PdfContentType, $"{document.Code}_{day}.pdf")
            : new ReportExportFileVm(RenderXlsx(document), XlsxContentType, $"{document.Code}_{day}.xlsx");
        _logger.LogInformation("Report {Code} exported as {Format} ({Bytes} bytes, {Rows} rows)",
            document.Code, format, file.Content.Length, document.Tables.Sum(t => t.Rows.Count));
        return file;
    }

    /// <summary>Excel workbook: title + filters on top, then each table, then the totals block.</summary>
    /// <param name="doc">Report content</param>
    private static byte[] RenderXlsx(ReportExportDocument doc)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(doc.Code);
        var row = 1;

        // Title, production time and the filter summary
        sheet.Cell(row, 1).Value = $"{doc.Code} – {doc.Title}";
        sheet.Cell(row++, 1).Style.Font.SetBold().Font.SetFontSize(14);
        sheet.Cell(row++, 1).Value = "Produced " + doc.ProducedAt.ToString("dd MMM yyyy HH:mm", English);
        foreach (var filter in doc.Filters)
        {
            sheet.Cell(row++, 1).Value = filter;
        }
        if (doc.Note != null)
        {
            sheet.Cell(row, 1).Value = doc.Note;
            sheet.Cell(row++, 1).Style.Font.SetItalic().Font.SetFontColor(XLColor.DarkRed);
        }
        row++;

        var firstDataRow = row;
        int? headerRow = null;
        var maxColumns = 2;
        foreach (var table in doc.Tables)
        {
            maxColumns = Math.Max(maxColumns, table.Columns.Count);
            if (table.Heading != null)
            {
                sheet.Cell(row, 1).Value = table.Heading;
                sheet.Cell(row++, 1).Style.Font.SetBold().Font.SetFontSize(12);
            }

            // Header row (the first one is frozen so it stays visible while scrolling)
            for (var c = 0; c < table.Columns.Count; c++)
            {
                sheet.Cell(row, c + 1).Value = table.Columns[c].Header;
            }
            var header = sheet.Range(row, 1, row, table.Columns.Count);
            header.Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.LightGray).Border.SetBottomBorder(XLBorderStyleValues.Thin);
            headerRow ??= row;
            row++;

            if (table.Rows.Count == 0)
            {
                sheet.Cell(row++, 1).Value = table.EmptyText;
            }
            foreach (var values in table.Rows)
            {
                WriteRow(sheet, row++, table.Columns, values);
            }

            // Totals row in bold under a line
            if (table.TotalsRow != null)
            {
                WriteRow(sheet, row, table.Columns, table.TotalsRow);
                var totals = sheet.Range(row, 1, row, table.Columns.Count);
                totals.Style.Font.SetBold().Border.SetTopBorder(XLBorderStyleValues.Thin);
                row++;
            }
            row++;
        }

        // Totals block: label + value
        if (doc.Totals.Count > 0)
        {
            sheet.Cell(row++, 1).Value = "Totals";
            sheet.Cell(row - 1, 1).Style.Font.SetBold().Font.SetFontSize(12);
            foreach (var total in doc.Totals)
            {
                sheet.Cell(row, 1).Value = total.Label;
                sheet.Cell(row, 1).Style.Font.SetBold();
                SetCell(sheet.Cell(row, 2), total.Value, total.Kind);
                row++;
            }
        }

        // Frozen header and auto-fit columns (measured on the tables only, not on the long title / filter lines)
        if (headerRow is { } frozen)
        {
            sheet.SheetView.FreezeRows(frozen);
        }
        sheet.Columns(1, maxColumns).AdjustToContents(firstDataRow, Math.Max(firstDataRow, row));
        foreach (var column in sheet.Columns(1, maxColumns))
        {
            column.Width = Math.Min(column.Width, MaxColumnWidth);
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    /// <summary>Writes one table row; each value is formatted by its column kind.</summary>
    /// <param name="sheet">Worksheet</param>
    /// <param name="row">Row number</param>
    /// <param name="columns">Columns of the table</param>
    /// <param name="values">Values of the row</param>
    private static void WriteRow(IXLWorksheet sheet, int row, IReadOnlyList<ReportExportColumn> columns, object?[] values)
    {
        for (var c = 0; c < columns.Count && c < values.Length; c++)
        {
            SetCell(sheet.Cell(row, c + 1), values[c], columns[c].Kind);
        }
    }

    /// <summary>Sets a typed cell value (numbers and dates stay numbers / dates so Excel can sum and sort them).</summary>
    /// <param name="cell">Cell</param>
    /// <param name="value">Value (null = empty cell)</param>
    /// <param name="kind">Column kind</param>
    private static void SetCell(IXLCell cell, object? value, ReportExportKind kind)
    {
        switch (value)
        {
            case null:
                return;
            case string text:
                cell.Value = text;
                return;
            case DateOnly date:
                cell.Value = date.ToDateTime(TimeOnly.MinValue);
                cell.Style.NumberFormat.Format = "dd MMM yyyy";
                return;
            case DateTime time:
                cell.Value = time;
                cell.Style.NumberFormat.Format = kind == ReportExportKind.Date ? "dd MMM yyyy" : "dd MMM yyyy HH:mm";
                return;
        }

        // Numbers: money and percent get their display format, counts stay plain
        cell.Value = Convert.ToDouble(value, CultureInfo.InvariantCulture);
        cell.Style.NumberFormat.Format = kind switch
        {
            ReportExportKind.Money => MoneyFormat,
            ReportExportKind.Percent => "0.0\"%\"",
            _ => "0",
        };
    }

    /// <summary>PDF document: A4 landscape, title on every page, filters, tables, totals and "Page x of y".</summary>
    /// <param name="doc">Report content</param>
    private static byte[] RenderPdf(ReportExportDocument doc)
    {
        return Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape());
            page.Margin(1, Unit.Centimetre);
            page.DefaultTextStyle(style => style.FontSize(8));

            // Title on every page
            page.Header().PaddingBottom(4).Column(header =>
            {
                header.Item().Text($"{doc.Code} – {doc.Title}").FontSize(14).Bold();
                header.Item().Text("Produced " + doc.ProducedAt.ToString("dd MMM yyyy HH:mm", English)).FontColor(Colors.Grey.Darken1);
            });

            page.Content().Column(content =>
            {
                content.Spacing(6);

                // Filter summary (and the row cap warning)
                content.Item().Column(filters =>
                {
                    foreach (var filter in doc.Filters)
                    {
                        filters.Item().Text(filter);
                    }
                    if (doc.Note != null)
                    {
                        filters.Item().Text(doc.Note).Italic().FontColor(Colors.Red.Darken2);
                    }
                });

                foreach (var table in doc.Tables)
                {
                    if (table.Heading != null)
                    {
                        content.Item().PaddingTop(4).Text(table.Heading).FontSize(11).Bold();
                    }
                    if (table.Rows.Count == 0)
                    {
                        content.Item().Text(table.EmptyText).FontColor(Colors.Grey.Darken1);
                        continue;
                    }
                    content.Item().Element(e => PdfTable(e, table));
                }

                // Totals block: two columns label / value, kept on one page (at most a dozen lines)
                if (doc.Totals.Count > 0)
                {
                    content.Item().ShowEntire().Column(block =>
                    {
                        block.Item().PaddingTop(4).Text("Totals").FontSize(11).Bold();
                        block.Item().Width(330).Table(totals =>
                        {
                            totals.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(3);
                                c.RelativeColumn(1);
                            });
                            foreach (var total in doc.Totals)
                            {
                                totals.Cell().Element(BodyCell).Text(total.Label).SemiBold();
                                totals.Cell().Element(BodyCell).AlignRight().Text(FormatValue(total.Value, total.Kind));
                            }
                        });
                    });
                }
            });

            // Page numbers
            page.Footer().AlignCenter().Text(text =>
            {
                text.Span("Page ");
                text.CurrentPageNumber();
                text.Span(" of ");
                text.TotalPages();
            });
        })).GeneratePdf();
    }

    /// <summary>One report table in the PDF: header repeated on each page, rows, bold totals row.</summary>
    /// <param name="container">Where to draw</param>
    /// <param name="table">Table content</param>
    private static void PdfTable(IContainer container, ReportExportTable table)
    {
        container.Table(t =>
        {
            t.ColumnsDefinition(c =>
            {
                foreach (var column in table.Columns)
                {
                    // Narrow column for row numbers, the others share the width
                    if (column.Header is "No." or "#")
                    {
                        c.ConstantColumn(28);
                    }
                    else
                    {
                        c.RelativeColumn();
                    }
                }
            });

            t.Header(header =>
            {
                foreach (var column in table.Columns)
                {
                    header.Cell().Element(HeaderCell).Text(column.Header).Bold();
                }
            });

            foreach (var values in table.Rows)
            {
                PdfRow(t, table.Columns, values, bold: false);
            }
            if (table.TotalsRow != null)
            {
                PdfRow(t, table.Columns, table.TotalsRow, bold: true);
            }
        });
    }

    /// <summary>Writes one row of PDF cells; numbers are right-aligned.</summary>
    /// <param name="table">Table descriptor</param>
    /// <param name="columns">Columns</param>
    /// <param name="values">Values</param>
    /// <param name="bold">Totals row</param>
    private static void PdfRow(TableDescriptor table, IReadOnlyList<ReportExportColumn> columns, object?[] values, bool bold)
    {
        for (var c = 0; c < columns.Count; c++)
        {
            var value = c < values.Length ? values[c] : null;
            var cell = table.Cell().Element(BodyCell);
            if (value is not string && columns[c].Kind is ReportExportKind.Number or ReportExportKind.Money or ReportExportKind.Percent)
            {
                cell = cell.AlignRight();
            }
            var text = cell.Text(FormatValue(value, columns[c].Kind));
            if (bold)
            {
                text.Bold();
            }
        }
    }

    /// <summary>Style of a PDF header cell.</summary>
    /// <param name="container">Cell</param>
    private static IContainer HeaderCell(IContainer container)
        => container.Background(Colors.Grey.Lighten3).BorderBottom(1).BorderColor(Colors.Grey.Darken1).Padding(3);

    /// <summary>Style of a PDF body cell.</summary>
    /// <param name="container">Cell</param>
    private static IContainer BodyCell(IContainer container)
        => container.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3);

    // ------------------------------------------------------------------ Helpers

    /// <summary>New document with the code, catalog title and production time.</summary>
    /// <param name="code">Report code</param>
    private static ReportExportDocument NewDocument(string code)
        => new()
        {
            Code = code,
            Title = ReportCatalog.All.First(r => r.Code == code).Title,
            ProducedAt = DateTimeHelper.Now,
        };

    /// <summary>Warning of a capped report (RPT-01/02/03/06), null when every row is shown.</summary>
    /// <param name="truncated">More rows matched than shown</param>
    /// <param name="totalCount">Matching rows</param>
    private static string? TruncatedNote(bool truncated, int totalCount)
        => truncated
            ? $"The result has {totalCount.ToString("#,##0", English)} rows; only the first {CompetitionReportService.MaxRows.ToString("#,##0", English)} are shown."
            : null;

    /// <summary>Formats a value as text for the PDF (same formats as the HTML report).</summary>
    /// <param name="value">Value</param>
    /// <param name="kind">Column kind</param>
    public static string FormatValue(object? value, ReportExportKind kind)
        => value switch
        {
            null => string.Empty,
            string text => text,
            DateOnly date => FormatDate(date),
            DateTime time => time.ToString(kind == ReportExportKind.Date ? "dd MMM yyyy" : "dd MMM yyyy HH:mm", English),
            _ => kind switch
            {
                ReportExportKind.Money => FormatMoney(Convert.ToDecimal(value, CultureInfo.InvariantCulture)),
                ReportExportKind.Percent => Convert.ToDecimal(value, CultureInfo.InvariantCulture).ToString("0.0", English) + "%",
                _ => Convert.ToString(value, English) ?? string.Empty,
            },
        };

    /// <summary>USD amount as $#,##0.00.</summary>
    /// <param name="amount">Amount</param>
    private static string FormatMoney(decimal amount) => amount.ToString(MoneyFormat, English);

    /// <summary>Date as dd MMM yyyy.</summary>
    /// <param name="date">Date</param>
    private static string FormatDate(DateOnly date) => date.ToString("dd MMM yyyy", English);

    /// <summary>Date as dd MMM yyyy, or the given text when empty.</summary>
    /// <param name="date">Date</param>
    /// <param name="empty">Text when no date</param>
    private static string DateOr(DateOnly? date, string empty) => date is { } d ? FormatDate(d) : empty;
}
