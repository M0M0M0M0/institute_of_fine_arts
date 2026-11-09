using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Helpers;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.Services.Interfaces;
using InstituteOfFineArts.Web.ViewModels.Admin.Students;
using Microsoft.AspNetCore.DataProtection;

namespace InstituteOfFineArts.Web.Services.Implementations;

/// <summary>
/// Student CSV import (F-M04-08, task BE2-OPT 4). Each row becomes a <see cref="CreateStudentVm"/> and is checked with
/// the same rules as the "New student" form: the view model's DataAnnotations, then <see cref="IStudentService.CheckNewAsync"/>
/// (BR-01 unique username / email / code in the database, BR-02 password). Values repeated inside the file are refused too.
/// The valid rows are saved by <see cref="IStudentService.CreateManyAsync"/> in one transaction.
/// </summary>
public class StudentImportService : IStudentImportService
{
    /// <summary>Largest accepted file (1 MB).</summary>
    public const int MaxFileBytes = 1024 * 1024;

    /// <summary>Largest number of student rows in one file.</summary>
    public const int MaxRows = 500;

    /// <summary>How long the preview stays valid before step 2.</summary>
    public static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(30);

    /// <summary>No file chosen.</summary>
    public const string NoFileMessage = "Please choose a CSV file";

    /// <summary>File without the .csv extension.</summary>
    public const string WrongTypeMessage = "Please choose a .csv file";

    /// <summary>File over 1 MB.</summary>
    public const string TooLargeMessage = "The file must be 1 MB or smaller";

    /// <summary>File that is not valid UTF-8 text.</summary>
    public const string NotUtf8Message = "The file must be saved as UTF-8 text (in Excel: CSV UTF-8)";

    /// <summary>Empty file.</summary>
    public const string NoHeaderMessage = "The file is empty; the first row must contain the column names";

    /// <summary>Required columns missing from the header ({0} = names).</summary>
    public const string MissingColumnsMessage = "Missing required columns: {0}";

    /// <summary>Header with a name that is not a known column ({0} = name).</summary>
    public const string UnknownColumnMessage = "Unknown column: {0}";

    /// <summary>Header with the same column twice ({0} = name).</summary>
    public const string RepeatedColumnMessage = "Column {0} appears more than once";

    /// <summary>Header only.</summary>
    public const string NoRowsMessage = "The file contains no student rows";

    /// <summary>More than <see cref="MaxRows"/> rows.</summary>
    public static readonly string TooManyRowsMessage = $"The file has more than {MaxRows} student rows; please split it into smaller files";

    /// <summary>Row whose number of values differs from the header ({0} = values, {1} = columns).</summary>
    public const string FieldCountMessage = "The row has {0} values but the header has {1} columns";

    /// <summary>Unreadable date ({0} = column).</summary>
    public const string InvalidDateMessage = "{0} must be a date such as 2025-09-01 or 01/09/2025";

    /// <summary>Unknown gender.</summary>
    public const string InvalidGenderMessage = "Gender must be M, F or O";

    /// <summary>Unknown study status.</summary>
    public const string InvalidStatusMessage = "Status must be STUDYING, GRADUATED, SUSPENDED or DROPPED";

    /// <summary>Value used on several rows ({0} = column, {1} = value, {2} = other lines).</summary>
    public const string RepeatedInFileMessage = "{0} {1} is also used on line {2} of the file";

    /// <summary>Step 2 with a missing, tampered or expired token.</summary>
    public const string ExpiredMessage = "The preview has expired. Please upload the file again.";

    /// <summary>Step 2 when no row is valid any more.</summary>
    public const string NothingToImportMessage = "There are no valid rows to import.";

    /// <summary>Column names of the template, in order.</summary>
    public static readonly string[] Columns =
    [
        "StudentCode", "FullName", "Username", "Email", "Password", "Gender", "DateOfBirth", "Phone", "Address",
        "EnrollmentDate", "Course", "Class", "Status", "Notes",
    ];

