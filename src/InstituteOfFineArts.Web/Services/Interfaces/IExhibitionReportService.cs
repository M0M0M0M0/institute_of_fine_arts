using InstituteOfFineArts.Web.ViewModels.Reports;

namespace InstituteOfFineArts.Web.Services.Interfaces;

/// <summary>
/// Financial exhibition reports (task BE2-12; F-M10-04, F-M10-05; BA §7 RPT-04, RPT-05): read-only queries.
/// A "From" date after the "To" date throws BusinessException (TC-M10-03).
/// </summary>
public interface IExhibitionReportService
{
    /// <summary>RPT-04: exhibition artworks with asking / sale price, buyer and payment, plus the totals (TC-M10-05).</summary>
    /// <param name="search">Filters</param>
    Task<ExhibitionSalesReportVm> GetSalesReportAsync(ExhibitionSalesSearchVm search);

    /// <summary>RPT-05: sold paintings not paid to the student yet, oldest sale first, with the total owed.</summary>
    /// <param name="search">Filters</param>
    /// <param name="today">Day used for "days unpaid" (null = today in the institute time zone)</param>
    Task<StudentDebtReportVm> GetStudentDebtReportAsync(StudentDebtSearchVm search, DateOnly? today = null);
}
