using InstituteOfFineArts.Web.Infrastructure.BackgroundJobs;
using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.Services.Implementations;
using InstituteOfFineArts.Web.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace InstituteOfFineArts.Tests.Services;

/// <summary>
/// DeadlineReminderService and its daily job (optional feature M12, F-M12-01 "deadline soon"; tasks BE1-OPT 3 +
/// BE2-OPT 5): active students without an entry in an ONGOING competition ending tomorrow are reminded once
/// (in-app + e-mail), never twice for the same competition; a failing run never stops the job.
/// </summary>
public class DeadlineReminderServiceTests : IDisposable
{
    /// <summary>Staff, student01, student02 and other base rows.</summary>
    private readonly ExhibitionEntryTestData _data = new();

    /// <summary>Fake e-mail sender.</summary>
    private readonly Mock<IEmailService> _email = new();

    /// <summary>Today in the institute time zone.</summary>
    private static readonly DateOnly Today = ExhibitionEntryTestData.Today;

    /// <summary>Makes the fake e-mail sender succeed.</summary>
    public DeadlineReminderServiceTests()
    {
        _email.Setup(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    /// <summary>Disposes the test database.</summary>
    public void Dispose() => _data.Dispose();

    /// <summary>Service under test with the real notification service (the duplicate check reads its rows).</summary>
    private DeadlineReminderService Service()
        => new(_data.Db, new NotificationService(_data.Db, NullLogger<NotificationService>.Instance, _email.Object),
            NullLogger<DeadlineReminderService>.Instance);

    /// <summary>account.id of student02.</summary>
    private long Student2Account => _data.Db.StudentProfiles.Where(s => s.Id == _data.Student2).Select(s => s.AccountId).Single();

    /// <summary>Adds a competition from <paramref name="startIn"/> to <paramref name="endIn"/> days from today.</summary>
    private Competition AddCompetition(string title, int startIn, int endIn, CompetitionStatus stored = CompetitionStatus.ONGOING)
    {
        var c = new Competition
        {
            Code = "X" + Guid.NewGuid().ToString("N")[..8], Title = title, StartDate = Today.AddDays(startIn),
            EndDate = Today.AddDays(endIn), Status = stored, CreatedBy = _data.StaffId,
        };
        _data.Db.Competitions.Add(c);
        _data.Db.SaveChanges();
        return c;
    }

    /// <summary>
    /// ONGOING competition ending tomorrow: student01 already submitted → only student02 is reminded (in-app + e-mail);
    /// a locked student and staff are never reminded.
    /// </summary>
    [Fact]
    public async Task Remind_NotSubmittedActiveStudents()
    {
        var c = AddCompetition("Rainy Season", -10, 1);
        _data.AddPainting(_data.Student1, null, c);
        var locked = new StudentProfile
        {
            StudentCode = "HV0010", EnrollmentDate = new DateOnly(2025, 9, 1), CourseName = "Painting",
            Account = new Account { Username = "student10", FullName = "Locked", Email = "st10@ifa.local", Role = Role.STUDENT, PasswordHash = "x", Active = false },
        };
        _data.Db.StudentProfiles.Add(locked);
        _data.Db.SaveChanges();

        var count = await Service().SendRemindersAsync();

        Assert.Equal(1, count);
        var row = await _data.Db.Notifications.AsNoTracking().SingleAsync();
        Assert.Equal(Student2Account, row.AccountId);
        Assert.Equal(NotificationService.DeadlineReminderTitle, row.Title);
        Assert.Equal(NotificationService.CompetitionLink(c.Id), row.Link);
        Assert.Equal(DeadlineReminderService.ReminderMessage("Rainy Season", c.EndDate), row.Message);
        _email.Verify(e => e.SendAsync("st2@ifa.local", It.IsAny<string>(), NotificationService.DeadlineReminderTitle, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Running again the same day (or after a restart) never reminds the same student twice.</summary>
    [Fact]
    public async Task Remind_Twice_NoDuplicate()
    {
        AddCompetition("Rainy Season", -10, 1);

        Assert.Equal(2, await Service().SendRemindersAsync());
        Assert.Equal(0, await Service().SendRemindersAsync());

        Assert.Equal(2, await _data.Db.Notifications.CountAsync());
    }

    /// <summary>Only competitions ending tomorrow and still ONGOING today: not ending today / in 2 days / GRADED / UPCOMING.</summary>
    [Fact]
    public async Task Remind_OnlyOngoingEndingTomorrow()
    {
        AddCompetition("Ends today", -10, 0);
        AddCompetition("Ends in two days", -10, 2);
        AddCompetition("Published early", -10, 1, CompetitionStatus.GRADED);
        AddCompetition("Starts tomorrow", 1, 1, CompetitionStatus.UPCOMING);

        Assert.Equal(0, await Service().SendRemindersAsync());
        Assert.Empty(_data.Db.Notifications);
    }

    /// <summary>The date passed by the caller is used: a competition ending in 3 days is due when "today" is 2 days later.</summary>
    [Fact]
    public async Task Remind_UsesGivenDate()
    {
        AddCompetition("Later", -10, 3);

        Assert.Equal(0, await Service().SendRemindersAsync(Today));
        Assert.Equal(2, await Service().SendRemindersAsync(Today.AddDays(2)));
    }

    /// <summary>The job runs the service in its own scope and a failing run is logged, not thrown.</summary>
    [Fact]
    public async Task Job_RunOnce_CallsServiceAndSwallowsErrors()
    {
        var reminders = new Mock<IDeadlineReminderService>();
        var provider = new ServiceCollection().AddScoped(_ => reminders.Object).BuildServiceProvider();
        var job = new DeadlineReminderBackgroundService(provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<DeadlineReminderBackgroundService>.Instance);

        await job.RunOnceAsync(CancellationToken.None);
        reminders.Verify(r => r.SendRemindersAsync(null, It.IsAny<CancellationToken>()), Times.Once);

        reminders.Setup(r => r.SendRemindersAsync(It.IsAny<DateOnly?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database down"));
        await job.RunOnceAsync(CancellationToken.None);
        Assert.Equal(TimeSpan.FromDays(1), DeadlineReminderBackgroundService.Interval);
    }
}
