using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Infrastructure.Security;
using InstituteOfFineArts.Web.Services.Interfaces;
using InstituteOfFineArts.Web.ViewModels.Admin.Staff;
using Microsoft.AspNetCore.Mvc;

namespace InstituteOfFineArts.Web.Areas.Admin.Controllers;

/// <summary>
/// Admin – staff accounts (M03, task BE2-02; screens SCR-11..13):
/// GET /admin/staff · GET/POST /admin/staff/new · GET /admin/staff/{id} · GET/POST /admin/staff/{id}/edit ·
/// POST /admin/staff/{id}/reset-password, /lock, /unlock, /delete.
/// </summary>
[Route("admin/staff")]
public class StaffController : AdminBaseController
{
    /// <summary>Staff service.</summary>
    private readonly IStaffService _staff;

    /// <summary>Receives the staff service through Dependency Injection.</summary>
    public StaffController(IStaffService staff)
    {
        _staff = staff;
    }

    /// <summary>List with filters and paging.</summary>
    /// <param name="search">Filters from the query string</param>
    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] StaffSearchVm search)
    {
        ViewBag.Search = search;
        return View(await _staff.SearchAsync(search));
    }

    /// <summary>Empty create form (role STAFF, joined today).</summary>
    [HttpGet("new")]
    public IActionResult Create() => View(new CreateStaffVm { JoinedDate = Helpers.DateTimeHelper.Today });

    /// <summary>Creates the account + profile, then opens the details.</summary>
    /// <param name="form">Form data</param>
    [HttpPost("new")]
    public async Task<IActionResult> Create(CreateStaffVm form)
    {
        if (ModelState.IsValid)
        {
            try
            {
                var id = await _staff.CreateAsync(form);
                TempData["Success"] = "Staff member created.";
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (BusinessException ex)
            {
                ModelState.AddModelError(ex.Field ?? string.Empty, ex.Message);
            }
        }
        // Typed values stay in the form (TC-M03-03); password inputs never render their value
        return View(form);
    }

    /// <summary>Details: login, personal and job information, related data.</summary>
    /// <param name="id">staff_profile.id</param>
    [HttpGet("{id:long}")]
    public async Task<IActionResult> Details(long id)
    {
        try
        {
            return View(await _staff.GetDetailsAsync(id, User.GetAccountId()));
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>Edit form.</summary>
    /// <param name="id">staff_profile.id</param>
    [HttpGet("{id:long}/edit")]
    public async Task<IActionResult> Edit(long id)
    {
        try
        {
            return View(await _staff.GetForEditAsync(id));
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>Saves changes (username never changes).</summary>
    /// <param name="id">staff_profile.id</param>
    /// <param name="form">Form data</param>
    [HttpPost("{id:long}/edit")]
    public async Task<IActionResult> Edit(long id, StaffFormVm form)
    {
        try
        {
            if (ModelState.IsValid)
            {
                await _staff.UpdateAsync(id, form, User.GetAccountId());
                TempData["Success"] = "Staff member updated.";
                return RedirectToAction(nameof(Details), new { id });
            }
            form.Username = (await _staff.GetForEditAsync(id)).Username;
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
        catch (BusinessException ex)
        {
            ModelState.AddModelError(ex.Field ?? string.Empty, ex.Message);
            form.Username = (await _staff.GetForEditAsync(id)).Username;
        }
        form.Id = id;
        return View(form);
    }

    /// <summary>Sets a temporary password.</summary>
    /// <param name="id">staff_profile.id</param>
    /// <param name="form">New password</param>
    [HttpPost("{id:long}/reset-password")]
    public Task<IActionResult> ResetPassword(long id, ResetPasswordVm form)
        => RunAsync(id, async () =>
        {
            if (!ModelState.IsValid)
            {
                throw new BusinessException(ModelState.Values.SelectMany(v => v.Errors).First().ErrorMessage);
            }
            await _staff.ResetPasswordAsync(id, form.NewPassword);
            return "Password reset. Give the new password to the staff member.";
        });

    /// <summary>Locks the account.</summary>
    /// <param name="id">staff_profile.id</param>
    [HttpPost("{id:long}/lock")]
    public Task<IActionResult> Lock(long id)
        => RunAsync(id, async () =>
        {
            await _staff.SetActiveAsync(id, false, User.GetAccountId());
            return "Account locked.";
        });

    /// <summary>Unlocks the account.</summary>
    /// <param name="id">staff_profile.id</param>
    [HttpPost("{id:long}/unlock")]
    public Task<IActionResult> Unlock(long id)
        => RunAsync(id, async () =>
        {
            await _staff.SetActiveAsync(id, true, User.GetAccountId());
            return "Account unlocked.";
        });

    /// <summary>Deletes the account, or locks it when related data exists (BR-04).</summary>
    /// <param name="id">staff_profile.id</param>
    [HttpPost("{id:long}/delete")]
    public async Task<IActionResult> Delete(long id)
    {
        try
        {
            var result = await _staff.DeleteAsync(id, User.GetAccountId());
            if (result == StaffDeleteResult.Deleted)
            {
                TempData["Success"] = "Staff member deleted.";
                return RedirectToAction(nameof(Index));
            }
            TempData["Success"] = "This staff member has related data (competitions, evaluations…), so the account was locked instead of deleted.";
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
        catch (BusinessException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>Runs an action from the details page and returns to it with a success or error message.</summary>
    private async Task<IActionResult> RunAsync(long id, Func<Task<string>> action)
    {
        try
        {
            TempData["Success"] = await action();
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
        catch (BusinessException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Details), new { id });
    }
}
