using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.Services.Implementations;
using InstituteOfFineArts.Web.ViewModels.Staff;
using InstituteOfFineArts.Web.ViewModels.Staff.Submissions;
using Microsoft.EntityFrameworkCore;

namespace InstituteOfFineArts.Tests.Services;

/// <summary>
/// Dashboards after the manager-dashboard review: "Needs attention" cards (counts equal the rows of the list they link
/// to, 0-count cards hidden, area-specific links), the period filter with the previous-period comparison, the
/// submissions list filter by competition status and the extra chart data (latest competitions, ids, paintings shown).
/// </summary>
public class DashboardAttentionTests : IDisposable
{
    /// <summary>Shared data: GRADED competition C1, CLOSED competition C2, exhibitions in every status.</summary>
    private readonly ExhibitionEntryTestData _data = new();

    /// <summary>Disposes the test database.</summary>
    public void Dispose() => _data.Dispose();

    /// <summary>C2 (closed): one entry graded, two not; one PENDING proposal; sales: one unpaid, one paid; one locked student.</summary>
    private void Seed()
    {
        _data.AddPainting(_data.Student1, null, _data.Closed);
        _data.AddPainting(_data.Student2, null, _data.Closed, "Harbour");
        _data.AddPainting(_data.Student1, Rating.BETTER, _data.Closed, "Bridge");
        var a = _data.AddPainting(_data.Student1, Rating.BEST);
        var b = _data.AddPainting(_data.Student2, Rating.BEST);
        var c = _data.AddPainting(_data.Student1, Rating.BETTER);
        _data.AddEntry(a, _data.Finished, EntryStatus.SOLD);
        _data.AddEntry(b, _data.Finished, EntryStatus.SOLD, paid: true);
        _data.AddEntry(c, _data.Upcoming, EntryStatus.PENDING);
        var student2 = _data.Db.StudentProfiles.Include(s => s.Account).Single(s => s.Id == _data.Student2);
        student2.Account.Active = false;
        _data.Db.SaveChanges();
    }

    /// <summary>
    /// Staff cards (the Staff dashboard took over the Manager overview): entries the teachers have not graded in closed
    /// competitions, closed competitions, proposals, unpaid sales – all linking into the Staff area, no account cards
    /// (accounts are the admin's work), no 0-count card.
    /// </summary>
    [Fact]
    public async Task Staff_AttentionCards_CountTheWaitingRows_StaffLinks()
    {
        Seed();

        var vm = await new StaffDashboardService(_data.Db).GetAsync(ExhibitionEntryTestData.Today);

        var cards = vm.Attention.ToDictionary(i => i.Title, i => i);
        Assert.Equal(2, cards["entries not graded yet"].Count);
        Assert.Equal("/staff/submissions?Evaluated=false&CompetitionStatus=CLOSED", cards["entries not graded yet"].Url);
        Assert.Equal(1, cards["closed competition"].Count);
        Assert.Equal(1, cards["exhibition proposal"].Count);
        Assert.Equal(1, cards["sale not paid out"].Count);
        Assert.All(vm.Attention, i => Assert.StartsWith("/staff/", i.Url));
        Assert.DoesNotContain(vm.Attention, i => i.Title.Contains("account"));
        Assert.All(vm.Attention, i => Assert.True(i.Count > 0));
    }

    /// <summary>Nothing waiting (empty database) → no card at all; the fixture's closed competition alone gives one card.</summary>
    [Fact]
    public async Task Attention_NothingWaiting_Empty()
    {
        using var empty = new InstituteOfFineArts.Web.Data.AppDbContext(new DbContextOptionsBuilder<InstituteOfFineArts.Web.Data.AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        Assert.Empty(await AttentionItems.BuildAsync(empty, ExhibitionEntryTestData.Today));

        var items = await AttentionItems.BuildAsync(_data.Db, ExhibitionEntryTestData.Today);
        Assert.Equal("closed competition", Assert.Single(items).Title);
    }

    /// <summary>The "entries to grade" link shows exactly the counted rows: not evaluated + competition CLOSED.</summary>
    [Fact]
    public async Task SubmissionSearch_CompetitionStatusClosed_MatchesTheCard()
    {
        Seed();
        // An ungraded entry of the GRADED competition must not be listed
        _data.AddPainting(_data.Student2, null, _data.Graded, "Late");

        var result = await new StaffSubmissionService(_data.Db).SearchAsync(
            new SubmissionSearchVm { Evaluated = false, CompetitionStatus = CompetitionStatus.CLOSED });

        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, i => Assert.Equal(_data.Closed.Id, i.CompetitionId));
    }

