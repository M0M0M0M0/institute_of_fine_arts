using System.ComponentModel.DataAnnotations;

namespace InstituteOfFineArts.Web.ViewModels.Staff.Customers;

/// <summary>
/// Create / edit form of a customer who buys paintings at exhibitions (F-M09-07, SCR-32).
/// </summary>
public class CustomerFormVm
{
    /// <summary>Phone pattern shared with the staff / student / profile forms.</summary>
    public const string PhonePattern = @"^\+?[0-9(][0-9 .\-()]{6,19}$";

    /// <summary>Customer id (0 when creating).</summary>
    public long Id { get; set; }

    /// <summary>Full name (required).</summary>
    [Required(ErrorMessage = "Please enter the full name")]
    [StringLength(100, ErrorMessage = "Full name must be at most 100 characters")]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    /// <summary>Email (optional).</summary>
    [EmailAddress(ErrorMessage = "Please enter a valid email address")]
    [StringLength(100, ErrorMessage = "Email must be at most 100 characters")]
    [Display(Name = "Email")]
    public string? Email { get; set; }

    /// <summary>Phone number: digits with optional +, spaces, dots, dashes or brackets (optional).</summary>
    [StringLength(20, ErrorMessage = "Phone must be at most 20 characters")]
    [RegularExpression(PhonePattern, ErrorMessage = "Please enter a valid phone number")]
    [Display(Name = "Phone")]
    public string? Phone { get; set; }

    /// <summary>Address.</summary>
    [StringLength(255, ErrorMessage = "Address must be at most 255 characters")]
    [Display(Name = "Address")]
    public string? Address { get; set; }

    /// <summary>Notes.</summary>
    [StringLength(500, ErrorMessage = "Notes must be at most 500 characters")]
    [Display(Name = "Notes")]
    public string? Notes { get; set; }
}

/// <summary>
/// Filters of the customer list: one keyword matched against name, phone and email; page.
/// </summary>
public class CustomerSearchVm
{
    /// <summary>Part of the name, phone or email.</summary>
    [StringLength(100)]
    [Display(Name = "Name, phone or email")]
    public string? Keyword { get; set; }

    /// <summary>Page number (1-based).</summary>
    public int Page { get; set; } = 1;
}

/// <summary>
/// One row of the customer list.
/// </summary>
public class CustomerListItemVm
{
    /// <summary>Customer id.</summary>
    public long Id { get; set; }

    /// <summary>Full name.</summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>Email.</summary>
    public string? Email { get; set; }

    /// <summary>Phone number.</summary>
    public string? Phone { get; set; }

    /// <summary>Number of paintings bought.</summary>
    public int PurchaseCount { get; set; }
}

/// <summary>
/// Customer details with the paintings they bought (read only).
/// </summary>
public class CustomerDetailsVm
{
    /// <summary>Customer id.</summary>
    public long Id { get; set; }

    /// <summary>Full name.</summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>Email.</summary>
    public string? Email { get; set; }

    /// <summary>Phone number.</summary>
    public string? Phone { get; set; }

    /// <summary>Address.</summary>
    public string? Address { get; set; }

    /// <summary>Notes.</summary>
    public string? Notes { get; set; }

    /// <summary>When the customer was recorded.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Paintings bought (sold exhibition entries), newest sale first.</summary>
    public IReadOnlyList<CustomerPurchaseVm> Purchases { get; set; } = Array.Empty<CustomerPurchaseVm>();

    /// <summary>Total paid by the customer (USD).</summary>
    public decimal PurchaseTotal => Purchases.Sum(p => p.SoldPrice ?? 0m);

    /// <summary>A customer with purchases cannot be deleted (the sale records must keep their buyer).</summary>
    public bool CanDelete => Purchases.Count == 0;
}

/// <summary>One painting bought by a customer.</summary>
/// <param name="EntryId">exhibition_entry.id</param>
/// <param name="ArtworkTitle">Title of the submission</param>
/// <param name="ExhibitionId">Exhibition id</param>
/// <param name="ExhibitionName">Exhibition name</param>
/// <param name="SoldPrice">Sale price</param>
/// <param name="SoldDate">Sale date</param>
public record CustomerPurchaseVm(long EntryId, string ArtworkTitle, long ExhibitionId, string ExhibitionName, decimal? SoldPrice, DateOnly? SoldDate);
