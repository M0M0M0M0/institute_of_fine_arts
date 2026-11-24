using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.ViewModels.Staff.Competitions;
using Microsoft.EntityFrameworkCore;

namespace InstituteOfFineArts.Web.Services.Implementations;

/// <summary>
/// Jury and grading criteria entered on the competition create / edit form (TODO steps 4–5).
/// <list type="bullet">
/// <item>Jury: active TEACHER accounts, at most one chair (the only member is the chair). A judge who already graded
/// entries of the competition cannot be removed. Adding a judge while grading makes the entries incomplete again
/// until the new judge grades them.</item>
/// <item>Criteria: chosen from the library with a weight each; no criterion twice; each weight &gt; 0 with at most 2
/// decimals; the weights add up to exactly 100. Locked as soon as one entry has been graded (scores would be wrong).</item>
/// <item>Both may be left empty while the competition is being prepared; grading needs them and publishing needs a
/// complete jury grading (CompetitionResultService).</item>
/// </list>
/// </summary>
public partial class CompetitionService
{
    /// <summary>Weights do not add up to 100.</summary>
    public const string WeightsTotalMessage = "The weights of the criteria must add up to exactly 100 %.";

    /// <summary>A weight is missing or out of range.</summary>
    public const string WeightRangeMessage = "Each weight must be more than 0 and at most 100, with at most 2 decimals.";

    /// <summary>A criterion was chosen twice.</summary>
    public const string CriterionTwiceMessage = "This criterion is already on the list.";

    /// <summary>Criteria cannot change once grading started.</summary>
    public const string CriteriaLockedMessage = "Entries have already been graded, so the criteria and their weights can no longer change.";

    /// <summary>A judge with grades is removed.</summary>
    public const string JudgeHasGradesMessage = "This judge has already graded entries of the competition and cannot be removed from the jury.";

    /// <summary>A chosen judge is not an active teacher.</summary>
    public const string NotTeacherMessage = "Only active teacher accounts can be on the jury.";

    /// <inheritdoc />
    public async Task FillJuryOptionsAsync(CompetitionFormVm form)
    {
        var graded = form.Id == 0 ? [] : await _db.JudgeEvaluations.AsNoTracking()
            .Where(e => e.Submission.CompetitionId == form.Id).Select(e => e.JudgeId).Distinct().ToListAsync();
        form.JudgeOptions = await _db.StaffProfiles.AsNoTracking()
            .Where(s => s.Account.Role == Role.TEACHER && (s.Account.Active || form.JudgeIds.Contains(s.Id)))
            .OrderBy(s => s.Account.FullName)
            .Select(s => new JudgeOptionVm(s.Id, s.Account.FullName, s.SubjectsInCharge, graded.Contains(s.Id)))
            .ToListAsync();
        var used = form.Criteria.Select(c => c.CriterionId).ToList();
        form.CriterionOptions = await _db.GradingCriteria.AsNoTracking()
            .Where(c => c.Active || used.Contains(c.Id))
            .OrderBy(c => c.Name)
            .Select(c => new CriterionOptionVm(c.Id, c.Name, c.Description, c.Active))
            .ToListAsync();
        form.CriteriaLocked = graded.Count > 0;
    }

