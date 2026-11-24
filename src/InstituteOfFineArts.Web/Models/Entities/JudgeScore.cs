namespace InstituteOfFineArts.Web.Models.Entities;

/// <summary>Score 0–100 and comment of one judge on one criterion of the competition (table judge_score).</summary>
public class JudgeScore : BaseEntity
{
    /// <summary>The judge's grade of the entry.</summary>
    public long JudgeEvaluationId { get; set; }

    /// <summary>Criterion of the competition (with its weight).</summary>
    public long CompetitionCriterionId { get; set; }

    /// <summary>Score from 0 to 100 (2 decimals at most).</summary>
    public decimal Score { get; set; }

    /// <summary>Comment on this criterion (optional).</summary>
    public string? Comment { get; set; }

    /// <summary>The judge's grade of the entry.</summary>
    public JudgeEvaluation JudgeEvaluation { get; set; } = null!;

    /// <summary>Criterion of the competition.</summary>
    public CompetitionCriterion CompetitionCriterion { get; set; } = null!;
}
