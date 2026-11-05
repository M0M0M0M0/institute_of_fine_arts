using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Helpers;
using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.Services.Implementations;
using InstituteOfFineArts.Web.ViewModels.Reports;
using Microsoft.EntityFrameworkCore;

namespace InstituteOfFineArts.Tests.Services;

/// <summary>
/// CompetitionReportService (task BE2-12 part 1; F-M10-01/02/03/06; BA doc §7; TC-M10-01..04): RPT-01 date bounds,
/// rating / class filters and totals per rating; RPT-02 counts, participation of active students, status / period
/// filters and the BR-12 non-submitter list; RPT-03 filters and total prize value; RPT-06 totals of one student.
/// Fixed dates (the reports filter on stored dates), with "today" passed explicitly where the status matters.
/// </summary>
public class CompetitionReportServiceTests
{
    /// <summary>Day used as "today" for the RPT-02 status (A GRADED, B CLOSED, C UPCOMING).</summary>
    private static readonly DateOnly Today = new(2026, 3, 10);

    /// <summary>The sample data of one test database.</summary>
    private sealed class Data
    {
        /// <summary>The database.</summary>
        public AppDbContext Db { get; init; } = null!;

        /// <summary>Students by short name (Dung has no entries, Em is locked).</summary>
        public Dictionary<string, StudentProfile> Students { get; } = new();

        /// <summary>GRADED competition A, January 2026.</summary>
        public Competition A { get; set; } = null!;

        /// <summary>Competition B, February 2026 (stored ONGOING, CLOSED by its dates).</summary>
        public Competition B { get; set; } = null!;

        /// <summary>Competition C, April 2026 (UPCOMING, no entries).</summary>
        public Competition C { get; set; } = null!;

        /// <summary>Submissions by key (a1..a4, b1, b2).</summary>
        public Dictionary<string, Submission> Entries { get; } = new();
    }

    /// <summary>
    /// In-memory database. A: a1 An BEST 05 Jan 00:00:00, a2 Binh BEST 10 Jan 23:59:59, a3 Cuong BETTER 11 Jan 00:00:00,
    /// a4 Em (locked) not graded. B: b1 An GOOD, b2 Cuong not graded. Awards: A First (300) → a1 on 05 Feb,
    /// A Second (200) → a2 on 06 Feb, B First (300) → b1 on 05 Mar. Exhibition: a1 SOLD for 260, b1 ACCEPTED.
    /// </summary>
    private static Data CreateData()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var data = new Data { Db = db };

        var staff = new StaffProfile
        {
            StaffCode = "NV0003", JoinedDate = new DateOnly(2024, 1, 1),
            Account = new Account { Username = "staff01", FullName = "Le Minh Hoang", Email = "s@ifa.local", Role = Role.STAFF, PasswordHash = "x" },
        };
        db.StaffProfiles.Add(staff);
        foreach (var (key, code, className, active) in new[]
                 {
                     ("An", "HV0001", "FA01", true), ("Binh", "HV0002", "FA01", true), ("Cuong", "HV0003", "FA02", true),
                     ("Dung", "HV0004", "FA02", true), ("Em", "HV0005", "FA02", false),
                 })
        {
            var student = new StudentProfile
            {
                StudentCode = code, CourseName = "Fine Arts", ClassName = className, EnrollmentDate = new DateOnly(2025, 9, 1),
                Account = new Account
                {
                    Username = key.ToLowerInvariant(), FullName = $"Student {key}", Email = $"{key}@ifa.local", Role = Role.STUDENT,
                    PasswordHash = "x", Active = active,
                },
            };
            db.StudentProfiles.Add(student);
            data.Students[key] = student;
        }
        db.SaveChanges();

