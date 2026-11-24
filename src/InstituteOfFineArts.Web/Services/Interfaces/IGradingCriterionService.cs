using InstituteOfFineArts.Web.ViewModels.Admin.Grading;

namespace InstituteOfFineArts.Web.Services.Interfaces;

/// <summary>Administrator – library of grading criteria and the rating thresholds (BusinessException on a broken rule).</summary>
public interface IGradingCriterionService
{
    /// <summary>Every criterion (active first) with how many competitions use it, and the current thresholds.</summary>
    Task<GradingLibraryVm> GetLibraryAsync();

    /// <summary>Edit form of a criterion (NotFoundException when unknown).</summary>
    Task<CriterionFormVm> GetForEditAsync(long id);

    /// <summary>Creates (Id = 0) or updates a criterion; returns its id.</summary>
    Task<long> SaveAsync(CriterionFormVm form);

    /// <summary>Deletes a criterion no competition uses (true), or hides one that is used (false).</summary>
    Task<bool> DeleteOrHideAsync(long id);

    /// <summary>Saves the thresholds; returns the changes ("BEST: 90 → 88") for the log.</summary>
    Task<IReadOnlyList<string>> SaveThresholdsAsync(ThresholdsFormVm form);
}
