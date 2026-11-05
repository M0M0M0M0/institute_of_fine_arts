using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Helpers;
using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.Services.Implementations;
using InstituteOfFineArts.Web.ViewModels.Staff.Submissions;
using Microsoft.EntityFrameworkCore;

namespace InstituteOfFineArts.Tests.Services;

/// <summary>
/// StaffSubmissionService (tasks BE2-06, BE2-07; F-M07-01, 02, 06, 07, 09): list filters (evaluated, rating,
/// student, inclusive date range), evaluation progress, details with late flag / award / exhibition entries,
/// rating search limited to CLOSED and GRADED competitions, student history. Dates are relative to today.
/// </summary>
public class StaffSubmissionServiceTests
{
    /// <summary>Today in the institute time zone.</summary>
    private static readonly DateOnly Today = DateTimeHelper.Today;

    /// <summary>The sample data of one test database.</summary>
    private sealed class Data
    {
        /// <summary>The database.</summary>
        public AppDbContext Db { get; init; } = null!;

        /// <summary>Students by short name (An, Binh, Cuong, Dung – Dung has no entries).</summary>
        public Dictionary<string, StudentProfile> Students { get; } = new();

        /// <summary>GRADED competition ("Hometown Landscapes").</summary>
        public Competition Graded { get; set; } = null!;

        /// <summary>CLOSED competition whose stored status is still ONGOING ("City at Night").</summary>
        public Competition Closed { get; set; } = null!;

        /// <summary>ONGOING competition ("Colors of Autumn").</summary>
        public Competition Ongoing { get; set; } = null!;

        /// <summary>Submissions by key (g1, g2, gLate, c1, c2, o1).</summary>
        public Dictionary<string, Submission> Entries { get; } = new();
    }

    /// <summary>
    /// In-memory database: graded competition (An BEST + First Prize + exhibition entry, Binh BETTER,
    /// Cuong late DISQUALIFIED), closed competition (An not evaluated, Binh BEST), ongoing competition
    /// (An with an unexpected BEST evaluation, to prove the rating search ignores it).
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
        foreach (var (key, name, code) in new[] { ("An", "Nguyen Van An", "HV0001"), ("Binh", "Tran Thi Binh", "HV0002"), ("Cuong", "Le Van Cuong", "HV0003"), ("Dung", "Pham Dung", "HV0004") })
        {
            var student = new StudentProfile
            {
                StudentCode = code, CourseName = "Fine Arts", ClassName = "FA01", EnrollmentDate = new DateOnly(2025, 9, 1),
                Account = new Account { Username = key.ToLowerInvariant(), FullName = name, Email = $"{key}@ifa.local", Role = Role.STUDENT, PasswordHash = "x" },
            };
            db.StudentProfiles.Add(student);
            data.Students[key] = student;
        }
        db.SaveChanges();

        data.Graded = AddCompetition(db, "CT-01", "Hometown Landscapes", -60, -30, CompetitionStatus.GRADED, staff.Id);
        data.Closed = AddCompetition(db, "CT-02", "City at Night", -25, -1, CompetitionStatus.ONGOING, staff.Id);
        data.Ongoing = AddCompetition(db, "CT-03", "Colors of Autumn", -10, 10, CompetitionStatus.ONGOING, staff.Id);

        // Entries: submitted a few days after the start; the late one the day after the deadline
        Submission Entry(string key, Competition c, string student, DateTime at, Rating? rating)
        {
            var s = new Submission
            {
                CompetitionId = c.Id, StudentId = data.Students[student].Id, Title = $"Painting {key}", ImagePath = "p.jpg",
                OriginalFileName = $"{key}.jpg", FileSize = 2048, TextType = TextType.POEM, TextContent = "line 1\nline 2",
                Description = "Why I painted it", SubmittedAt = at,
            };
            if (rating is { } r)
            {
                s.Evaluation = new Evaluation
                {
                    EvaluatedBy = staff.Id, Rating = r, PositivePoints = "Good light", NegativePoints = "Busy background",
                    ImprovementAreas = "Simplify", EvaluatedAt = c.EndDate.AddDays(2).ToDateTime(new TimeOnly(10, 0)),
                };
            }
            db.Submissions.Add(s);
            data.Entries[key] = s;
            return s;
        }
        Entry("g1", data.Graded, "An", data.Graded.StartDate.AddDays(2).ToDateTime(new TimeOnly(9, 0)), Rating.BEST);
        Entry("g2", data.Graded, "Binh", data.Graded.StartDate.AddDays(10).ToDateTime(new TimeOnly(14, 0)), Rating.BETTER);
        Entry("gLate", data.Graded, "Cuong", data.Graded.EndDate.AddDays(1).ToDateTime(new TimeOnly(9, 30)), Rating.DISQUALIFIED);
        Entry("c1", data.Closed, "An", data.Closed.StartDate.AddDays(3).ToDateTime(new TimeOnly(8, 0)), null);
        Entry("c2", data.Closed, "Binh", data.Closed.StartDate.AddDays(8).ToDateTime(new TimeOnly(20, 0)), Rating.BEST);
        Entry("o1", data.Ongoing, "An", data.Ongoing.StartDate.AddDays(1).ToDateTime(new TimeOnly(11, 0)), Rating.BEST);
        db.SaveChanges();

