using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.Services.Implementations;
using InstituteOfFineArts.Web.Services.Interfaces;
using InstituteOfFineArts.Web.ViewModels.Staff.ExhibitionEntries;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace InstituteOfFineArts.Tests.Services;

/// <summary>
/// ExhibitionEntryService (tasks BE1-12, BE1-13; F-M09-05, 06, 08, 09; state diagram §4.7c): staff direct create
/// ACCEPTED with the student's price and a source note (BR-41), accept / reject only PENDING, sale only from ACCEPTED
/// with price, date within the exhibition and customer (BR-44, US-13 AC3, TC-M09-05/06), UNSOLD only after the
/// exhibition, payment only when SOLD with the full price (BR-45, US-13 AC4, TC-M09-07/08), delete rules (TC-M09-09).
/// </summary>
public class ExhibitionEntryServiceTests : IDisposable
{
    /// <summary>Shared test data.</summary>
    private readonly ExhibitionEntryTestData _data = new();

    /// <summary>Today in the institute time zone.</summary>
    private static readonly DateOnly Today = ExhibitionEntryTestData.Today;

    /// <summary>Fake notification service (M12): the tests check which event is raised.</summary>
    private readonly Mock<INotificationService> _notifications = new();

    /// <summary>Service under test.</summary>
    private ExhibitionEntryService Service => new(_data.Db, NullLogger<ExhibitionEntryService>.Instance, _notifications.Object);

    /// <summary>Disposes the test database.</summary>
    public void Dispose() => _data.Dispose();

    /// <summary>Adds a BEST painting of student01 with an entry in <paramref name="exhibitionName"/> (default: ongoing).</summary>
    private long Entry(EntryStatus status, string exhibitionName = "ongoing", bool paid = false)
    {
        var exhibition = exhibitionName switch
        {
            "upcoming" => _data.Upcoming,
            "finished" => _data.Finished,
            _ => _data.Ongoing,
        };
        var s = _data.AddPainting(_data.Student1, Rating.BEST);
        return _data.AddEntry(s, exhibition, status, 200m, paid).Id;
    }

    /// <summary>Valid sale form for the ongoing exhibition.</summary>
    private SoldFormVm SoldForm(decimal? price = 250m, DateOnly? date = null, long? customerId = -1)
        => new() { SoldPrice = price, SoldDate = date ?? Today, CustomerId = customerId == -1 ? _data.Customer.Id : customerId };

    /// <summary>UC-07 3b, BR-41: staff direct create → ACCEPTED with the student's price, note, reviewer and proposer.</summary>
    [Fact]
    public async Task Create_Direct_Accepted()
    {
        var s = _data.AddPainting(_data.Student1, Rating.BEST);
        var form = new EntryCreateFormVm { ExhibitionId = _data.Upcoming.Id, SubmissionId = s.Id, AskingPrice = 200m, Notes = "  Phone call 12 Nov  " };

        var id = await Service.CreateAsync(form, _data.StaffId, _data.StaffAccountId);

        var e = _data.Reload(id)!;
        Assert.Equal(EntryStatus.ACCEPTED, e.Status);
        Assert.Equal(200m, e.AskingPrice);
        Assert.Equal("Phone call 12 Nov", e.Notes);
        Assert.Equal(_data.StaffId, e.ReviewedBy);
        Assert.Equal(_data.StaffAccountId, e.ProposedBy);
    }

    /// <summary>BR-41: the note about the source of the price is required.</summary>
    [Fact]
    public async Task Create_NoNote_Refused()
    {
        var s = _data.AddPainting(_data.Student1, Rating.BEST);
        var form = new EntryCreateFormVm { ExhibitionId = _data.Upcoming.Id, SubmissionId = s.Id, AskingPrice = 200m, Notes = " " };

        var ex = await Assert.ThrowsAsync<BusinessException>(() => Service.CreateAsync(form, _data.StaffId, _data.StaffAccountId));

        Assert.Equal(EntryCreateFormVm.NotesRequiredMessage, ex.Message);
        Assert.Empty(_data.Db.ExhibitionEntries);
    }

