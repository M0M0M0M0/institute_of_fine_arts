using InstituteOfFineArts.Web.ViewModels.Reports;

namespace InstituteOfFineArts.Web.Services.Interfaces;

/// <summary>
/// Excel / PDF export of the reports RPT-01..07 (task BE2-OPT 1; F-M14-01). The controller runs the same report
/// service as the HTML page, turns its result into a <see cref="ReportExportDocument"/> with one of the Build
/// methods (same rows, totals and filter summary as the page) and writes it with <see cref="Render"/>.
/// No database access: an export can never show other rows than the HTML report.
/// </summary>
public interface IReportExportService
{
    /// <summary>RPT-01 submissions by rating.</summary>
    /// <param name="result">Result shown by /reports/rpt-01</param>
    /// <param name="filter">Filters of the result</param>
    ReportExportDocument BuildRpt01(Rpt01ResultVm result, Rpt01FilterVm filter);

    /// <summary>RPT-02 participation and ratings per competition (+ the BR-12 list when chosen).</summary>
    /// <param name="result">Result shown by /reports/rpt-02</param>
    /// <param name="filter">Filters of the result</param>
    ReportExportDocument BuildRpt02(Rpt02ResultVm result, Rpt02FilterVm filter);

    /// <summary>RPT-03 awards issued.</summary>
    /// <param name="result">Result shown by /reports/rpt-03</param>
    /// <param name="filter">Filters of the result</param>
    /// <param name="competitionLabel">"Code – Title" of the chosen competition (null = all)</param>
    ReportExportDocument BuildRpt03(Rpt03ResultVm result, Rpt03FilterVm filter, string? competitionLabel);

    /// <summary>RPT-04 exhibition paintings and sales.</summary>
    /// <param name="result">Result shown by /reports/rpt-04</param>
    /// <param name="search">Filters of the result</param>
    /// <param name="exhibitionName">Name of the chosen exhibition (null = all)</param>
    ReportExportDocument BuildRpt04(ExhibitionSalesReportVm result, ExhibitionSalesSearchVm search, string? exhibitionName);

    /// <summary>RPT-05 money owed to students.</summary>
    /// <param name="result">Result shown by /reports/rpt-05</param>
    /// <param name="search">Filters of the result</param>
    /// <param name="exhibitionName">Name of the chosen exhibition (null = all)</param>
    ReportExportDocument BuildRpt05(StudentDebtReportVm result, StudentDebtSearchVm search, string? exhibitionName);

    /// <summary>RPT-06 student achievement profile.</summary>
    /// <param name="result">Result shown by /reports/rpt-06</param>
    /// <param name="filter">Filters of the result</param>
    ReportExportDocument BuildRpt06(Rpt06ResultVm result, Rpt06FilterVm filter);

    /// <summary>RPT-07 account statistics.</summary>
    /// <param name="result">Result shown by /reports/rpt-07</param>
    /// <param name="search">Filters of the result</param>
    ReportExportDocument BuildRpt07(AccountStatisticsReportVm result, AccountStatisticsSearchVm search);

    /// <summary>Writes the document as an Excel workbook or a PDF file named like <c>RPT-01_2026-11-09.xlsx</c>.</summary>
    /// <param name="document">Report content</param>
    /// <param name="format">Wanted format</param>
    ReportExportFileVm Render(ReportExportDocument document, ReportExportFormat format);
}
