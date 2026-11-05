using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Helpers;
using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace InstituteOfFineArts.Tests.Services;

/// <summary>
/// In-memory test data shared by StudentExhibitionEntryServiceTests and ExhibitionEntryServiceTests: one staff member,
/// two students, one GRADED and one CLOSED competition, exhibitions in every status and one customer.
/// </summary>
public sealed class ExhibitionEntryTestData : IDisposable
{
    /// <summary>Today in the institute time zone.</summary>
    public static readonly DateOnly Today = DateTimeHelper.Today;

    /// <summary>Creates the database and the base rows.</summary>
    public ExhibitionEntryTestData()
    {
        Db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        // People: staff01, student01 (Nguyen Van An, HV0001), student02 (Tran Thi Binh, HV0002)
        var staff = new StaffProfile
        {
            StaffCode = "NV0003", JoinedDate = new DateOnly(2024, 1, 1),
            Account = new Account { Username = "staff01", FullName = "Le Minh Hoang", Email = "s1@ifa.local", Role = Role.STAFF, PasswordHash = "x" },
        };
        var student1 = new StudentProfile
        {
            StudentCode = "HV0001", EnrollmentDate = new DateOnly(2025, 9, 1), CourseName = "Painting",
            Account = new Account { Username = "student01", FullName = "Nguyen Van An", Email = "st1@ifa.local", Role = Role.STUDENT, PasswordHash = "x" },
        };
        var student2 = new StudentProfile
        {
            StudentCode = "HV0002", EnrollmentDate = new DateOnly(2025, 9, 1), CourseName = "Painting",
            Account = new Account { Username = "student02", FullName = "Tran Thi Binh", Email = "st2@ifa.local", Role = Role.STUDENT, PasswordHash = "x" },
        };
        Db.StaffProfiles.Add(staff);
        Db.StudentProfiles.AddRange(student1, student2);
        Db.SaveChanges();
        StaffId = staff.Id;
        StaffAccountId = staff.AccountId;
        Student1 = student1.Id;
        Student1Account = student1.AccountId;
        Student2 = student2.Id;

        // Competitions: results published (GRADED) and closed but not published yet
        Graded = new Competition
        {
            Code = "C1", Title = "Hometown Landscapes", StartDate = Today.AddDays(-60), EndDate = Today.AddDays(-40),
            Status = CompetitionStatus.GRADED, CreatedBy = StaffId,
        };
        Closed = new Competition
        {
            Code = "C2", Title = "City at Night", StartDate = Today.AddDays(-30), EndDate = Today.AddDays(-10),
            Status = CompetitionStatus.CLOSED, CreatedBy = StaffId,
        };
        Db.Competitions.AddRange(Graded, Closed);

        // Exhibitions in each calculated status
        Upcoming = NewExhibition("Saigon Spring Exhibition", 20, 27);
        Upcoming2 = NewExhibition("Da Nang Summer Exhibition", 30, 35);
        Ongoing = NewExhibition("Hue Autumn Exhibition", -3, 4);
        Finished = NewExhibition("Young Artists Exhibition (Hanoi)", -20, -5);
        Db.Exhibitions.AddRange(Upcoming, Upcoming2, Ongoing, Finished);

        Customer = new Customer { FullName = "Hoang Anh Tuan", Phone = "0901000001", Email = "khachhang1@example.com" };
        Db.Customers.Add(Customer);
        Db.SaveChanges();
    }

    /// <summary>The database.</summary>
    public AppDbContext Db { get; }

    /// <summary>staff_profile.id of staff01.</summary>
    public long StaffId { get; }

    /// <summary>account.id of staff01.</summary>
    public long StaffAccountId { get; }

    /// <summary>student_profile.id of student01.</summary>
    public long Student1 { get; }

    /// <summary>account.id of student01.</summary>
    public long Student1Account { get; }

    /// <summary>student_profile.id of student02.</summary>
    public long Student2 { get; }

    /// <summary>Competition with published results.</summary>
    public Competition Graded { get; }

    /// <summary>Competition evaluated but not published.</summary>
    public Competition Closed { get; }

    /// <summary>Exhibition starting in 20 days.</summary>
    public Exhibition Upcoming { get; }

    /// <summary>Second exhibition starting in 30 days.</summary>
    public Exhibition Upcoming2 { get; }

    /// <summary>Exhibition running today.</summary>
    public Exhibition Ongoing { get; }

    /// <summary>Exhibition that ended 5 days ago.</summary>
    public Exhibition Finished { get; }

    /// <summary>A customer.</summary>
    public Customer Customer { get; }

    /// <summary>Adds a painting of <paramref name="studentId"/> with an evaluation (null = not evaluated).</summary>
    public Submission AddPainting(long studentId, Rating? rating, Competition? competition = null, string title = "Rice Fields")
    {
        var s = new Submission
        {
            CompetitionId = (competition ?? Graded).Id, StudentId = studentId, Title = title, ImagePath = "p.jpg",
            OriginalFileName = "p.jpg", FileSize = 10, TextContent = "A poem", Description = "Why I painted it",
            SubmittedAt = Today.AddDays(-50).ToDateTime(new TimeOnly(10, 0)),
        };
        Db.Submissions.Add(s);
        Db.SaveChanges();
        if (rating is { } r)
        {
            Db.Evaluations.Add(new Evaluation { SubmissionId = s.Id, EvaluatedBy = StaffId, Rating = r, PositivePoints = "Nice", EvaluatedAt = DateTime.Now });
            Db.SaveChanges();
        }
        return s;
    }

    /// <summary>Adds an entry of the painting in the exhibition with the given status (SOLD gets a sale, optionally paid).</summary>
    public ExhibitionEntry AddEntry(Submission s, Exhibition exhibition, EntryStatus status, decimal askingPrice = 200m,
        bool paid = false, long? proposedBy = null)
    {
        var entry = new ExhibitionEntry
        {
            ExhibitionId = exhibition.Id, SubmissionId = s.Id, AskingPrice = askingPrice, Status = status,
            ProposedBy = proposedBy ?? Db.StudentProfiles.Where(p => p.Id == s.StudentId).Select(p => p.AccountId).Single(),
            RejectReason = status == EntryStatus.REJECTED ? "Does not fit the theme" : null,
        };
        if (status == EntryStatus.SOLD)
        {
            entry.SoldPrice = 250m;
            entry.SoldDate = exhibition.StartDate;
            entry.CustomerId = Customer.Id;
            if (paid)
            {
                entry.PaidToStudent = true;
                entry.PaidAmount = 250m;
                entry.PaidDate = exhibition.StartDate;
            }
        }
        Db.ExhibitionEntries.Add(entry);
        Db.SaveChanges();
        return entry;
    }

    /// <summary>Reloads an entry from the database (no tracking).</summary>
    public ExhibitionEntry? Reload(long id) => Db.ExhibitionEntries.AsNoTracking().FirstOrDefault(e => e.Id == id);

    /// <summary>Disposes the database.</summary>
    public void Dispose() => Db.Dispose();

    /// <summary>Exhibition from <paramref name="startIn"/> to <paramref name="endIn"/> days from today.</summary>
    private Exhibition NewExhibition(string name, int startIn, int endIn) => new()
    {
        Name = name, Venue = "Fine Arts Museum", City = "Ho Chi Minh City",
        StartDate = Today.AddDays(startIn), EndDate = Today.AddDays(endIn), CreatedBy = StaffId,
    };
}
