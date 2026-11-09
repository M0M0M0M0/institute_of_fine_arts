namespace InstituteOfFineArts.Web.Helpers;

/// <summary>
/// Languages of the user interface (F-M19-01, task BE1-OPT 5). English is the default; Vietnamese is optional.
/// Only the UI language changes: numbers, dates and USD amounts are always formatted with en-US.
/// </summary>
public static class AppCultures
{
    /// <summary>Default language and the only formatting culture.</summary>
    public const string English = "en-US";

    /// <summary>Optional Vietnamese UI.</summary>
    public const string Vietnamese = "vi-VN";

    /// <summary>Every supported UI language.</summary>
    public static readonly string[] UiCultures = [English, Vietnamese];
}
