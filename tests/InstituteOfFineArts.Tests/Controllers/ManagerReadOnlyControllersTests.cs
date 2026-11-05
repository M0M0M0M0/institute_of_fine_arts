using System.Reflection;
using InstituteOfFineArts.Web.Areas.Manager.Controllers;
using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Services.Interfaces;
using InstituteOfFineArts.Web.ViewModels.Common;
using InstituteOfFineArts.Web.ViewModels.Staff.Awards;
using InstituteOfFineArts.Web.ViewModels.Staff.Customers;
using InstituteOfFineArts.Web.ViewModels.Staff.ExhibitionEntries;
using InstituteOfFineArts.Web.ViewModels.Staff.Submissions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Moq;
using ManagerAwardsController = InstituteOfFineArts.Web.Areas.Manager.Controllers.AwardsController;
using ManagerCustomersController = InstituteOfFineArts.Web.Areas.Manager.Controllers.CustomersController;
using ManagerEntriesController = InstituteOfFineArts.Web.Areas.Manager.Controllers.ExhibitionEntriesController;

namespace InstituteOfFineArts.Tests.Controllers;

/// <summary>
/// Manager read-only pages of task BE2-13 part 2 (F-M11-04, F-M09-11 = F-M11-07, role matrix Manager R,S on
/// AwardIssued, ExhibitionEntry, Customer): only GET actions, Manager role, filters passed to the existing services,
/// unknown ids → 404.
/// </summary>
public class ManagerReadOnlyControllersTests
{
    /// <summary>The three controllers of this task.</summary>
    public static TheoryData<Type> Controllers => new()
    {
        typeof(ManagerAwardsController),
        typeof(ManagerEntriesController),
        typeof(ManagerCustomersController),
    };

    /// <summary>Read only (TC-M11-01/02): every public action is an HTTP GET, none accepts POST/PUT/DELETE.</summary>
    [Theory]
    [MemberData(nameof(Controllers))]
    public void Actions_AreGetOnly(Type controller)
    {
        var actions = controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        Assert.NotEmpty(actions);
        foreach (var action in actions)
        {
            var verbs = action.GetCustomAttributes<HttpMethodAttribute>().SelectMany(a => a.HttpMethods).ToList();
            Assert.Equal(new[] { "GET" }, verbs);
        }
    }

    /// <summary>The pages are in the Manager area (role MANAGER only, other roles → 403).</summary>
    [Theory]
    [MemberData(nameof(Controllers))]
    public void Controllers_InheritManagerBase(Type controller)
    {
        Assert.True(controller.IsSubclassOf(typeof(ManagerBaseController)));
    }

    /// <summary>F-M11-04: the award list uses the same search as /staff/awards with the query-string filters.</summary>
    [Fact]
    public async Task Awards_Index_PassesFiltersToService()
    {
        var search = new AwardIssuedSearchVm { CompetitionId = 3, Student = "Lan", Award = "First", Page = 2 };
        var page = new PagedResult<AwardIssuedListItemVm> { Page = 2, PageSize = 10 };
        var service = new Mock<IAwardIssuedService>();
        service.Setup(s => s.SearchAsync(search, 10)).ReturnsAsync(page);
        service.Setup(s => s.GetCompetitionOptionsAsync()).ReturnsAsync(new List<CompetitionOptionVm>());
        var controller = new ManagerAwardsController(service.Object);

        var result = Assert.IsType<ViewResult>(await controller.Index(search));

        Assert.Same(page, result.Model);
        Assert.Same(search, controller.ViewBag.Search);
        service.Verify(s => s.SearchAsync(search, 10), Times.Once);
    }

    /// <summary>The ?exhibitionId= filter (link from the exhibition details page) reaches the entry search.</summary>
    [Fact]
    public async Task Entries_Index_ExhibitionFilter()
    {
        var search = new EntrySearchVm { ExhibitionId = 7 };
        var page = new PagedResult<EntryListItemVm> { Page = 1, PageSize = 10 };
        var service = new Mock<IExhibitionEntryService>();
        service.Setup(s => s.SearchAsync(It.Is<EntrySearchVm>(x => x.ExhibitionId == 7), 10)).ReturnsAsync(page);
        service.Setup(s => s.GetExhibitionOptionsAsync()).ReturnsAsync(new List<EntryExhibitionOptionVm>());
        var controller = new ManagerEntriesController(service.Object);

        var result = Assert.IsType<ViewResult>(await controller.Index(search));

        Assert.Same(page, result.Model);
    }

    /// <summary>Unknown entry id → 404.</summary>
    [Fact]
    public async Task Entries_Details_Unknown_NotFound()
    {
        var service = new Mock<IExhibitionEntryService>();
        service.Setup(s => s.GetDetailsAsync(99)).ThrowsAsync(new NotFoundException("Entry not found."));
        var controller = new ManagerEntriesController(service.Object);

        Assert.IsType<NotFoundResult>(await controller.Details(99));
    }

    /// <summary>Existing entry → details view with the service's model.</summary>
    [Fact]
    public async Task Entries_Details_Found()
    {
        var vm = new EntryDetailsVm { Id = 5, Title = "Sunrise" };
        var service = new Mock<IExhibitionEntryService>();
        service.Setup(s => s.GetDetailsAsync(5)).ReturnsAsync(vm);
        var controller = new ManagerEntriesController(service.Object);

        var result = Assert.IsType<ViewResult>(await controller.Details(5));

        Assert.Same(vm, result.Model);
    }

    /// <summary>Customer list uses the customer search with the keyword.</summary>
    [Fact]
    public async Task Customers_Index_PassesKeyword()
    {
        var search = new CustomerSearchVm { Keyword = "Nguyen" };
        var page = new PagedResult<CustomerListItemVm> { Page = 1, PageSize = 10 };
        var service = new Mock<ICustomerService>();
        service.Setup(s => s.SearchAsync(search, 10)).ReturnsAsync(page);
        var controller = new ManagerCustomersController(service.Object);

        var result = Assert.IsType<ViewResult>(await controller.Index(search));

        Assert.Same(page, result.Model);
    }

    /// <summary>Unknown customer id → 404.</summary>
    [Fact]
    public async Task Customers_Details_Unknown_NotFound()
    {
        var service = new Mock<ICustomerService>();
        service.Setup(s => s.GetDetailsAsync(99)).ThrowsAsync(new NotFoundException("Customer not found."));
        var controller = new ManagerCustomersController(service.Object);

        Assert.IsType<NotFoundResult>(await controller.Details(99));
    }
}
