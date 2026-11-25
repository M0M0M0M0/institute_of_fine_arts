using System.ComponentModel.DataAnnotations;
using InstituteOfFineArts.Web.Helpers;

namespace InstituteOfFineArts.Web.Validation;

/// <summary>
/// Length of a field written with the rich text editor (BR-50): at most <see cref="MaxText"/> characters of text, the
/// tags not counted, and at most <see cref="RichText.HtmlLimit"/> characters of HTML. Use it instead of
/// [StringLength], which would count the formatting. The services check again (an app or a direct request skips the form).
/// An empty value is accepted here – combine with [Required] when the field is mandatory.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public class RichTextLengthAttribute : ValidationAttribute
{
    /// <summary>Creates the attribute for at most <paramref name="maxText"/> characters of text.</summary>
    public RichTextLengthAttribute(int maxText)
        : base("{0} must be at most {1} characters.")
    {
        MaxText = maxText;
    }

    /// <summary>Most characters of text.</summary>
    public int MaxText { get; }

    /// <inheritdoc />
    public override string FormatErrorMessage(string name) => string.Format(ErrorMessageString, name, MaxText);

    /// <inheritdoc />
    public override bool IsValid(object? value)
        => value is not string text
           || (text.Length <= RichText.HtmlLimit(MaxText) && RichText.ToPlainText(text).Length <= MaxText);
}
