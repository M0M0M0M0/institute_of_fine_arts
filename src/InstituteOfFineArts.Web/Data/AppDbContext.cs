using InstituteOfFineArts.Web.Helpers;
using InstituteOfFineArts.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace InstituteOfFineArts.Web.Data;

/// <summary>
/// DbContext của ứng dụng: khai báo 11 bảng, áp dụng cấu hình trong Data/Configurations
/// và tự gán thời gian tạo/sửa cho mọi entity.
/// </summary>
public class AppDbContext : DbContext
{
    /// <summary>Khởi tạo DbContext với cấu hình được đăng ký trong Program.cs.</summary>
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    /// <summary>Tài khoản đăng nhập.</summary>
    public DbSet<Account> Accounts => Set<Account>();

    /// <summary>Hồ sơ nhân sự (ADMIN, MANAGER, STAFF).</summary>
    public DbSet<StaffProfile> StaffProfiles => Set<StaffProfile>();

    /// <summary>Hồ sơ học viên.</summary>
    public DbSet<StudentProfile> StudentProfiles => Set<StudentProfile>();

    /// <summary>Cuộc thi.</summary>
    public DbSet<Competition> Competitions => Set<Competition>();

    /// <summary>Giải thưởng dự kiến của cuộc thi.</summary>
    public DbSet<CompetitionAward> CompetitionAwards => Set<CompetitionAward>();

    /// <summary>Bài dự thi.</summary>
    public DbSet<Submission> Submissions => Set<Submission>();

    /// <summary>Kết quả chấm.</summary>
    public DbSet<Evaluation> Evaluations => Set<Evaluation>();

    /// <summary>Giải đã trao.</summary>
    public DbSet<AwardIssued> AwardsIssued => Set<AwardIssued>();

    /// <summary>Triển lãm.</summary>
    public DbSet<Exhibition> Exhibitions => Set<Exhibition>();

    /// <summary>Tranh gửi triển lãm.</summary>
    public DbSet<ExhibitionEntry> ExhibitionEntries => Set<ExhibitionEntry>();

    /// <summary>Khách mua tranh.</summary>
    public DbSet<Customer> Customers => Set<Customer>();

    /// <summary>
    /// Cấu hình mô hình: collation tiếng Việt, cấu hình từng bảng, cột audit chung.
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // CSDL dùng collation tiếng Việt, hỗ trợ UTF-8 để lưu tiếng Việt có dấu
        modelBuilder.UseCollation("Vietnamese_100_CI_AS_SC_UTF8");

        // Nạp toàn bộ lớp IEntityTypeConfiguration trong thư mục Data/Configurations
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Cột audit của mọi entity kế thừa BaseEntity: DATETIME2(0), created_at có giá trị mặc định
        foreach (var entityType in modelBuilder.Model.GetEntityTypes()
                     .Where(t => typeof(BaseEntity).IsAssignableFrom(t.ClrType)))
        {
            var entity = modelBuilder.Entity(entityType.ClrType);
            entity.Property(nameof(BaseEntity.CreatedAt)).HasPrecision(0).HasDefaultValueSql("SYSDATETIME()");
            entity.Property(nameof(BaseEntity.UpdatedAt)).HasPrecision(0);
        }
    }

    /// <summary>
    /// Lưu thay đổi; trước khi lưu tự gán CreatedAt (bản ghi mới) và UpdatedAt (bản ghi sửa).
    /// </summary>
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditTimes();
        return base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Phiên bản đồng bộ của SaveChanges, cũng gán thời gian audit.</summary>
    public override int SaveChanges()
    {
        ApplyAuditTimes();
        return base.SaveChanges();
    }

    /// <summary>Gán thời gian tạo/sửa theo giờ Việt Nam cho các entity đang được thêm hoặc sửa.</summary>
    private void ApplyAuditTimes()
    {
        var now = DateTimeHelper.Now;

        // Duyệt các entity đang được theo dõi có thay đổi
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
            }
        }
    }
}
