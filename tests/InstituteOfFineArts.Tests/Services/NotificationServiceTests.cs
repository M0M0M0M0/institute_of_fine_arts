using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.Services.Implementations;
using InstituteOfFineArts.Web.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace InstituteOfFineArts.Tests.Services;

/// <summary>
/// NotificationService (optional feature M12, F-M12-01 e-mail + F-M12-02 bell; tasks BE1-OPT 3 + BE2-OPT 5):
/// one row per account, e-mails HTML-encoded and never breaking the action, own notifications only, and the
/// receivers of every event (new competition, results published, award issued, sold, paid, accepted / rejected).
/// </summary>
public class NotificationServiceTests : IDisposable
{
    /// <summary>Staff, student01, student02, competitions, exhibitions and a customer.</summary>
    private readonly ExhibitionEntryTestData _data = new();

    /// <summary>Fake e-mail sender; every send succeeds unless a test says otherwise.</summary>
    private readonly Mock<IEmailService> _email = new();

    /// <summary>account.id of a locked student (never notified about new competitions).</summary>
    private readonly long _lockedAccount;

    /// <summary>Adds a locked student and makes the fake e-mail sender succeed.</summary>
    public NotificationServiceTests()
    {
        var locked = new StudentProfile
        {
            StudentCode = "HV0010", EnrollmentDate = new DateOnly(2025, 9, 1), CourseName = "Painting",
            Account = new Account { Username = "student10", FullName = "Locked One", Email = "st10@ifa.local", Role = Role.STUDENT, PasswordHash = "x", Active = false },
        };
        _data.Db.StudentProfiles.Add(locked);
        _data.Db.SaveChanges();
        _lockedAccount = locked.AccountId;

        _email.Setup(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    /// <summary>Disposes the test database.</summary>
    public void Dispose() => _data.Dispose();

    /// <summary>Service under test on <paramref name="db"/> (default: the test database).</summary>
    private NotificationService Service(AppDbContext? db = null, bool withEmail = true)
        => new(db ?? _data.Db, NullLogger<NotificationService>.Instance, withEmail ? _email.Object : null);

    /// <summary>account.id of student02.</summary>
    private long Student2Account => _data.Db.StudentProfiles.Where(s => s.Id == _data.Student2).Select(s => s.AccountId).Single();

    /// <summary>All notification rows, oldest first.</summary>
    private List<Notification> Rows() => _data.Db.Notifications.AsNoTracking().OrderBy(n => n.Id).ToList();

    /// <summary>Number of e-mails handed to the sender.</summary>
    private int EmailsSent() => _email.Invocations.Count(i => i.Method.Name == nameof(IEmailService.SendAsync));

    // ------------------------------------------------------------------ Core

    /// <summary>F-M12-01/02: one unread row per distinct account, and one e-mail each with the title as subject.</summary>
    [Fact]
    public async Task Notify_RowPerAccount_AndEmail()
    {
        var a1 = _data.Student1Account;
        var a2 = Student2Account;

        var count = await Service().NotifyAsync([a1, a2, a1], "New competition", "Paint the sea", "/student/competitions/1", sendEmail: true);

        Assert.Equal(2, count);
        var rows = Rows();
        Assert.Equal(new[] { a1, a2 }, rows.Select(r => r.AccountId).Order());
        Assert.All(rows, r =>
        {
            Assert.False(r.IsRead);
            Assert.Equal("New competition", r.Title);
            Assert.Equal("Paint the sea", r.Message);
            Assert.Equal("/student/competitions/1", r.Link);
        });
        _email.Verify(e => e.SendAsync("st1@ifa.local", "Nguyen Van An", "New competition", It.Is<string>(b => b.Contains("Paint the sea")), It.IsAny<CancellationToken>()), Times.Once);
        _email.Verify(e => e.SendAsync("st2@ifa.local", "Tran Thi Binh", "New competition", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>In-app only events send no e-mail; an empty receiver list does nothing.</summary>
    [Fact]
    public async Task Notify_NoEmailFlag_OrNobody()
    {
        await Service().NotifyAsync([_data.Student1Account], "Payment recorded", "Paid", null, sendEmail: false);
        Assert.Equal(0, await Service().NotifyAsync([], "X", "Y", null, sendEmail: true));

        Assert.Single(Rows());
        Assert.Equal(0, EmailsSent());
    }

    /// <summary>A failing e-mail (exception or false) is logged; the other e-mails are still sent and the rows stay.</summary>
    [Fact]
    public async Task Notify_EmailFails_Continues()
    {
        _email.Setup(e => e.SendAsync("st1@ifa.local", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SMTP down"));

        var count = await Service().NotifyAsync([_data.Student1Account, Student2Account], "T", "M", null, sendEmail: true);

        Assert.Equal(2, count);
        Assert.Equal(2, Rows().Count);
        _email.Verify(e => e.SendAsync("st2@ifa.local", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Without a registered e-mail service the in-app notification still works.</summary>
    [Fact]
    public async Task Notify_NoEmailService_StillSaved()
    {
        Assert.Equal(1, await Service(withEmail: false).NotifyAsync([_data.Student1Account], "T", "M", null, sendEmail: true));
        Assert.Single(Rows());
    }

    /// <summary>A database failure never reaches the caller and the rows are not retried by a later save.</summary>
    [Fact]
    public async Task Notify_SaveFails_NoThrow()
    {
        using var db = new FailingDb(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        var count = await Service(db).NotifyAsync([1, 2], "T", "M", null, sendEmail: true);

        Assert.Equal(0, count);
        Assert.Empty(db.ChangeTracker.Entries<Notification>());
        Assert.Equal(0, EmailsSent());
    }

    /// <summary>Long texts are cut to the column lengths (150 / 1000) instead of failing the insert.</summary>
    [Fact]
    public async Task Notify_LongText_Cut()
    {
        await Service().NotifyAsync([_data.Student1Account], new string('t', 200), new string('m', 1200), null, sendEmail: false);

        var row = Rows().Single();
        Assert.Equal(150, row.Title.Length);
        Assert.Equal(1000, row.Message.Length);
    }

    /// <summary>User text is HTML-encoded in e-mails; the configured site address gives an absolute link.</summary>
    [Fact]
    public void EmailBody_Encoded_WithLink()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [NotificationService.BaseUrlKey] = "https://ifa.example/" })
            .Build();
        var service = new NotificationService(_data.Db, NullLogger<NotificationService>.Instance, _email.Object, config);

        var body = service.EmailBody("An <b>", "Title \"<script>alert(1)</script>\" & more", "/student/results");

        Assert.DoesNotContain("<script>", body);
        Assert.Contains("&lt;script&gt;", body);
        Assert.Contains("An &lt;b&gt;", body);
        Assert.Contains("&amp; more", body);
        Assert.Contains("href=\"https://ifa.example/student/results\"", body);
        Assert.Contains("/student/results", Service().EmailBody("An", "M", "/student/results"));
    }

    // ------------------------------------------------------------------ List, bell, read

    /// <summary>F-M12-02: list and bell show only the user's notifications, newest first, with the unread count.</summary>
    [Fact]
    public async Task ListAndBell_OwnNewestFirst()
    {
        var me = _data.Student1Account;
        for (var i = 1; i <= 7; i++)
        {
            _data.Db.Notifications.Add(new Notification { AccountId = me, Title = $"N{i}", Message = "m", IsRead = i <= 2, CreatedAt = DateTime.Now.AddMinutes(i) });
        }
        _data.Db.Notifications.Add(new Notification { AccountId = Student2Account, Title = "Other", Message = "m" });
        _data.Db.SaveChanges();

        var page = await Service().GetListAsync(me, 1, pageSize: 5);
        var bell = await Service().GetBellAsync(me);

        Assert.Equal(7, page.TotalCount);
        Assert.Equal(new[] { "N7", "N6", "N5", "N4", "N3" }, page.Items.Select(n => n.Title));
        Assert.Equal(5, bell.UnreadCount);
        Assert.Equal(5, bell.Items.Count);
        Assert.Equal("N7", bell.Items[0].Title);
        Assert.Equal(5, await Service().CountUnreadAsync(me));
    }

    /// <summary>Opening marks one notification as read and returns its link; another user's notification is 404.</summary>
    [Fact]
    public async Task MarkRead_OwnOnly()
    {
        var mine = new Notification { AccountId = _data.Student1Account, Title = "T", Message = "M", Link = "/student/results" };
        var other = new Notification { AccountId = Student2Account, Title = "T", Message = "M" };
        _data.Db.Notifications.AddRange(mine, other);
        _data.Db.SaveChanges();

        Assert.Equal("/student/results", await Service().MarkReadAsync(mine.Id, _data.Student1Account));
        await Assert.ThrowsAsync<NotFoundException>(() => Service().MarkReadAsync(other.Id, _data.Student1Account));
        await Assert.ThrowsAsync<NotFoundException>(() => Service().MarkReadAsync(999, _data.Student1Account));

        Assert.True(_data.Db.Notifications.AsNoTracking().Single(n => n.Id == mine.Id).IsRead);
        Assert.False(_data.Db.Notifications.AsNoTracking().Single(n => n.Id == other.Id).IsRead);
    }

    /// <summary>"Mark all as read" changes only the user's unread notifications.</summary>
    [Fact]
    public async Task MarkAllRead_OwnOnly()
    {
        var me = _data.Student1Account;
        _data.Db.Notifications.AddRange(
            new Notification { AccountId = me, Title = "A", Message = "m" },
            new Notification { AccountId = me, Title = "B", Message = "m" },
            new Notification { AccountId = me, Title = "C", Message = "m", IsRead = true },
            new Notification { AccountId = Student2Account, Title = "D", Message = "m" });
        _data.Db.SaveChanges();

        Assert.Equal(2, await Service().MarkAllReadAsync(me));

        Assert.Equal(0, await Service().CountUnreadAsync(me));
        Assert.Equal(1, await Service().CountUnreadAsync(Student2Account));
    }

    // ------------------------------------------------------------------ Events

    /// <summary>Event 1: a new competition → every active student (not the locked one, not staff), with e-mail.</summary>
    [Fact]
    public async Task CompetitionCreated_ActiveStudents()
    {
        var c = new Competition
        {
            Code = "C9", Title = "Sea & Sky", StartDate = ExhibitionEntryTestData.Today.AddDays(3),
            EndDate = ExhibitionEntryTestData.Today.AddDays(20), CreatedBy = _data.StaffId,
        };
        _data.Db.Competitions.Add(c);
        _data.Db.SaveChanges();

        await Service().CompetitionCreatedAsync(c.Id);

        var rows = Rows();
        Assert.Equal(new[] { _data.Student1Account, Student2Account }.Order(), rows.Select(r => r.AccountId).Order());
        Assert.DoesNotContain(rows, r => r.AccountId == _lockedAccount || r.AccountId == _data.StaffAccountId);
        Assert.All(rows, r =>
        {
            Assert.Equal(NotificationService.NewCompetitionTitle, r.Title);
            Assert.Equal(NotificationService.CompetitionLink(c.Id), r.Link);
            Assert.Contains("Sea & Sky", r.Message);
        });
        Assert.Equal(2, EmailsSent());
    }

    /// <summary>Event 2: results published → every student who submitted (once each), link to /student/results, e-mail.</summary>
    [Fact]
    public async Task ResultsPublished_Submitters()
    {
        _data.AddPainting(_data.Student1, Rating.BEST, _data.Closed, "Night 1");

        await Service().ResultsPublishedAsync(_data.Closed.Id);

        var row = Rows().Single();
        Assert.Equal(_data.Student1Account, row.AccountId);
        Assert.Equal(NotificationService.ResultsPublishedTitle, row.Title);
        Assert.Equal(NotificationService.ResultsLink, row.Link);
        Assert.Contains("City at Night", row.Message);
        Assert.Equal(1, EmailsSent());
    }

    /// <summary>Event 3: award issued → the winner only, with e-mail.</summary>
    [Fact]
    public async Task AwardIssued_Winner()
    {
        var s = _data.AddPainting(_data.Student2, Rating.BEST, _data.Closed, "Neon Rain");
        var award = new CompetitionAward { CompetitionId = _data.Closed.Id, AwardName = "First Prize", Rank = 1, PrizeValue = 250m, Quantity = 1 };
        _data.Db.CompetitionAwards.Add(award);
        _data.Db.SaveChanges();
        var issued = new AwardIssued { CompetitionAwardId = award.Id, SubmissionId = s.Id, IssuedBy = _data.StaffId, IssuedDate = ExhibitionEntryTestData.Today };
        _data.Db.AwardsIssued.Add(issued);
        _data.Db.SaveChanges();

        await Service().AwardIssuedAsync(issued.Id);

        var row = Rows().Single();
        Assert.Equal(Student2Account, row.AccountId);
        Assert.Contains("First Prize", row.Message);
        Assert.Contains("Neon Rain", row.Message);
        Assert.Equal(1, EmailsSent());
    }

    /// <summary>Event 4: sold → the student with price and e-mail; paid → the student in-app only.</summary>
    [Fact]
    public async Task EntrySoldAndPaid_Student()
    {
        var entry = _data.AddEntry(_data.AddPainting(_data.Student1, Rating.BEST), _data.Ongoing, EntryStatus.SOLD, paid: true);

        await Service().EntrySoldAsync(entry.Id);
        await Service().EntryPaidAsync(entry.Id);

        var rows = Rows();
        Assert.Equal(2, rows.Count);
        Assert.All(rows, r =>
        {
            Assert.Equal(_data.Student1Account, r.AccountId);
            Assert.Equal(NotificationService.EntryLink(entry.Id), r.Link);
        });
        Assert.Equal(NotificationService.PaintingSoldTitle, rows[0].Title);
        Assert.Contains("$250.00", rows[0].Message);
        Assert.Equal(NotificationService.PaymentTitle, rows[1].Title);
        Assert.Contains("$250.00", rows[1].Message);
        Assert.Equal(1, EmailsSent());
    }

    /// <summary>Event 5: accepted / rejected (with the reason) → the student, in-app only; PENDING → nothing.</summary>
    [Fact]
    public async Task EntryReviewed_Student()
    {
        var accepted = _data.AddEntry(_data.AddPainting(_data.Student1, Rating.BEST), _data.Upcoming, EntryStatus.ACCEPTED);
        var rejected = _data.AddEntry(_data.AddPainting(_data.Student2, Rating.BEST), _data.Upcoming, EntryStatus.REJECTED);
        var pending = _data.AddEntry(_data.AddPainting(_data.Student1, Rating.BEST, _data.Closed), _data.Upcoming2, EntryStatus.PENDING);

        await Service().EntryReviewedAsync(accepted.Id);
        await Service().EntryReviewedAsync(rejected.Id);
        await Service().EntryReviewedAsync(pending.Id);

        var rows = Rows();
        Assert.Equal(2, rows.Count);
        Assert.Equal((_data.Student1Account, NotificationService.ProposalAcceptedTitle), (rows[0].AccountId, rows[0].Title));
        Assert.Equal((Student2Account, NotificationService.ProposalRejectedTitle), (rows[1].AccountId, rows[1].Title));
        Assert.Contains("Does not fit the theme", rows[1].Message);
        Assert.Equal(0, EmailsSent());
    }

    /// <summary>Unknown ids and database failures in an event are logged, never thrown.</summary>
    [Fact]
    public async Task Events_UnknownOrFailure_NoThrow()
    {
        await Service().CompetitionCreatedAsync(999);
        await Service().ResultsPublishedAsync(999);
        await Service().AwardIssuedAsync(999);
        await Service().EntrySoldAsync(999);
        await Service().EntryPaidAsync(999);
        await Service().EntryReviewedAsync(999);
        Assert.Empty(Rows());

        // Disposed context: every query throws, the event still returns normally
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Dispose();
        await Service(db).CompetitionCreatedAsync(1);
    }

    /// <summary>Database whose SaveChanges always fails (simulates a lost connection).</summary>
    private sealed class FailingDb(DbContextOptions<AppDbContext> options) : AppDbContext(options)
    {
        /// <inheritdoc />
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => throw new DbUpdateException("Connection lost");
    }
}
