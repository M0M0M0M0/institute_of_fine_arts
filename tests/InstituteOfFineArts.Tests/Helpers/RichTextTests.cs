using InstituteOfFineArts.Web.Helpers;

namespace InstituteOfFineArts.Tests.Helpers;

/// <summary>
/// Rich text of a competition's Description / Rules (TODO step 8, BR-30): what the editor may keep, what is removed
/// (scripts, event attributes, dangerous links, styles, pictures), older plain text, plain text for cards.
/// </summary>
public class RichTextTests
{
    /// <summary>Formatting the editor offers is kept as it is.</summary>
    [Fact]
    public void Clean_KeepsAllowedFormatting()
    {
        const string html = "<h2>Theme</h2><p><strong>Bold</strong>, <em>italic</em>, <u>under</u> and <s>old</s>.</p>"
                            + "<ul><li>One</li></ul><ol><li>Two</li></ol><blockquote>Quote</blockquote>"
                            + "<table><tbody><tr><th>Size</th><td colspan=\"2\">A3</td></tr></tbody></table>";
        Assert.Equal(html, RichText.Clean(html));
    }

    /// <summary>Anything that could run code or load something is removed (XSS).</summary>
    [Theory]
    [InlineData("<p>Hi</p><script>alert(1)</script>", "<p>Hi</p>")]
    [InlineData("<p onclick=\"alert(1)\">Hi</p>", "<p>Hi</p>")]
    [InlineData("<p>Hi<img src=x onerror=alert(1)></p>", "<p>Hi</p>")]
    [InlineData("<p><a href=\"javascript:alert(1)\">Hi</a></p>", "<p><a>Hi</a></p>")]
    [InlineData("<p style=\"position:fixed;top:0\">Hi</p>", "<p>Hi</p>")]
    [InlineData("<p>Hi</p><iframe src=\"https://evil.example\"></iframe>", "<p>Hi</p>")]
    [InlineData("<p>Hi</p><style>body{display:none}</style>", "<p>Hi</p>")]
    [InlineData("<svg><p onload=alert(1)>Hi</p></svg>", "<p>Hi</p>")]
    public void Clean_RemovesDangerousContent(string posted, string expected)
    {
        Assert.Equal(expected, RichText.Clean(posted));
    }

    /// <summary>A web link opens in a new tab without access back to the page.</summary>
    [Fact]
    public void Clean_Link_OpensSafely()
    {
        var html = RichText.Clean("<p><a href=\"https://ifa.example/rules\" target=\"_self\" onclick=\"x()\">Rules</a></p>");
        Assert.Equal("<p><a href=\"https://ifa.example/rules\" target=\"_blank\" rel=\"noopener noreferrer nofollow\">Rules</a></p>", html);
    }

    /// <summary>An editor left empty (or only spaces) is saved as nothing.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    [InlineData("<p></p>")]
    [InlineData("<p>&nbsp;</p><p><br></p>")]
    public void Clean_Empty_IsNull(string? posted)
    {
        Assert.Null(RichText.Clean(posted));
    }

    /// <summary>Older plain text is shown as paragraphs, encoded; a single line break is kept.</summary>
    [Fact]
    public void ToHtml_PlainText_BecomesParagraphs()
    {
        Assert.Equal("<p>Line one<br>a &lt; b</p><p>Second</p>", RichText.ToHtml("Line one\na < b\n\nSecond"));
        Assert.False(RichText.LooksLikeHtml("Size < 5 MB, ratio > 1"));
    }

    /// <summary>Stored HTML is sanitized again when shown (e.g. a row changed in the database).</summary>
    [Fact]
    public void Show_SanitizesStoredHtml()
    {
        var html = RichText.Show("<p>Hi</p><script>alert(1)</script>").ToString();
        Assert.Equal("<div class=\"rich-text\"><p>Hi</p></div>", html);
        Assert.Equal("<p class=\"\">–</p>", RichText.Show(null).ToString());
    }

    /// <summary>Plain text for cards and searches: no tags, entities decoded, one line per block.</summary>
    [Fact]
    public void ToPlainText_And_Excerpt()
    {
        const string html = "<h2>Theme</h2><p>Paint&nbsp;your <strong>town</strong> &amp; its people.</p><ul><li>A3</li><li>Oil</li></ul>";
        Assert.Equal("Theme\nPaint your town & its people.\nA3\nOil", RichText.ToPlainText(html));
        Assert.Equal("Theme Paint your town & its people. A3 Oil", RichText.Excerpt(html));
        Assert.Equal("Theme Paint your…", RichText.Excerpt(html, 20));
        Assert.Null(RichText.Excerpt("<p></p>"));
    }

    /// <summary>Saving older plain text from the editor (now paragraphs) is not a change of content.</summary>
    [Fact]
    public void SameContent_IgnoresEditorLineBreaks()
    {
        Assert.True(RichText.SameContent("One\n\nTwo", "<p>One</p>\n<p>Two</p>"));
        Assert.False(RichText.SameContent("One\n\nTwo", "<p><strong>One</strong></p>\n<p>Two</p>"));
        Assert.True(RichText.SameContent(null, null));
    }
}
