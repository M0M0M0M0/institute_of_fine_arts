namespace InstituteOfFineArts.Web.ViewModels.Admin.Students;

/// <summary>One data row of an uploaded student CSV file as shown in the import preview (F-M04-08).</summary>
public class StudentImportRowVm
{
    /// <summary>Line of the file where the row starts (the header is line 1).</summary>
    public int Line { get; set; }

    /// <summary>Student code from the file (empty → generated HVnnnn on import).</summary>
    public string? StudentCode { get; set; }

    /// <summary>Full name.</summary>
    public string? FullName { get; set; }

    /// <summary>Username.</summary>
    public string? Username { get; set; }

    /// <summary>Email.</summary>
    public string? Email { get; set; }

    /// <summary>Course.</summary>
    public string? CourseName { get; set; }

    /// <summary>Class.</summary>
    public string? ClassName { get; set; }

    /// <summary>Every problem found on the row (empty = the row will be imported).</summary>
    public List<string> Errors { get; } = new();

    /// <summary>True when the row has no error.</summary>
    public bool IsValid => Errors.Count == 0;
}

/// <summary>Step 1 of the import: the checked rows of the uploaded file.</summary>
public class StudentImportPreviewVm
{
    /// <summary>Name of the uploaded file.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>All data rows in file order.</summary>
    public List<StudentImportRowVm> Rows { get; } = new();

    /// <summary>Number of rows that will be imported.</summary>
    public int ValidCount => Rows.Count(r => r.IsValid);

    /// <summary>Number of rows with errors (skipped by the import).</summary>
    public int InvalidCount => Rows.Count(r => !r.IsValid);

    /// <summary>
    /// The file content, encrypted and time-limited (ASP.NET Core Data Protection), posted back by step 2;
    /// null when no row is valid. Passwords from the file therefore never appear in the page.
    /// </summary>
    public string? Token { get; set; }
}

/// <summary>Step 2 of the import: what was saved.</summary>
public class StudentImportResultVm
{
    /// <summary>Students created.</summary>
    public int Imported { get; set; }

    /// <summary>Rows skipped because of errors.</summary>
    public int Skipped { get; set; }
}
