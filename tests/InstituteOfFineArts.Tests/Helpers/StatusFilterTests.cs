using InstituteOfFineArts.Web.Helpers;
using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;

namespace InstituteOfFineArts.Tests.Helpers;

/// <summary>
/// Shared status filters used by the staff, public and exhibition lists (BR-07, BR-42):
/// the query version must give the same answer as <see cref="CompetitionStatusResolver.Resolve(Competition, DateOnly?)"/>.
/// </summary>
public class StatusFilterTests
{
    /// <summary>Fixed "today" so the test does not depend on the clock.</summary>
    private static readonly DateOnly Today = new(2026, 11, 5);

    /// <summary>One competition per case: upcoming, ongoing, last day, closed, graded (dates still open).</summary>
    private static IQueryable<Competition> Competitions() => new List<Competition>
    {
        new() { Id = 1, StartDate = Today.AddDays(1), EndDate = Today.AddDays(9) },
        new() { Id = 2, StartDate = Today.AddDays(-3), EndDate = Today.AddDays(3) },
        new() { Id = 3, StartDate = Today.AddDays(-3), EndDate = Today },
        new() { Id = 4, StartDate = Today.AddDays(-9), EndDate = Today.AddDays(-1) },
        new() { Id = 5, StartDate = Today.AddDays(-9), EndDate = Today.AddDays(5), Status = CompetitionStatus.GRADED },
    }.AsQueryable();

    /// <summary>Each competition status keeps exactly the rows that Resolve gives that status.</summary>
    [Theory]
    [InlineData(CompetitionStatus.UPCOMING, new long[] { 1 })]
    [InlineData(CompetitionStatus.ONGOING, new long[] { 2, 3 })]
    [InlineData(CompetitionStatus.CLOSED, new long[] { 4 })]
    [InlineData(CompetitionStatus.GRADED, new long[] { 5 })]
    public void Competition_WhereStatus_MatchesResolve(CompetitionStatus status, long[] expected)
    {
        var ids = Competitions().WhereStatus(status, Today).Select(c => c.Id).ToArray();
        Assert.Equal(expected, ids);
        Assert.All(Competitions().Where(c => ids.Contains(c.Id)), c => Assert.Equal(status, CompetitionStatusResolver.Resolve(c, Today)));
    }

    /// <summary>No status selected = no filter.</summary>
    [Fact]
    public void Competition_WhereStatus_Null_KeepsAll()
    {
        Assert.Equal(5, Competitions().WhereStatus(null, Today).Count());
    }

    /// <summary>Exhibitions: upcoming / ongoing (incl. last day) / finished follow the dates only.</summary>
    [Theory]
    [InlineData(ExhibitionStatus.UPCOMING, new long[] { 1 })]
    [InlineData(ExhibitionStatus.ONGOING, new long[] { 2, 3 })]
    [InlineData(ExhibitionStatus.FINISHED, new long[] { 4 })]
    public void Exhibition_WhereStatus_MatchesResolve(ExhibitionStatus status, long[] expected)
    {
        var exhibitions = new List<Exhibition>
        {
            new() { Id = 1, StartDate = Today.AddDays(1), EndDate = Today.AddDays(9) },
            new() { Id = 2, StartDate = Today.AddDays(-3), EndDate = Today.AddDays(3) },
            new() { Id = 3, StartDate = Today.AddDays(-3), EndDate = Today },
            new() { Id = 4, StartDate = Today.AddDays(-9), EndDate = Today.AddDays(-1) },
        }.AsQueryable();

        var ids = exhibitions.WhereStatus(status, Today).Select(e => e.Id).ToArray();
        Assert.Equal(expected, ids);
        Assert.All(exhibitions.Where(e => ids.Contains(e.Id)), e => Assert.Equal(status, CompetitionStatusResolver.Resolve(e, Today)));
        Assert.Equal(4, exhibitions.WhereStatus(null, Today).Count());
    }
}
