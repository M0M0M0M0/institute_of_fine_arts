using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Helpers;
using InstituteOfFineArts.Web.Validation;

namespace InstituteOfFineArts.Tests.Helpers;

/// <summary>
/// The rich text fields beyond the competition (BR-50): an entry's description and poem, the judges' comments, result
/// remarks, an exhibition's description, an award's remarks. One check is used by every service
/// (<see cref="RichText.CleanChecked"/>) and one attribute by the forms (<see cref="RichTextLengthAttribute"/>).
/// </summary>
public class RichTextFieldsTests
{
    /// <summary>Sanitized HTML is returned; the limit counts the text, not the tags.</summary>
    [Fact]
    public void CleanChecked_SanitizesAndCountsText()
    {
        var html = "<p><strong>" + new string('a', 20) + "</strong></p><script>x()</script>";
        Assert.Equal("<p><strong>" + new string('a', 20) + "</strong></p>", RichText.CleanChecked(html, 20, "Description", "Too long"));

        var ex = Assert.Throws<BusinessException>(() => RichText.CleanChecked("<p>" + new string('a', 21) + "</p>", 20, "Description", "Too long"));
        Assert.Equal("Too long", ex.Message);
        Assert.Equal("Description", ex.Field);
    }

    /// <summary>An editor with only formatting counts as empty: refused when required, null otherwise.</summary>
    [Fact]
    public void CleanChecked_EmptyEditor()
    {
        var ex = Assert.Throws<BusinessException>(() => RichText.CleanChecked("<p>&nbsp;</p>", 2000, "TextContent", "Too long", "Please enter the poem"));
        Assert.Equal("Please enter the poem", ex.Message);
        Assert.Null(RichText.CleanChecked("<p></p>", 2000, "Remarks", "Too long"));
    }

    /// <summary>A poem keeps its lines; older plain text is kept as it is.</summary>
    [Fact]
    public void CleanChecked_PoemAndPlainText()
    {
        Assert.Equal("<p>Autumn wind<br>red leaves</p>", RichText.CleanChecked("<p>Autumn wind<br>red leaves</p>", 4000, "TextContent", "x"));
        Assert.Equal("Autumn wind\nred leaves", RichText.CleanChecked("  Autumn wind\nred leaves  ", 4000, "TextContent", "x"));
    }

    /// <summary>The form attribute accepts formatting beyond the text limit, refuses more text.</summary>
    [Fact]
    public void RichTextLength_CountsText()
    {
        var attribute = new RichTextLengthAttribute(10);
        Assert.True(attribute.IsValid("<p><strong><em>0123456789</em></strong></p>"));
        Assert.False(attribute.IsValid("<p>01234567890</p>"));
        Assert.True(attribute.IsValid(null));
        Assert.Equal("Remarks must be at most 10 characters.", attribute.FormatErrorMessage("Remarks"));
    }
}
