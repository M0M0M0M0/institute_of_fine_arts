using System.ComponentModel.DataAnnotations;
using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Helpers;
using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.Services.Implementations;
using InstituteOfFineArts.Web.ViewModels.Staff.Customers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InstituteOfFineArts.Tests.Services;

/// <summary>
/// CustomerService (task BE2-10; F-M09-07, SCR-32): create / edit with name, email and phone rules, search by
/// name / phone / email, details with purchases, delete blocked when the customer bought artworks.
/// </summary>
public class CustomerServiceTests
{
    /// <summary>Empty in-memory database.</summary>
    private static AppDbContext CreateDb()
        => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    /// <summary>Service under test.</summary>
    private static CustomerService Service(AppDbContext db) => new(db, NullLogger<CustomerService>.Instance);

    /// <summary>Valid form.</summary>
    private static CustomerFormVm Form(string name = "Hoang Anh Tuan") => new()
    {
        FullName = name, Email = "khachhang1@example.com", Phone = "0901 000 001", Address = "District 1", Notes = "Collector",
    };

    /// <summary>Adds a customer directly.</summary>
    private static Customer Add(AppDbContext db, string name, string? email, string? phone)
    {
        var c = new Customer { FullName = name, Email = email, Phone = phone };
        db.Customers.Add(c);
        db.SaveChanges();
        return c;
    }

    /// <summary>Records a sold painting bought by the customer.</summary>
    private static void AddPurchase(AppDbContext db, Customer c, string title, decimal price, int soldDaysAgo)
    {
        db.ExhibitionEntries.Add(new ExhibitionEntry
        {
            AskingPrice = price, Status = EntryStatus.SOLD, SoldPrice = price, SoldDate = DateTimeHelper.Today.AddDays(-soldDaysAgo),
            CustomerId = c.Id, ProposedBy = 1,
            Exhibition = new Exhibition { Name = "Hanoi Expo", Venue = "V", City = "Hanoi", StartDate = DateTimeHelper.Today.AddDays(-20), EndDate = DateTimeHelper.Today, CreatedBy = 1 },
            Submission = new Submission { CompetitionId = 1, StudentId = 1, Title = title, ImagePath = "p.jpg", OriginalFileName = "p.jpg", TextContent = "t", Description = "d" },
        });
        db.SaveChanges();
    }

    /// <summary>F-M09-07: valid form → saved with trimmed values; blank optional fields become null.</summary>
    [Fact]
    public async Task Create_Valid()
    {
        using var db = CreateDb();
        var form = Form("  Hoang Anh Tuan ");
        form.Address = "  ";

        var id = await Service(db).CreateAsync(form);

        var c = await db.Customers.SingleAsync(x => x.Id == id);
        Assert.Equal("Hoang Anh Tuan", c.FullName);
        Assert.Equal("0901 000 001", c.Phone);
        Assert.Null(c.Address);
    }

    /// <summary>Full name required; email and phone formats checked by the service as well as the form.</summary>
    [Fact]
    public async Task Create_InvalidData_Rejected()
    {
        using var db = CreateDb();
        var service = Service(db);

        var blank = Form("  ");
        Assert.Equal(CustomerService.FullNameRequiredMessage, (await Assert.ThrowsAsync<BusinessException>(() => service.CreateAsync(blank))).Message);

        var badEmail = Form();
        badEmail.Email = "not-an-email";
        var ex = await Assert.ThrowsAsync<BusinessException>(() => service.CreateAsync(badEmail));
        Assert.Equal(CustomerService.InvalidEmailMessage, ex.Message);
        Assert.Equal(nameof(CustomerFormVm.Email), ex.Field);

        var badPhone = Form();
        badPhone.Phone = "abc";
        Assert.Equal(CustomerService.InvalidPhoneMessage, (await Assert.ThrowsAsync<BusinessException>(() => service.CreateAsync(badPhone))).Message);

        // The form attributes give the same messages
        var results = new List<ValidationResult>();
        Assert.False(Validator.TryValidateObject(badPhone, new ValidationContext(badPhone), results, true));
        Assert.Contains(results, r => r.ErrorMessage == CustomerService.InvalidPhoneMessage);
        Assert.Empty(db.Customers);
    }

