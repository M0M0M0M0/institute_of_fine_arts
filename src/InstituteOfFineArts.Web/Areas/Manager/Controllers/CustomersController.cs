using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Services.Interfaces;
using InstituteOfFineArts.Web.ViewModels.Staff.Customers;
using Microsoft.AspNetCore.Mvc;

namespace InstituteOfFineArts.Web.Areas.Manager.Controllers;

/// <summary>
/// Manager – customers who bought paintings, read only (task BE2-13; role matrix: Manager R,S on Customer):
/// GET /manager/customers (list + search) · GET /manager/customers/{id} (details with purchases).
/// Create / edit / delete stay in the Staff area (POST there → 403 for a manager).
/// </summary>
[Route("manager/customers")]
public class CustomersController : ManagerBaseController
{
    /// <summary>Customer service (only its read methods are used here).</summary>
    private readonly ICustomerService _customers;

    /// <summary>Receives the customer service through Dependency Injection.</summary>
    public CustomersController(ICustomerService customers)
    {
        _customers = customers;
    }

    /// <summary>List with a name / phone / email filter and paging.</summary>
    /// <param name="search">Filters from the query string</param>
    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] CustomerSearchVm search)
    {
        ViewBag.Search = search;
        return View(await _customers.SearchAsync(search));
    }

    /// <summary>Details with the paintings the customer bought.</summary>
    /// <param name="id">Customer id</param>
    [HttpGet("{id:long}")]
    public async Task<IActionResult> Details(long id)
    {
        try
        {
            return View(await _customers.GetDetailsAsync(id));
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
    }
}
