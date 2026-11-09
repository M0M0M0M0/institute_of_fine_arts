namespace InstituteOfFineArts.Web;

/// <summary>
/// Marker type of the shared UI texts (F-M19-01, task BE1-OPT 5): <c>IStringLocalizer&lt;SharedResource&gt;</c> reads
/// <c>Resources/SharedResource.resx</c> (English) and <c>Resources/SharedResource.vi-VN.resx</c> (Vietnamese).
/// The key of every text is its English wording, so a text without a translation is shown in English unchanged.
/// </summary>
/// <remarks>
/// Kept in the project root (not in Resources/) on purpose: a .cs file next to the .resx would change the
/// embedded resource name and the translations would no longer be found.
/// </remarks>
public sealed class SharedResource
{
}