        data.A = AddCompetition(db, "CT-A", "Hometown Landscapes", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), CompetitionStatus.GRADED, staff.Id);
        data.B = AddCompetition(db, "CT-B", "Wildlife", new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 28), CompetitionStatus.ONGOING, staff.Id);
        data.C = AddCompetition(db, "CT-C", "Lunar New Year", new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30), CompetitionStatus.UPCOMING, staff.Id);

        // Entries (with an evaluation when a rating is given)
        void Entry(string key, Competition c, string student, DateTime at, Rating? rating)
        {
            var s = new Submission
            {
                CompetitionId = c.Id, StudentId = data.Students[student].Id, Title = $"Painting {key}", ImagePath = "p.jpg",
                OriginalFileName = $"{key}.jpg", FileSize = 2048, TextType = TextType.POEM, TextContent = "poem",
                Description = "description", SubmittedAt = at,
            };
            if (rating is { } r)
            {
                s.Evaluation = new Evaluation
                {
                    EvaluatedBy = staff.Id, Rating = r, PositivePoints = $"Good light in {key}",
                    EvaluatedAt = c.EndDate.AddDays(2).ToDateTime(new TimeOnly(10, 0)),
                };
            }
            db.Submissions.Add(s);
            data.Entries[key] = s;
        }
        Entry("a1", data.A, "An", new DateTime(2026, 1, 5, 0, 0, 0), Rating.BEST);
        Entry("a2", data.A, "Binh", new DateTime(2026, 1, 10, 23, 59, 59), Rating.BEST);
        Entry("a3", data.A, "Cuong", new DateTime(2026, 1, 11, 0, 0, 0), Rating.BETTER);
        Entry("a4", data.A, "Em", new DateTime(2026, 1, 15, 9, 0, 0), null);
        Entry("b1", data.B, "An", new DateTime(2026, 2, 3, 10, 0, 0), Rating.GOOD);
        Entry("b2", data.B, "Cuong", new DateTime(2026, 2, 4, 10, 0, 0), null);
        db.SaveChanges();

        // Issued awards
        void Issue(Competition c, int rank, string entry, DateOnly on) => db.AwardsIssued.Add(new AwardIssued
        {
            CompetitionAwardId = c.Awards.Single(a => a.Rank == rank).Id, SubmissionId = data.Entries[entry].Id,
            IssuedBy = staff.Id, IssuedDate = on, Remarks = "Well done",
        });
        Issue(data.A, 1, "a1", new DateOnly(2026, 2, 5));
        Issue(data.A, 2, "a2", new DateOnly(2026, 2, 6));
        Issue(data.B, 1, "b1", new DateOnly(2026, 3, 5));

        // Exhibition: a1 sold, b1 accepted (not sold)
        var exhibition = new Exhibition
        {
            Name = "Young Artists Exhibition", Venue = "Museum", City = "Hanoi",
            StartDate = new DateOnly(2026, 2, 10), EndDate = new DateOnly(2026, 2, 20), CreatedBy = staff.Id,
        };
        db.Exhibitions.Add(exhibition);
        db.SaveChanges();
        db.ExhibitionEntries.AddRange(
            new ExhibitionEntry
            {
                ExhibitionId = exhibition.Id, SubmissionId = data.Entries["a1"].Id, AskingPrice = 250m, Status = EntryStatus.SOLD,
                SoldPrice = 260m, SoldDate = new DateOnly(2026, 2, 15), ProposedBy = staff.AccountId,
            },
            new ExhibitionEntry
            {
                ExhibitionId = exhibition.Id, SubmissionId = data.Entries["b1"].Id, AskingPrice = 100m, Status = EntryStatus.ACCEPTED,
                ProposedBy = staff.AccountId,
            });
        db.SaveChanges();
        db.ChangeTracker.Clear();
        return data;
    }

    /// <summary>Adds a competition with First Prize ($300) and Second Prize ($200).</summary>
    private static Competition AddCompetition(AppDbContext db, string code, string title, DateOnly start, DateOnly end, CompetitionStatus stored, long staffId)
    {
        var c = new Competition { Code = code, Title = title, StartDate = start, EndDate = end, Status = stored, CreatedBy = staffId };
        c.Awards.Add(new CompetitionAward { AwardName = "First Prize", Rank = 1, PrizeValue = 300m, Quantity = 1 });
        c.Awards.Add(new CompetitionAward { AwardName = "Second Prize", Rank = 2, PrizeValue = 200m, Quantity = 1 });
        db.Competitions.Add(c);
        db.SaveChanges();
        return c;
    }

    /// <summary>Service under test.</summary>
    private static CompetitionReportService Service(Data data) => new(data.Db);

    // ------------------------------------------------------------------ RPT-01

    /// <summary>TC-M10-01: BEST entries of one competition between two days → exactly those two, totals match.</summary>
    [Fact]
    public async Task Rpt01_BestInRange()
    {
        var data = CreateData();
        var result = await Service(data).GetSubmissionsByRatingAsync(new Rpt01FilterVm
        {
            CompetitionId = data.A.Id, From = new DateOnly(2026, 1, 1), To = new DateOnly(2026, 1, 10), Ratings = { "BEST" },
        });

        Assert.Equal(new[] { "Painting a1", "Painting a2" }, result.Rows.Select(r => r.Title));
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.CountByRating[Rating.BEST]);
        Assert.Equal(0, result.CountByRating[Rating.BETTER]);
        Assert.Equal(0, result.NotGradedCount);
        Assert.Equal("CT-A", result.CompetitionCode);
    }

    /// <summary>TC-M10-02: an entry at 00:00:00 of From and one at 23:59:59 of To are both included; 00:00 next day is not.</summary>
    [Fact]
    public async Task Rpt01_DateRange_BothEndsIncluded()
    {
        var data = CreateData();
        var result = await Service(data).GetSubmissionsByRatingAsync(new Rpt01FilterVm
        {
            CompetitionId = data.A.Id, From = new DateOnly(2026, 1, 5), To = new DateOnly(2026, 1, 10),
        });

        Assert.Equal(new[] { "Painting a1", "Painting a2" }, result.Rows.Select(r => r.Title));
    }

    /// <summary>TC-M10-03: From after To → the date range error on the From field.</summary>
    [Fact]
    public async Task Rpt01_FromAfterTo_Blocked()
    {
        var data = CreateData();
        var ex = await Assert.ThrowsAsync<BusinessException>(() => Service(data).GetSubmissionsByRatingAsync(new Rpt01FilterVm
        {
            CompetitionId = data.A.Id, From = new DateOnly(2026, 1, 10), To = new DateOnly(2026, 1, 5),
        }));

        Assert.Equal(DateTimeHelper.InvalidDateRangeMessage, ex.Message);
        Assert.Equal("From", ex.Field);
    }

    /// <summary>TC-M10-04: a range in the year 2000 → no rows, every total 0, no error.</summary>
    [Fact]
    public async Task Rpt01_Empty_NoData()
    {
        var data = CreateData();
        var result = await Service(data).GetSubmissionsByRatingAsync(new Rpt01FilterVm
        {
            CompetitionId = data.A.Id, From = new DateOnly(2000, 1, 1), To = new DateOnly(2000, 12, 31),
        });

        Assert.Empty(result.Rows);
        Assert.Equal(0, result.TotalCount);
        Assert.All(result.CountByRating.Values, v => Assert.Equal(0, v));
        Assert.False(result.IsTruncated);
    }

    /// <summary>F-M10-01: the competition is required and must exist.</summary>
    [Fact]
    public async Task Rpt01_NoOrUnknownCompetition_Blocked()
    {
        var data = CreateData();
        var none = await Assert.ThrowsAsync<BusinessException>(() => Service(data).GetSubmissionsByRatingAsync(new Rpt01FilterVm()));
        var unknown = await Assert.ThrowsAsync<BusinessException>(() => Service(data).GetSubmissionsByRatingAsync(new Rpt01FilterVm { CompetitionId = 9999 }));

        Assert.Equal(CompetitionReportService.ChooseCompetitionMessage, none.Message);
        Assert.Equal(CompetitionReportService.ChooseCompetitionMessage, unknown.Message);
    }

    /// <summary>§7 filters: "Not graded", rating + "Not graded" together, class; unknown rating values are ignored.</summary>
    [Fact]
    public async Task Rpt01_RatingAndClassFilters()
    {
        var data = CreateData();
        var service = Service(data);

        var notGraded = await service.GetSubmissionsByRatingAsync(new Rpt01FilterVm { CompetitionId = data.A.Id, Ratings = { Rpt01FilterVm.NotGraded } });
        var bestOrNotGraded = await service.GetSubmissionsByRatingAsync(new Rpt01FilterVm { CompetitionId = data.A.Id, Ratings = { "BEST", Rpt01FilterVm.NotGraded } });
        var classFa02 = await service.GetSubmissionsByRatingAsync(new Rpt01FilterVm { CompetitionId = data.A.Id, ClassName = "FA02" });
        var garbage = await service.GetSubmissionsByRatingAsync(new Rpt01FilterVm { CompetitionId = data.A.Id, Ratings = { "nonsense" } });

        Assert.Equal(new[] { "Painting a4" }, notGraded.Rows.Select(r => r.Title));
        Assert.Equal(1, notGraded.NotGradedCount);
        Assert.Equal(new[] { "Painting a1", "Painting a2", "Painting a4" }, bestOrNotGraded.Rows.Select(r => r.Title));
        Assert.Equal(new[] { "Painting a3", "Painting a4" }, classFa02.Rows.Select(r => r.Title));
        Assert.Equal(4, garbage.TotalCount);
    }

    /// <summary>§7 columns and totals: row numbers, evaluator, award, count per rating over the whole competition.</summary>
    [Fact]
    public async Task Rpt01_ColumnsAndTotals()
    {
        var data = CreateData();
        var result = await Service(data).GetSubmissionsByRatingAsync(new Rpt01FilterVm { CompetitionId = data.A.Id });

        Assert.Equal(new[] { 1, 2, 3, 4 }, result.Rows.Select(r => r.No));
        var first = result.Rows[0];
        Assert.Equal("HV0001", first.StudentCode);
        Assert.Equal("Student An", first.StudentName);
        Assert.Equal("FA01", first.ClassName);
        Assert.Equal(Rating.BEST, first.Rating);
        Assert.Equal("Le Minh Hoang", first.EvaluatorName);
        Assert.Equal("First Prize", first.AwardName);
        Assert.Null(result.Rows[3].Rating);
        Assert.Null(result.Rows[3].EvaluatorName);
        Assert.Null(result.Rows[3].AwardName);
        Assert.Equal(4, result.TotalCount);
        Assert.Equal(2, result.CountByRating[Rating.BEST]);
        Assert.Equal(1, result.CountByRating[Rating.BETTER]);
        Assert.Equal(1, result.NotGradedCount);
        Assert.Equal(6, result.CountByRating.Count);
    }

    /// <summary>Row cap: only maxRows rows are returned, the totals still cover every matching entry.</summary>
    [Fact]
    public async Task Rpt01_RowCap_TotalsCoverEverything()
    {
        var data = CreateData();
        var result = await Service(data).GetSubmissionsByRatingAsync(new Rpt01FilterVm { CompetitionId = data.A.Id }, maxRows: 2);

        Assert.Equal(2, result.Rows.Count);
        Assert.Equal(4, result.TotalCount);
        Assert.True(result.IsTruncated);
        Assert.Equal(2, result.CountByRating[Rating.BEST]);
    }

    // ------------------------------------------------------------------ RPT-02

    /// <summary>
    /// F-M10-02: per competition submissions, ratings, not graded, participation = active students with an entry /
    /// active students (the locked student's entry counts as a submission but not as a participant); totals row.
    /// </summary>
    [Fact]
    public async Task Rpt02_CountsParticipationAndTotals()
    {
        var data = CreateData();
        var result = await Service(data).GetParticipationAsync(new Rpt02FilterVm(), Today);

        Assert.Equal(4, result.ActiveStudents);
        Assert.Equal(new[] { "CT-C", "CT-B", "CT-A" }, result.Rows.Select(r => r.Code));
        var a = result.Rows.Single(r => r.Code == "CT-A");
        Assert.Equal(CompetitionStatus.GRADED, a.Status);
        Assert.Equal(4, a.Submissions);
        Assert.Equal(3, a.Participants);
        Assert.Equal(75.0m, a.ParticipationPercent);
        Assert.Equal(2, a.CountByRating[Rating.BEST]);
        Assert.Equal(1, a.CountByRating[Rating.BETTER]);
        Assert.Equal(1, a.NotGraded);
        Assert.Equal(1, a.NotSubmitted);
        var b = result.Rows.Single(r => r.Code == "CT-B");
        Assert.Equal(CompetitionStatus.CLOSED, b.Status);
        Assert.Equal(50.0m, b.ParticipationPercent);
        var c = result.Rows.Single(r => r.Code == "CT-C");
        Assert.Equal(CompetitionStatus.UPCOMING, c.Status);
        Assert.Equal(0, c.Submissions);
        Assert.Equal(0m, c.ParticipationPercent);

        // Totals row
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(6, result.TotalSubmissions);
        Assert.Equal(5, result.TotalParticipants);
        Assert.Equal(41.7m, result.TotalParticipationPercent);
        Assert.Equal(2, result.TotalByRating[Rating.BEST]);
        Assert.Equal(1, result.TotalByRating[Rating.GOOD]);
        Assert.Equal(2, result.TotalNotGraded);
        Assert.Null(result.NotSubmitted);
    }

    /// <summary>F-M10-02 filters: actual status (B is CLOSED although stored ONGOING) and overlapping period.</summary>
    [Fact]
    public async Task Rpt02_StatusAndPeriodFilters()
    {
        var data = CreateData();
        var service = Service(data);

        var closed = await service.GetParticipationAsync(new Rpt02FilterVm { Status = CompetitionStatus.CLOSED }, Today);
        var overlap = await service.GetParticipationAsync(new Rpt02FilterVm { From = new DateOnly(2026, 1, 31), To = new DateOnly(2026, 2, 1) }, Today);
        var later = await service.GetParticipationAsync(new Rpt02FilterVm { From = new DateOnly(2026, 3, 1) }, Today);

        Assert.Equal(new[] { "CT-B" }, closed.Rows.Select(r => r.Code));
        Assert.Equal(new[] { "CT-B", "CT-A" }, overlap.Rows.Select(r => r.Code));
        Assert.Equal(new[] { "CT-C" }, later.Rows.Select(r => r.Code));
    }

    /// <summary>BR-12: active students without an entry, by student code; locked students are left out.</summary>
    [Fact]
    public async Task Rpt02_NotSubmittedList()
    {
        var data = CreateData();
        var service = Service(data);

        var a = await service.GetParticipationAsync(new Rpt02FilterVm { NotSubmittedCompetitionId = data.A.Id }, Today);
        var c = await service.GetParticipationAsync(new Rpt02FilterVm { NotSubmittedCompetitionId = data.C.Id }, Today);

        Assert.Equal(new[] { "HV0004" }, a.NotSubmitted!.Students.Select(s => s.StudentCode));
        Assert.Equal("CT-A", a.NotSubmitted.Code);
        Assert.Equal(new[] { "HV0001", "HV0002", "HV0003", "HV0004" }, c.NotSubmitted!.Students.Select(s => s.StudentCode));
        Assert.Equal(new[] { 1, 2, 3, 4 }, c.NotSubmitted.Students.Select(s => s.No));
        await Assert.ThrowsAsync<BusinessException>(() => service.GetParticipationAsync(new Rpt02FilterVm { NotSubmittedCompetitionId = 9999 }, Today));
    }

    /// <summary>TC-M10-03 / TC-M10-04 for RPT-02: From after To refused; no competition in the year 2000 → empty totals.</summary>
    [Fact]
    public async Task Rpt02_InvalidRangeAndEmpty()
    {
        var data = CreateData();
        var service = Service(data);

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            service.GetParticipationAsync(new Rpt02FilterVm { From = new DateOnly(2026, 2, 1), To = new DateOnly(2026, 1, 1) }, Today));
        var empty = await service.GetParticipationAsync(new Rpt02FilterVm { From = new DateOnly(2000, 1, 1), To = new DateOnly(2000, 12, 31) }, Today);

        Assert.Equal(DateTimeHelper.InvalidDateRangeMessage, ex.Message);
        Assert.Empty(empty.Rows);
        Assert.Equal(0, empty.TotalSubmissions);
        Assert.Equal(0m, empty.TotalParticipationPercent);
    }

    // ------------------------------------------------------------------ RPT-03

    /// <summary>F-M10-03: every issued award, newest first, with the number of awards and the total prize value.</summary>
    [Fact]
    public async Task Rpt03_AllAwards_TotalValue()
    {
        var data = CreateData();
        var result = await Service(data).GetAwardsIssuedAsync(new Rpt03FilterVm());

        Assert.Equal(new[] { "Painting b1", "Painting a2", "Painting a1" }, result.Rows.Select(r => r.SubmissionTitle));
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(800m, result.TotalPrizeValue);
        var row = result.Rows[0];
        Assert.Equal(1, row.No);
        Assert.Equal("CT-B", row.CompetitionCode);
        Assert.Equal("First Prize", row.AwardName);
        Assert.Equal("HV0001", row.StudentCode);
        Assert.Equal("Le Minh Hoang", row.IssuedByName);
    }

    /// <summary>F-M10-03 filters: competition, issued date range (both days included), student name or code.</summary>
    [Fact]
    public async Task Rpt03_Filters()
    {
        var data = CreateData();
        var service = Service(data);

        var competitionA = await service.GetAwardsIssuedAsync(new Rpt03FilterVm { CompetitionId = data.A.Id });
        var range = await service.GetAwardsIssuedAsync(new Rpt03FilterVm { From = new DateOnly(2026, 2, 6), To = new DateOnly(2026, 3, 5) });
        var byCode = await service.GetAwardsIssuedAsync(new Rpt03FilterVm { Student = " HV0001 " });
        var byName = await service.GetAwardsIssuedAsync(new Rpt03FilterVm { Student = "Binh" });

        Assert.Equal(2, competitionA.TotalCount);
        Assert.Equal(500m, competitionA.TotalPrizeValue);
        Assert.Equal(new[] { "Painting b1", "Painting a2" }, range.Rows.Select(r => r.SubmissionTitle));
        Assert.Equal(500m, range.TotalPrizeValue);
        Assert.Equal(600m, byCode.TotalPrizeValue);
        Assert.Equal(new[] { "Painting a2" }, byName.Rows.Select(r => r.SubmissionTitle));
    }

    /// <summary>TC-M10-03 / TC-M10-04 for RPT-03.</summary>
    [Fact]
    public async Task Rpt03_InvalidRangeAndEmpty()
    {
        var data = CreateData();
        var service = Service(data);

        await Assert.ThrowsAsync<BusinessException>(() =>
            service.GetAwardsIssuedAsync(new Rpt03FilterVm { From = new DateOnly(2026, 3, 1), To = new DateOnly(2026, 2, 1) }));
        var empty = await service.GetAwardsIssuedAsync(new Rpt03FilterVm { From = new DateOnly(2000, 1, 1), To = new DateOnly(2000, 12, 31) });

        Assert.Empty(empty.Rows);
        Assert.Equal(0, empty.TotalCount);
        Assert.Equal(0m, empty.TotalPrizeValue);
    }

    // ------------------------------------------------------------------ RPT-06

    /// <summary>F-M10-06: entries of one student with competitions joined, BEST count, awards and total sales.</summary>
    [Fact]
    public async Task Rpt06_StudentTotals()
    {
        var data = CreateData();
        var result = await Service(data).GetStudentProfileAsync(new Rpt06FilterVm { StudentId = data.Students["An"].Id });

        Assert.Equal("HV0001", result.StudentCode);
        Assert.Equal(new[] { "Painting a1", "Painting b1" }, result.Rows.Select(r => r.Title));
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.CompetitionsJoined);
        Assert.Equal(1, result.BestCount);
        Assert.Equal(2, result.AwardCount);
        Assert.Equal(600m, result.TotalPrizeValue);
        Assert.Equal(260m, result.TotalSales);
        var a1 = result.Rows[0];
        Assert.Equal("First Prize", a1.AwardName);
        Assert.Equal("Good light in a1", a1.CommentSummary);
        Assert.Equal(EntryStatus.SOLD, Assert.Single(a1.Exhibitions).Status);
        Assert.Equal(EntryStatus.ACCEPTED, Assert.Single(result.Rows[1].Exhibitions).Status);
    }

    /// <summary>F-M10-06 period: only entries submitted in the range count (sales and BEST follow the entries).</summary>
    [Fact]
    public async Task Rpt06_Period()
    {
        var data = CreateData();
        var result = await Service(data).GetStudentProfileAsync(new Rpt06FilterVm
        {
            StudentId = data.Students["An"].Id, From = new DateOnly(2026, 2, 1), To = new DateOnly(2026, 2, 3),
        });

        Assert.Equal(new[] { "Painting b1" }, result.Rows.Select(r => r.Title));
        Assert.Equal(1, result.CompetitionsJoined);
        Assert.Equal(0, result.BestCount);
        Assert.Equal(1, result.AwardCount);
        Assert.Equal(0m, result.TotalSales);
    }

    /// <summary>TC-M10-04 for RPT-06: a student without entries → no rows, totals 0.</summary>
    [Fact]
    public async Task Rpt06_NoEntries_Empty()
    {
        var data = CreateData();
        var result = await Service(data).GetStudentProfileAsync(new Rpt06FilterVm { StudentId = data.Students["Dung"].Id });

        Assert.Empty(result.Rows);
        Assert.Equal(0, result.CompetitionsJoined);
        Assert.Equal(0, result.AwardCount);
        Assert.Equal(0m, result.TotalSales);
    }

    /// <summary>F-M10-06: the student is required and must exist; From after To is refused (TC-M10-03).</summary>
    [Fact]
    public async Task Rpt06_InvalidFilters_Blocked()
    {
        var data = CreateData();
        var service = Service(data);

        var none = await Assert.ThrowsAsync<BusinessException>(() => service.GetStudentProfileAsync(new Rpt06FilterVm()));
        var unknown = await Assert.ThrowsAsync<BusinessException>(() => service.GetStudentProfileAsync(new Rpt06FilterVm { StudentId = 9999 }));
        var range = await Assert.ThrowsAsync<BusinessException>(() => service.GetStudentProfileAsync(new Rpt06FilterVm
        {
            StudentId = data.Students["An"].Id, From = new DateOnly(2026, 2, 1), To = new DateOnly(2026, 1, 1),
        }));

        Assert.Equal(CompetitionReportService.ChooseStudentMessage, none.Message);
        Assert.Equal(CompetitionReportService.ChooseStudentMessage, unknown.Message);
        Assert.Equal(DateTimeHelper.InvalidDateRangeMessage, range.Message);
    }

    /// <summary>RPT-06 comment summary: long comments are cut with "…".</summary>
    [Fact]
    public async Task Rpt06_LongComment_Summarized()
    {
        var data = CreateData();
        var evaluation = data.Db.Evaluations.Single(e => e.SubmissionId == data.Entries["a1"].Id);
        evaluation.PositivePoints = new string('x', 150);
        data.Db.SaveChanges();

        var result = await Service(data).GetStudentProfileAsync(new Rpt06FilterVm { StudentId = data.Students["An"].Id });

        var summary = result.Rows[0].CommentSummary!;
        Assert.Equal(CompetitionReportService.CommentSummaryLength + 1, summary.Length);
        Assert.EndsWith("…", summary);
    }

    // ------------------------------------------------------------------ Options and catalog

    /// <summary>Dropdowns: distinct sorted classes, students by code.</summary>
    [Fact]
    public async Task Options_ClassesAndStudents()
    {
        var data = CreateData();
        var service = Service(data);

        Assert.Equal(new[] { "FA01", "FA02" }, await service.GetClassOptionsAsync());
        Assert.Equal(new[] { "HV0001", "HV0002", "HV0003", "HV0004", "HV0005" }, (await service.GetStudentOptionsAsync()).Select(s => s.Code));
    }

    /// <summary>F-M10-08 / role matrix §2.3: RPT-01..06 for STAFF and MANAGER, RPT-07 for ADMIN and MANAGER.</summary>
    [Fact]
    public void Catalog_RolesFollowTheMatrix()
    {
        Assert.Equal(7, ReportCatalog.All.Count);
        var staff = ReportCatalog.All.Where(r => r.Roles.Contains(RoleNames.Staff)).Select(r => r.Code);
        var admin = ReportCatalog.All.Where(r => r.Roles.Contains(RoleNames.Admin)).Select(r => r.Code);
        var manager = ReportCatalog.All.Where(r => r.Roles.Contains(RoleNames.Manager)).Select(r => r.Code);

        Assert.Equal(new[] { "RPT-01", "RPT-02", "RPT-03", "RPT-04", "RPT-05", "RPT-06" }, staff);
        Assert.Equal(new[] { "RPT-07" }, admin);
        Assert.Equal(7, manager.Count());
        Assert.DoesNotContain(ReportCatalog.All, r => r.Roles.Contains(RoleNames.Student));
    }
}
