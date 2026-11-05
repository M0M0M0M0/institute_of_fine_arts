using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.Services.Implementations;
using InstituteOfFineArts.Web.ViewModels.Student.ExhibitionEntries;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InstituteOfFineArts.Tests.Services;

/// <summary>
/// StudentExhibitionEntryService (task BE1-12; F-M09-04, 10, 12; US-13 AC1/AC2/AC5/AC6): propose own BEST/BETTER
/// paintings of GRADED competitions (BR-40) to UPCOMING exhibitions (BR-42) at a price &gt; 0 (BR-41), one active entry
/// and never after SOLD (BR-43, TC-M09-04), change price / withdraw only while PENDING (BR-46), other students → 404.
/// </summary>
public class StudentExhibitionEntryServiceTests : IDisposable
{
    /// <summary>Shared test data.</summary>
    private readonly ExhibitionEntryTestData _data = new();

    /// <summary>Service under test.</summary>
    private StudentExhibitionEntryService Service => new(_data.Db, NullLogger<StudentExhibitionEntryService>.Instance);

    /// <summary>Disposes the test database.</summary>
    public void Dispose() => _data.Dispose();

    /// <summary>Proposal form for the upcoming exhibition.</summary>
    private ProposeEntryFormVm Form(long submissionId, decimal? price = 200m, long? exhibitionId = null)
        => new() { ExhibitionId = exhibitionId ?? _data.Upcoming.Id, SubmissionId = submissionId, AskingPrice = price };

    /// <summary>US-13 AC1, TC-M09-02: BEST painting, UPCOMING exhibition, $200.00 → PENDING entry proposed by the student's account.</summary>
    [Fact]
    public async Task Create_Best_Pending()
    {
        var s = _data.AddPainting(_data.Student1, Rating.BEST);

        var id = await Service.CreateAsync(Form(s.Id, 200.00m), _data.Student1, _data.Student1Account);

        var e = _data.Reload(id)!;
        Assert.Equal(EntryStatus.PENDING, e.Status);
        Assert.Equal(200.00m, e.AskingPrice);
        Assert.Equal(_data.Student1Account, e.ProposedBy);
        Assert.Null(e.ReviewedBy);
        Assert.False(e.PaidToStudent);
        Assert.Null(e.SoldPrice);
    }

    /// <summary>BR-40: a BETTER painting may also be proposed.</summary>
    [Fact]
    public async Task Create_Better_Pending()
    {
        var s = _data.AddPainting(_data.Student1, Rating.BETTER);

        var id = await Service.CreateAsync(Form(s.Id), _data.Student1, _data.Student1Account);

        Assert.Equal(EntryStatus.PENDING, _data.Reload(id)!.Status);
    }

    /// <summary>
    /// US-13 AC2, BR-40, BR-43: the dropdown lists only own BEST/BETTER paintings of GRADED competitions without an
    /// active or sold entry and not sent to this exhibition before; GOOD, unpublished, other students' are hidden.
    /// </summary>
    [Fact]
    public async Task GetNewForm_ListsOnlyEligible()
    {
        var best = _data.AddPainting(_data.Student1, Rating.BEST, title: "Best");
        var better = _data.AddPainting(_data.Student1, Rating.BETTER, title: "Better");
        _data.AddPainting(_data.Student1, Rating.GOOD, title: "Good");
        _data.AddPainting(_data.Student1, Rating.BEST, _data.Closed, "Not published");
        _data.AddPainting(_data.Student2, Rating.BEST, title: "Other student");
        var pending = _data.AddPainting(_data.Student1, Rating.BEST, title: "Pending elsewhere");
        _data.AddEntry(pending, _data.Upcoming2, EntryStatus.PENDING);
        var sold = _data.AddPainting(_data.Student1, Rating.BEST, title: "Sold");
        _data.AddEntry(sold, _data.Finished, EntryStatus.SOLD);
        var rejectedHere = _data.AddPainting(_data.Student1, Rating.BEST, title: "Rejected here");
        _data.AddEntry(rejectedHere, _data.Upcoming, EntryStatus.REJECTED);
        var unsold = _data.AddPainting(_data.Student1, Rating.BETTER, title: "Unsold elsewhere");
        _data.AddEntry(unsold, _data.Finished, EntryStatus.UNSOLD);

        var form = await Service.GetNewFormAsync(_data.Upcoming.Id, _data.Student1);

        Assert.Equal(_data.Upcoming.Name, form.ExhibitionName);
        Assert.Equal(new[] { best.Id, better.Id, unsold.Id }.Order(), form.Candidates.Select(c => c.Id).Order());
    }

