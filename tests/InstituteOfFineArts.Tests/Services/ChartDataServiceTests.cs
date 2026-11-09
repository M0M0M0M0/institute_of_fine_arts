using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.Services.Implementations;
using Microsoft.EntityFrameworkCore;

namespace InstituteOfFineArts.Tests.Services;

/// <summary>
/// ChartDataService (task BE2-OPT 2; F-M15-01): submissions per competition, rating distribution and sales per
/// exhibition equal the plain counts / sums of the in-memory data; status filters follow CompetitionStatusResolver;
/// every series has one value per label.
/// </summary>
public class ChartDataServiceTests : IDisposable
{
    /// <summary>Shared test data (GRADED competition C1, CLOSED competition C2, exhibitions in every status).</summary>
    private readonly ExhibitionEntryTestData _data = new();

    /// <summary>Service under test.</summary>
    private ChartDataService Service => new(_data.Db);

    /// <summary>Disposes the test database.</summary>
    public void Dispose() => _data.Dispose();

    /// <summary>
    /// C1: BEST, BEST, GOOD, not graded; C2: BETTER. Finished exhibition: 2 SOLD (250 each), 1 UNSOLD; ongoing: ACCEPTED.
    /// </summary>
    private void Seed()
    {
        var a = _data.AddPainting(_data.Student1, Rating.BEST);
        var b = _data.AddPainting(_data.Student2, Rating.BEST);
        var c = _data.AddPainting(_data.Student1, Rating.GOOD);
        _data.AddPainting(_data.Student2, null);
        var d = _data.AddPainting(_data.Student1, Rating.BETTER, _data.Closed);
        _data.AddEntry(a, _data.Finished, EntryStatus.SOLD, paid: true);
        _data.AddEntry(b, _data.Finished, EntryStatus.SOLD);
        _data.AddEntry(c, _data.Finished, EntryStatus.UNSOLD);
        _data.AddEntry(d, _data.Ongoing, EntryStatus.ACCEPTED);
    }

    /// <summary>One bar per competition (oldest first) with the submission and evaluation counts of the data.</summary>
    [Fact]
    public async Task SubmissionsByCompetition_EqualsCounts()
    {
        Seed();

        var chart = await Service.GetSubmissionsByCompetitionAsync();

        Assert.Equal(new[] { "C1", "C2" }, chart.Labels);
        Assert.Equal(new[] { "Submissions", "Evaluated" }, chart.Datasets.Select(d => d.Label));
        var expected = await _data.Db.Competitions.OrderBy(c => c.StartDate)
            .Select(c => new { All = c.Submissions.Count(), Evaluated = c.Submissions.Count(s => s.Evaluation != null) }).ToListAsync();
        Assert.Equal(expected.Select(e => (decimal)e.All), chart.Datasets[0].Data);
        Assert.Equal(expected.Select(e => (decimal)e.Evaluated), chart.Datasets[1].Data);
        Assert.Equal(new[] { 4m, 1m }, chart.Datasets[0].Data);
    }

    /// <summary>Status filter uses the resolver: GRADED = stored, CLOSED = past end date and not graded.</summary>
    [Fact]
    public async Task SubmissionsByCompetition_StatusFilter()
    {
        Seed();

        Assert.Equal(new[] { "C1" }, (await Service.GetSubmissionsByCompetitionAsync(CompetitionStatus.GRADED)).Labels);
        Assert.Equal(new[] { "C2" }, (await Service.GetSubmissionsByCompetitionAsync(CompetitionStatus.CLOSED)).Labels);
        Assert.Empty((await Service.GetSubmissionsByCompetitionAsync(CompetitionStatus.ONGOING)).Labels);
    }

    /// <summary>Every rating plus "Not graded" is listed with the counts of the data (0 when none).</summary>
    [Fact]
    public async Task Ratings_AllCompetitions()
    {
        Seed();

        var chart = await Service.GetRatingsAsync();

        Assert.Equal(Enum.GetNames<Rating>().Append(ChartDataService.NotGradedLabel), chart.Labels);
        var data = chart.Datasets.Single().Data;
        Assert.Equal(new[] { 2m, 1m, 1m, 0m, 0m, 0m, 1m }, data);
        Assert.Equal(await _data.Db.Submissions.CountAsync(), data.Sum());
    }

    /// <summary>One competition only; an unknown competition → NotFoundException (404).</summary>
    [Fact]
    public async Task Ratings_OneCompetition_UnknownNotFound()
    {
        Seed();

        var chart = await Service.GetRatingsAsync(_data.Closed.Id);

        Assert.Equal(new[] { 0m, 1m, 0m, 0m, 0m, 0m, 0m }, chart.Datasets.Single().Data);
        await Assert.ThrowsAsync<NotFoundException>(() => Service.GetRatingsAsync(999_999));
    }

    /// <summary>Revenue = sum of the sold prices of SOLD entries per exhibition; every exhibition listed.</summary>
    [Fact]
    public async Task SalesByExhibition_EqualsSums()
    {
        Seed();

        var chart = await Service.GetSalesByExhibitionAsync();

        var expected = await _data.Db.Exhibitions.OrderBy(e => e.StartDate).ThenBy(e => e.Id)
            .Select(e => new
            {
                e.Name,
                Revenue = e.Entries.Where(x => x.Status == EntryStatus.SOLD).Sum(x => x.SoldPrice ?? 0m),
                Sold = e.Entries.Count(x => x.Status == EntryStatus.SOLD),
            }).ToListAsync();
        Assert.Equal(expected.Select(e => e.Name), chart.Labels);
        Assert.Equal(expected.Select(e => e.Revenue), chart.Datasets[0].Data);
        Assert.Equal(expected.Select(e => (decimal)e.Sold), chart.Datasets[1].Data);
        Assert.Equal(500m, chart.Datasets[0].Data[chart.Labels.IndexOf(_data.Finished.Name)]);
    }

    /// <summary>Exhibition status filter (BR-42 dates): only the finished exhibition.</summary>
    [Fact]
    public async Task SalesByExhibition_StatusFilter()
    {
        Seed();

        var chart = await Service.GetSalesByExhibitionAsync(ExhibitionStatus.FINISHED);

        Assert.Equal(new[] { _data.Finished.Name }, chart.Labels);
        Assert.Equal(new[] { 500m }, chart.Datasets[0].Data);
        Assert.Equal(new[] { 2m }, chart.Datasets[1].Data);
    }
}