    /// <summary>Period ranges: this month / last month (across a year end) and this year / last year.</summary>
    [Fact]
    public void Range_MonthAndYear()
    {
        var day = new DateOnly(2026, 1, 20);

        Assert.Equal((new DateOnly(2026, 1, 1), new DateOnly(2026, 2, 1)), StaffDashboardService.Range(DashboardPeriod.Month, day, 0));
        Assert.Equal((new DateOnly(2025, 12, 1), new DateOnly(2026, 1, 1)), StaffDashboardService.Range(DashboardPeriod.Month, day, -1));
        Assert.Equal((new DateOnly(2025, 1, 1), new DateOnly(2026, 1, 1)), StaffDashboardService.Range(DashboardPeriod.Year, day, -1));
        Assert.Equal(((DateOnly?)null, (DateOnly?)null), StaffDashboardService.Range(DashboardPeriod.All, day, 0));
    }

    /// <summary>Month period: only this month's submissions and sales, with last month's figures for comparison.</summary>
    [Fact]
    public async Task Staff_MonthPeriod_CountsThisMonth_ComparesLastMonth()
    {
        var today = new DateOnly(2026, 6, 15);
        var thisMonth = _data.AddPainting(_data.Student1, null);
        var lastMonth1 = _data.AddPainting(_data.Student2, null, _data.Closed);
        var lastMonth2 = _data.AddPainting(_data.Student1, Rating.BEST, _data.Closed, "Bridge");
        thisMonth.SubmittedAt = new DateTime(2026, 6, 2, 10, 0, 0);
        lastMonth1.SubmittedAt = new DateTime(2026, 5, 31, 23, 0, 0);
        lastMonth2.SubmittedAt = new DateTime(2026, 5, 1, 8, 0, 0);
        var sale = _data.AddEntry(thisMonth, _data.Finished, EntryStatus.SOLD);
        sale.SoldDate = new DateOnly(2026, 6, 10);
        _data.Db.SaveChanges();

        var vm = await new StaffDashboardService(_data.Db).GetAsync(today, DashboardPeriod.Month);

        Assert.Equal(1, vm.SubmissionsTotal);
        Assert.Equal(2, vm.SubmissionsPrevious);
        Assert.Equal(1, vm.PaintingsSold);
        Assert.Equal(250m, vm.TotalSales);
        Assert.Equal(0m, vm.SalesPrevious);
        Assert.Equal(250m, vm.UnpaidToStudents);
        Assert.Equal("last month", vm.PreviousLabel);
    }

    /// <summary>All time: no comparison figures.</summary>
    [Fact]
    public async Task Staff_AllPeriod_NoComparison()
    {
        var vm = await new StaffDashboardService(_data.Db).GetAsync(ExhibitionEntryTestData.Today);

        Assert.Null(vm.PreviousLabel);
        Assert.Null(vm.SubmissionsPrevious);
        Assert.Null(vm.SalesPrevious);
    }

    /// <summary>Submissions chart: ids and details match the labels; take keeps the latest competitions, still oldest first.</summary>
    [Fact]
    public async Task Chart_Submissions_LatestFirstTaken_IdsAndDetails()
    {
        var service = new ChartDataService(_data.Db);

        var all = await service.GetSubmissionsByCompetitionAsync();
        var latest = await service.GetSubmissionsByCompetitionAsync(take: 1);
        var otherYear = await service.GetSubmissionsByCompetitionAsync(year: 1990);

        Assert.Equal(new[] { _data.Graded.Code, _data.Closed.Code }, all.Labels);
        Assert.Equal(new[] { _data.Graded.Id, _data.Closed.Id }, all.Ids);
        Assert.Equal(all.Labels.Count, all.Details!.Count);
        Assert.StartsWith(_data.Graded.Title, all.Details[0]);
        Assert.Equal(new[] { _data.Closed.Code }, latest.Labels);
        Assert.Empty(otherYear.Labels);
    }

    /// <summary>Sales chart: third series = paintings shown (accepted, sold, unsold), so "sold / shown" can be drawn.</summary>
    [Fact]
    public async Task Chart_Sales_PaintingsShown()
    {
        var a = _data.AddPainting(_data.Student1, Rating.BEST);
        var b = _data.AddPainting(_data.Student2, Rating.BEST);
        var c = _data.AddPainting(_data.Student1, Rating.BETTER);
        var d = _data.AddPainting(_data.Student2, Rating.BETTER);
        _data.AddEntry(a, _data.Finished, EntryStatus.SOLD);
        _data.AddEntry(b, _data.Finished, EntryStatus.UNSOLD);
        _data.AddEntry(c, _data.Finished, EntryStatus.REJECTED);
        _data.AddEntry(d, _data.Ongoing, EntryStatus.ACCEPTED);

        var chart = await new ChartDataService(_data.Db).GetSalesByExhibitionAsync();

        var shown = chart.Datasets.Single(s => s.Label == "Paintings shown");
        Assert.Equal(2m, shown.Data[chart.Labels.IndexOf(_data.Finished.Name)]);
        Assert.Equal(1m, shown.Data[chart.Labels.IndexOf(_data.Ongoing.Name)]);
    }
}
