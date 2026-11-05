using InstituteOfFineArts.Web.ViewModels.Common;
using InstituteOfFineArts.Web.ViewModels.Staff.Customers;

namespace InstituteOfFineArts.Web.Services.Interfaces;

/// <summary>
/// Customers who buy paintings at exhibitions (M09, task BE2-10; F-M09-07, SCR-32): create, search, details with
/// purchases, edit, delete. Business rule violations throw BusinessException; unknown ids throw NotFoundException.
/// </summary>
public interface ICustomerService
{
    /// <summary>Searches customers by part of the name, phone or email, alphabetically.</summary>
    Task<PagedResult<CustomerListItemVm>> SearchAsync(CustomerSearchVm search, int pageSize = 10);

    /// <summary>Details with the paintings the customer bought (read only).</summary>
    Task<CustomerDetailsVm> GetDetailsAsync(long id);

    /// <summary>Current values for the edit form.</summary>
    Task<CustomerFormVm> GetForEditAsync(long id);

    /// <summary>Creates a customer; returns its id.</summary>
    Task<long> CreateAsync(CustomerFormVm form);

    /// <summary>Saves the changes.</summary>
    Task UpdateAsync(long id, CustomerFormVm form);

    /// <summary>Deletes a customer who has not bought anything.</summary>
    Task DeleteAsync(long id);
}