    /// <summary>BR-40, BR-43, BR-42: GOOD, already SOLD painting or a started exhibition are refused for staff too.</summary>
    [Fact]
    public async Task Create_Rules_Refused()
    {
        var good = _data.AddPainting(_data.Student1, Rating.GOOD);
        var sold = _data.AddPainting(_data.Student2, Rating.BEST);
        _data.AddEntry(sold, _data.Finished, EntryStatus.SOLD);
        var best = _data.AddPainting(_data.Student2, Rating.BEST);
        EntryCreateFormVm Form(long submissionId, long exhibitionId) => new() { ExhibitionId = exhibitionId, SubmissionId = submissionId, AskingPrice = 100m, Notes = "Email" };

        var goodEx = await Assert.ThrowsAsync<BusinessException>(() => Service.CreateAsync(Form(good.Id, _data.Upcoming.Id), _data.StaffId, _data.StaffAccountId));
        var soldEx = await Assert.ThrowsAsync<BusinessException>(() => Service.CreateAsync(Form(sold.Id, _data.Upcoming.Id), _data.StaffId, _data.StaffAccountId));
        var startedEx = await Assert.ThrowsAsync<BusinessException>(() => Service.CreateAsync(Form(best.Id, _data.Ongoing.Id), _data.StaffId, _data.StaffAccountId));

        Assert.Equal(ExhibitionEntryRules.NotEligibleMessage, goodEx.Message);
        Assert.Equal(ExhibitionEntryRules.SoldMessage, soldEx.Message);
        Assert.Equal(ExhibitionEntryRules.NotUpcomingMessage, startedEx.Message);
        Assert.Single(_data.Db.ExhibitionEntries);
    }

    /// <summary>F-M09-05: the staff dropdown lists eligible paintings of every student, BEST first.</summary>
    [Fact]
    public async Task GetNewForm_AllStudentsEligible()
    {
        var better = _data.AddPainting(_data.Student1, Rating.BETTER);
        var best = _data.AddPainting(_data.Student2, Rating.BEST);
        _data.AddPainting(_data.Student2, Rating.MODERATE);

        var form = await Service.GetNewFormAsync(_data.Upcoming.Id);

        Assert.Equal(new[] { best.Id, better.Id }, form.Candidates.Select(c => c.Id));
        Assert.Equal("HV0002", form.Candidates[0].StudentCode);
    }

    /// <summary>F-M09-05: PENDING → ACCEPTED with the reviewer.</summary>
    [Fact]
    public async Task Accept_Pending_Accepted()
    {
        var id = Entry(EntryStatus.PENDING, "upcoming");

        await Service.AcceptAsync(id, _data.StaffId);

        var e = _data.Reload(id)!;
        Assert.Equal(EntryStatus.ACCEPTED, e.Status);
        Assert.Equal(_data.StaffId, e.ReviewedBy);
        _notifications.Verify(n => n.EntryReviewedAsync(id), Times.Once);
    }

    /// <summary>Accept only from PENDING.</summary>
    [Theory]
    [InlineData(EntryStatus.ACCEPTED)]
    [InlineData(EntryStatus.REJECTED)]
    public async Task Accept_NotPending_Refused(EntryStatus status)
    {
        var id = Entry(status, "upcoming");

        var ex = await Assert.ThrowsAsync<BusinessException>(() => Service.AcceptAsync(id, _data.StaffId));

        Assert.Equal(ExhibitionEntryService.NotPendingMessage, ex.Message);
    }

    /// <summary>BR-42: a proposal cannot be accepted once the exhibition has started.</summary>
    [Fact]
    public async Task Accept_ExhibitionStarted_Refused()
    {
        var id = Entry(EntryStatus.PENDING);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => Service.AcceptAsync(id, _data.StaffId));

