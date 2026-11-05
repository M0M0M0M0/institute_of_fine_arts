using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.Services.Implementations;
using InstituteOfFineArts.Web.ViewModels.Reports;

namespace InstituteOfFineArts.Tests.Services;

/// <summary>
/// ExhibitionReportService (task BE2-12; F-M10-04, F-M10-05; BA §7 RPT-04, RPT-05): sales rows and totals
/// (sent / sold / unsold, asking vs sold price, paid vs unpaid – TC-M10-05), filters, "No data" (TC-M10-04),
/// From &gt; To refused (TC-M10-03), money owed to students with days unpaid.
/// </summary>
public class ExhibitionReportServiceTests : IDisposable
{
    /// <summary>Shared test data (staff, two students, exhibitions in every status, one customer).</summary>
    private readonly ExhibitionEntryTestData _data = new();

    /// <summary>Service under test.</summary>
    private ExhibitionReportService Service => new(_data.Db);

    /// <summary>Disposes the test database.</summary>
    public void Dispose() => _data.Dispose();

    /// <summary>
    /// Finished exhibition: SOLD paid (ask 200, sold 250), SOLD unpaid (ask 300, sold 250), UNSOLD (ask 100);
    /// ongoing exhibition: ACCEPTED (ask 150) and REJECTED (ask 90, student02).
    /// </summary>
    private void Seed()
    {
        _data.AddEntry(_data.AddPainting(_data.Student1, Rating.BEST, title: "Paid"), _data.Finished, EntryStatus.SOLD, 200m, paid: true);
        _data.AddEntry(_data.AddPainting(_data.Student1, Rating.BETTER, title: "Owed"), _data.Finished, EntryStatus.SOLD, 300m);
        _data.AddEntry(_data.AddPainting(_data.Student1, Rating.BEST, title: "Left"), _data.Finished, EntryStatus.UNSOLD, 100m);
        _data.AddEntry(_data.AddPainting(_data.Student1, Rating.BEST, title: "On show"), _data.Ongoing, EntryStatus.ACCEPTED, 150m);
        _data.AddEntry(_data.AddPainting(_data.Student2, Rating.BETTER, title: "Refused"), _data.Ongoing, EntryStatus.REJECTED, 90m);
    }

    /// <summary>TC-M10-05: totals – sent excludes REJECTED, revenue = sum of soldPrice, paid / unpaid split.</summary>
    [Fact]
    public async Task Sales_Totals()
    {
        Seed();

        var report = await Service.GetSalesReportAsync(new ExhibitionSalesSearchVm());

        var t = report.Totals;
        Assert.Equal(5, report.Rows.Count);
        Assert.Equal(5, t.Entries);
        Assert.Equal(4, t.Sent);
        Assert.Equal(2, t.Sold);
        Assert.Equal(2, t.Unsold);
        Assert.Equal(750m, t.TotalAskingPrice);
        Assert.Equal(500m, t.TotalSoldPrice);
        Assert.Equal(500m, t.SoldAskingPrice);
        Assert.Equal(0m, t.Difference);
        Assert.Equal(1, t.PaidCount);
        Assert.Equal(250m, t.PaidAmount);
        Assert.Equal(1, t.UnpaidCount);
        Assert.Equal(250m, t.UnpaidAmount);
        Assert.False(report.Truncated);
    }

    /// <summary>F-M10-04: a row carries the rating, sale, buyer and payment details; newest exhibition first.</summary>
    [Fact]
    public async Task Sales_RowDetails()
    {
        Seed();

        var report = await Service.GetSalesReportAsync(new ExhibitionSalesSearchVm());

        Assert.Equal(_data.Ongoing.Name, report.Rows[0].ExhibitionName);
        var paid = report.Rows.Single(r => r.Title == "Paid");
        Assert.Equal(Rating.BEST, paid.Rating);
        Assert.Equal(EntryStatus.SOLD, paid.Status);
        Assert.Equal(250m, paid.SoldPrice);
        Assert.Equal(_data.Finished.StartDate, paid.SoldDate);
        Assert.Equal("Hoang Anh Tuan", paid.CustomerName);
        Assert.True(paid.PaidToStudent);
        Assert.Equal(250m, paid.PaidAmount);
        Assert.Equal("HV0001", paid.StudentCode);
    }

    /// <summary>F-M10-04 filters: exhibition, status and student (name or code).</summary>
    [Fact]
    public async Task Sales_Filters()
    {
        Seed();

        var byExhibition = await Service.GetSalesReportAsync(new ExhibitionSalesSearchVm { ExhibitionId = _data.Finished.Id });
        var byStatus = await Service.GetSalesReportAsync(new ExhibitionSalesSearchVm { Status = EntryStatus.SOLD });
        var byStudent = await Service.GetSalesReportAsync(new ExhibitionSalesSearchVm { Student = " HV0002 " });

        Assert.Equal(3, byExhibition.Totals.Entries);
        Assert.Equal(new[] { "Owed", "Paid" }, byStatus.Rows.Select(r => r.Title));
        Assert.Equal("Refused", Assert.Single(byStudent.Rows).Title);
        Assert.Equal(0, byStudent.Totals.Sent);
    }

