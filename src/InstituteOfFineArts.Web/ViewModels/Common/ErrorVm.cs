namespace InstituteOfFineArts.Web.ViewModels.Common;

/// <summary>
/// Data of the friendly error page (/error/{code}) – F-M01-07.
/// </summary>
public class ErrorVm
{
    /// <summary>HTTP status code shown on the page (403, 404, 413, 500…).</summary>
    public int StatusCode { get; set; }

    /// <summary>Short title, e.g. "Page not found".</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Explanation for the user.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Id of the request, so a log entry can be found when the user reports the problem.</summary>
    public string? RequestId { get; set; }

    /// <summary>Technical details of the exception – filled only in the Development environment.</summary>
    public string? DeveloperDetails { get; set; }
}
