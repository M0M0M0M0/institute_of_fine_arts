using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InstituteOfFineArts.Web.Data.Configurations;

/// <summary>
/// EF Core configuration of table customer: column types, keys, CHECK constraints, indexes and relationships.
/// Design: docs/plan/02_Kien_truc_Cong_nghe_CSDL.md section 6.2.
/// </summary>
public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    /// <summary>Applies the configuration to the Customer entity.</summary>
    public void Configure(EntityTypeBuilder<Customer> b)
    {
        b.ToTable("customer");

        // Column types
        b.Property(x => x.FullName).HasMaxLength(100);
        b.Property(x => x.Email).HasMaxLength(100).IsUnicode(false);
        b.Property(x => x.Phone).HasMaxLength(20).IsUnicode(false);
        b.Property(x => x.Address).HasMaxLength(255);
        b.Property(x => x.Notes).HasMaxLength(500);

        // Look up customers by phone number
        b.HasIndex(x => x.Phone).HasDatabaseName("IX_customer_phone");
    }
}
