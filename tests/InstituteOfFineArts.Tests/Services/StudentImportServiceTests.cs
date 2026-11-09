using System.Text;
using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.Services.Implementations;
using InstituteOfFineArts.Web.ViewModels.Admin.Students;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InstituteOfFineArts.Tests.Services;

/// <summary>
/// StudentImportService (task BE2-OPT 4, F-M04-08): CSV preview with the create-student rules (DataAnnotations, BR-01, BR-02),
/// duplicates in the file and in the database, file limits, and import of the valid rows in one save.
/// </summary>
public class StudentImportServiceTests
{
    /// <summary>Header of the template.</summary>
    private const string Header = "StudentCode,FullName,Username,Email,Password,Gender,DateOfBirth,Phone,Address,EnrollmentDate,Course,Class,Status,Notes";

    /// <summary>Database with student01 (HV0001, student01@ifa.local).</summary>
    private static AppDbContext CreateDb()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.StudentProfiles.Add(new StudentProfile
        {
            StudentCode = "HV0001", EnrollmentDate = new DateOnly(2025, 1, 1), CourseName = "Painting", EnrollmentStatus = EnrollmentStatus.STUDYING,
            Account = new Account
            {
                Username = "student01", FullName = "Nguyen Minh Khoa", Email = "student01@ifa.local", Role = Role.STUDENT,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Student@123", workFactor: 4), Active = true,
            },
        });
        db.SaveChanges();
        return db;
    }

    /// <summary>Service under test with the real StudentService (same rules as the create form).</summary>
    private static StudentImportService Service(AppDbContext db)
        => new(new StudentService(db, NullLogger<StudentService>.Instance), new EphemeralDataProtectionProvider(),
            NullLogger<StudentImportService>.Instance);

    /// <summary>One valid CSV row.</summary>
    private static string Row(string username, string email = "", string code = "", string name = "Tran Thi Mai",
        string password = "Welcome2026", string dob = "2004-05-01", string gender = "F", string enrolled = "2025-09-01",
        string address = "12 Le Loi")
        => $"{code},{name},{username},{(email.Length > 0 ? email : username + "@example.com")},{password},{gender},{dob},0901234567,{address},{enrolled},Painting,PT03,STUDYING,";

    /// <summary>Runs the preview on a UTF-8 text.</summary>
    private static Task<StudentImportPreviewVm> PreviewAsync(StudentImportService service, string csv, string fileName = "students.csv")
    {
        var bytes = Encoding.UTF8.GetBytes(csv);
        return service.PreviewAsync(fileName, new MemoryStream(bytes), bytes.Length);
    }

    // ------------------------------------------------------------------ preview

    /// <summary>Valid rows: all OK, a token for step 2, nothing saved yet.</summary>
    [Fact]
    public async Task Preview_ValidRows()
    {
        using var db = CreateDb();
        var preview = await PreviewAsync(Service(db), $"{Header}\r\n{Row("e2e.one")}\r\n{Row("e2e.two", code: "hv0100")}\r\n");

        Assert.Equal(2, preview.ValidCount);
        Assert.Equal(0, preview.InvalidCount);
        Assert.NotNull(preview.Token);
        Assert.Equal(new[] { 2, 3 }, preview.Rows.Select(r => r.Line));
        Assert.Equal("students.csv", preview.FileName);
        Assert.Equal(1, await db.StudentProfiles.CountAsync());
    }

    /// <summary>Each row is checked with the create form's DataAnnotations: same messages as the page.</summary>
    [Fact]
    public async Task Preview_ValidationErrors()
    {
        using var db = CreateDb();
        var csv = string.Join("\n",
            Header,
            Row("bad.email", email: "not-an-email"),
            Row("no.name", name: ""),
            Row("weak.pass", password: "short"),
            Row("future.dob", dob: DateTime.Today.AddYears(1).ToString("yyyy-MM-dd")),
            Row("bad.date", enrolled: "31/31/2025"),
            Row("bad.gender", gender: "X"),
            Row("x"));
        var rows = (await PreviewAsync(Service(db), csv)).Rows;

        Assert.Contains("Please enter a valid email address", rows[0].Errors);
        Assert.Contains("Please enter the full name", rows[1].Errors);
        Assert.Contains(rows[2].Errors, e => e.StartsWith("Password must be"));
        Assert.Contains(StudentDateMessages.DateOfBirthInPast, rows[3].Errors);
        Assert.Contains(string.Format(StudentImportService.InvalidDateMessage, "Enrollment date"), rows[4].Errors);
        Assert.Contains("Please choose the enrollment date", rows[4].Errors);
        Assert.Contains(StudentImportService.InvalidGenderMessage, rows[5].Errors);
        Assert.Contains("Username must be 3–50 characters", rows[6].Errors);
        Assert.All(rows, r => Assert.False(r.IsValid));
    }

    /// <summary>A row with a wrong number of values is reported and not read further.</summary>
    [Fact]
    public async Task Preview_WrongFieldCount()
    {
        using var db = CreateDb();
        var preview = await PreviewAsync(Service(db), $"{Header}\nonly,three,values\n{Row("ok.user")}");

        Assert.Equal(string.Format(StudentImportService.FieldCountMessage, 3, 14), Assert.Single(preview.Rows[0].Errors));
        Assert.True(preview.Rows[1].IsValid);
    }

    /// <summary>Username (any case), email and code used twice in the file: both rows are refused.</summary>
    [Fact]
    public async Task Preview_DuplicatesInFile()
    {
        using var db = CreateDb();
        var csv = string.Join("\n", Header,
            Row("same.user", email: "a@example.com", code: "HV0200"),
            Row("Same.User", email: "b@example.com", code: "HV0201"),
            Row("other1", email: "dup@example.com", code: "hv0300"),
            Row("other2", email: "DUP@example.com", code: "HV0300"),
            Row("fine.user"));
        var rows = (await PreviewAsync(Service(db), csv)).Rows;

        Assert.Contains(string.Format(StudentImportService.RepeatedInFileMessage, "Username", "same.user", 3), rows[0].Errors);
        Assert.Contains(string.Format(StudentImportService.RepeatedInFileMessage, "Username", "same.user", 2), rows[1].Errors);
        Assert.Contains(string.Format(StudentImportService.RepeatedInFileMessage, "Email", "dup@example.com", 5), rows[2].Errors);
        Assert.Contains(string.Format(StudentImportService.RepeatedInFileMessage, "Student code", "HV0300", 4), rows[3].Errors);
        Assert.True(rows[4].IsValid);
    }

    /// <summary>BR-01 against the database: existing username, email (any case) and code are refused with the create form messages.</summary>
    [Fact]
    public async Task Preview_DuplicatesInDb()
    {
        using var db = CreateDb();
        var csv = string.Join("\n", Header,
            Row("STUDENT01"),
            Row("new.one", email: "Student01@IFA.local"),
            Row("new.two", code: "hv0001"));
        var rows = (await PreviewAsync(Service(db), csv)).Rows;

        Assert.Equal(new[] { StudentService.UsernameTakenMessage }, rows[0].Errors);
        Assert.Equal(new[] { StudentService.EmailTakenMessage }, rows[1].Errors);
        Assert.Equal(new[] { StudentService.CodeTakenMessage }, rows[2].Errors);
    }

    /// <summary>BOM, quoted address with a comma and a line break, header names with spaces / other case.</summary>
    [Fact]
    public async Task Preview_BomQuotesAndHeaderSpelling()
    {
        using var db = CreateDb();
        var csv = "﻿full name,USERNAME,E-mail,password,Enrollment Date,course name,Address\r\n"
                  + "\"Nguyễn Văn An\",an.nguyen,an@example.com,Welcome2026,01/09/2025,Painting,\"12 Le Loi, District 1\r\nHo Chi Minh City\"\r\n"
                  + "Le Van Binh,binh.le,binh@example.com,Welcome2026,2025-09-01,Painting,\r\n";
        var preview = await PreviewAsync(Service(db), csv);

        Assert.Equal(2, preview.ValidCount);
        Assert.Equal("Nguyễn Văn An", preview.Rows[0].FullName);
        Assert.Equal(4, preview.Rows[1].Line);
    }

    /// <summary>Header problems refuse the whole file.</summary>
    [Theory]
    [InlineData("FullName,Username,Email,Password,Course", "Missing required columns: EnrollmentDate")]
    [InlineData("FullName,Username,Email,Password,EnrollmentDate,Course,Nickname", "Unknown column: Nickname")]
    [InlineData("FullName,Username,Email,Password,EnrollmentDate,Course,Class,ClassName", "Column Class appears more than once")]
    public async Task Preview_BadHeader_Refused(string header, string message)
    {
        using var db = CreateDb();
        var ex = await Assert.ThrowsAsync<BusinessException>(() => PreviewAsync(Service(db), header + "\nx,y,z,w,v,u,t,s"));
        Assert.Equal(message, ex.Message);
    }

    /// <summary>File-level limits: no file, not .csv, over 1 MB, not UTF-8, header only, unclosed quote.</summary>
    [Fact]
    public async Task Preview_FileChecks()
    {
        using var db = CreateDb();
        var service = Service(db);

        Assert.Equal(StudentImportService.NoFileMessage,
            (await Assert.ThrowsAsync<BusinessException>(() => service.PreviewAsync(null, null, 0))).Message);
        Assert.Equal(StudentImportService.WrongTypeMessage,
            (await Assert.ThrowsAsync<BusinessException>(() => PreviewAsync(service, Header, "students.xlsx"))).Message);
        Assert.Equal(StudentImportService.TooLargeMessage,
            (await Assert.ThrowsAsync<BusinessException>(() => service.PreviewAsync("big.csv", new MemoryStream(), StudentImportService.MaxFileBytes + 1))).Message);
        var latin1 = new byte[] { (byte)'a', (byte)'\n', 0xE9, (byte)'t', (byte)'\xE9' };
        Assert.Equal(StudentImportService.NotUtf8Message,
            (await Assert.ThrowsAsync<BusinessException>(() => service.PreviewAsync("x.csv", new MemoryStream(latin1), latin1.Length))).Message);
        Assert.Equal(StudentImportService.NoRowsMessage,
            (await Assert.ThrowsAsync<BusinessException>(() => PreviewAsync(service, Header + "\r\n"))).Message);
        Assert.Equal(string.Format(Web.Helpers.CsvParser.UnclosedQuoteMessage, 2),
            (await Assert.ThrowsAsync<BusinessException>(() => PreviewAsync(service, Header + "\n\"open"))).Message);
    }

    /// <summary>At most 500 rows: 500 are accepted, 501 refuse the file.</summary>
    [Fact]
    public async Task Preview_MaxRows()
    {
        using var db = CreateDb();
        var service = Service(db);
        string Csv(int n) => Header + "\n" + string.Join("\n", Enumerable.Range(1, n).Select(i => Row($"bulk{i:D3}")));

        var ok = await PreviewAsync(service, Csv(StudentImportService.MaxRows));
        Assert.Equal(StudentImportService.MaxRows, ok.ValidCount);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => PreviewAsync(service, Csv(StudentImportService.MaxRows + 1)));
        Assert.Equal(StudentImportService.TooManyRowsMessage, ex.Message);
    }

    /// <summary>No valid row: no token, so step 2 cannot be posted.</summary>
    [Fact]
    public async Task Preview_NoValidRow_NoToken()
    {
        using var db = CreateDb();
        var preview = await PreviewAsync(Service(db), $"{Header}\n{Row("student01")}");
        Assert.Null(preview.Token);
    }

    // ------------------------------------------------------------------ import

    /// <summary>Step 2: every valid row is created (codes generated in order, BCrypt passwords), invalid rows skipped.</summary>
    [Fact]
    public async Task Import_SavesAllValidRows()
    {
        using var db = CreateDb();
        var service = Service(db);
        var csv = string.Join("\r\n", Header, Row("e2e.a"), Row("e2e.b"), Row("bad", email: "nope"), Row("e2e.c", code: "pt-77"));
        var preview = await PreviewAsync(service, csv);

        var result = await service.ImportAsync(preview.Token);

        Assert.Equal(3, result.Imported);
        Assert.Equal(1, result.Skipped);
        var created = await db.StudentProfiles.Include(s => s.Account).Where(s => s.Account.Username.StartsWith("e2e."))
            .OrderBy(s => s.Account.Username).ToListAsync();
        Assert.Equal(new[] { "HV0002", "HV0003", "PT-77" }, created.Select(s => s.StudentCode));
        Assert.All(created, s => Assert.Equal(Role.STUDENT, s.Account.Role));
        Assert.True(BCrypt.Net.BCrypt.Verify("Welcome2026", created[0].Account.PasswordHash));
        Assert.Equal("PT03", created[0].ClassName);
        Assert.Equal(new DateOnly(2004, 5, 1), created[0].Account.DateOfBirth);
    }

    /// <summary>Step 2 checks the rows again: a username taken since the preview is skipped, the others are created.</summary>
    [Fact]
    public async Task Import_RechecksDatabase()
    {
        using var db = CreateDb();
        var service = Service(db);
        var preview = await PreviewAsync(service, string.Join("\n", Header, Row("late.one"), Row("late.two")));
        await new StudentService(db, NullLogger<StudentService>.Instance).CreateAsync(new CreateStudentVm
        {
            NewUsername = "late.two", Password = "Welcome2026", ConfirmPassword = "Welcome2026", FullName = "Someone",
            Email = "someone@example.com", EnrollmentDate = new DateOnly(2025, 9, 1), CourseName = "Painting",
        });

        var result = await service.ImportAsync(preview.Token);

        Assert.Equal(1, result.Imported);
        Assert.Equal(1, result.Skipped);
    }

    /// <summary>A missing or tampered token is refused and nothing is saved.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-token")]
    public async Task Import_BadToken_Refused(string? token)
    {
        using var db = CreateDb();
        var ex = await Assert.ThrowsAsync<BusinessException>(() => Service(db).ImportAsync(token));
        Assert.Equal(StudentImportService.ExpiredMessage, ex.Message);
        Assert.Equal(1, await db.StudentProfiles.CountAsync());
    }

    /// <summary>When no row is valid any more at step 2, nothing is imported.</summary>
    [Fact]
    public async Task Import_NothingValid_Refused()
    {
        using var db = CreateDb();
        var service = Service(db);
        var preview = await PreviewAsync(service, $"{Header}\n{Row("gone.soon")}");
        db.StudentProfiles.Add(new StudentProfile
        {
            StudentCode = "HV0500", EnrollmentDate = new DateOnly(2025, 1, 1), CourseName = "Painting",
            Account = new Account { Username = "gone.soon", FullName = "X", Email = "x@example.com", Role = Role.STUDENT, PasswordHash = "x" },
        });
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<BusinessException>(() => service.ImportAsync(preview.Token));
        Assert.Equal(StudentImportService.NothingToImportMessage, ex.Message);
    }

    /// <summary>The template has the column header (with BOM) and can itself be previewed as one valid row.</summary>
    [Fact]
    public async Task Template_IsValidCsv()
    {
        using var db = CreateDb();
        var service = Service(db);
        var bytes = service.GetTemplate();

        Assert.Equal(Encoding.UTF8.GetPreamble(), bytes.Take(3));
        var preview = await service.PreviewAsync("students-template.csv", new MemoryStream(bytes), bytes.Length);
        Assert.Equal(1, preview.ValidCount);
    }
}
