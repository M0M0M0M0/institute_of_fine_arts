namespace InstituteOfFineArts.Web.Helpers;

/// <summary>
/// Readable text for the upper-case enum values shown on screens: STUDYING → "Studying", NOT_GRADED → "Not graded".
/// The stored values stay upper case; only the text on the page changes.
/// </summary>
public static class EnumText
{
    /// <summary>Sentence-case text of an enum value.</summary>
    public static string Of(Enum value) => Of(value.ToString());

    /// <summary>Sentence-case text of an enum name (underscores become spaces).</summary>
    public static string Of(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        var text = name.Replace('_', ' ').ToLowerInvariant();
        return char.ToUpperInvariant(text[0]) + text[1..];
    }
}
