using InstituteOfFineArts.Web.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace InstituteOfFineArts.Tests.Controllers;

/// <summary>
/// Old list address /competitions: without a query it sends visitors to the main competition page
/// /gallery/competitions; with a search (old bookmarks, header search before the change) to /competitions/search,
/// keeping the query string.
/// </summary>
public class CompetitionsRedirectTests
{
    private static CompetitionsController Controller(string query)
    {
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString(query);
        return new CompetitionsController(null!) { ControllerContext = new ControllerContext { HttpContext = context } };
    }

    [Fact]
    public void Index_NoQuery_GoesToGallery()
        => Assert.Equal("/gallery/competitions", Assert.IsType<RedirectResult>(Controller("").Index()).Url);

    [Fact]
    public void Index_WithSearch_GoesToSearchPage_KeepingQuery()
        => Assert.Equal("/competitions/search?Keyword=night&Status=ONGOING",
            Assert.IsType<RedirectResult>(Controller("?Keyword=night&Status=ONGOING").Index()).Url);
}
