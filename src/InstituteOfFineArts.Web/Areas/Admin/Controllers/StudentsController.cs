using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Services.Interfaces;
using InstituteOfFineArts.Web.ViewModels.Admin.Students;
using Microsoft.AspNetCore.Mvc;

namespace InstituteOfFineArts.Web.Areas.Admin.Controllers;

/// <summary>
/// Admin – student accounts (M04, task BE2-03; screens SCR-14..16):
/// GET /admin/students · GET/POST /admin/students/new · GET /admin/students/{id} · GET/POST /admin/students/{id}/edit ·
/// POST /admin/students/{id}/reset-password, /lock, /unlock, /delete.
/// </summary>
[Route("admin/students")]
public class StudentsController : AdminBaseController
{
    /// <summary>Student service.</summary>
    private readonly IStudentService _students;

    /// <summary>Receives the student service through Dependency Injection.</summary>
    public StudentsController(IStudentService students)
    {
        _students = students;
    }

    /// <summary>List with filters and paging.</summary>
    /// <param name="search">Filters from the query string</param>
    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] StudentSearchVm search)
    {
        ViewBag.Search = search;
        return View(await _students.SearchAsync(search));
    }

    /// <summary>Empty create form (status STUDYING, enrolled today).</summary>
    [HttpGet("new")]
    public IActionResult Create() => View(new CreateStudentVm { EnrollmentDate = Helpers.DateTimeHelper.Today });

    /// <summary>Creates the account + profile, then opens the details.</summary>
    /// <param name="form">Form data</param>
    [HttpPost("new")]
    public async Task<IActionResult> Create(CreateStudentVm form)
    {
        if (ModelState.IsValid)
        {
            try
            {
                var id = await _students.CreateAsync(form);
                TempData["Success"] = "Student created.";
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (BusinessException ex)
            {
                ModelState.AddModelError(ex.Field ?? string.Empty, ex.Message);
            }
        }
        // Typed values stay in the form; password inputs never render their value
        return View(form);
    }

    /// <summary>Details: login, personal and study information, related data.</summary>
    /// <param name="id">student_profile.id</param>
    [HttpGet("{id:long}")]
    public async Task<IActionResult> Details(long id)
    {
        try
        {
            return View(await _students.GetDetailsAsync(id));
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>Edit form.</summary>
    /// <param name="id">student_profile.id</param>
    [HttpGet("{id:long}/edit")]
    public async Task<IActionResult> Edit(long id)
    {
        try
        {
            return View(await _students.GetForEditAsync(id));
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>Saves changes (username and role never change).</summary>
    /// <param name="id">student_profile.id</param>
    /// <param name="form">Form data</param>
    [HttpPost("{id:long}/edit")]
    public async Task<IActionResult> Edit(long id, StudentFormVm form)
    {
        try
        {
            if (ModelState.IsValid)
            {
                await _students.UpdateAsync(id, form);
                TempData["Success"] = "Student updated.";
                return RedirectToAction(nameof(Details), new { id });
            }
            // Invalid form: the read-only username comes from the database, not from the browser
            form.Username = (await _students.GetForEditAsync(id)).Username;
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
        catch (BusinessException ex)
        {
            ModelState.AddModelError(ex.Field ?? string.Empty, ex.Message);
            form.Username = (await _students.GetForEditAsync(id)).Username;
        }
        form.Id = id;
        return View(form);
    }

    /// <summary>Sets a temporary password.</summary>
    /// <param name="id">student_profile.id</param>
    /// <param name="form">New password</param>
    [HttpPost("{id:long}/reset-password")]
    public Task<IActionResult> ResetPassword(long id, ResetStudentPasswordVm form)
        => RunAsync(id, async () =>
        {
            if (!ModelState.IsValid)
            {
                throw new BusinessException(ModelState.Values.SelectMany(v => v.Errors).First().ErrorMessage);
            }
            await _students.ResetPasswordAsync(id, form.NewPassword);
            return "Password reset. Give the new password to the student.";
        });

    /// <summary>Locks the account.</summary>
    /// <param name="id">student_profile.id</param>
    [HttpPost("{id:long}/lock")]
    public Task<IActionResult> Lock(long id)
        => RunAsync(id, async () =>
        {
            await _students.SetActiveAsync(id, false);
            return "Account locked.";
        });

    /// <summary>Unlocks the account.</summary>
    /// <param name="id">student_profile.id</param>
    [HttpPost("{id:long}/unlock")]
    public Task<IActionResult> Unlock(long id)
        => RunAsync(id, async () =>
        {
            await _students.SetActiveAsync(id, true);
            return "Account unlocked.";
        });

    /// <summary>Deletes the student, or locks the account when submissions or exhibition entries exist (BR-04).</summary>
    /// <param name="id">student_profile.id</param>
    [HttpPost("{id:long}/delete")]
    public async Task<IActionResult> Delete(long id)
    {
        try
        {
            var result = await _students.DeleteAsync(id);
            if (result == StudentDeleteResult.Deleted)
            {
                TempData["Success"] = "Student deleted.";
                return RedirectToAction(nameof(Index));
            }
            TempData["Success"] = "This student has submissions or exhibition entries, so the account was locked instead of deleted.";
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