        // First Prize for An's graded entry, which was also sold at an exhibition
        var first = data.Graded.Awards.Single(a => a.Rank == 1);
        db.AwardsIssued.Add(new AwardIssued
        {
            CompetitionAwardId = first.Id, SubmissionId = data.Entries["g1"].Id, IssuedBy = staff.Id,
            IssuedDate = Today.AddDays(-26), Remarks = "Outstanding",
        });
        var exhibition = new Exhibition
        {
            Name = "Young Artists Exhibition (Hanoi)", Venue = "Museum", City = "Hanoi",
            StartDate = Today.AddDays(-20), EndDate = Today.AddDays(-5), CreatedBy = staff.Id,
        };
        db.Exhibitions.Add(exhibition);
        db.SaveChanges();
        db.ExhibitionEntries.Add(new ExhibitionEntry
        {
            ExhibitionId = exhibition.Id, SubmissionId = data.Entries["g1"].Id, AskingPrice = 250m, Status = EntryStatus.SOLD,
            SoldPrice = 260m, ProposedBy = staff.AccountId,
        });
        db.SaveChanges();
        db.ChangeTracker.Clear();
        return data;
    }

    /// <summary>Adds a competition with First and Second Prize.</summary>
    private static Competition AddCompetition(AppDbContext db, string code, string title, int startIn, int endIn, CompetitionStatus stored, long staffId)
    {
        var c = new Competition
        {
            Code = code, Title = title, StartDate = Today.AddDays(startIn), EndDate = Today.AddDays(endIn), Status = stored, CreatedBy = staffId,
        };
        c.Awards.Add(new CompetitionAward { AwardName = "First Prize", Rank = 1, PrizeValue = 300m, Quantity = 1 });
        c.Awards.Add(new CompetitionAward { AwardName = "Second Prize", Rank = 2, PrizeValue = 200m, Quantity = 1 });
        db.Competitions.Add(c);
        db.SaveChanges();
        return c;
    }

    /// <summary>Titles of the rows, in order.</summary>
    private static string[] Titles(IEnumerable<SubmissionListItemVm> rows) => rows.Select(r => r.Title).ToArray();

    // ------------------------------------------------------------------ List (F-M07-01)

    /// <summary>No filters: every entry, newest submitted first, with student, competition, rating and award.</summary>
    [Fact]
    public async Task Search_NoFilters_AllNewestFirst()
    {
        var d = CreateData();
        var page = await new StaffSubmissionService(d.Db).SearchAsync(new SubmissionSearchVm());

        Assert.Equal(6, page.TotalCount);
        Assert.Equal(new[] { "Painting o1", "Painting c2", "Painting c1", "Painting gLate", "Painting g2", "Painting g1" }, Titles(page.Items));
        var g1 = page.Items.Single(r => r.Title == "Painting g1");
        Assert.Equal("Nguyen Van An", g1.StudentName);
        Assert.Equal("HV0001", g1.StudentCode);
        Assert.Equal("Hometown Landscapes", g1.CompetitionTitle);
        Assert.Equal(Rating.BEST, g1.Rating);
        Assert.Equal("First Prize", g1.AwardName);
        Assert.Equal(1, g1.ExhibitionEntryCount);
        Assert.Null(page.Items.Single(r => r.Title == "Painting c1").Rating);
    }

    /// <summary>Competition filter; the late entry of that competition is flagged (BR-14).</summary>
    [Fact]
    public async Task Search_ByCompetition_FlagsLateEntry()
    {
        var d = CreateData();
        var page = await new StaffSubmissionService(d.Db).SearchAsync(new SubmissionSearchVm { CompetitionId = d.Graded.Id });

        Assert.Equal(3, page.TotalCount);
        Assert.True(page.Items.Single(r => r.Title == "Painting gLate").IsLate);
        Assert.All(page.Items.Where(r => r.Title != "Painting gLate"), r => Assert.False(r.IsLate));
    }

    /// <summary>Evaluated / not evaluated filter.</summary>
    [Fact]
    public async Task Search_EvaluatedOrNot()
    {
        var d = CreateData();
        var service = new StaffSubmissionService(d.Db);

        var evaluated = await service.SearchAsync(new SubmissionSearchVm { Evaluated = true });
        var notEvaluated = await service.SearchAsync(new SubmissionSearchVm { Evaluated = false });

        Assert.Equal(5, evaluated.TotalCount);
        Assert.All(evaluated.Items, r => Assert.NotNull(r.Rating));
        Assert.Equal(new[] { "Painting c1" }, Titles(notEvaluated.Items));
    }

    /// <summary>TC-M07-05: rating filter shows only entries with that rating.</summary>
    [Fact]
    public async Task Search_ByRating()
    {
        var d = CreateData();
        var service = new StaffSubmissionService(d.Db);

        var best = await service.SearchAsync(new SubmissionSearchVm { Rating = Rating.BEST });
        var disqualified = await service.SearchAsync(new SubmissionSearchVm { Rating = Rating.DISQUALIFIED, CompetitionId = d.Graded.Id });

        Assert.Equal(new[] { "Painting o1", "Painting c2", "Painting g1" }, Titles(best.Items));
        Assert.Equal(new[] { "Painting gLate" }, Titles(disqualified.Items));
    }

    /// <summary>Student filter: part of the full name or of the student code, trimmed.</summary>
    [Fact]
    public async Task Search_ByStudentNameOrCode()
    {
        var d = CreateData();
        var service = new StaffSubmissionService(d.Db);

        var byName = await service.SearchAsync(new SubmissionSearchVm { Student = "  Binh " });
        var byCode = await service.SearchAsync(new SubmissionSearchVm { Student = "HV0003" });

        Assert.Equal(new[] { "Painting c2", "Painting g2" }, Titles(byName.Items));
        Assert.Equal(new[] { "Painting gLate" }, Titles(byCode.Items));
    }

    /// <summary>TC-M10-02: the date range includes both ends, from 00:00 of From to 23:59:59 of To.</summary>
    [Fact]
    public async Task Search_DateRange_InclusiveBothEnds()
    {
        var d = CreateData();
        var dayA = Today.AddDays(-40);
        var dayB = Today.AddDays(-35);
        // Four extra entries around the range edges
        foreach (var (title, at) in new[]
                 {
                     ("Before", dayA.AddDays(-1).ToDateTime(new TimeOnly(23, 59, 59))),
                     ("Start", dayA.ToDateTime(TimeOnly.MinValue)),
                     ("End", dayB.ToDateTime(new TimeOnly(23, 59, 59))),
                     ("After", dayB.AddDays(1).ToDateTime(TimeOnly.MinValue)),
                 })
        {
            d.Db.Submissions.Add(new Submission
            {
                CompetitionId = d.Graded.Id, StudentId = d.Students["Dung"].Id, Title = title, ImagePath = "p.jpg",
                OriginalFileName = "p.jpg", TextContent = "t", Description = "d", SubmittedAt = at,
            });
        }
        d.Db.SaveChanges();

        var page = await new StaffSubmissionService(d.Db).SearchAsync(new SubmissionSearchVm { Student = "Pham Dung", From = dayA, To = dayB });

        Assert.Equal(new[] { "End", "Start" }, Titles(page.Items));
    }

    /// <summary>Paging: 10 per page, the second page holds the rest.</summary>
    [Fact]
    public async Task Search_Paging()
    {
        var d = CreateData();
        var page2 = await new StaffSubmissionService(d.Db).SearchAsync(new SubmissionSearchVm { Page = 2 }, pageSize: 4);

        Assert.Equal(6, page2.TotalCount);
        Assert.Equal(2, page2.TotalPages);
        Assert.Equal(new[] { "Painting g2", "Painting g1" }, Titles(page2.Items));
    }

    /// <summary>Competition dropdown: every competition, newest start first.</summary>
    [Fact]
    public async Task CompetitionOptions_NewestFirst()
    {
        var d = CreateData();
        var options = await new StaffSubmissionService(d.Db).GetCompetitionOptionsAsync();
        Assert.Equal(new[] { "Colors of Autumn", "City at Night", "Hometown Landscapes" }, options.Select(o => o.Title));
    }

    // ------------------------------------------------------------------ Progress (F-M07-09)

    /// <summary>"Evaluated X / total Y" per competition, with the calculated status.</summary>
    [Fact]
    public async Task Progress_Counts()
    {
        var d = CreateData();
        var service = new StaffSubmissionService(d.Db);

        var graded = await service.GetProgressAsync(d.Graded.Id);
        var closed = await service.GetProgressAsync(d.Closed.Id);

        Assert.Equal((3, 3, CompetitionStatus.GRADED), (graded!.Evaluated, graded.Total, graded.Status));
        Assert.Equal((1, 2, CompetitionStatus.CLOSED), (closed!.Evaluated, closed.Total, closed.Status));
    }

    /// <summary>Unknown competition → no progress (the list simply shows no box).</summary>
    [Fact]
    public async Task Progress_UnknownCompetition_Null()
    {
        var d = CreateData();
        Assert.Null(await new StaffSubmissionService(d.Db).GetProgressAsync(9999));
    }

    // ------------------------------------------------------------------ Details (F-M07-02)

    /// <summary>Details of an awarded entry: texts, student, competition, evaluation with evaluator, award, exhibition entry; no Evaluate button when GRADED.</summary>
    [Fact]
    public async Task Details_WithEvaluationAwardAndExhibition()
    {
        var d = CreateData();
        var vm = await new StaffSubmissionService(d.Db).GetDetailsAsync(d.Entries["g1"].Id);

        Assert.Equal("Painting g1", vm.Title);
        Assert.Equal("line 1\nline 2", vm.TextContent);
        Assert.Equal(TextType.POEM, vm.TextType);
        Assert.Equal("Nguyen Van An", vm.StudentName);
        Assert.Equal(d.Students["An"].Id, vm.StudentId);
        Assert.Equal("CT-01", vm.CompetitionCode);
        Assert.Equal(CompetitionStatus.GRADED, vm.CompetitionStatus);
        Assert.False(vm.IsLate);
        Assert.False(vm.CanEvaluate);
        Assert.Equal(Rating.BEST, vm.Evaluation!.Rating);
        Assert.Equal("Le Minh Hoang", vm.Evaluation.EvaluatorName);
        Assert.Equal("Good light", vm.Evaluation.PositivePoints);
        Assert.Equal("First Prize", vm.Award!.AwardName);
        Assert.Equal(300m, vm.Award.PrizeValue);
        var entry = Assert.Single(vm.ExhibitionEntries);
        Assert.Equal(("Young Artists Exhibition (Hanoi)", EntryStatus.SOLD, 260m), (entry.ExhibitionName, entry.Status, entry.SoldPrice!.Value));
    }

    /// <summary>TC-M07-04: an entry submitted after 23:59:59 of the end date is flagged late.</summary>
    [Fact]
    public async Task Details_LateEntry_Flagged()
    {
        var d = CreateData();
        var vm = await new StaffSubmissionService(d.Db).GetDetailsAsync(d.Entries["gLate"].Id);
        Assert.True(vm.IsLate);
        Assert.Equal(Rating.DISQUALIFIED, vm.Evaluation!.Rating);
        Assert.Null(vm.Award);
        Assert.Empty(vm.ExhibitionEntries);
    }

    /// <summary>The deadline itself (23:59:59 of the end date) is not late; one second later is.</summary>
    [Fact]
    public void IsLate_Boundary()
    {
        var end = new DateOnly(2026, 10, 31);
        Assert.False(CompetitionStatusResolver.IsLateSubmission(end.ToDateTime(new TimeOnly(23, 59, 59)), end));
        Assert.True(CompetitionStatusResolver.IsLateSubmission(end.AddDays(1).ToDateTime(TimeOnly.MinValue), end));
    }

    /// <summary>BR-21: CLOSED competition (stored status stale) → the Evaluate button is shown; not evaluated yet.</summary>
    [Fact]
    public async Task Details_ClosedCompetition_CanEvaluate()
    {
        var d = CreateData();
        var vm = await new StaffSubmissionService(d.Db).GetDetailsAsync(d.Entries["c1"].Id);
        Assert.Equal(CompetitionStatus.CLOSED, vm.CompetitionStatus);
        Assert.True(vm.CanEvaluate);
        Assert.Null(vm.Evaluation);
    }

    /// <summary>ONGOING competition → no Evaluate button.</summary>
    [Fact]
    public async Task Details_OngoingCompetition_CannotEvaluate()
    {
        var d = CreateData();
        var vm = await new StaffSubmissionService(d.Db).GetDetailsAsync(d.Entries["o1"].Id);
        Assert.False(vm.CanEvaluate);
    }

    /// <summary>Unknown submission → NotFoundException (404).</summary>
    [Fact]
    public async Task Details_NotFound()
    {
        var d = CreateData();
        await Assert.ThrowsAsync<NotFoundException>(() => new StaffSubmissionService(d.Db).GetDetailsAsync(9999));
    }

    // ------------------------------------------------------------------ Rating search (F-M07-06)

    /// <summary>BEST in CLOSED or GRADED competitions only (the ONGOING one is ignored); newest competition first.</summary>
    [Fact]
    public async Task ByRating_OnlyClosedOrGraded()
    {
        var d = CreateData();
        var page = await new StaffSubmissionService(d.Db).SearchByRatingAsync(new RatingSearchVm { Rating = Rating.BEST });
        Assert.Equal(new[] { "Painting c2", "Painting g1" }, Titles(page.Items));
    }

    /// <summary>No rating chosen → BEST; the competition filter narrows the result.</summary>
    [Fact]
    public async Task ByRating_DefaultBest_CompetitionFilter()
    {
        var d = CreateData();
        var service = new StaffSubmissionService(d.Db);

        var graded = await service.SearchByRatingAsync(new RatingSearchVm { CompetitionId = d.Graded.Id });
        var better = await service.SearchByRatingAsync(new RatingSearchVm { Rating = Rating.BETTER });

        Assert.Equal(new[] { "Painting g1" }, Titles(graded.Items));
        Assert.Equal(new[] { "Painting g2" }, Titles(better.Items));
    }

    /// <summary>Rating search dropdown: only CLOSED and GRADED competitions.</summary>
    [Fact]
    public async Task EvaluatedCompetitionOptions_ClosedAndGraded()
    {
        var d = CreateData();
        var options = await new StaffSubmissionService(d.Db).GetEvaluatedCompetitionOptionsAsync();
        Assert.Equal(new[] { "City at Night", "Hometown Landscapes" }, options.Select(o => o.Title));
    }

    // ------------------------------------------------------------------ Student history (F-M07-07)

    /// <summary>History: only that student's entries, newest first, with rating, comments, award and exhibition entries.</summary>
    [Fact]
    public async Task History_OnlyThatStudent()
    {
        var d = CreateData();
        var vm = await new StaffSubmissionService(d.Db).GetStudentHistoryAsync(d.Students["An"].Id);

        Assert.Equal(("Nguyen Van An", "HV0001"), (vm.FullName, vm.StudentCode));
        Assert.Equal(new[] { "Painting o1", "Painting c1", "Painting g1" }, vm.Entries.Select(e => e.Title));
        Assert.Equal(2, vm.EvaluatedCount);
        Assert.Equal(1, vm.AwardCount);
        var g1 = vm.Entries.Single(e => e.Title == "Painting g1");
        Assert.Equal("Busy background", g1.Evaluation!.NegativePoints);
        Assert.Equal("First Prize", g1.Award!.AwardName);
        Assert.Single(g1.ExhibitionEntries);
        Assert.Equal(CompetitionStatus.CLOSED, vm.Entries.Single(e => e.Title == "Painting c1").CompetitionStatus);
    }

    /// <summary>History of the student with the late entry shows the late flag.</summary>
    [Fact]
    public async Task History_LateEntryFlagged()
    {
        var d = CreateData();
        var vm = await new StaffSubmissionService(d.Db).GetStudentHistoryAsync(d.Students["Cuong"].Id);
        Assert.True(Assert.Single(vm.Entries).IsLate);
    }

    /// <summary>A student without entries has an empty history.</summary>
    [Fact]
    public async Task History_NoEntries_Empty()
    {
        var d = CreateData();
        var vm = await new StaffSubmissionService(d.Db).GetStudentHistoryAsync(d.Students["Dung"].Id);
        Assert.Empty(vm.Entries);
    }

    /// <summary>Unknown student → NotFoundException (404).</summary>
    [Fact]
    public async Task History_NotFound()
    {
        var d = CreateData();
        await Assert.ThrowsAsync<NotFoundException>(() => new StaffSubmissionService(d.Db).GetStudentHistoryAsync(9999));
    }
}