    /// <summary>Search: one keyword matched against name, phone and email, alphabetical order.</summary>
    [Fact]
    public async Task Search_ByNamePhoneOrEmail()
    {
        using var db = CreateDb();
        Add(db, "Nguyen Thi Lan", "lan@example.com", "0901000002");
        Add(db, "Hoang Anh Tuan", "tuan@example.com", "0901000001");
        Add(db, "Pham Gia Huy", null, null);
        var service = Service(db);

        Assert.Equal(new[] { "Hoang Anh Tuan", "Nguyen Thi Lan", "Pham Gia Huy" },
            (await service.SearchAsync(new CustomerSearchVm())).Items.Select(x => x.FullName));
        Assert.Equal("Nguyen Thi Lan", (await service.SearchAsync(new CustomerSearchVm { Keyword = "Lan" })).Items.Single().FullName);
        Assert.Equal("Hoang Anh Tuan", (await service.SearchAsync(new CustomerSearchVm { Keyword = "000001" })).Items.Single().FullName);
        Assert.Equal("Hoang Anh Tuan", (await service.SearchAsync(new CustomerSearchVm { Keyword = "tuan@" })).Items.Single().FullName);
    }

    /// <summary>Details list the purchases (sold entries), newest sale first, with the total.</summary>
    [Fact]
    public async Task GetDetails_ListsPurchases()
    {
        using var db = CreateDb();
        var c = Add(db, "Hoang Anh Tuan", null, null);
        AddPurchase(db, c, "Old village", 250m, 12);
        AddPurchase(db, c, "Rice fields", 320m, 2);

        var vm = await Service(db).GetDetailsAsync(c.Id);

        Assert.Equal(new[] { "Rice fields", "Old village" }, vm.Purchases.Select(p => p.ArtworkTitle));
        Assert.Equal("Hanoi Expo", vm.Purchases[0].ExhibitionName);
        Assert.Equal(570m, vm.PurchaseTotal);
        Assert.False(vm.CanDelete);
    }

    /// <summary>Edit saves the new values.</summary>
    [Fact]
    public async Task Update_Valid()
    {
        using var db = CreateDb();
        var c = Add(db, "Old name", null, null);
        await Service(db).UpdateAsync(c.Id, Form("New name"));
        Assert.Equal("New name", (await db.Customers.SingleAsync()).FullName);
    }

    /// <summary>A customer without purchases is deleted.</summary>
    [Fact]
    public async Task Delete_NoPurchases_Deleted()
    {
        using var db = CreateDb();
        var c = Add(db, "Nobody", null, null);
        await Service(db).DeleteAsync(c.Id);
        Assert.Empty(db.Customers);
    }

    /// <summary>A customer who bought artworks cannot be deleted (sale records keep their buyer).</summary>
    [Fact]
    public async Task Delete_WithPurchases_Blocked()
    {
        using var db = CreateDb();
        var c = Add(db, "Buyer", null, null);
        AddPurchase(db, c, "Rice fields", 320m, 2);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => Service(db).DeleteAsync(c.Id));
        Assert.Equal(CustomerService.HasPurchasesDeleteMessage, ex.Message);
        Assert.Single(db.Customers);
    }

    /// <summary>Unknown id → NotFoundException (404).</summary>
    [Fact]
    public async Task Unknown_NotFound()
    {
        using var db = CreateDb();
        var service = Service(db);
        await Assert.ThrowsAsync<NotFoundException>(() => service.GetDetailsAsync(999));
        await Assert.ThrowsAsync<NotFoundException>(() => service.GetForEditAsync(999));
        await Assert.ThrowsAsync<NotFoundException>(() => service.UpdateAsync(999, Form()));
        await Assert.ThrowsAsync<NotFoundException>(() => service.DeleteAsync(999));
    }
}
