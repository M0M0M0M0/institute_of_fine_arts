namespace InstituteOfFineArts.Web.Models.Entities;

/// <summary>
/// Base class of every entity: primary key and two audit columns.
/// CreatedAt/UpdatedAt are set automatically in AppDbContext.SaveChangesAsync.
/// </summary>
public abstract class BaseEntity
{
    /// <summary>Primary key (BIGINT IDENTITY).</summary>
    public long Id { get; set; }

    /// <summary>Time the record was created (institute local time).</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Time of the latest update; null if never updated.</summary>
    public DateTime? UpdatedAt { get; set; }
}
