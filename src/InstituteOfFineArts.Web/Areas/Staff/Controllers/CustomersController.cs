using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Services.Interfaces;
using InstituteOfFineArts.Web.ViewModels.Staff.Customers;
using Microsoft.AspNetCore.Mvc;

namespace InstituteOfFineArts.Web.Areas.Staff.Controllers;

/// <summary>
/// Staff – customers who buy paintings (M09, task BE2-10; F-M09-07, screen SCR-32):
/// GET /staff/customers (list + search) · GET/POST /staff/customers/new · GET /staff/customers/{id} (with purchases)
/// GET/POST /staff/customers/{id}/edit · POST /staff/customers/{id}/delete.
/// </summary>
[Route("staff/customers")]
public class CustomersController : StaffBaseController
{
    /// <summary>Customer service.</summary>
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

    /// <summary>Empty create form.</summary>
    [HttpGet("new")]
    public IActionResult Create() => View(new CustomerFormVm());

    /// <summary>Creates the customer, then opens the details.</summary>
    /// <param name="form">Form data</param>
    [HttpPost("new")]
    public async Task<IActionResult> Create(CustomerFormVm form)
    {
        if (!ModelState.IsValid)
        {
            return View(form);
        }
        try
        {
            var id = await _customers.CreateAsync(form);
            TempData["Success"] = "Customer created.";
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (BusinessException ex)
        {
            ModelState.AddModelError(ex.Field ?? string.Empty, ex.Message);
            return View(form);
        }
    }

    /// <summary>Details with the paintings the customer bought (read only).</summary>
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

    /// <summary>Edit form with the current values.</summary>
    /// <param name="id">Customer id</param>
    [HttpGet("{id:long}/edit")]
    public async Task<IActionResult> Edit(long id)
    {
        try
        {
            return View(await _customers.GetForEditAsync(id));
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>Saves the changes.</summary>
    /// <param name="id">Customer id</param>
    /// <param name="form">Form data</param>
    [HttpPost("{id:long}/edit")]
    public async Task<IActionResult> Edit(long id, CustomerFormVm form)
    {
        form.Id = id;
        if (!ModelState.IsValid)
        {
            return View(form);
        }
        try
        {
            await _customers.UpdateAsync(id, form);
            TempData["Success"] = "Customer updated.";
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
        catch (BusinessException ex)
        {
            ModelState.AddModelError(ex.Field ?? string.Empty, ex.Message);
            return View(form);
        }
    }

    /// <summary>Deletes a customer without purchases. POST only, with anti-forgery token.</summary>
    /// <param name="id">Customer id</param>
    [HttpPost("{id:long}/delete")]
    public async Task<IActionResult> Delete(long id)
    {
        try
        {
            await _customers.DeleteAsync(id);
            TempData["Success"] = "Customer deleted.";
            return RedirectToAction(nameof(Index));
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
        catch (BusinessException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Details), new { id });
        }
    }
}
