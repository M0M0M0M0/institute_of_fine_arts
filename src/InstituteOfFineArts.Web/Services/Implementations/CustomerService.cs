using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Services.Interfaces;
using InstituteOfFineArts.Web.ViewModels.Common;
using InstituteOfFineArts.Web.ViewModels.Staff.Customers;
using Microsoft.EntityFrameworkCore;

namespace InstituteOfFineArts.Web.Services.Implementations;

/// <summary>
/// Customer management for staff (task BE2-10, F-M09-07). Customers do not log in; they are only recorded so a
/// sale (BR-44) can name its buyer. A customer who bought a painting is kept, so sale records never lose their buyer.
/// </summary>
public class CustomerService : ICustomerService
{
    /// <summary>Message when a customer with purchases is deleted.</summary>
    public const string HasPurchasesDeleteMessage = "This customer has bought artworks and cannot be deleted.";

    /// <summary>Message when the full name is blank.</summary>
    public const string FullNameRequiredMessage = "Please enter the full name";

    /// <summary>Message when the email format is wrong (same text as the form).</summary>
    public const string InvalidEmailMessage = "Please enter a valid email address";

    /// <summary>Message when the phone format is wrong (same text as the form).</summary>
    public const string InvalidPhoneMessage = "Please enter a valid phone number";

    /// <summary>Database access.</summary>
    private readonly AppDbContext _db;

    /// <summary>Logs created / changed / deleted customers.</summary>
    private readonly ILogger<CustomerService> _logger;

    /// <summary>Receives the required services through Dependency Injection.</summary>
    public CustomerService(AppDbContext db, ILogger<CustomerService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<PagedResult<CustomerListItemVm>> SearchAsync(CustomerSearchVm search, int pageSize = 10)
    {
        var query = _db.Customers.AsNoTracking().AsQueryable();

        // Keyword: part of the name, phone or email
        if (!string.IsNullOrWhiteSpace(search.Keyword))
        {
            var keyword = search.Keyword.Trim();
            query = query.Where(c => c.FullName.Contains(keyword)
                                     || (c.Phone != null && c.Phone.Contains(keyword))
                                     || (c.Email != null && c.Email.Contains(keyword)));
        }

        var rows = query
            .OrderBy(c => c.FullName).ThenBy(c => c.Id)
            .Select(c => new CustomerListItemVm
            {
                Id = c.Id,
                FullName = c.FullName,
                Email = c.Email,
                Phone = c.Phone,
                PurchaseCount = c.Purchases.Count,
            });
        return await PagedResult<CustomerListItemVm>.CreateAsync(rows, search.Page, pageSize);
    }

    /// <inheritdoc />
    public async Task<CustomerDetailsVm> GetDetailsAsync(long id)
    {
        var vm = await _db.Customers.AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new CustomerDetailsVm
            {
                Id = c.Id,
                FullName = c.FullName,
                Email = c.Email,
                Phone = c.Phone,
                Address = c.Address,
                Notes = c.Notes,
                CreatedAt = c.CreatedAt,
            })
            .FirstOrDefaultAsync()
            ?? throw new NotFoundException($"Customer {id} was not found.");

        // Every entry that names this customer as buyer (sold entries), newest sale first
        vm.Purchases = await _db.ExhibitionEntries.AsNoTracking()
            .Where(x => x.CustomerId == id)
            .OrderByDescending(x => x.SoldDate).ThenByDescending(x => x.Id)
            .Select(x => new CustomerPurchaseVm(x.Id, x.Submission.Title, x.ExhibitionId, x.Exhibition.Name, x.SoldPrice, x.SoldDate))
            .ToListAsync();
        return vm;
    }

    /// <inheritdoc />
    public async Task<CustomerFormVm> GetForEditAsync(long id)
    {
        var c = await FindAsync(id);
        return new CustomerFormVm
        {
            Id = c.Id,
            FullName = c.FullName,
            Email = c.Email,
            Phone = c.Phone,
            Address = c.Address,
            Notes = c.Notes,
        };
    }

    /// <inheritdoc />
    public async Task<long> CreateAsync(CustomerFormVm form)
    {
        Check(form);
        var customer = new Customer();
        Copy(form, customer);
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Customer {CustomerId} created", customer.Id);
        return customer.Id;
    }

    /// <inheritdoc />
    public async Task UpdateAsync(long id, CustomerFormVm form)
    {
        var customer = await FindAsync(id);
        Check(form);
        Copy(form, customer);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Customer {CustomerId} updated", customer.Id);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(long id)
    {
        var customer = await FindAsync(id);

        // A buyer of a sold painting must stay on the sale record (BR-44 requires a customer for SOLD)
        if (await _db.ExhibitionEntries.AnyAsync(x => x.CustomerId == id))
        {
            throw new BusinessException(HasPurchasesDeleteMessage);
        }

        _db.Customers.Remove(customer);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Customer {CustomerId} deleted", id);
    }

    /// <summary>Loads a tracked customer or throws NotFoundException.</summary>
    private async Task<Customer> FindAsync(long id)
        => await _db.Customers.FirstOrDefaultAsync(c => c.Id == id)
           ?? throw new NotFoundException($"Customer {id} was not found.");

    /// <summary>Re-checks the form rules here so no caller can skip them (name required, email and phone format).</summary>
    private static void Check(CustomerFormVm form)
    {
        if (string.IsNullOrWhiteSpace(form.FullName))
        {
            throw new BusinessException(FullNameRequiredMessage, nameof(CustomerFormVm.FullName));
        }
        var email = Clean(form.Email);
        if (email != null && !new EmailAddressAttribute().IsValid(email))
        {
            throw new BusinessException(InvalidEmailMessage, nameof(CustomerFormVm.Email));
        }
        var phone = Clean(form.Phone);
        if (phone != null && !Regex.IsMatch(phone, CustomerFormVm.PhonePattern))
        {
            throw new BusinessException(InvalidPhoneMessage, nameof(CustomerFormVm.Phone));
        }
    }

    /// <summary>Copies the trimmed form values to the entity (blank optional fields become null).</summary>
    private static void Copy(CustomerFormVm form, Customer customer)
    {
        customer.FullName = form.FullName.Trim();
        customer.Email = Clean(form.Email);
        customer.Phone = Clean(form.Phone);
        customer.Address = Clean(form.Address);
        customer.Notes = Clean(form.Notes);
    }

    /// <summary>Trims text; blank becomes null.</summary>
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