    /// <summary>Columns that must be in the header (required fields of the create form).</summary>
    private static readonly string[] RequiredColumns = ["FullName", "Username", "Email", "Password", "EnrollmentDate", "Course"];

    /// <summary>Other accepted header spellings → column name.</summary>
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["CourseName"] = "Course",
        ["ClassName"] = "Class",
        ["EnrollmentStatus"] = "Status",
        ["StudyStatus"] = "Status",
        ["Code"] = "StudentCode",
        ["Name"] = "FullName",
    };

    /// <summary>Accepted date formats (ISO first, then the Vietnamese day/month/year order and the app's display format).</summary>
    private static readonly string[] DateFormats = ["yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy", "dd MMM yyyy", "d MMM yyyy"];

    /// <summary>Strict UTF-8 decoder: invalid bytes throw instead of becoming "?".</summary>
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>Create rules shared with the "New student" form.</summary>
    private readonly IStudentService _students;

    /// <summary>Encrypts the file content between step 1 and step 2.</summary>
    private readonly ITimeLimitedDataProtector _protector;

    /// <summary>Logs imports (never passwords or file contents).</summary>
    private readonly ILogger<StudentImportService> _logger;

    /// <summary>Receives the required services through Dependency Injection.</summary>
    public StudentImportService(IStudentService students, IDataProtectionProvider dataProtection, ILogger<StudentImportService> logger)
    {
        _students = students;
        _protector = dataProtection.CreateProtector("InstituteOfFineArts.StudentImport.v1").ToTimeLimitedDataProtector();
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<StudentImportPreviewVm> PreviewAsync(string? fileName, Stream? content, long length)
    {
        // File-level checks first: presence, extension, size
        if (content == null || length <= 0)
        {
            throw new BusinessException(NoFileMessage, "File");
        }
        if (!string.Equals(Path.GetExtension(fileName), ".csv", StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException(WrongTypeMessage, "File");
        }
        if (length > MaxFileBytes)
        {
            throw new BusinessException(TooLargeMessage, "File");
        }

        // Read at most one byte more than the limit, in case the announced length was wrong
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await content.ReadAsync(chunk)) > 0)
        {
            buffer.Write(chunk, 0, read);
            if (buffer.Length > MaxFileBytes)
            {
                throw new BusinessException(TooLargeMessage, "File");
            }
        }

        string text;
        try
        {
            text = StrictUtf8.GetString(buffer.ToArray());
        }
        catch (DecoderFallbackException)
        {
            throw new BusinessException(NotUtf8Message, "File");
        }

        var (preview, _) = await CheckAsync(text);
        preview.FileName = Path.GetFileName(fileName ?? string.Empty);
        if (preview.ValidCount > 0)
        {
            preview.Token = _protector.Protect(text, TokenLifetime);
        }
        _logger.LogInformation("Student import preview of {File}: {Valid} valid, {Invalid} invalid rows",
            preview.FileName, preview.ValidCount, preview.InvalidCount);
        return preview;
    }

    /// <inheritdoc />
    public async Task<StudentImportResultVm> ImportAsync(string? token)
    {
        // The token carries the previewed file; a tampered or expired token cannot be read
        string text;
        try
        {
            text = _protector.Unprotect(token ?? string.Empty);
        }
        catch (CryptographicException)
        {
            throw new BusinessException(ExpiredMessage);
        }

        // Check again: another administrator may have created one of the usernames since the preview
        var (preview, forms) = await CheckAsync(text);
        if (forms.Count == 0)
        {
            throw new BusinessException(NothingToImportMessage);
        }

        var imported = await _students.CreateManyAsync(forms);
        _logger.LogInformation("Student import: {Imported} created, {Skipped} invalid rows skipped", imported, preview.InvalidCount);
        return new StudentImportResultVm { Imported = imported, Skipped = preview.InvalidCount };
    }

    /// <inheritdoc />
    public byte[] GetTemplate()
    {
        var example = new[]
        {
            "", "Nguyen Van An", "nguyenvanan", "nguyenvanan@example.com", "Welcome2026", "M", "2004-05-01", "0901234567",
            "12 Le Loi, District 1", "2025-09-01", "Painting", "PT03", "STUDYING", "Example row - replace or delete it",
        };
        var csv = string.Join(",", Columns) + "\r\n" + string.Join(",", example.Select(CsvParser.Escape)) + "\r\n";
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray();
    }

    /// <summary>
    /// Parses the text and checks every row. Returns the preview and the forms of the valid rows (file order).
    /// </summary>
    /// <param name="text">Decoded file content</param>
    private async Task<(StudentImportPreviewVm Preview, List<CreateStudentVm> ValidForms)> CheckAsync(string text)
    {
        List<CsvRecord> records;
        try
        {
            records = CsvParser.Parse(text);
        }
        catch (FormatException ex)
        {
            throw new BusinessException(ex.Message, "File");
        }
        if (records.Count == 0)
        {
            throw new BusinessException(NoHeaderMessage, "File");
        }

        var columns = ReadHeader(records[0]);
        var dataRows = records.Skip(1).ToList();
        if (dataRows.Count == 0)
        {
            throw new BusinessException(NoRowsMessage, "File");
        }
        if (dataRows.Count > MaxRows)
        {
            throw new BusinessException(TooManyRowsMessage, "File");
        }

        // 1) Each row on its own: values → form, then the form's DataAnnotations
        var preview = new StudentImportPreviewVm();
        var forms = new List<CreateStudentVm?>();
        foreach (var record in dataRows)
        {
            var row = new StudentImportRowVm { Line = record.Line };
            preview.Rows.Add(row);
            forms.Add(ReadRow(record, columns, row));
        }

        // 2) Values repeated inside the file (same normalisation as the service: username/email ignore case, code upper case)
        MarkRepeated(preview.Rows, forms, "Username", f => f.NewUsername.Trim().ToLowerInvariant());
        MarkRepeated(preview.Rows, forms, "Email", f => f.Email.Trim().ToLowerInvariant());
        MarkRepeated(preview.Rows, forms, "Student code", f => string.IsNullOrWhiteSpace(f.StudentCode) ? null : f.StudentCode.Trim().ToUpperInvariant());

        // 3) Database rules of the create form, only for rows still valid (saves queries)
        var valid = new List<CreateStudentVm>();
        for (var i = 0; i < forms.Count; i++)
        {
            if (forms[i] is not { } form || !preview.Rows[i].IsValid)
            {
                continue;
            }
            foreach (var error in await _students.CheckNewAsync(form))
            {
                preview.Rows[i].Errors.Add(error.Message);
            }
            if (preview.Rows[i].IsValid)
            {
                valid.Add(form);
            }
        }
        return (preview, valid);
    }

    /// <summary>Maps each header cell to a column name; refuses unknown, repeated and missing required columns.</summary>
    /// <param name="header">First record of the file</param>
    private static string[] ReadHeader(CsvRecord header)
    {
        var columns = new string[header.Fields.Count];
        for (var i = 0; i < header.Fields.Count; i++)
        {
            // "Full name", "full_name" and "FullName" are the same column
            var key = new string(header.Fields[i].Where(char.IsLetterOrDigit).ToArray());
            var name = Columns.FirstOrDefault(c => c.Equals(key, StringComparison.OrdinalIgnoreCase))
                       ?? (Aliases.TryGetValue(key, out var alias) ? alias : null)
                       ?? throw new BusinessException(string.Format(UnknownColumnMessage, header.Fields[i].Trim()), "File");
            if (columns.Contains(name))
            {
                throw new BusinessException(string.Format(RepeatedColumnMessage, name), "File");
            }
            columns[i] = name;
        }

        var missing = RequiredColumns.Where(r => !columns.Contains(r)).ToList();
        if (missing.Count > 0)
        {
            throw new BusinessException(string.Format(MissingColumnsMessage, string.Join(", ", missing)), "File");
        }
        return columns;
    }

    /// <summary>
    /// Copies one record into a create form and validates it with the form's DataAnnotations (same messages as the page).
    /// Returns null when the row cannot be read at all (wrong number of values).
    /// </summary>
    private static CreateStudentVm? ReadRow(CsvRecord record, string[] columns, StudentImportRowVm row)
    {
        if (record.Fields.Count != columns.Length)
        {
            row.Errors.Add(string.Format(FieldCountMessage, record.Fields.Count, columns.Length));
            return null;
        }

        var values = new Dictionary<string, string>();
        for (var i = 0; i < columns.Length; i++)
        {
            values[columns[i]] = record.Fields[i].Trim();
        }
        string? Get(string column) => values.TryGetValue(column, out var v) && v.Length > 0 ? v : null;

        var form = new CreateStudentVm
        {
            StudentCode = Get("StudentCode"),
            FullName = Get("FullName") ?? string.Empty,
            NewUsername = Get("Username") ?? string.Empty,
            Email = Get("Email") ?? string.Empty,
            // The file has the password once; the confirmation is the same value
            Password = Get("Password") ?? string.Empty,
            ConfirmPassword = Get("Password") ?? string.Empty,
            Phone = Get("Phone"),
            Address = Get("Address"),
            CourseName = Get("Course") ?? string.Empty,
            ClassName = Get("Class"),
            Notes = Get("Notes"),
        };
        row.StudentCode = form.StudentCode;
        row.FullName = form.FullName;
        row.Username = form.NewUsername;
        row.Email = form.Email;
        row.CourseName = form.CourseName;
        row.ClassName = form.ClassName;

        // Typed values: dates and enums (empty = not given; the form's [Required] reports missing required ones)
        form.DateOfBirth = ReadDate(Get("DateOfBirth"), "Date of birth", row);
        form.EnrollmentDate = ReadDate(Get("EnrollmentDate"), "Enrollment date", row);
        if (Get("Gender") is { } gender)
        {
            if (Enum.TryParse<Gender>(gender, ignoreCase: true, out var g) && Enum.IsDefined(g))
            {
                form.Gender = g;
            }
            else
            {
                row.Errors.Add(InvalidGenderMessage);
            }
        }
        if (Get("Status") is { } status)
        {
            if (Enum.TryParse<EnrollmentStatus>(status, ignoreCase: true, out var s) && Enum.IsDefined(s))
            {
                form.EnrollmentStatus = s;
            }
            else
            {
                row.Errors.Add(InvalidStatusMessage);
            }
        }

        // Same DataAnnotations (and IValidatableObject date rule) as the "New student" form
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(form, new ValidationContext(form), results, validateAllProperties: true);
        row.Errors.AddRange(results.Select(r => r.ErrorMessage ?? string.Empty).Where(m => m.Length > 0));
        return form;
    }

    /// <summary>Parses an optional date in one of the accepted formats; adds an error when it cannot be read.</summary>
    private static DateOnly? ReadDate(string? value, string label, StudentImportRowVm row)
    {
        if (value == null)
        {
            return null;
        }
        if (DateOnly.TryParseExact(value, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return date;
        }
        row.Errors.Add(string.Format(InvalidDateMessage, label));
        return null;
    }

    /// <summary>Adds an error to every row whose value (after normalisation) is also used by another row of the file.</summary>
    private static void MarkRepeated(List<StudentImportRowVm> rows, List<CreateStudentVm?> forms, string label,
        Func<CreateStudentVm, string?> key)
    {
        // Lines per value, rows that could not be read are ignored
        var lines = new Dictionary<string, List<int>>();
        for (var i = 0; i < forms.Count; i++)
        {
            if (forms[i] is { } form && key(form) is { Length: > 0 } value)
            {
                if (!lines.TryGetValue(value, out var list))
                {
                    lines[value] = list = new List<int>();
                }
                list.Add(i);
            }
        }

        foreach (var (value, indexes) in lines.Where(l => l.Value.Count > 1))
        {
            foreach (var i in indexes)
            {
                var others = string.Join(", ", indexes.Where(j => j != i).Select(j => rows[j].Line));
                rows[i].Errors.Add(string.Format(RepeatedInFileMessage, label, value, others));
            }
        }
    }
}