    /// <summary>US-13 AC2: a GOOD painting sent with a direct request is refused.</summary>
    [Fact]
    public async Task Create_Good_Refused()
    {
        var s = _data.AddPainting(_data.Student1, Rating.GOOD);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => Service.CreateAsync(Form(s.Id), _data.Student1, _data.Student1Account));

        Assert.Equal(ExhibitionEntryRules.NotEligibleMessage, ex.Message);
        Assert.Equal(nameof(ProposeEntryFormVm.SubmissionId), ex.Field);
        Assert.Empty(_data.Db.ExhibitionEntries);
    }

    /// <summary>BR-40: BEST but the results are not published (competition not GRADED) → refused.</summary>
    [Fact]
    public async Task Create_NotGraded_Refused()
    {
        var s = _data.AddPainting(_data.Student1, Rating.BEST, _data.Closed);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => Service.CreateAsync(Form(s.Id), _data.Student1, _data.Student1Account));

        Assert.Equal(ExhibitionEntryRules.NotEligibleMessage, ex.Message);
    }

    /// <summary>US-13 AC6, BR-43: a SOLD painting cannot be sent to another exhibition.</summary>
    [Fact]
    public async Task Create_Sold_Refused()
    {
        var s = _data.AddPainting(_data.Student1, Rating.BEST);
        _data.AddEntry(s, _data.Finished, EntryStatus.SOLD, paid: true);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => Service.CreateAsync(Form(s.Id), _data.Student1, _data.Student1Account));

        Assert.Equal(ExhibitionEntryRules.SoldMessage, ex.Message);
    }

    /// <summary>BR-43, TC-M09-04: a painting with a PENDING/ACCEPTED entry cannot be proposed again.</summary>
    [Theory]
    [InlineData(EntryStatus.PENDING)]
    [InlineData(EntryStatus.ACCEPTED)]
    public async Task Create_ActiveEntry_Refused(EntryStatus status)
    {
        var s = _data.AddPainting(_data.Student1, Rating.BEST);
        _data.AddEntry(s, _data.Upcoming2, status);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => Service.CreateAsync(Form(s.Id), _data.Student1, _data.Student1Account));

        Assert.Equal(ExhibitionEntryRules.DuplicateMessage, ex.Message);
        Assert.Single(_data.Db.ExhibitionEntries);
    }

    /// <summary>TC-M09-04: a painting rejected by this exhibition cannot be proposed to it again.</summary>
    [Fact]
    public async Task Create_RejectedSameExhibition_Refused()
    {
        var s = _data.AddPainting(_data.Student1, Rating.BEST);
        _data.AddEntry(s, _data.Upcoming, EntryStatus.REJECTED);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => Service.CreateAsync(Form(s.Id), _data.Student1, _data.Student1Account));

        Assert.Equal(ExhibitionEntryRules.SameExhibitionMessage, ex.Message);
    }

    /// <summary>BR-43: UNSOLD or REJECTED elsewhere → the painting may go to another exhibition.</summary>
    [Theory]
    [InlineData(EntryStatus.UNSOLD)]
    [InlineData(EntryStatus.REJECTED)]
    public async Task Create_AfterUnsoldOrRejectedElsewhere_Allowed(EntryStatus status)
    {
        var s = _data.AddPainting(_data.Student1, Rating.BEST);
        _data.AddEntry(s, _data.Finished, status);

        var id = await Service.CreateAsync(Form(s.Id), _data.Student1, _data.Student1Account);

        Assert.Equal(EntryStatus.PENDING, _data.Reload(id)!.Status);
    }

    /// <summary>BR-42: ONGOING or FINISHED exhibition → refused (form and save).</summary>
    [Fact]
    public async Task Create_NotUpcoming_Refused()
    {
        var s = _data.AddPainting(_data.Student1, Rating.BEST);

        foreach (var exhibition in new[] { _data.Ongoing, _data.Finished })
        {
            var ex = await Assert.ThrowsAsync<BusinessException>(() => Service.CreateAsync(Form(s.Id, exhibitionId: exhibition.Id), _data.Student1, _data.Student1Account));
            Assert.Equal(ExhibitionEntryRules.NotUpcomingMessage, ex.Message);
            var formEx = await Assert.ThrowsAsync<BusinessException>(() => Service.GetNewFormAsync(exhibition.Id, _data.Student1));
            Assert.Equal(ExhibitionEntryRules.NotUpcomingMessage, formEx.Message);
        }
        Assert.Empty(_data.Db.ExhibitionEntries);
    }

    /// <summary>Unknown exhibition → not found.</summary>
    [Fact]
    public async Task Create_UnknownExhibition_NotFound()
    {
        var s = _data.AddPainting(_data.Student1, Rating.BEST);

        await Assert.ThrowsAsync<NotFoundException>(() => Service.CreateAsync(Form(s.Id, exhibitionId: 9999), _data.Student1, _data.Student1Account));
    }

    /// <summary>BR-41, TC-M09-03: missing, zero, negative or 3-decimal price → refused on the price field.</summary>
    [Theory]
    [InlineData(null, ExhibitionEntryRules.PriceRequiredMessage)]
    [InlineData("0", ExhibitionEntryRules.PriceMessage)]
    [InlineData("-5", ExhibitionEntryRules.PriceMessage)]
    [InlineData("10.555", ExhibitionEntryRules.DecimalsMessage)]
    public async Task Create_InvalidPrice_Refused(string? price, string message)
    {
        var s = _data.AddPainting(_data.Student1, Rating.BEST);
        decimal? value = price == null ? null : decimal.Parse(price, System.Globalization.CultureInfo.InvariantCulture);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => Service.CreateAsync(Form(s.Id, value), _data.Student1, _data.Student1Account));

        Assert.Equal(message, ex.Message);
        Assert.Equal(nameof(ProposeEntryFormVm.AskingPrice), ex.Field);
    }

    /// <summary>Another student's painting cannot be proposed.</summary>
    [Fact]
    public async Task Create_OtherStudentsPainting_Refused()
    {
        var s = _data.AddPainting(_data.Student2, Rating.BEST);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => Service.CreateAsync(Form(s.Id), _data.Student1, _data.Student1Account));

        Assert.Equal(StudentExhibitionEntryService.NotOwnMessage, ex.Message);
    }

    /// <summary>F-M09-10, US-13 AC5: only own entries, with status and exhibition filters; sale and payment shown.</summary>
    [Fact]
    public async Task Search_OnlyOwn_Filtered()
    {
        var mine1 = _data.AddPainting(_data.Student1, Rating.BEST);
        var mine2 = _data.AddPainting(_data.Student1, Rating.BETTER);
        var other = _data.AddPainting(_data.Student2, Rating.BEST);
        var sold = _data.AddEntry(mine1, _data.Finished, EntryStatus.SOLD, paid: true);
        _data.AddEntry(mine2, _data.Upcoming, EntryStatus.PENDING);
        _data.AddEntry(other, _data.Upcoming, EntryStatus.PENDING);

        var all = await Service.SearchMineAsync(_data.Student1, new MyEntrySearchVm());
        var soldOnly = await Service.SearchMineAsync(_data.Student1, new MyEntrySearchVm { Status = EntryStatus.SOLD });
        var upcomingOnly = await Service.SearchMineAsync(_data.Student1, new MyEntrySearchVm { ExhibitionId = _data.Upcoming.Id });
        var exhibitions = await Service.GetMyExhibitionsAsync(_data.Student1);

        Assert.Equal(2, all.TotalCount);
        var row = Assert.Single(soldOnly.Items);
        Assert.Equal(sold.Id, row.Id);
        Assert.Equal(250m, row.SoldPrice);
        Assert.True(row.PaidToStudent);
        Assert.Equal(_data.Finished.StartDate, row.PaidDate);
        Assert.Equal(EntryStatus.PENDING, Assert.Single(upcomingOnly.Items).Status);
        Assert.Equal(2, exhibitions.Count);
    }

    /// <summary>Details of an own entry; another student's entry → not found.</summary>
    [Fact]
    public async Task GetDetails_OwnOnly()
    {
        var s = _data.AddPainting(_data.Student1, Rating.BEST);
        var e = _data.AddEntry(s, _data.Upcoming, EntryStatus.REJECTED);

        var vm = await Service.GetDetailsAsync(e.Id, _data.Student1);

        Assert.Equal("Does not fit the theme", vm.RejectReason);
        Assert.Equal(ExhibitionStatus.UPCOMING, vm.ExhibitionStatus);
        Assert.False(vm.CanChange);
        Assert.False(vm.CreatedByStaff);
        await Assert.ThrowsAsync<NotFoundException>(() => Service.GetDetailsAsync(e.Id, _data.Student2));
    }

    /// <summary>BR-46: the asking price can be changed while PENDING.</summary>
    [Fact]
    public async Task UpdatePrice_Pending_Saved()
    {
        var s = _data.AddPainting(_data.Student1, Rating.BEST);
        var e = _data.AddEntry(s, _data.Upcoming, EntryStatus.PENDING);

        await Service.UpdatePriceAsync(e.Id, new EditPriceFormVm { AskingPrice = 320.50m }, _data.Student1);

        Assert.Equal(320.50m, _data.Reload(e.Id)!.AskingPrice);
    }

    /// <summary>BR-46: after review (ACCEPTED / REJECTED / SOLD) the student cannot change the price.</summary>
    [Theory]
    [InlineData(EntryStatus.ACCEPTED)]
    [InlineData(EntryStatus.REJECTED)]
    [InlineData(EntryStatus.SOLD)]
    public async Task UpdatePrice_NotPending_Refused(EntryStatus status)
    {
        var s = _data.AddPainting(_data.Student1, Rating.BEST);
        var e = _data.AddEntry(s, _data.Upcoming, status);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => Service.UpdatePriceAsync(e.Id, new EditPriceFormVm { AskingPrice = 999m }, _data.Student1));
        var formEx = await Assert.ThrowsAsync<BusinessException>(() => Service.GetForEditAsync(e.Id, _data.Student1));

        Assert.Equal(StudentExhibitionEntryService.NotPendingMessage, ex.Message);
        Assert.Equal(StudentExhibitionEntryService.NotPendingMessage, formEx.Message);
        Assert.Equal(200m, _data.Reload(e.Id)!.AskingPrice);
    }

    /// <summary>BR-41: the new price must also be &gt; 0.</summary>
    [Fact]
    public async Task UpdatePrice_Zero_Refused()
    {
        var s = _data.AddPainting(_data.Student1, Rating.BEST);
        var e = _data.AddEntry(s, _data.Upcoming, EntryStatus.PENDING);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => Service.UpdatePriceAsync(e.Id, new EditPriceFormVm { AskingPrice = 0m }, _data.Student1));

        Assert.Equal(ExhibitionEntryRules.PriceMessage, ex.Message);
    }

    /// <summary>F-M09-12: a PENDING proposal is withdrawn (deleted).</summary>
    [Fact]
    public async Task Withdraw_Pending_Deleted()
    {
        var s = _data.AddPainting(_data.Student1, Rating.BEST);
        var e = _data.AddEntry(s, _data.Upcoming, EntryStatus.PENDING);

        await Service.WithdrawAsync(e.Id, _data.Student1);

        Assert.False(await _data.Db.ExhibitionEntries.AnyAsync());
    }

    /// <summary>BR-46: an ACCEPTED entry cannot be withdrawn by the student.</summary>
    [Fact]
    public async Task Withdraw_Accepted_Refused()
    {
        var s = _data.AddPainting(_data.Student1, Rating.BEST);
        var e = _data.AddEntry(s, _data.Upcoming, EntryStatus.ACCEPTED);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => Service.WithdrawAsync(e.Id, _data.Student1));

        Assert.Equal(StudentExhibitionEntryService.NotPendingMessage, ex.Message);
        Assert.NotNull(_data.Reload(e.Id));
    }

    /// <summary>Another student's entry cannot be changed or withdrawn (404).</summary>
    [Fact]
    public async Task Change_OtherStudent_NotFound()
    {
        var s = _data.AddPainting(_data.Student2, Rating.BEST);
        var e = _data.AddEntry(s, _data.Upcoming, EntryStatus.PENDING);

        await Assert.ThrowsAsync<NotFoundException>(() => Service.WithdrawAsync(e.Id, _data.Student1));
        await Assert.ThrowsAsync<NotFoundException>(() => Service.UpdatePriceAsync(e.Id, new EditPriceFormVm { AskingPrice = 5m }, _data.Student1));
        Assert.NotNull(_data.Reload(e.Id));
    }
}