        Assert.Equal(ExhibitionEntryService.AcceptStartedMessage, ex.Message);
        Assert.Equal(EntryStatus.PENDING, _data.Reload(id)!.Status);
        _notifications.Verify(n => n.EntryReviewedAsync(It.IsAny<long>()), Times.Never);
    }

    /// <summary>UC-07 3a: PENDING → REJECTED with the reason, also after the exhibition started.</summary>
    [Fact]
    public async Task Reject_WithReason_Rejected()
    {
        var id = Entry(EntryStatus.PENDING);

        await Service.RejectAsync(id, new RejectFormVm { Reason = " Too dark for the venue " }, _data.StaffId);

        var e = _data.Reload(id)!;
        Assert.Equal(EntryStatus.REJECTED, e.Status);
        Assert.Equal("Too dark for the venue", e.RejectReason);
        Assert.Equal(_data.StaffId, e.ReviewedBy);
        _notifications.Verify(n => n.EntryReviewedAsync(id), Times.Once);
    }

    /// <summary>The reason is required; only PENDING can be rejected.</summary>
    [Fact]
    public async Task Reject_NoReasonOrNotPending_Refused()
    {
        var pending = Entry(EntryStatus.PENDING, "upcoming");
        var accepted = Entry(EntryStatus.ACCEPTED, "upcoming");

        var noReason = await Assert.ThrowsAsync<BusinessException>(() => Service.RejectAsync(pending, new RejectFormVm { Reason = "  " }, _data.StaffId));
        var notPending = await Assert.ThrowsAsync<BusinessException>(() => Service.RejectAsync(accepted, new RejectFormVm { Reason = "No" }, _data.StaffId));

        Assert.Equal(RejectFormVm.ReasonRequiredMessage, noReason.Message);
        Assert.Equal(nameof(RejectFormVm.Reason), noReason.Field);
        Assert.Equal(ExhibitionEntryService.NotPendingMessage, notPending.Message);
    }

    /// <summary>TC-M09-05, BR-44: ACCEPTED → SOLD with price, date, customer and the staff member.</summary>
    [Fact]
    public async Task Sold_Valid_Saved()
    {
        var id = Entry(EntryStatus.ACCEPTED);

        await Service.MarkSoldAsync(id, SoldForm(250.00m), _data.StaffId);

        var e = _data.Reload(id)!;
        Assert.Equal(EntryStatus.SOLD, e.Status);
        Assert.Equal(250.00m, e.SoldPrice);
        Assert.Equal(Today, e.SoldDate);
        Assert.Equal(_data.Customer.Id, e.CustomerId);
        Assert.False(e.PaidToStudent);
        Assert.Equal(_data.StaffId, e.ReviewedBy);
        _notifications.Verify(n => n.EntrySoldAsync(id), Times.Once);
    }

    /// <summary>US-13 AC3, TC-M09-06: no customer / no price / price 0 → error on the field, nothing saved.</summary>
    [Fact]
    public async Task Sold_MissingFields_Refused()
    {
        var id = Entry(EntryStatus.ACCEPTED);

        var noCustomer = await Assert.ThrowsAsync<BusinessException>(() => Service.MarkSoldAsync(id, SoldForm(customerId: null), _data.StaffId));
        var noPrice = await Assert.ThrowsAsync<BusinessException>(() => Service.MarkSoldAsync(id, SoldForm(price: null), _data.StaffId));
        var zero = await Assert.ThrowsAsync<BusinessException>(() => Service.MarkSoldAsync(id, SoldForm(price: 0m), _data.StaffId));
        var unknown = await Assert.ThrowsAsync<BusinessException>(() => Service.MarkSoldAsync(id, SoldForm(customerId: 9999), _data.StaffId));

        Assert.Equal(SoldFormVm.CustomerRequiredMessage, noCustomer.Message);
        Assert.Equal(nameof(SoldFormVm.CustomerId), noCustomer.Field);
        Assert.Equal(SoldFormVm.PriceRequiredMessage, noPrice.Message);
        Assert.Equal(SoldFormVm.PriceMessage, zero.Message);
        Assert.Equal(ExhibitionEntryService.CustomerNotFoundMessage, unknown.Message);
        Assert.Equal(EntryStatus.ACCEPTED, _data.Reload(id)!.Status);
    }

    /// <summary>BR-44: the sale date must be within the exhibition dates and not in the future.</summary>
    [Fact]
    public async Task Sold_BadDate_Refused()
    {
        var id = Entry(EntryStatus.ACCEPTED);

        var before = await Assert.ThrowsAsync<BusinessException>(() => Service.MarkSoldAsync(id, SoldForm(date: _data.Ongoing.StartDate.AddDays(-1)), _data.StaffId));
        var after = await Assert.ThrowsAsync<BusinessException>(() => Service.MarkSoldAsync(id, SoldForm(date: _data.Ongoing.EndDate.AddDays(1)), _data.StaffId));
        var future = await Assert.ThrowsAsync<BusinessException>(() => Service.MarkSoldAsync(id, SoldForm(date: Today.AddDays(1)), _data.StaffId));

        Assert.Equal(ExhibitionEntryService.SoldDateRangeMessage, before.Message);
        Assert.Equal(ExhibitionEntryService.SoldDateRangeMessage, after.Message);
        Assert.Equal(ExhibitionEntryService.SoldDateFutureMessage, future.Message);
        Assert.Equal(nameof(SoldFormVm.SoldDate), future.Field);
    }

    /// <summary>
    /// The status job turns ACCEPTED into UNSOLD when the exhibition ends; a sale made on the last day and entered
    /// afterwards is still recorded (sale date inside the exhibition, BR-44).
    /// </summary>
    [Fact]
    public async Task Sold_FromUnsold_AfterExhibition_Recorded()
    {
        var id = Entry(EntryStatus.UNSOLD, "finished");

        await Service.MarkSoldAsync(id, SoldForm(date: _data.Finished.EndDate), _data.StaffId);

        var e = _data.Reload(id)!;
        Assert.Equal(EntryStatus.SOLD, e.Status);
        Assert.Equal(_data.Finished.EndDate, e.SoldDate);
        Assert.Equal(_data.Customer.Id, e.CustomerId);
    }

    /// <summary>BR-43: an UNSOLD painting that has since been sent to another exhibition cannot become a sale of the old one.</summary>
    [Fact]
    public async Task Sold_FromUnsold_SentElsewhere_Refused()
    {
        var s = _data.AddPainting(_data.Student1, Rating.BEST);
        var old = _data.AddEntry(s, _data.Finished, EntryStatus.UNSOLD);
        _data.AddEntry(s, _data.Upcoming, EntryStatus.PENDING);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => Service.MarkSoldAsync(old.Id, SoldForm(date: _data.Finished.EndDate), _data.StaffId));
        Assert.Equal(ExhibitionEntryService.SellMovedOnMessage, ex.Message);
        Assert.Equal(EntryStatus.UNSOLD, _data.Reload(old.Id)!.Status);
    }

    /// <summary>Sale only from ACCEPTED or UNSOLD (PENDING, REJECTED, SOLD refused) and only once the exhibition has started.</summary>
    [Fact]
    public async Task Sold_WrongState_Refused()
    {
        foreach (var status in new[] { EntryStatus.PENDING, EntryStatus.REJECTED, EntryStatus.SOLD })
        {
            var id = Entry(status);
            var ex = await Assert.ThrowsAsync<BusinessException>(() => Service.MarkSoldAsync(id, SoldForm(), _data.StaffId));
            Assert.Equal(ExhibitionEntryService.SellNotAcceptedMessage, ex.Message);
        }

        var upcoming = Entry(EntryStatus.ACCEPTED, "upcoming");
        var notStarted = await Assert.ThrowsAsync<BusinessException>(() => Service.MarkSoldAsync(upcoming, SoldForm(date: _data.Upcoming.StartDate), _data.StaffId));
        Assert.Equal(ExhibitionEntryService.SellNotStartedMessage, notStarted.Message);
    }

    /// <summary>A sale on a past day of a finished exhibition is accepted (recorded late, still ACCEPTED).</summary>
    [Fact]
    public async Task Sold_FinishedExhibition_Allowed()
    {
        var id = Entry(EntryStatus.ACCEPTED, "finished");

        await Service.MarkSoldAsync(id, SoldForm(date: _data.Finished.EndDate), _data.StaffId);

        Assert.Equal(EntryStatus.SOLD, _data.Reload(id)!.Status);
    }

    /// <summary>F-M09-06: ACCEPTED → UNSOLD once the exhibition has finished (same rule as StatusSyncService).</summary>
    [Fact]
    public async Task Unsold_Finished_Saved()
    {
        var id = Entry(EntryStatus.ACCEPTED, "finished");

        await Service.MarkUnsoldAsync(id, _data.StaffId);

        Assert.Equal(EntryStatus.UNSOLD, _data.Reload(id)!.Status);
    }

    /// <summary>UNSOLD before the end of the exhibition, or from another state, is refused.</summary>
    [Fact]
    public async Task Unsold_NotFinishedOrNotAccepted_Refused()
    {
        var ongoing = Entry(EntryStatus.ACCEPTED);
        var pending = Entry(EntryStatus.PENDING, "finished");

        var early = await Assert.ThrowsAsync<BusinessException>(() => Service.MarkUnsoldAsync(ongoing, _data.StaffId));
        var wrong = await Assert.ThrowsAsync<BusinessException>(() => Service.MarkUnsoldAsync(pending, _data.StaffId));

        Assert.Equal(ExhibitionEntryService.UnsoldNotFinishedMessage, early.Message);
        Assert.Equal(ExhibitionEntryService.UnsoldNotAcceptedMessage, wrong.Message);
    }

    /// <summary>TC-M09-07, BR-45: SOLD → paid the full sale price on a date ≥ sale date.</summary>
    [Fact]
    public async Task Paid_Sold_Saved()
    {
        var id = Entry(EntryStatus.SOLD);

        await Service.MarkPaidAsync(id, new PaidFormVm { PaidAmount = 250m, PaidDate = Today }, _data.StaffId);

        var e = _data.Reload(id)!;
        Assert.True(e.PaidToStudent);
        Assert.Equal(250m, e.PaidAmount);
        Assert.Equal(Today, e.PaidDate);
        Assert.Equal(EntryStatus.SOLD, e.Status);
        _notifications.Verify(n => n.EntryPaidAsync(id), Times.Once);
        _notifications.Verify(n => n.EntrySoldAsync(It.IsAny<long>()), Times.Never);
    }

    /// <summary>US-13 AC4, TC-M09-08: not SOLD (PENDING, ACCEPTED) → "Not sold yet".</summary>
    [Theory]
    [InlineData(EntryStatus.PENDING)]
    [InlineData(EntryStatus.ACCEPTED)]
    [InlineData(EntryStatus.UNSOLD)]
    public async Task Paid_NotSold_Refused(EntryStatus status)
    {
        var id = Entry(status);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => Service.MarkPaidAsync(id, new PaidFormVm { PaidAmount = 200m, PaidDate = Today }, _data.StaffId));

        Assert.Equal(ExhibitionEntryService.NotSoldMessage, ex.Message);
        Assert.StartsWith("Not sold yet", ex.Message);
        Assert.False(_data.Reload(id)!.PaidToStudent);
        _notifications.Verify(n => n.EntryPaidAsync(It.IsAny<long>()), Times.Never);
    }

    /// <summary>BR-45: already paid, partial amount, date before the sale or in the future → refused.</summary>
    [Fact]
    public async Task Paid_BadValues_Refused()
    {
        var paid = Entry(EntryStatus.SOLD, paid: true);
        var sold = Entry(EntryStatus.SOLD);
        var saleDate = _data.Ongoing.StartDate;

        var again = await Assert.ThrowsAsync<BusinessException>(() => Service.MarkPaidAsync(paid, new PaidFormVm { PaidAmount = 250m, PaidDate = Today }, _data.StaffId));
        var partial = await Assert.ThrowsAsync<BusinessException>(() => Service.MarkPaidAsync(sold, new PaidFormVm { PaidAmount = 200m, PaidDate = Today }, _data.StaffId));
        var early = await Assert.ThrowsAsync<BusinessException>(() => Service.MarkPaidAsync(sold, new PaidFormVm { PaidAmount = 250m, PaidDate = saleDate.AddDays(-1) }, _data.StaffId));
        var future = await Assert.ThrowsAsync<BusinessException>(() => Service.MarkPaidAsync(sold, new PaidFormVm { PaidAmount = 250m, PaidDate = Today.AddDays(1) }, _data.StaffId));

        Assert.Equal(ExhibitionEntryService.AlreadyPaidMessage, again.Message);
        Assert.Equal(ExhibitionEntryService.PaidAmountMessage, partial.Message);
        Assert.Equal(nameof(PaidFormVm.PaidAmount), partial.Field);
        Assert.Equal(ExhibitionEntryService.PaidDateBeforeSaleMessage, early.Message);
        Assert.Equal(ExhibitionEntryService.PaidDateFutureMessage, future.Message);
        Assert.False(_data.Reload(sold)!.PaidToStudent);
    }

    /// <summary>F-M09-09: before the exhibition starts PENDING / ACCEPTED / REJECTED entries can be deleted.</summary>
    [Theory]
    [InlineData(EntryStatus.PENDING)]
    [InlineData(EntryStatus.ACCEPTED)]
    [InlineData(EntryStatus.REJECTED)]
    public async Task Delete_BeforeExhibition_Deleted(EntryStatus status)
    {
        var id = Entry(status, "upcoming");

        var exhibitionId = await Service.DeleteAsync(id);

        Assert.Equal(_data.Upcoming.Id, exhibitionId);
        Assert.Null(_data.Reload(id));
    }

    /// <summary>TC-M09-09: SOLD (paid) / UNSOLD entries and entries of a started exhibition are never deleted.</summary>
    [Fact]
    public async Task Delete_SoldOrStarted_Blocked()
    {
        var ids = new[] { Entry(EntryStatus.SOLD, paid: true), Entry(EntryStatus.UNSOLD, "finished"), Entry(EntryStatus.ACCEPTED) };

        foreach (var id in ids)
        {
            var ex = await Assert.ThrowsAsync<BusinessException>(() => Service.DeleteAsync(id));
            Assert.Equal(ExhibitionEntryService.DeleteBlockedMessage, ex.Message);
        }
        Assert.Equal(3, await _data.Db.ExhibitionEntries.CountAsync());
    }

    /// <summary>F-M09-09: filters exhibition, status, paid (false = sold but not paid) and student name/code.</summary>
    [Fact]
    public async Task Search_Filters()
    {
        var paid = Entry(EntryStatus.SOLD, paid: true);
        var unpaid = Entry(EntryStatus.SOLD);
        var pending = Entry(EntryStatus.PENDING, "upcoming");
        var other = _data.AddPainting(_data.Student2, Rating.BEST);
        var otherEntry = _data.AddEntry(other, _data.Upcoming, EntryStatus.ACCEPTED).Id;

        var byPaid = await Service.SearchAsync(new EntrySearchVm { Paid = true });
        var byUnpaid = await Service.SearchAsync(new EntrySearchVm { Paid = false });
        var byExhibition = await Service.SearchAsync(new EntrySearchVm { ExhibitionId = _data.Upcoming.Id });
        var byStatus = await Service.SearchAsync(new EntrySearchVm { Status = EntryStatus.PENDING });
        var byStudent = await Service.SearchAsync(new EntrySearchVm { Student = "HV0002" });
        var byName = await Service.SearchAsync(new EntrySearchVm { Student = "Van An" });

        Assert.Equal(paid, Assert.Single(byPaid.Items).Id);
        Assert.Equal(unpaid, Assert.Single(byUnpaid.Items).Id);
        Assert.Equal(new[] { pending, otherEntry }.Order(), byExhibition.Items.Select(i => i.Id).Order());
        Assert.Equal(pending, Assert.Single(byStatus.Items).Id);
        Assert.Equal(otherEntry, Assert.Single(byStudent.Items).Id);
        Assert.Equal(3, byName.TotalCount);
    }

    /// <summary>Details flags follow the state diagram; the forms get their defaults (BR-44, BR-45).</summary>
    [Fact]
    public async Task GetDetails_FlagsAndDefaults()
    {
        var pendingUpcoming = await Service.GetDetailsAsync(Entry(EntryStatus.PENDING, "upcoming"));
        var acceptedOngoing = await Service.GetDetailsAsync(Entry(EntryStatus.ACCEPTED));
        var acceptedFinished = await Service.GetDetailsAsync(Entry(EntryStatus.ACCEPTED, "finished"));
        var sold = await Service.GetDetailsAsync(Entry(EntryStatus.SOLD));

        Assert.True(pendingUpcoming.CanAccept && pendingUpcoming.CanReject && pendingUpcoming.CanDelete);
        Assert.False(pendingUpcoming.CanSell || pendingUpcoming.CanPay);
        Assert.True(acceptedOngoing.CanSell);
        Assert.False(acceptedOngoing.CanMarkUnsold || acceptedOngoing.CanDelete);
        Assert.Equal(200m, acceptedOngoing.SoldForm.SoldPrice);
        Assert.Equal(Today, acceptedOngoing.SoldForm.SoldDate);
        Assert.Single(acceptedOngoing.Customers);
        Assert.True(acceptedFinished.CanSell && acceptedFinished.CanMarkUnsold);
        Assert.Equal(_data.Finished.EndDate, acceptedFinished.SoldForm.SoldDate);
        Assert.True(sold.CanPay);
        Assert.Equal(250m, sold.PaidForm.PaidAmount);
        Assert.Equal(_data.Customer.FullName, sold.CustomerName);
    }

    /// <summary>Unknown id → not found for every action.</summary>
    [Fact]
    public async Task UnknownEntry_NotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => Service.GetDetailsAsync(9999));
        await Assert.ThrowsAsync<NotFoundException>(() => Service.AcceptAsync(9999, _data.StaffId));
        await Assert.ThrowsAsync<NotFoundException>(() => Service.MarkPaidAsync(9999, new PaidFormVm(), _data.StaffId));
        await Assert.ThrowsAsync<NotFoundException>(() => Service.DeleteAsync(9999));
    }
}
