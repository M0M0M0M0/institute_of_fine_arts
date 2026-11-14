using InstituteOfFineArts.Web.Controllers.Api.Dto;
using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Helpers;
using InstituteOfFineArts.Web.Infrastructure.Api;
using InstituteOfFineArts.Web.Infrastructure.Security;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.Services.Interfaces;
using InstituteOfFineArts.Web.ViewModels.Staff.Customers;
using InstituteOfFineArts.Web.ViewModels.Staff.ExhibitionEntries;
using InstituteOfFineArts.Web.ViewModels.Staff.Exhibitions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InstituteOfFineArts.Web.Controllers.Api;

/// <summary>
/// Mobile app, staff side of exhibitions (same role as the web area: STAFF): /api/staff/exhibitions, /exhibition-entries,
/// /eligible-submissions, /customers. Reads are AsNoTracking queries or service view models; every write goes through
/// IExhibitionService / IExhibitionEntryService / ICustomerService.
/// Not supported by this system (flags false, endpoints answer 409): cancelling an exhibition, adjusting a sale.
/// </summary>
[Route("api/staff")]
[Authorize(Roles = RoleNames.Staff)]
public class StaffExhibitionsApiController : ApiControllerBase
{
    private readonly AppDbContext _db;
    private readonly IExhibitionService _exhibitions;
    private readonly IExhibitionEntryService _entries;
    private readonly ICustomerService _customers;

    public StaffExhibitionsApiController(AppDbContext db, IExhibitionService exhibitions, IExhibitionEntryService entries,
        ICustomerService customers)
    {
        _db = db;
        _exhibitions = exhibitions;
        _entries = entries;
        _customers = customers;
    }

    /// <summary>Body of POST / PUT /api/staff/exhibitions.</summary>
    public sealed class ExhibitionRequest
    {
        public string? Name { get; set; }
        public string? Venue { get; set; }
        public string? City { get; set; }
        public string? Organizer { get; set; }
        public string? Description { get; set; }
        public DateOnly? StartDate { get; set; }
        public DateOnly? EndDate { get; set; }
        public string? RowVersion { get; set; }
    }

    /// <summary>Body of POST /api/staff/exhibitions/{id}/cancel.</summary>
    public sealed class CancelRequest
    {
        public string? Reason { get; set; }
    }

    /// <summary>Body of POST /api/staff/exhibitions/{exhibitionId}/entries.</summary>
    public sealed class DirectEntryRequest
    {
        public long? SubmissionId { get; set; }
        public decimal? AskingPrice { get; set; }
        public string? PriceSourceNote { get; set; }
    }

    /// <summary>Body of POST /api/staff/exhibition-entries/{id}/review.</summary>
    public sealed class ReviewRequest
    {
        public string? Decision { get; set; }
        public string? Reason { get; set; }
        public string? RowVersion { get; set; }
    }

    /// <summary>Buyer typed in the sale dialog.</summary>
    public sealed class NewCustomerRequest
    {
        public string? FullName { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
    }

    /// <summary>
    /// Body of POST /sell and PUT /sale. consentDate / consentMethod / consentNote / lateReason / reason are accepted but not
    /// stored: this system keeps no column for them.
    /// </summary>
    public sealed class SellRequest
    {
        public decimal? SoldPrice { get; set; }
        public DateOnly? SoldDate { get; set; }
        public long? CustomerId { get; set; }
        public NewCustomerRequest? NewCustomer { get; set; }
        public DateOnly? ConsentDate { get; set; }
        public string? ConsentMethod { get; set; }
        public string? ConsentNote { get; set; }
        public string? LateReason { get; set; }
        public string? Reason { get; set; }
        public string? RowVersion { get; set; }
    }

    /// <summary>Body of POST /api/staff/exhibition-entries/{id}/pay (paymentMethod / paymentReference are not stored).</summary>
    public sealed class PayRequest
    {
        public decimal? PaidAmount { get; set; }
        public DateOnly? PaidDate { get; set; }
        public string? PaymentMethod { get; set; }
        public string? PaymentReference { get; set; }
        public string? Reason { get; set; }
        public string? RowVersion { get; set; }
    }

    // ----- Exhibitions -----

    /// <summary>GET /api/staff/exhibitions?q=&amp;status=Upcoming|Ongoing|Finished (Cancelled → empty list).</summary>
    [HttpGet("exhibitions")]
    public async Task<IActionResult> Exhibitions([FromQuery] string? q, [FromQuery] string? status)
    {
        if (!ExhibitionApiQueries.TryParse<ExhibitionStatus>(status, out var parsed)) return Ok(Array.Empty<ExhibitionSummaryDto>());
        return Ok(await ExhibitionApiQueries.ExhibitionsAsync(_db, q, parsed, null));
    }