    /// <summary>Decision: the date range keeps the exhibitions whose period overlaps [From, To], both days included.</summary>
    [Fact]
    public async Task Sales_DateRange_ExhibitionPeriodOverlap()
    {
        Seed();
        var finishedEnd = _data.Finished.EndDate;

        // Only the last day of the finished exhibition is inside the range → it is kept; the ongoing one starts later
        var report = await Service.GetSalesReportAsync(new ExhibitionSalesSearchVm { From = finishedEnd.AddDays(-10), To = finishedEnd });

        Assert.All(report.Rows, r => Assert.Equal(_data.Finished.Name, r.ExhibitionName));
        Assert.Equal(3, report.Rows.Count);
    }

    /// <summary>TC-M10-04: no matching entry → empty rows and zero totals ("No data"), no error.</summary>
    [Fact]
    public async Task Sales_NoData()
    {
        Seed();

        var report = await Service.GetSalesReportAsync(new ExhibitionSalesSearchVm { From = new DateOnly(2000, 1, 1), To = new DateOnly(2000, 12, 31) });

        Assert.Empty(report.Rows);
        Assert.Equal(0, report.Totals.Entries);
        Assert.Equal(0m, report.Totals.TotalSoldPrice);
    }

    /// <summary>TC-M10-03: From after To is refused with the date range message, on both reports.</summary>
    [Fact]
    public async Task FromAfterTo_Refused()
    {
        var from = new DateOnly(2026, 5, 2);
        var to = new DateOnly(2026, 5, 1);

        var sales = await Assert.ThrowsAsync<BusinessException>(() => Service.GetSalesReportAsync(new ExhibitionSalesSearchVm { From = from, To = to }));
        var debt = await Assert.ThrowsAsync<BusinessException>(() => Service.GetStudentDebtReportAsync(new StudentDebtSearchVm { From = from, To = to }));

        Assert.Equal(ExhibitionReportService.DateRangeMessage, sales.Message);
        Assert.Equal(ExhibitionReportService.DateRangeMessage, debt.Message);
        Assert.Equal(nameof(ExhibitionSalesSearchVm.From), sales.Field);
    }

    /// <summary>RPT-05: only SOLD and not paid; days unpaid = today − sold date; total unpaid.</summary>
    [Fact]
    public async Task Debt_OnlySoldUnpaid()
    {
        Seed();
        var today = _data.Finished.StartDate.AddDays(12);

        var report = await Service.GetStudentDebtReportAsync(new StudentDebtSearchVm(), today);

        var row = Assert.Single(report.Rows);
        Assert.Equal("Owed", row.Title);
        Assert.Equal(_data.Finished.Name, row.ExhibitionName);
        Assert.Equal(250m, row.SoldPrice);
        Assert.Equal(12, row.DaysUnpaid);
        Assert.Equal(1, report.Count);
        Assert.Equal(250m, report.TotalUnpaid);
    }

    /// <summary>RPT-05: oldest sale first; sale date range with both days included; exhibition and student filters.</summary>
    [Fact]
    public async Task Debt_OrderAndFilters()
    {
        var older = _data.AddEntry(_data.AddPainting(_data.Student2, Rating.BEST, title: "Older"), _data.Finished, EntryStatus.SOLD);
        older.SoldDate = _data.Finished.StartDate.AddDays(-1);
        var newer = _data.AddEntry(_data.AddPainting(_data.Student1, Rating.BEST, title: "Newer"), _data.Ongoing, EntryStatus.SOLD);
        _data.Db.SaveChanges();

        var all = await Service.GetStudentDebtReportAsync(new StudentDebtSearchVm());
        var oneDay = await Service.GetStudentDebtReportAsync(new StudentDebtSearchVm { From = newer.SoldDate, To = newer.SoldDate });
        var byExhibition = await Service.GetStudentDebtReportAsync(new StudentDebtSearchVm { ExhibitionId = _data.Finished.Id });
        var byStudent = await Service.GetStudentDebtReportAsync(new StudentDebtSearchVm { Student = "Binh" });

        Assert.Equal(new[] { "Older", "Newer" }, all.Rows.Select(r => r.Title));
        Assert.Equal(500m, all.TotalUnpaid);
        Assert.Equal("Newer", Assert.Single(oneDay.Rows).Title);
        Assert.Equal("Older", Assert.Single(byExhibition.Rows).Title);
        Assert.Equal("Older", Assert.Single(byStudent.Rows).Title);
    }

    /// <summary>TC-M10-04: nothing owed → no rows, total 0.</summary>
    [Fact]
    public async Task Debt_NoData()
    {
        _data.AddEntry(_data.AddPainting(_data.Student1, Rating.BEST), _data.Finished, EntryStatus.SOLD, paid: true);

        var report = await Service.GetStudentDebtReportAsync(new StudentDebtSearchVm());

        Assert.Empty(report.Rows);
        Assert.Equal(0, report.Count);
        Assert.Equal(0m, report.TotalUnpaid);
    }
}
