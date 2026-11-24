using InstituteOfFineArts.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InstituteOfFineArts.Web.Data.Configurations;

/// <summary>EF Core configuration of tables judge_evaluation and judge_score.</summary>
public class JudgeEvaluationConfiguration : IEntityTypeConfiguration<JudgeEvaluation>
{
    /// <summary>Applies the configuration.</summary>
    public void Configure(EntityTypeBuilder<JudgeEvaluation> b)
    {
        // A disqualification has a reason and no total; a grade has a total between 0 and 100
        b.ToTable("judge_evaluation", t =>
        {
            t.HasCheckConstraint("CK_judge_evaluation_dq", "[disqualified] = 0 OR ([negative_points] IS NOT NULL AND [total_score] IS NULL)");
            t.HasCheckConstraint("CK_judge_evaluation_total", "[total_score] IS NULL OR ([total_score] >= 0 AND [total_score] <= 100)");
        });
        b.Property(x => x.TotalScore).HasPrecision(5, 2);
        b.Property(x => x.EvaluatedAt).HasPrecision(0).HasDefaultValueSql("SYSDATETIME()");
        // One grade per judge per entry
        b.HasIndex(x => new { x.SubmissionId, x.JudgeId }).IsUnique().HasDatabaseName("UX_judge_evaluation");
        b.HasIndex(x => x.JudgeId).HasDatabaseName("IX_judge_evaluation_judge");
        b.HasOne(x => x.Submission).WithMany(s => s.JudgeEvaluations).HasForeignKey(x => x.SubmissionId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Judge).WithMany().HasForeignKey(x => x.JudgeId).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>EF Core configuration of table judge_score.</summary>
public class JudgeScoreConfiguration : IEntityTypeConfiguration<JudgeScore>
{
    /// <summary>Applies the configuration.</summary>
    public void Configure(EntityTypeBuilder<JudgeScore> b)
    {
        b.ToTable("judge_score", t => t.HasCheckConstraint("CK_judge_score_range", "[score] >= 0 AND [score] <= 100"));
        b.Property(x => x.Score).HasPrecision(5, 2);
        b.Property(x => x.Comment).HasMaxLength(1000);
        b.HasIndex(x => new { x.JudgeEvaluationId, x.CompetitionCriterionId }).IsUnique().HasDatabaseName("UX_judge_score");
        b.HasOne(x => x.JudgeEvaluation).WithMany(e => e.Scores).HasForeignKey(x => x.JudgeEvaluationId).OnDelete(DeleteBehavior.Cascade);
        // NoAction: the criterion row is removed only when the competition is (which cascades through the entries)
        b.HasOne(x => x.CompetitionCriterion).WithMany().HasForeignKey(x => x.CompetitionCriterionId).OnDelete(DeleteBehavior.NoAction);
    }
}