    /// <summary>GET /api/staff/exhibitions/{id}</summary>
    [HttpGet("exhibitions/{id:long}")]
    public async Task<IActionResult> Exhibition(long id)
        => Ok(await ExhibitionApiQueries.ExhibitionAsync(_db, id, null));

    /// <summary>POST /api/staff/exhibitions → the new exhibition.</summary>
    [HttpPost("exhibitions")]
    public async Task<IActionResult> CreateExhibition(ExhibitionRequest request)
    {
        var id = await _exhibitions.CreateAsync(ToForm(request), User.GetProfileId());
        return StatusCode(StatusCodes.Status201Created, await ExhibitionApiQueries.ExhibitionAsync(_db, id, null));
    }

    /// <summary>PUT /api/staff/exhibitions/{id}: 409 when the rowVersion is stale.</summary>
    [HttpPut("exhibitions/{id:long}")]
    public async Task<IActionResult> UpdateExhibition(long id, ExhibitionRequest request)
    {
        if (!await ExhibitionApiQueries.ExhibitionVersionOkAsync(_db, id, request.RowVersion)) return StaleVersion();
        await _exhibitions.UpdateAsync(id, ToForm(request));
        return Ok(await ExhibitionApiQueries.ExhibitionAsync(_db, id, null));
    }

    /// <summary>DELETE /api/staff/exhibitions/{id}: only without entries (BR-42).</summary>
    [HttpDelete("exhibitions/{id:long}")]
    public async Task<IActionResult> DeleteExhibition(long id)
    {
        await _exhibitions.DeleteAsync(id);
        return NoContent();
    }

    /// <summary>POST /api/staff/exhibitions/{id}/cancel: this system has no cancelled exhibitions.</summary>
    [HttpPost("exhibitions/{id:long}/cancel")]
    public IActionResult CancelExhibition(long id, CancelRequest request) => NotSupported("Cancelling an exhibition");

    /// <summary>
    /// POST /api/staff/exhibitions/{id}/finish: once the exhibition has ended, every ACCEPTED (unsold) painting becomes UNSOLD
    /// through IExhibitionEntryService.MarkUnsoldAsync, the rule the status job applies automatically.
    /// </summary>
    [HttpPost("exhibitions/{id:long}/finish")]
    public async Task<IActionResult> FinishExhibition(long id)
    {
        var exhibition = await ExhibitionApiQueries.ExhibitionAsync(_db, id, null);
        if (exhibition.Status != ExhibitionStatus.FINISHED)
        {
            throw new BusinessException("The exhibition has not finished yet: its end date has not passed.");
        }
        var accepted = await _db.ExhibitionEntries.AsNoTracking()
            .Where(e => e.ExhibitionId == id && e.Status == EntryStatus.ACCEPTED)
            .Select(e => e.Id)
            .ToListAsync();
        foreach (var entryId in accepted)
        {
            await _entries.MarkUnsoldAsync(entryId, User.GetProfileId());
        }
        return Ok(await ExhibitionApiQueries.ExhibitionAsync(_db, id, null));
    }

    // ----- Entries -----

    /// <summary>GET /api/staff/exhibition-entries?exhibitionId=&amp;status=&amp;paid=&amp;q= (status Withdrawn → empty list).</summary>
    [HttpGet("exhibition-entries")]
    public async Task<IActionResult> Entries([FromQuery] long? exhibitionId, [FromQuery] string? status, [FromQuery] bool? paid,
        [FromQuery] string? q)
    {
        if (!ExhibitionApiQueries.TryParse<EntryStatus>(status, out var parsed)) return Ok(Array.Empty<EntryListItemDto>());
        return Ok(await ExhibitionApiQueries.StaffEntriesAsync(_db, exhibitionId, parsed, paid, q));
    }

    /// <summary>GET /api/staff/exhibition-entries/{id}</summary>
    [HttpGet("exhibition-entries/{id:long}")]
    public async Task<IActionResult> Entry(long id)
        => Ok(await ExhibitionApiQueries.StaffEntryAsync(_db, _entries, id));

    /// <summary>GET /api/staff/eligible-submissions?q= (any student's painting that may be sent).</summary>
    [HttpGet("eligible-submissions")]
    public async Task<IActionResult> Eligible([FromQuery] string? q)
        => Ok(await ExhibitionApiQueries.EligibleAsync(_db, null, q));

    /// <summary>POST /api/staff/exhibitions/{exhibitionId}/entries: staff create an ACCEPTED entry with the student's price.</summary>
    [HttpPost("exhibitions/{exhibitionId:long}/entries")]
    public async Task<IActionResult> CreateEntry(long exhibitionId, DirectEntryRequest request)
    {
        var form = new EntryCreateFormVm
        {
            ExhibitionId = exhibitionId,
            SubmissionId = request.SubmissionId,
            AskingPrice = request.AskingPrice,
            Notes = request.PriceSourceNote,
        };
        var id = await _entries.CreateAsync(form, User.GetProfileId(), User.GetAccountId());
        return StatusCode(StatusCodes.Status201Created, await ExhibitionApiQueries.StaffEntryAsync(_db, _entries, id));
    }

