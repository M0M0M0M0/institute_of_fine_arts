using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using InstituteOfFineArts.Tests.Services;
using InstituteOfFineArts.Web.Controllers.Api;
using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Infrastructure.Api;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.Services.Implementations;
using InstituteOfFineArts.Web.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace InstituteOfFineArts.Tests.Api;

/// <summary>
/// Exhibition endpoints of the mobile API (student + staff): JSON keys read by mobile/lib/features/exhibitions/exhibitions.dart,
/// the main rules (propose / change price / withdraw / review / sell / pay / finish), ownership (404) and stale rowVersion (409).
/// </summary>
public class ExhibitionsApiTests : IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private static readonly DateOnly Today = ExhibitionEntryTestData.Today;

    private readonly ExhibitionEntryTestData _data = new();
    private readonly Mock<INotificationService> _notifications = new();

    public void Dispose() => _data.Dispose();

    private static ControllerContext Ctx(long accountId, long profileId, string role) => new()
    {
        HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, accountId.ToString()),
                new Claim("ProfileId", profileId.ToString()),
                new Claim(ClaimTypes.Role, role),
            }, "test")),
        },
    };

    private StudentExhibitionsApiController Student(bool second = false)
    {
        var entries = new StudentExhibitionEntryService(_data.Db, NullLogger<StudentExhibitionEntryService>.Instance);
        var account = second ? _data.Db.StudentProfiles.Single(p => p.Id == _data.Student2).AccountId : _data.Student1Account;
        return new StudentExhibitionsApiController(_data.Db, entries)
        {
            ControllerContext = Ctx(account, second ? _data.Student2 : _data.Student1, "STUDENT"),
        };
    }

    private StaffExhibitionsApiController Staff()
    {
        var entries = new ExhibitionEntryService(_data.Db, NullLogger<ExhibitionEntryService>.Instance, _notifications.Object);
        var exhibitions = new ExhibitionService(_data.Db, NullLogger<ExhibitionService>.Instance);
        var customers = new CustomerService(_data.Db, NullLogger<CustomerService>.Instance);
        return new StaffExhibitionsApiController(_data.Db, exhibitions, entries, customers)
        {
            ControllerContext = Ctx(_data.StaffAccountId, _data.StaffId, "STAFF"),
        };
    }

    /// <summary>The JSON (as the app reads it) of an action result.</summary>
    private static JsonElement Body(IActionResult result)
    {
        var value = Assert.IsAssignableFrom<ObjectResult>(result).Value;
        return JsonSerializer.SerializeToElement(value, Json);
    }

    private static void AssertKeys(JsonElement obj, params string[] keys)
    {
        foreach (var key in keys) Assert.True(obj.TryGetProperty(key, out _), $"missing JSON key '{key}'");
    }

    private static readonly string[] SummaryKeys = { "id", "name", "venue", "city", "startDate", "endDate", "status", "daysUntilStart", "entryCount", "pendingCount" };
    private static readonly string[] DetailKeys =
    {
        "id", "name", "venue", "city", "organizer", "description", "startDate", "endDate", "status", "cancelReason", "entryCount", "pendingCount",
        "acceptedCount", "soldCount", "canEdit", "canDelete", "canCancel", "canFinish", "canSubmitEntries", "rowVersion",
    };
    private static readonly string[] StudentEntryKeys =
    {
        "id", "exhibitionId", "exhibitionName", "venue", "city", "exhibitionStartDate", "exhibitionEndDate", "exhibitionStatus", "submissionTitle",
        "competitionTitle", "askingPrice", "status", "statusReason", "soldPrice", "soldDate", "paidToStudent", "paidAmount", "paidDate", "canEdit", "rowVersion",
    };
    private static readonly string[] EntryDetailKeys =
    {
        "id", "exhibitionId", "exhibitionName", "exhibitionStatus", "exhibitionStartDate", "exhibitionEndDate", "submissionTitle", "studentName",
        "studentCode", "competitionTitle", "askingPrice", "priceSourceNote", "status", "statusReason", "soldPrice", "soldDate", "customerId",
        "customerName", "consentDate", "consentMethod", "consentNote", "paidToStudent", "paidAmount", "paidDate", "paymentMethod", "paymentReference",
        "payDueDate", "overdueDays", "canReview", "canSell", "isLateSale", "canAdjustSale", "adjustLimitedToPayment", "canPay", "rowVersion",
    };

    // ---------------- Student ----------------

    [Fact]
    public async Task Student_exhibitions_list_and_detail_have_the_app_keys()
    {
        var s = _data.AddPainting(_data.Student1, Rating.BEST);
        _data.AddEntry(s, _data.Upcoming, EntryStatus.PENDING);

        var list = Body(await Student().Exhibitions());
        Assert.Equal(JsonValueKind.Array, list.ValueKind);
        Assert.Equal(4, list.GetArrayLength());
        var first = list[0];
        AssertKeys(first, SummaryKeys);
        Assert.Equal("UPCOMING", first.GetProperty("status").GetString());
        var mine = list.EnumerateArray().Single(x => x.GetProperty("id").GetInt64() == _data.Upcoming.Id);
        Assert.Equal(1, mine.GetProperty("entryCount").GetInt32());
        Assert.Equal(1, mine.GetProperty("pendingCount").GetInt32());
        Assert.Equal(20, mine.GetProperty("daysUntilStart").GetInt32());

        var detail = Body(await Student().Exhibition(_data.Upcoming.Id));
        AssertKeys(detail, DetailKeys);
        Assert.True(detail.GetProperty("canSubmitEntries").GetBoolean());
        Assert.False(detail.GetProperty("canEdit").GetBoolean());
        Assert.False(detail.GetProperty("canCancel").GetBoolean());
        Assert.Equal(JsonValueKind.Null, detail.GetProperty("cancelReason").ValueKind);
        Assert.False(Body(await Student().Exhibition(_data.Finished.Id)).GetProperty("canSubmitEntries").GetBoolean());
        await Assert.ThrowsAsync<NotFoundException>(() => Student().Exhibition(999999));
    }

    [Fact]
    public async Task Student_eligible_submissions_are_only_own()
    {
        var mine = _data.AddPainting(_data.Student1, Rating.BEST, title: "Mine");
        _data.AddPainting(_data.Student2, Rating.BEST, title: "Theirs");
        _data.AddPainting(_data.Student1, Rating.GOOD, title: "Too weak");

        var list = Body(await Student().Eligible());
        Assert.Equal(1, list.GetArrayLength());
        AssertKeys(list[0], "id", "title", "studentName", "studentCode", "competitionTitle");
        Assert.Equal(mine.Id, list[0].GetProperty("id").GetInt64());
    }

    [Fact]
    public async Task Student_propose_update_withdraw()
    {
        var s = _data.AddPainting(_data.Student1, Rating.BEST);
        var api = Student();

        var created = await api.Propose(new StudentExhibitionsApiController.ProposeRequest
        {
            ExhibitionId = _data.Upcoming.Id, SubmissionId = s.Id, AskingPrice = 120m,
        });
        Assert.Equal(201, Assert.IsAssignableFrom<ObjectResult>(created).StatusCode);
        var entry = Body(created);
        AssertKeys(entry, StudentEntryKeys);
        Assert.Equal("PENDING", entry.GetProperty("status").GetString());
        Assert.True(entry.GetProperty("canEdit").GetBoolean());
        var id = entry.GetProperty("id").GetInt64();
        var version = entry.GetProperty("rowVersion").GetString();

        // Stale rowVersion → 409 and nothing changes
        var stale = await api.UpdatePrice(id, new StudentExhibitionsApiController.UpdatePriceRequest { AskingPrice = 150m, RowVersion = "1" });
        Assert.Equal(409, Assert.IsAssignableFrom<ObjectResult>(stale).StatusCode);
        Assert.Equal(120m, _data.Reload(id)!.AskingPrice);

        var ok = await api.UpdatePrice(id, new StudentExhibitionsApiController.UpdatePriceRequest { AskingPrice = 150m, RowVersion = version });
        Assert.Equal(150m, Body(ok).GetProperty("askingPrice").GetDecimal());

        Assert.IsType<NoContentResult>(await api.Withdraw(id));
        Assert.Null(_data.Reload(id));
    }

    [Fact]
    public async Task Student_cannot_see_or_change_another_students_entry()
    {
        var s = _data.AddPainting(_data.Student2, Rating.BEST);
        var entry = _data.AddEntry(s, _data.Upcoming, EntryStatus.PENDING);

        await Assert.ThrowsAsync<NotFoundException>(() => Student().Entry(entry.Id));
        await Assert.ThrowsAsync<NotFoundException>(() => Student().UpdatePrice(entry.Id, new StudentExhibitionsApiController.UpdatePriceRequest { AskingPrice = 5m }));
        await Assert.ThrowsAsync<NotFoundException>(() => Student().Withdraw(entry.Id));
        Assert.Equal(0, Body(await Student().Entries()).GetArrayLength());
        Assert.Equal(1, Body(await Student(second: true).Entries()).GetArrayLength());
    }

    [Fact]
    public async Task Student_cannot_change_a_reviewed_entry()
    {
        var s = _data.AddPainting(_data.Student1, Rating.BEST);
        var entry = _data.AddEntry(s, _data.Upcoming, EntryStatus.REJECTED);

        var detail = Body(await Student().Entry(entry.Id));
        Assert.Equal("REJECTED", detail.GetProperty("status").GetString());
        Assert.Equal("Does not fit the theme", detail.GetProperty("statusReason").GetString());
        Assert.False(detail.GetProperty("canEdit").GetBoolean());
        await Assert.ThrowsAsync<BusinessException>(() => Student().Withdraw(entry.Id));
    }

    // ---------------- Staff: exhibitions ----------------

    [Fact]
    public async Task Staff_exhibitions_filter_and_detail_flags()
    {
        var all = Body(await Staff().Exhibitions(null, null));
        Assert.Equal(4, all.GetArrayLength());
        AssertKeys(all[0], SummaryKeys);

        Assert.Equal(2, Body(await Staff().Exhibitions(null, "Upcoming")).GetArrayLength());
        Assert.Equal(1, Body(await Staff().Exhibitions("Hue", "ongoing")).GetArrayLength());
        Assert.Equal(0, Body(await Staff().Exhibitions(null, "Cancelled")).GetArrayLength());

        var detail = Body(await Staff().Exhibition(_data.Upcoming.Id));
        AssertKeys(detail, DetailKeys);
        Assert.True(detail.GetProperty("canEdit").GetBoolean());
        Assert.True(detail.GetProperty("canDelete").GetBoolean());
        Assert.False(detail.GetProperty("canCancel").GetBoolean());
        Assert.False(detail.GetProperty("canSubmitEntries").GetBoolean());

        var cancel = Staff().CancelExhibition(_data.Upcoming.Id, new StaffExhibitionsApiController.CancelRequest { Reason = "x" });
        Assert.Equal(409, Assert.IsAssignableFrom<ObjectResult>(cancel).StatusCode);
    }

    [Fact]
    public async Task Staff_create_update_delete_exhibition()
    {
        var api = Staff();
        var created = await api.CreateExhibition(new StaffExhibitionsApiController.ExhibitionRequest
        {
            Name = "New Show", Venue = "Hall", City = "Hue", Organizer = "IFA", Description = "d",
            StartDate = Today.AddDays(10), EndDate = Today.AddDays(12),
        });
        Assert.Equal(201, Assert.IsAssignableFrom<ObjectResult>(created).StatusCode);
        var json = Body(created);
        var id = json.GetProperty("id").GetInt64();
        var version = json.GetProperty("rowVersion").GetString();

        var request = new StaffExhibitionsApiController.ExhibitionRequest
        {
            Name = "Renamed", Venue = "Hall", City = "Hue", StartDate = Today.AddDays(10), EndDate = Today.AddDays(13), RowVersion = "1",
        };
        Assert.Equal(409, Assert.IsAssignableFrom<ObjectResult>(await api.UpdateExhibition(id, request)).StatusCode);

        request.RowVersion = version;
        var updated = Body(await api.UpdateExhibition(id, request));
        Assert.Equal("Renamed", updated.GetProperty("name").GetString());

        Assert.IsType<NoContentResult>(await api.DeleteExhibition(id));
        await Assert.ThrowsAsync<NotFoundException>(() => api.Exhibition(id));

        // BR-42: an exhibition with entries cannot be deleted
        var s = _data.AddPainting(_data.Student1, Rating.BEST);
        _data.AddEntry(s, _data.Upcoming, EntryStatus.PENDING);
        await Assert.ThrowsAsync<BusinessException>(() => api.DeleteExhibition(_data.Upcoming.Id));
        Assert.False(Body(await api.Exhibition(_data.Upcoming.Id)).GetProperty("canDelete").GetBoolean());
    }

    [Fact]
    public async Task Staff_finish_turns_accepted_entries_into_unsold()
    {
        var s = _data.AddPainting(_data.Student1, Rating.BEST);
        var entry = _data.AddEntry(s, _data.Finished, EntryStatus.ACCEPTED);
        var api = Staff();

        Assert.True(Body(await api.Exhibition(_data.Finished.Id)).GetProperty("canFinish").GetBoolean());
        await Assert.ThrowsAsync<BusinessException>(() => api.FinishExhibition(_data.Ongoing.Id));

        var result = Body(await api.FinishExhibition(_data.Finished.Id));
        Assert.False(result.GetProperty("canFinish").GetBoolean());
        Assert.Equal(EntryStatus.UNSOLD, _data.Reload(entry.Id)!.Status);
    }

    // ---------------- Staff: entries ----------------

    [Fact]
    public async Task Staff_entries_list_filters_and_detail_shape()
    {
        var a = _data.AddPainting(_data.Student1, Rating.BEST, title: "Rice Fields");
        var b = _data.AddPainting(_data.Student2, Rating.BETTER, title: "River");
        var pending = _data.AddEntry(a, _data.Upcoming, EntryStatus.PENDING);
        _data.AddEntry(b, _data.Ongoing, EntryStatus.SOLD, paid: false);
        var api = Staff();

        var all = Body(await api.Entries(null, null, null, null));
        Assert.Equal(2, all.GetArrayLength());
        AssertKeys(all[0], "id", "exhibitionId", "exhibitionName", "submissionTitle", "studentName", "studentCode", "askingPrice",
            "status", "paidToStudent", "soldPrice", "statusReason");
        Assert.Equal(1, Body(await api.Entries(_data.Upcoming.Id, null, null, null)).GetArrayLength());
        Assert.Equal(1, Body(await api.Entries(null, "Sold", false, null)).GetArrayLength());
        Assert.Equal(0, Body(await api.Entries(null, "Sold", true, null)).GetArrayLength());
        Assert.Equal(0, Body(await api.Entries(null, "Withdrawn", null, null)).GetArrayLength());
        Assert.Equal(1, Body(await api.Entries(null, null, null, "HV0002")).GetArrayLength());

        var detail = Body(await api.Entry(pending.Id));
        AssertKeys(detail, EntryDetailKeys);
        Assert.True(detail.GetProperty("canReview").GetBoolean());
        Assert.False(detail.GetProperty("canAdjustSale").GetBoolean());
        Assert.Equal(JsonValueKind.Null, detail.GetProperty("consentDate").ValueKind);
        Assert.Equal(0, detail.GetProperty("overdueDays").GetInt32());
        await Assert.ThrowsAsync<NotFoundException>(() => api.Entry(999999));
    }

    [Fact]
    public async Task Staff_review_accept_reject_and_stale()
    {
        var a = _data.AddPainting(_data.Student1, Rating.BEST);
        var b = _data.AddPainting(_data.Student2, Rating.BEST);
        var e1 = _data.AddEntry(a, _data.Upcoming, EntryStatus.PENDING);
        var e2 = _data.AddEntry(b, _data.Upcoming, EntryStatus.PENDING);
        var api = Staff();

        var stale = await api.Review(e1.Id, new StaffExhibitionsApiController.ReviewRequest { Decision = "Accepted", RowVersion = "1" });
        Assert.Equal(409, Assert.IsAssignableFrom<ObjectResult>(stale).StatusCode);
        Assert.Equal(EntryStatus.PENDING, _data.Reload(e1.Id)!.Status);

        var accepted = Body(await api.Review(e1.Id, new StaffExhibitionsApiController.ReviewRequest { Decision = "Accepted" }));
        Assert.Equal("ACCEPTED", accepted.GetProperty("status").GetString());

        await Assert.ThrowsAsync<BusinessException>(() => api.Review(e2.Id, new StaffExhibitionsApiController.ReviewRequest { Decision = "Rejected" }));
        var rejected = Body(await api.Review(e2.Id, new StaffExhibitionsApiController.ReviewRequest { Decision = "rejected", Reason = "Poor quality" }));
        Assert.Equal("REJECTED", rejected.GetProperty("status").GetString());
        Assert.Equal("Poor quality", rejected.GetProperty("statusReason").GetString());

        await Assert.ThrowsAsync<BusinessException>(() => api.Review(e2.Id, new StaffExhibitionsApiController.ReviewRequest { Decision = "Maybe" }));
    }

    [Fact]
    public async Task Staff_create_entry_directly_with_price_source_note()
    {
        var s = _data.AddPainting(_data.Student1, Rating.BEST);
        var api = Staff();

        var eligible = Body(await api.Eligible("Rice"));
        Assert.Equal(1, eligible.GetArrayLength());

        var created = await api.CreateEntry(_data.Upcoming.Id, new StaffExhibitionsApiController.DirectEntryRequest
        {
            SubmissionId = s.Id, AskingPrice = 300m, PriceSourceNote = "Email of the student",
        });
        Assert.Equal(201, Assert.IsAssignableFrom<ObjectResult>(created).StatusCode);
        var json = Body(created);
        Assert.Equal("ACCEPTED", json.GetProperty("status").GetString());
        Assert.Equal("Email of the student", json.GetProperty("priceSourceNote").GetString());
        Assert.Equal(0, Body(await api.Eligible(null)).GetArrayLength());

        await Assert.ThrowsAsync<BusinessException>(() => api.CreateEntry(_data.Upcoming.Id,
            new StaffExhibitionsApiController.DirectEntryRequest { SubmissionId = s.Id, AskingPrice = 300m, PriceSourceNote = "x" }));
    }

    [Fact]
    public async Task Staff_sell_with_existing_and_new_customer_then_pay()
    {
        var a = _data.AddPainting(_data.Student1, Rating.BEST);
        var b = _data.AddPainting(_data.Student2, Rating.BEST);
        var e1 = _data.AddEntry(a, _data.Ongoing, EntryStatus.ACCEPTED, 200m);
        var e2 = _data.AddEntry(b, _data.Ongoing, EntryStatus.ACCEPTED, 200m);
        var api = Staff();

        var detail = Body(await api.Entry(e1.Id));
        Assert.True(detail.GetProperty("canSell").GetBoolean());
        Assert.False(detail.GetProperty("isLateSale").GetBoolean());

        // Stale → 409
        var stale = await api.Sell(e1.Id, new StaffExhibitionsApiController.SellRequest { SoldPrice = 200m, SoldDate = Today, CustomerId = _data.Customer.Id, RowVersion = "1" });
        Assert.Equal(409, Assert.IsAssignableFrom<ObjectResult>(stale).StatusCode);

        var sold = Body(await api.Sell(e1.Id, new StaffExhibitionsApiController.SellRequest
        {
            SoldPrice = 180m, SoldDate = Today, CustomerId = _data.Customer.Id, ConsentDate = Today, ConsentMethod = "Email",
            RowVersion = detail.GetProperty("rowVersion").GetString(),
        }));
        Assert.Equal("SOLD", sold.GetProperty("status").GetString());
        Assert.Equal(180m, sold.GetProperty("soldPrice").GetDecimal());
        Assert.Equal(_data.Customer.Id, sold.GetProperty("customerId").GetInt64());
        Assert.True(sold.GetProperty("canPay").GetBoolean());

        // New customer typed in the dialog
        var sold2 = Body(await api.Sell(e2.Id, new StaffExhibitionsApiController.SellRequest
        {
            SoldPrice = 200m, SoldDate = Today,
            NewCustomer = new StaffExhibitionsApiController.NewCustomerRequest { FullName = "Pham Thi Mai", Phone = "0912345678" },
        }));
        Assert.Equal("Pham Thi Mai", sold2.GetProperty("customerName").GetString());

        // Adjusting a recorded sale is not supported
        Assert.Equal(409, Assert.IsAssignableFrom<ObjectResult>(api.AdjustSale(e1.Id, new StaffExhibitionsApiController.SellRequest())).StatusCode);

        // Pay: the whole sale price
        await Assert.ThrowsAsync<BusinessException>(() => api.Pay(e1.Id, new StaffExhibitionsApiController.PayRequest { PaidAmount = 100m, PaidDate = Today }));
        var paid = Body(await api.Pay(e1.Id, new StaffExhibitionsApiController.PayRequest { PaidAmount = 180m, PaidDate = Today, PaymentMethod = "Bank" }));
        Assert.True(paid.GetProperty("paidToStudent").GetBoolean());
        Assert.Equal(180m, paid.GetProperty("paidAmount").GetDecimal());
        Assert.False(paid.GetProperty("canPay").GetBoolean());
    }

    [Fact]
    public async Task Staff_failed_sale_does_not_leave_the_new_customer_behind()
    {
        var a = _data.AddPainting(_data.Student1, Rating.BEST);
        var e = _data.AddEntry(a, _data.Upcoming, EntryStatus.ACCEPTED); // exhibition not started: sale refused
        var before = _data.Db.Customers.Count();

        await Assert.ThrowsAsync<BusinessException>(() => Staff().Sell(e.Id, new StaffExhibitionsApiController.SellRequest
        {
            SoldPrice = 200m, SoldDate = Today, NewCustomer = new StaffExhibitionsApiController.NewCustomerRequest { FullName = "Nobody" },
        }));

        Assert.Equal(before, _data.Db.Customers.Count());
    }

    [Fact]
    public async Task Staff_customers_search()
    {
        var all = Body(await Staff().Customers(null));
        Assert.Equal(1, all.GetArrayLength());
        AssertKeys(all[0], "id", "fullName", "phone", "email");
        Assert.Equal(1, Body(await Staff().Customers("Tuan")).GetArrayLength());
        Assert.Equal(0, Body(await Staff().Customers("zzz")).GetArrayLength());
    }
}
