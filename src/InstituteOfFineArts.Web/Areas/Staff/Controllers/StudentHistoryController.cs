using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace InstituteOfFineArts.Web.Areas.Staff.Controllers;

/// <summary>
/// Staff – study history of one student, read only (F-M07-07, task BE2-07; SCR-22):
/// GET /staff/students/{studentId}/history (every entry with rating, comments, award, exhibition entries).
/// </summary>
[Route("staff/students/{studentId:long}/history")]
public class StudentHistoryController : StaffBaseController
{
    /// <summary>Submission queries.</summary>
    private readonly IStaffSubmissionService _submissions;

    /// <summary>Receives the submission service through Dependency Injection.</summary>
    public StudentHistoryController(IStaffSubmissionService submissions)
    {
        _submissions = submissions;
    }

    /// <summary>History page; unknown student → 404.</summary>
    /// <param name="studentId">student_profile.id</param>
    [HttpGet("")]
    public async Task<IActionResult> Index(long studentId)
    {
        try
        {
            return View(await _submissions.GetStudentHistoryAsync(studentId));
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
    }
}