    /// <summary>POST /api/staff/exhibition-entries/{id}/review: decision Accepted | Rejected (+ reason).</summary>
    [HttpPost("exhibition-entries/{id:long}/review")]
    public async Task<IActionResult> Review(long id, ReviewRequest request)
    {
        if (!await ExhibitionApiQueries.EntryVersionOkAsync(_db, id, request.RowVersion)) return StaleVersion();
        var decision = request.Decision?.Trim();
        if (string.Equals(decision, "Accepted", StringComparison.OrdinalIgnoreCase))
        {
            await _entries.AcceptAsync(id, User.GetProfileId());
        }
        else if (string.Equals(decision, "Rejected", StringComparison.OrdinalIgnoreCase))
        {
            await _entries.RejectAsync(id, new RejectFormVm { Reason = request.Reason }, User.GetProfileId());
        }
        else
        {
            throw new BusinessException("The decision must be Accepted or Rejected.", "decision");
        }
        return Ok(await ExhibitionApiQueries.StaffEntryAsync(_db, _entries, id));
    }

    /// <summary>POST /api/staff/exhibition-entries/{id}/sell: ACCEPTED (or late UNSOLD) → SOLD; a new customer is created first.</summary>
    [HttpPost("exhibition-entries/{id:long}/sell")]
    public async Task<IActionResult> Sell(long id, SellRequest request)
    {
        if (!await ExhibitionApiQueries.EntryVersionOkAsync(_db, id, request.RowVersion)) return StaleVersion();

        long? customerId = request.CustomerId;
        long? createdCustomerId = null;
        if (request.NewCustomer is { } c)
        {
            createdCustomerId = customerId = await _customers.CreateAsync(new CustomerFormVm
            {
                FullName = c.FullName?.Trim() ?? string.Empty,
                Phone = string.IsNullOrWhiteSpace(c.Phone) ? null : c.Phone.Trim(),
                Email = string.IsNullOrWhiteSpace(c.Email) ? null : c.Email.Trim(),
            });
        }
        try
        {
            await _entries.MarkSoldAsync(id, new SoldFormVm
            {
                SoldPrice = request.SoldPrice,
                SoldDate = request.SoldDate,
                CustomerId = customerId,
            }, User.GetProfileId());
        }
        catch
        {
            // The sale was refused: do not leave the customer typed in the dialog behind
            if (createdCustomerId is { } orphan)
            {
                try { await _customers.DeleteAsync(orphan); } catch (Exception) { /* keep the original error */ }
            }
            throw;
        }
        return Ok(await ExhibitionApiQueries.StaffEntryAsync(_db, _entries, id));
    }

    /// <summary>PUT /api/staff/exhibition-entries/{id}/sale: this system cannot edit a recorded sale (canAdjustSale is false).</summary>
    [HttpPut("exhibition-entries/{id:long}/sale")]
    public IActionResult AdjustSale(long id, SellRequest request) => NotSupported("Adjusting a recorded sale");

    /// <summary>POST /api/staff/exhibition-entries/{id}/pay: records the full payment to the student (BR-45).</summary>
    [HttpPost("exhibition-entries/{id:long}/pay")]
    public async Task<IActionResult> Pay(long id, PayRequest request)
    {
        if (!await ExhibitionApiQueries.EntryVersionOkAsync(_db, id, request.RowVersion)) return StaleVersion();
        await _entries.MarkPaidAsync(id, new PaidFormVm { PaidAmount = request.PaidAmount, PaidDate = request.PaidDate }, User.GetProfileId());
        return Ok(await ExhibitionApiQueries.StaffEntryAsync(_db, _entries, id));
    }

    // ----- Customers -----

    /// <summary>GET /api/staff/customers?q= (name, phone or email).</summary>
    [HttpGet("customers")]
    public async Task<IActionResult> Customers([FromQuery] string? q)
    {
        var page = await _customers.SearchAsync(new CustomerSearchVm { Keyword = q }, 200);
        return Ok(page.Items.Select(c => new CustomerDto(c.Id, c.FullName, c.Phone, c.Email)).ToList());
    }

    private static ExhibitionFormVm ToForm(ExhibitionRequest r) => new()
    {
        Name = r.Name ?? string.Empty,
        Venue = r.Venue ?? string.Empty,
        City = r.City ?? string.Empty,
        Organizer = r.Organizer,
        Description = r.Description,
        StartDate = r.StartDate,
        EndDate = r.EndDate,
    };
}
