using System.Text;
using ClosedXML.Excel;
using InstituteOfFineArts.Web.Helpers;
using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.Services.Implementations;
using InstituteOfFineArts.Web.ViewModels.Reports;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;

namespace InstituteOfFineArts.Tests.Services;

/// <summary>
/// ReportExportService (task BE2-OPT 1; F-M14-01): every report RPT-01..07 exported to Excel opens with ClosedXML and
/// has the same number of rows and the same totals as the report service result; money cells use $#,##0.00, the
/// header row is frozen; the PDF is a non-empty file starting with %PDF; file names RPT-0N_yyyy-MM-dd; format parsing
/// and the "back to the page" URL keep the filters.
/// </summary>
public class ReportExportServiceTests : IDisposable
{
    /// <summary>Shared test data (staff, two students, two competitions, exhibitions in every status, one customer).</summary>
    private readonly ExhibitionEntryTestData _data = new();

    /// <summary>QuestPDF needs a license choice before the first document (Program.cs does it in the app).</summary>
    public ReportExportServiceTests()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
    }

    /// <summary>Service under test.</summary>
    private static ReportExportService Export => new(NullLogger<ReportExportService>.Instance);

    /// <summary>Disposes the test database.</summary>
    public void Dispose() => _data.Dispose();

    /// <summary>
    /// Graded competition: BEST (student01, sold paid 250, award First Prize $300), BETTER (student01, sold unpaid 250),
    /// GOOD (student02, unsold), not graded (student02, rejected); closed competition: BEST (student01, on show).
    /// </summary>
    private void Seed()
    {
        var best = _data.AddPainting(_data.Student1, Rating.BEST, title: "Paid");
        var better = _data.AddPainting(_data.Student1, Rating.BETTER, title: "Owed");
        var good = _data.AddPainting(_data.Student2, Rating.GOOD, title: "Left");
        var none = _data.AddPainting(_data.Student2, null, title: "Refused");
        var onShow = _data.AddPainting(_data.Student1, Rating.BEST, _data.Closed, "On show");
        _data.AddEntry(best, _data.Finished, EntryStatus.SOLD, 200m, paid: true);
        _data.AddEntry(better, _data.Finished, EntryStatus.SOLD, 300m);
        _data.AddEntry(good, _data.Finished, EntryStatus.UNSOLD, 100m);
        _data.AddEntry(none, _data.Ongoing, EntryStatus.REJECTED, 90m);
        _data.AddEntry(onShow, _data.Ongoing, EntryStatus.ACCEPTED, 150m);

        // One issued award of the graded competition
        var award = new CompetitionAward { CompetitionId = _data.Graded.Id, AwardName = "First Prize", Rank = 1, PrizeValue = 300m, Quantity = 1 };
        _data.Db.CompetitionAwards.Add(award);
        _data.Db.SaveChanges();
        _data.Db.AwardsIssued.Add(new AwardIssued
        {
            CompetitionAwardId = award.Id, SubmissionId = best.Id, IssuedBy = _data.StaffId, IssuedDate = _data.Graded.EndDate.AddDays(5),
        });
        _data.Db.SaveChanges();
        _data.Db.ChangeTracker.Clear();
    }

    // ------------------------------------------------------------------ Helpers

    /// <summary>Opens the exported workbook (fails the test when ClosedXML cannot read it).</summary>
    private static IXLWorksheet OpenSheet(ReportExportFileVm file)
    {
        Assert.Equal(ReportExportService.XlsxContentType, file.ContentType);
        var workbook = new XLWorkbook(new MemoryStream(file.Content));
        return workbook.Worksheet(1);
    }

    /// <summary>Row whose first cell is <paramref name="text"/> (the first one after <paramref name="after"/>).</summary>
    private static int FindRow(IXLWorksheet sheet, string text, int after = 0)
    {
        var last = sheet.LastRowUsed()!.RowNumber();
        for (var r = after + 1; r <= last; r++)
        {
            if (sheet.Cell(r, 1).GetString() == text)
            {
                return r;
            }
        }
        throw new Xunit.Sdk.XunitException($"Row '{text}' not found");
    }

    /// <summary>Number of data rows under the header row (until the first empty first cell or a "Total…" row).</summary>
    private static int DataRows(IXLWorksheet sheet, int headerRow)
    {
        var count = 0;
        for (var r = headerRow + 1; !sheet.Cell(r, 1).IsEmpty() && !sheet.Cell(r, 1).GetString().StartsWith("Total"); r++)
        {
            count++;
        }
        return count;
    }

    /// <summary>Value of a line of the totals block (label in column A, value in column B).</summary>
    private static decimal Total(IXLWorksheet sheet, string label)
        => (decimal)sheet.Cell(FindRow(sheet, label, FindRow(sheet, "Totals")), 2).GetDouble();

    /// <summary>Asserts a non-empty PDF file.</summary>
    private static void AssertPdf(ReportExportFileVm file)
    {
        Assert.Equal(ReportExportService.PdfContentType, file.ContentType);
        Assert.True(file.Content.Length > 500);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(file.Content, 0, 4));
    }

    // ------------------------------------------------------------------ Excel per report

    /// <summary>RPT-01: one sheet row per result row, totals per rating equal the service, frozen header, file name.</summary>
    [Fact]
    public async Task Rpt01_Xlsx_RowsAndTotalsEqualService()
    {
        Seed();
        var filter = new Rpt01FilterVm { CompetitionId = _data.Graded.Id };
        var result = await new CompetitionReportService(_data.Db).GetSubmissionsByRatingAsync(filter);

        var file = Export.Render(Export.BuildRpt01(result, filter), ReportExportFormat.Xlsx);

        var sheet = OpenSheet(file);
        var header = FindRow(sheet, "No.");
        Assert.Equal(4, result.TotalCount);
        Assert.Equal(result.Rows.Count, DataRows(sheet, header));
        Assert.Equal(result.TotalCount, Total(sheet, "Total submissions"));
        Assert.Equal(result.CountByRating[Rating.BEST], Total(sheet, "BEST"));
        Assert.Equal(result.NotGradedCount, Total(sheet, "Not graded"));
        Assert.Equal(header, sheet.SheetView.SplitRow);
        Assert.Contains($"Competition: C1 – Hometown Landscapes", sheet.Cell(3, 1).GetString());
        Assert.Equal($"RPT-01_{DateTimeHelper.Today:yyyy-MM-dd}.xlsx", file.FileName);
    }

    /// <summary>RPT-02: competition rows, the totals row and the BR-12 list equal the service.</summary>
    [Fact]
    public async Task Rpt02_Xlsx_TotalsRowAndNotSubmittedList()
    {
        Seed();
        var filter = new Rpt02FilterVm { NotSubmittedCompetitionId = _data.Closed.Id };
        var result = await new CompetitionReportService(_data.Db).GetParticipationAsync(filter);

        var sheet = OpenSheet(Export.Render(Export.BuildRpt02(result, filter), ReportExportFormat.Xlsx));

        var header = FindRow(sheet, "Competition");
        Assert.Equal(result.Rows.Count, DataRows(sheet, header));
        var totals = header + result.Rows.Count + 1;
        Assert.StartsWith("Total (", sheet.Cell(totals, 1).GetString());
        Assert.Equal(result.TotalSubmissions, sheet.Cell(totals, 5).GetDouble());
        Assert.Equal((double)result.TotalParticipationPercent, sheet.Cell(totals, 6).GetDouble(), 6);

        // BR-12: student02 has not submitted to the closed competition
        var listHeader = FindRow(sheet, "No.", totals);
        Assert.Equal(result.NotSubmitted!.Students.Count, DataRows(sheet, listHeader));
        Assert.Equal("HV0002", sheet.Cell(listHeader + 1, 2).GetString());
    }

    /// <summary>RPT-03: award rows, prize cell formatted $#,##0.00, total prize value equals the service.</summary>
    [Fact]
    public async Task Rpt03_Xlsx_MoneyFormatAndTotal()
    {
        Seed();
        var filter = new Rpt03FilterVm();
        var result = await new CompetitionReportService(_data.Db).GetAwardsIssuedAsync(filter);

        var sheet = OpenSheet(Export.Render(Export.BuildRpt03(result, filter, null), ReportExportFormat.Xlsx));

        var header = FindRow(sheet, "No.");
        Assert.Equal(1, DataRows(sheet, header));
        var prize = sheet.Cell(header + 1, 5);
        Assert.Equal(300d, prize.GetDouble());
        Assert.Equal(ReportExportService.MoneyFormat, prize.Style.NumberFormat.Format);
        Assert.Equal(result.TotalPrizeValue, Total(sheet, "Total prize value"));
        Assert.Equal(result.TotalCount, Total(sheet, "Total awards"));
        Assert.Contains("Competition: all", sheet.Cell(3, 1).GetString());
    }

    /// <summary>RPT-04 (TC-M10-05): same rows and every money total as the service.</summary>
    [Fact]
    public async Task Rpt04_Xlsx_RowsAndTotalsEqualService()
    {
        Seed();
        var search = new ExhibitionSalesSearchVm();
        var report = await new ExhibitionReportService(_data.Db).GetSalesReportAsync(search);

        var sheet = OpenSheet(Export.Render(Export.BuildRpt04(report, search, null), ReportExportFormat.Xlsx));

        var header = FindRow(sheet, "#");
        Assert.Equal(5, report.Rows.Count);
        Assert.Equal(report.Rows.Count, DataRows(sheet, header));
        var t = report.Totals;
        Assert.Equal(t.Entries, Total(sheet, "Matching entries"));
        Assert.Equal(t.TotalAskingPrice, Total(sheet, "Total asking price (sent paintings)"));
        Assert.Equal(t.TotalSoldPrice, Total(sheet, "Total sold price (revenue)"));
        Assert.Equal(t.PaidAmount, Total(sheet, "Amount paid to students"));
        Assert.Equal(t.UnpaidAmount, Total(sheet, "Amount not paid to students yet"));
        Assert.Equal(ReportExportService.MoneyFormat, sheet.Cell(header + 1, 7).Style.NumberFormat.Format);
    }

    /// <summary>RPT-05: unpaid rows and the totals row equal the service.</summary>
    [Fact]
    public async Task Rpt05_Xlsx_TotalUnpaid()
    {
        Seed();
        var search = new StudentDebtSearchVm();
        var report = await new ExhibitionReportService(_data.Db).GetStudentDebtReportAsync(search);

        var sheet = OpenSheet(Export.Render(Export.BuildRpt05(report, search, null), ReportExportFormat.Xlsx));

        var header = FindRow(sheet, "#");
        Assert.Equal(report.Count, DataRows(sheet, header));
        var totals = header + report.Rows.Count + 1;
        Assert.Equal($"Total unpaid ({report.Count} painting(s))", sheet.Cell(totals, 1).GetString());
        Assert.Equal((double)report.TotalUnpaid, sheet.Cell(totals, 7).GetDouble());
        Assert.Equal(250d, sheet.Cell(totals, 7).GetDouble());
    }

    /// <summary>RPT-06: one student's rows and achievement totals equal the service.</summary>
    [Fact]
    public async Task Rpt06_Xlsx_TotalsEqualService()
    {
        Seed();
        var filter = new Rpt06FilterVm { StudentId = _data.Student1 };
        var result = await new CompetitionReportService(_data.Db).GetStudentProfileAsync(filter);

        var sheet = OpenSheet(Export.Render(Export.BuildRpt06(result, filter), ReportExportFormat.Xlsx));

        Assert.Equal(result.Rows.Count, DataRows(sheet, FindRow(sheet, "No.")));
        Assert.Equal(result.TotalCount, Total(sheet, "Entries"));
        Assert.Equal(result.BestCount, Total(sheet, "BEST ratings"));
        Assert.Equal(result.TotalPrizeValue, Total(sheet, "Total prize value"));
        Assert.Equal(result.TotalSales, Total(sheet, "Total sales of paintings"));
    }

    /// <summary>RPT-07: accounts per role and students per class, with the totals rows of the page.</summary>
    [Fact]
    public async Task Rpt07_Xlsx_BothTables()
    {
        var search = new AccountStatisticsSearchVm();
        var result = await new AccountReportService(_data.Db).GetAccountStatisticsAsync(search);

        var sheet = OpenSheet(Export.Render(Export.BuildRpt07(result, search), ReportExportFormat.Xlsx));

        var roles = FindRow(sheet, "Role");
        Assert.Equal(result.Roles.Count, DataRows(sheet, roles));
        var rolesTotal = FindRow(sheet, "Total", roles);
        Assert.Equal(result.TotalAccounts, sheet.Cell(rolesTotal, 4).GetDouble());
        var classes = FindRow(sheet, "Course");
        var classesTotal = FindRow(sheet, "Total", classes);
        Assert.Equal(result.TotalStudents, sheet.Cell(classesTotal, 2 + Enum.GetValues<EnrollmentStatus>().Length + 1).GetDouble());
    }

    /// <summary>A report without rows still exports: "No data" instead of rows.</summary>
    [Fact]
    public async Task Rpt04_Empty_NoData()
    {
        var search = new ExhibitionSalesSearchVm();
        var report = await new ExhibitionReportService(_data.Db).GetSalesReportAsync(search);
        var doc = Export.BuildRpt04(report, search, null);

        var sheet = OpenSheet(Export.Render(doc, ReportExportFormat.Xlsx));
        AssertPdf(Export.Render(doc, ReportExportFormat.Pdf));

        Assert.Equal("No data", sheet.Cell(FindRow(sheet, "#") + 1, 1).GetString());
        Assert.Empty(doc.Totals);
    }

    // ------------------------------------------------------------------ PDF

    /// <summary>Every report exports to a non-empty PDF starting with %PDF, named RPT-0N_yyyy-MM-dd.pdf.</summary>
    [Fact]
    public async Task AllReports_Pdf_Valid()
    {
        Seed();
        var competitions = new CompetitionReportService(_data.Db);
        var exhibitions = new ExhibitionReportService(_data.Db);
        var docs = new[]
        {
            Export.BuildRpt01(await competitions.GetSubmissionsByRatingAsync(new Rpt01FilterVm { CompetitionId = _data.Graded.Id }),
                new Rpt01FilterVm { CompetitionId = _data.Graded.Id }),
            Export.BuildRpt02(await competitions.GetParticipationAsync(new Rpt02FilterVm { NotSubmittedCompetitionId = _data.Graded.Id }),
                new Rpt02FilterVm { NotSubmittedCompetitionId = _data.Graded.Id }),
            Export.BuildRpt03(await competitions.GetAwardsIssuedAsync(new Rpt03FilterVm()), new Rpt03FilterVm(), "C1 – Hometown Landscapes"),
            Export.BuildRpt04(await exhibitions.GetSalesReportAsync(new ExhibitionSalesSearchVm()), new ExhibitionSalesSearchVm(), null),
            Export.BuildRpt05(await exhibitions.GetStudentDebtReportAsync(new StudentDebtSearchVm()), new StudentDebtSearchVm(), null),
            Export.BuildRpt06(await competitions.GetStudentProfileAsync(new Rpt06FilterVm { StudentId = _data.Student1 }),
                new Rpt06FilterVm { StudentId = _data.Student1 }),
            Export.BuildRpt07(await new AccountReportService(_data.Db).GetAccountStatisticsAsync(new AccountStatisticsSearchVm()),
                new AccountStatisticsSearchVm()),
        };

        for (var i = 0; i < docs.Length; i++)
        {
            var file = Export.Render(docs[i], ReportExportFormat.Pdf);
            AssertPdf(file);
            Assert.Equal($"RPT-0{i + 1}_{DateTimeHelper.Today:yyyy-MM-dd}.pdf", file.FileName);
        }
    }

    /// <summary>Vietnamese text entered by users (NFR-11) does not break the PDF.</summary>
    [Fact]
    public void Pdf_VietnameseText_Valid()
    {
        var doc = new ReportExportDocument
        {
            Code = "RPT-03", Title = "Awards issued", ProducedAt = DateTimeHelper.Now, Filters = { "Student: Nguyễn Thị Hồng Nhung" },
            Tables =
            {
                new ReportExportTable
                {
                    Columns = { new("Painting"), new("Prize value", ReportExportKind.Money) },
                    Rows = { new object?[] { "Phố cổ Hội An – mùa thu", 1234.5m } },
                },
            },
        };

        AssertPdf(Export.Render(doc, ReportExportFormat.Pdf));
    }

    // ------------------------------------------------------------------ Format and URLs

    /// <summary>Only xlsx and pdf (any case) are export formats.</summary>
    [Theory]
    [InlineData("xlsx", true, ReportExportFormat.Xlsx)]
    [InlineData("PDF", true, ReportExportFormat.Pdf)]
    [InlineData("csv", false, ReportExportFormat.Xlsx)]
    [InlineData(null, false, ReportExportFormat.Xlsx)]
    public void TryParseFormat_Cases(string? value, bool ok, ReportExportFormat expected)
    {
        Assert.Equal(ok, ReportExportService.TryParseFormat(value, out var format));
        if (ok)
        {
            Assert.Equal(expected, format);
        }
    }

    /// <summary>The export / back-to-page URL keeps every filter (repeated Ratings too) and replaces the format.</summary>
    [Fact]
    public void HtmlReportUrl_KeepsFilters()
    {
        var query = new QueryCollection(new Dictionary<string, StringValues>
        {
            ["CompetitionId"] = "5", ["Ratings"] = new[] { "BEST", "GOOD" }, ["format"] = "pdf",
        });

        Assert.Equal("/reports/rpt-01?CompetitionId=5&Ratings=BEST&Ratings=GOOD", ReportExportService.HtmlReportUrl("/reports/rpt-01", query));
        Assert.Equal("/reports/rpt-01/export?CompetitionId=5&Ratings=BEST&Ratings=GOOD&format=xlsx",
            ReportExportService.HtmlReportUrl("/reports/rpt-01/export", query, "xlsx"));
    }

    /// <summary>Money values are written with the en-US $#,##0.00 format in the PDF text.</summary>
    [Fact]
    public void FormatValue_MoneyPercentDate()
    {
        Assert.Equal("$1,234.50", ReportExportService.FormatValue(1234.5m, ReportExportKind.Money));
        Assert.Equal("33.3%", ReportExportService.FormatValue(33.333m, ReportExportKind.Percent));
        Assert.Equal("09 Nov 2026", ReportExportService.FormatValue(new DateOnly(2026, 11, 9), ReportExportKind.Date));
        Assert.Equal(string.Empty, ReportExportService.FormatValue(null, ReportExportKind.Money));
    }
}