    /// <summary>Puts the current jury and criteria of a competition on the edit form.</summary>
    private async Task LoadJuryAndCriteriaAsync(CompetitionFormVm form)
    {
        var jury = await _db.CompetitionJudges.AsNoTracking().Where(j => j.CompetitionId == form.Id).ToListAsync();
        form.JudgeIds = jury.Select(j => j.JudgeId).ToList();
        form.ChairId = jury.FirstOrDefault(j => j.IsChair)?.JudgeId;
        form.Criteria = await _db.CompetitionCriteria.AsNoTracking().Where(c => c.CompetitionId == form.Id)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Id)
            .Select(c => new CompetitionCriterionRowVm { CriterionId = c.CriterionId, Weight = c.WeightPercent })
            .ToListAsync();
    }

    /// <summary>Checks the jury of the form; returns the judges and the chair (null when nobody is chosen).</summary>
    private async Task<(List<long> Judges, long? Chair)> CheckJuryAsync(CompetitionFormVm form)
    {
        var judges = form.JudgeIds.Where(id => id > 0).Distinct().ToList();
        if (judges.Count > 0)
        {
            var teachers = await _db.StaffProfiles.CountAsync(s => judges.Contains(s.Id) && s.Account.Role == Role.TEACHER && s.Account.Active);
            if (teachers != judges.Count)
            {
                throw new BusinessException(NotTeacherMessage, nameof(CompetitionFormVm.JudgeIds));
            }
        }
        // The chair is one of the members; a single member is the chair
        long? chair = form.ChairId is { } c && judges.Contains(c) ? c : judges.Count == 1 ? judges[0] : null;
        return (judges, chair);
    }

    /// <summary>Checks the criteria rows of the form (blank rows skipped); returns (criterion, weight) in form order.</summary>
    private async Task<List<(long CriterionId, decimal Weight)>> CheckCriteriaAsync(CompetitionFormVm form, IReadOnlyCollection<long> alreadyUsed)
    {
        var rows = new List<(long, decimal)>();
        var seen = new HashSet<long>();
        for (var i = 0; i < form.Criteria.Count; i++)
        {
            var row = form.Criteria[i];
            if (row.CriterionId == 0 && row.Weight == null)
            {
                continue;
            }
            var field = $"{nameof(CompetitionFormVm.Criteria)}[{i}]";
            if (row.CriterionId == 0)
            {
                throw new BusinessException("Please choose a criterion.", field + "." + nameof(CompetitionCriterionRowVm.CriterionId));
            }
            if (!seen.Add(row.CriterionId))
            {
                throw new BusinessException(CriterionTwiceMessage, field + "." + nameof(CompetitionCriterionRowVm.CriterionId));
            }
            if (row.Weight is not { } weight || weight <= 0 || weight > 100 || decimal.Round(weight, 2) != weight)
            {
                throw new BusinessException(WeightRangeMessage, field + "." + nameof(CompetitionCriterionRowVm.Weight));
            }
            rows.Add((row.CriterionId, weight));
        }
        if (rows.Count > 0)
        {
            var ids = rows.Select(r => r.Item1).ToList();
            var valid = await _db.GradingCriteria.CountAsync(c => ids.Contains(c.Id) && (c.Active || alreadyUsed.Contains(c.Id)));
            if (valid != ids.Count)
            {
                throw new BusinessException("A chosen criterion is no longer available. Reload the page and try again.", nameof(CompetitionFormVm.Criteria));
            }
            if (rows.Sum(r => r.Item2) != 100m)
            {
                throw new BusinessException(WeightsTotalMessage, nameof(CompetitionFormVm.Criteria));
            }
        }
        return rows;
    }

    /// <summary>Adds the jury and criteria of a new competition (saved with it).</summary>
    private async Task AddJuryAndCriteriaAsync(Competition competition, CompetitionFormVm form)
    {
        var (judges, chair) = await CheckJuryAsync(form);
        var criteria = await CheckCriteriaAsync(form, []);
        foreach (var judge in judges)
        {
            competition.Judges.Add(new CompetitionJudge { JudgeId = judge, IsChair = judge == chair });
        }
        for (var i = 0; i < criteria.Count; i++)
        {
            competition.Criteria.Add(new CompetitionCriterion { CriterionId = criteria[i].CriterionId, WeightPercent = criteria[i].Weight, SortOrder = i });
        }
    }

    /// <summary>
    /// Brings the jury and criteria of an existing competition in line with the form (not saved yet).
    /// Returns the changes for the competition's history.
    /// </summary>
    private async Task<List<string>> SyncJuryAndCriteriaAsync(long competitionId, CompetitionFormVm form)
    {
        var changes = new List<string>();
        var graded = await _db.JudgeEvaluations.Where(e => e.Submission.CompetitionId == competitionId)
            .Select(e => e.JudgeId).Distinct().ToListAsync();
        // Projected first: ToDictionaryAsync runs its selectors in memory, where Account is not loaded
        var names = await _db.StaffProfiles.Where(s => s.Account.Role == Role.TEACHER)
            .Select(s => new { s.Id, s.Account.FullName })
            .ToDictionaryAsync(s => s.Id, s => s.FullName);

        // ---- jury
        var (judges, chair) = await CheckJuryAsync(form);
        var current = await _db.CompetitionJudges.Where(j => j.CompetitionId == competitionId).ToListAsync();
        foreach (var member in current.Where(m => !judges.Contains(m.JudgeId)))
        {
            if (graded.Contains(member.JudgeId))
            {
                throw new BusinessException(JudgeHasGradesMessage, nameof(CompetitionFormVm.JudgeIds));
            }
            _db.CompetitionJudges.Remove(member);
            changes.Add($"Judge removed: {names.GetValueOrDefault(member.JudgeId, "#" + member.JudgeId)}");
        }
        foreach (var judge in judges)
        {
            var member = current.FirstOrDefault(m => m.JudgeId == judge);
            if (member == null)
            {
                _db.CompetitionJudges.Add(new CompetitionJudge { CompetitionId = competitionId, JudgeId = judge, IsChair = judge == chair });
                changes.Add($"Judge added: {names.GetValueOrDefault(judge, "#" + judge)}");
            }
            else if (member.IsChair != (judge == chair))
            {
                member.IsChair = judge == chair;
                if (member.IsChair) changes.Add($"Chair of the jury: {names.GetValueOrDefault(judge, "#" + judge)}");
            }
        }

        // ---- criteria (locked once grading started: the posted rows are ignored)
        var existing = await _db.CompetitionCriteria.Include(c => c.Criterion).Where(c => c.CompetitionId == competitionId)
            .OrderBy(c => c.SortOrder).ToListAsync();
        if (graded.Count > 0)
        {
            var posted = form.Criteria.Where(r => r.CriterionId != 0 || r.Weight != null).Select(r => (r.CriterionId, r.Weight)).ToList();
            var stored = existing.Select(c => (c.CriterionId, (decimal?)c.WeightPercent)).ToList();
            if (posted.Count > 0 && !posted.SequenceEqual(stored))
            {
                throw new BusinessException(CriteriaLockedMessage, nameof(CompetitionFormVm.Criteria));
            }
            return changes;
        }
        var rows = await CheckCriteriaAsync(form, existing.Select(c => c.CriterionId).ToList());
        var criterionNames = await _db.GradingCriteria.ToDictionaryAsync(c => c.Id, c => c.Name);
        foreach (var old in existing.Where(c => rows.All(r => r.CriterionId != c.CriterionId)))
        {
            _db.CompetitionCriteria.Remove(old);
            changes.Add($"Criterion removed: {old.Criterion.Name}");
        }
        for (var i = 0; i < rows.Count; i++)
        {
            var (criterionId, weight) = rows[i];
            var row = existing.FirstOrDefault(c => c.CriterionId == criterionId);
            if (row == null)
            {
                _db.CompetitionCriteria.Add(new CompetitionCriterion { CompetitionId = competitionId, CriterionId = criterionId, WeightPercent = weight, SortOrder = i });
                changes.Add($"Criterion added: {criterionNames[criterionId]} ({weight:0.##} %)");
                continue;
            }
            if (row.WeightPercent != weight)
            {
                changes.Add($"Criterion weight: {row.Criterion.Name} {row.WeightPercent:0.##} % → {weight:0.##} %");
            }
            row.WeightPercent = weight;
            row.SortOrder = i;
        }
        return changes;
    }
}
