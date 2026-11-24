using InstituteOfFineArts.Web.Helpers;
using InstituteOfFineArts.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace InstituteOfFineArts.Web.Data;

/// <summary>
/// Application DbContext: declares the 11 tables, applies the configurations in Data/Configurations
/// and sets the created/updated time of every entity automatically.
/// </summary>
public class AppDbContext : DbContext
{
    /// <summary>Creates the DbContext with the options registered in Program.cs.</summary>
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    /// <summary>Login accounts.</summary>
    public DbSet<Account> Accounts => Set<Account>();

    /// <summary>Staff profiles (ADMIN, STAFF, TEACHER).</summary>
    public DbSet<StaffProfile> StaffProfiles => Set<StaffProfile>();

    /// <summary>Student profiles.</summary>
    public DbSet<StudentProfile> StudentProfiles => Set<StudentProfile>();

    /// <summary>Competitions.</summary>
    public DbSet<Competition> Competitions => Set<Competition>();

    /// <summary>Planned awards of competitions.</summary>
    public DbSet<CompetitionAward> CompetitionAwards => Set<CompetitionAward>();

    /// <summary>Submissions.</summary>
    public DbSet<Submission> Submissions => Set<Submission>();

    /// <summary>Evaluations.</summary>
    public DbSet<Evaluation> Evaluations => Set<Evaluation>();

    /// <summary>Issued awards.</summary>
    public DbSet<AwardIssued> AwardsIssued => Set<AwardIssued>();

    /// <summary>Exhibitions.</summary>
    public DbSet<Exhibition> Exhibitions => Set<Exhibition>();

    /// <summary>Paintings sent to exhibitions.</summary>
    public DbSet<ExhibitionEntry> ExhibitionEntries => Set<ExhibitionEntry>();

    /// <summary>Painting buyers.</summary>
    public DbSet<Customer> Customers => Set<Customer>();

    /// <summary>In-app notifications (optional feature M12).</summary>
    public DbSet<Notification> Notifications => Set<Notification>();

    /// <summary>Activity log (optional feature M17).</summary>
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    /// <summary>Forgot-password tokens (optional feature M16).</summary>
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();

    /// <summary>Juries: teachers who grade a competition.</summary>
    public DbSet<CompetitionJudge> CompetitionJudges => Set<CompetitionJudge>();

    /// <summary>Library of grading criteria (admin).</summary>
    public DbSet<GradingCriterion> GradingCriteria => Set<GradingCriterion>();

    /// <summary>Criteria chosen for each competition with their weights.</summary>
    public DbSet<CompetitionCriterion> CompetitionCriteria => Set<CompetitionCriterion>();

    /// <summary>Table competition_milestone (timeline of a competition).</summary>
    public DbSet<CompetitionMilestone> CompetitionMilestones => Set<CompetitionMilestone>();

    /// <summary>The grade of each judge for each entry.</summary>
    public DbSet<JudgeEvaluation> JudgeEvaluations => Set<JudgeEvaluation>();

    /// <summary>Scores per criterion of each judge's grade.</summary>
    public DbSet<JudgeScore> JudgeScores => Set<JudgeScore>();

    /// <summary>Lowest average score of each rating (admin).</summary>
    public DbSet<RatingThreshold> RatingThresholds => Set<RatingThreshold>();

    /// <summary>Pictures of the entries (1–5 per entry).</summary>
    public DbSet<SubmissionImage> SubmissionImages => Set<SubmissionImage>();

    /// <summary>
    /// Model configuration: UTF-8 collation, per-table configuration, common audit columns.
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // UTF-8 collation: English data works normally and Vietnamese text entered by users is stored correctly
        modelBuilder.UseCollation("Vietnamese_100_CI_AS_SC_UTF8");

        // Load every IEntityTypeConfiguration class in Data/Configurations
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Audit columns of every BaseEntity: DATETIME2(0), created_at has a database default
        foreach (var entityType in modelBuilder.Model.GetEntityTypes()
                     .Where(t => typeof(BaseEntity).IsAssignableFrom(t.ClrType)))
        {
            var entity = modelBuilder.Entity(entityType.ClrType);
            entity.Property(nameof(BaseEntity.CreatedAt)).HasPrecision(0).HasDefaultValueSql("SYSDATETIME()");
            entity.Property(nameof(BaseEntity.UpdatedAt)).HasPrecision(0);
        }
    }

    /// <summary>
    /// Saves changes; sets CreatedAt (new records) and UpdatedAt (modified records) before saving.
    /// </summary>
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditTimes();
        return base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Synchronous SaveChanges, also sets the audit times.</summary>
    public override int SaveChanges()
    {
        ApplyAuditTimes();
        return base.SaveChanges();
    }

    /// <summary>Sets created/updated times (institute local time) for added or modified entities.</summary>
    private void ApplyAuditTimes()
    {
        var now = DateTimeHelper.Now;

        // Walk through the tracked entities that have changes
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
