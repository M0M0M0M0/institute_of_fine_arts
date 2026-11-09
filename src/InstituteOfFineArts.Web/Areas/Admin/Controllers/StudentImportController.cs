using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace InstituteOfFineArts.Web.Areas.Admin.Controllers;

/// <summary>
/// Admin – import students from a CSV file (F-M04-08, task BE2-OPT 4):
/// GET /admin/students/import (upload form) · POST /admin/students/import (step 1: preview) ·
/// POST /admin/students/import/confirm (step 2: create the valid rows) · GET /admin/students/import/template.csv.
/// </summary>
[Route("admin/students/import")]
public class StudentImportController : AdminBaseController
{
    /// <summary>Import service.</summary>
    private readonly IStudentImportService _import;

    /// <summary>Receives the import service through Dependency Injection.</summary>
    public StudentImportController(IStudentImportService import)
    {
        _import = import;
    }

    /// <summary>Upload form with the column list and the template link.</summary>
    [HttpGet("")]
    public IActionResult Index() => View();

    /// <summary>Step 1: reads the file and shows every row with its errors; nothing is saved.</summary>
    /// <param name="file">Uploaded CSV file</param>
    [HttpPost("")]
    public async Task<IActionResult> Index(IFormFile? file)
    {
        try
        {
            await using var stream = file?.OpenReadStream();
            var preview = await _import.PreviewAsync(file?.FileName, stream, file?.Length ?? 0);
            return View("Preview", preview);
        }
        catch (BusinessException ex)
        {
            // File-level problem (no file, too large, bad header…): back to the upload form
            ModelState.AddModelError(ex.Field ?? string.Empty, ex.Message);
            return View();
        }
    }

    /// <summary>Step 2: creates the valid rows of the previewed file in one transaction, then opens the student list.</summary>
    /// <param name="token">Encrypted file content from the preview page</param>
    [HttpPost("confirm")]
    public async Task<IActionResult> Confirm(string? token)
    {
        try
        {
            var result = await _import.ImportAsync(token);
            TempData["Success"] = result.Imported == 1 ? "1 student imported." : $"{result.Imported} students imported.";
            if (result.Skipped > 0)
            {
                TempData["Warning"] = result.Skipped == 1
                    ? "1 row with errors was skipped."
                    : $"{result.Skipped} rows with errors were skipped.";
            }
            return RedirectToAction("Index", "Students");
        }
        catch (BusinessException ex)
        {
            // Nothing was saved: tell why and let the administrator upload again
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>Downloads the CSV template (header + one example row).</summary>
    [HttpGet("template.csv")]
    public IActionResult Template() => File(_import.GetTemplate(), "text/csv", "students-template.csv");
}
