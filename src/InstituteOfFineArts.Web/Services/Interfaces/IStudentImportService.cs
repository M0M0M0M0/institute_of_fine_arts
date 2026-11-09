using InstituteOfFineArts.Web.ViewModels.Admin.Students;

namespace InstituteOfFineArts.Web.Services.Interfaces;

/// <summary>
/// Import of student accounts from a CSV file (F-M04-08, task BE2-OPT 4), in two steps:
/// preview (every row checked with the create-student rules) and import of the valid rows in one transaction.
/// File-level problems (no file, too large, not UTF-8, bad header, too many rows) throw BusinessException.
/// </summary>
public interface IStudentImportService
{
    /// <summary>Reads and checks an uploaded file without saving anything.</summary>
    /// <param name="fileName">Original file name (must end with .csv)</param>
    /// <param name="content">File content (null = no file)</param>
    /// <param name="length">File size in bytes</param>
    Task<StudentImportPreviewVm> PreviewAsync(string? fileName, Stream? content, long length);

    /// <summary>
    /// Checks the previewed file again (the database may have changed) and creates the valid rows in one transaction.
    /// </summary>
    /// <param name="token">Token returned by the preview</param>
    Task<StudentImportResultVm> ImportAsync(string? token);

    /// <summary>Template file (UTF-8 with BOM so Excel shows accents) with the header row and one example row.</summary>
    byte[] GetTemplate();
}
