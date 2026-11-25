using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Ganss.Xss;
using Microsoft.AspNetCore.Html;

namespace InstituteOfFineArts.Web.Helpers;

/// <summary>
/// Rich text written with the TinyMCE editor (BR-50): a competition's Description, Rules and result remarks, an entry's
/// description and poem / story, the judges' comments, an exhibition's description and an award's remarks.
/// <list type="bullet">
/// <item>Only a short list of tags is kept (paragraphs, headings, bold / italic / underline / strike, lists, quotes,
/// links, tables); everything else (scripts, styles, event attributes, images, iframes, <c>javascript:</c> links) is
/// removed by <see cref="Sanitize"/>, when the text is saved and again when it is shown.</item>
/// <item>Texts written before the editor are plain text: <see cref="ToHtml"/> shows them as paragraphs (a blank line
/// between two paragraphs, a single line break kept).</item>
/// <item>Lists, cards, search results, the API's short descriptions and the reports use <see cref="ToPlainText"/>,
/// never the HTML.</item>
/// </list>
/// </summary>
public static partial class RichText
{
    /// <summary>Most characters of text (without the tags) in a Description or Rules.</summary>
    public const int MaxTextLength = 4000;

    /// <summary>Most characters of HTML stored for a Description or Rules (the text plus its formatting).</summary>
    public const int MaxHtmlLength = 20000;

    /// <summary>Most characters of HTML kept for a rich text of at most <paramref name="maxText"/> characters of text.</summary>
    public static int HtmlLimit(int maxText) => maxText * 5 + 1000;

    /// <summary>
    /// What to save for a rich text posted by a form or the app: sanitized HTML (or older plain text, trimmed), null when
    /// there is no text. Throws on <paramref name="field"/> when the text is missing (<paramref name="requiredMessage"/>
    /// given) or longer than <paramref name="maxText"/> characters (the tags do not count).
    /// </summary>
    public static string? CleanChecked(string? posted, int maxText, string field, string tooLongMessage, string? requiredMessage = null)
    {
        var html = Clean(posted);
        if (html == null)
        {
            if (requiredMessage != null) throw new Exceptions.BusinessException(requiredMessage, field);
            return null;
        }
        if (ToPlainText(html).Length > maxText)
        {
            throw new Exceptions.BusinessException(tooLongMessage, field);
        }
        if (html.Length > HtmlLimit(maxText))
        {
            throw new Exceptions.BusinessException("This text has too much formatting. Remove some of it or shorten the text.", field);
        }
        return html;
    }

    private static readonly HtmlSanitizer Sanitizer = CreateSanitizer();

    private static HtmlSanitizer CreateSanitizer()
    {
        var sanitizer = new HtmlSanitizer(new HtmlSanitizerOptions
        {
            AllowedTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "p", "br", "h2", "h3", "h4", "strong", "b", "em", "i", "u", "s", "ul", "ol", "li", "blockquote", "a",
                "table", "thead", "tbody", "tr", "th", "td",
            },
            AllowedAttributes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "href", "title", "colspan", "rowspan" },
            AllowedSchemes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "http", "https", "mailto" },
            UriAttributes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "href" },
            AllowedCssProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
        });
        // A link opens in a new tab and gives the other site no access back to this page
        sanitizer.PostProcessNode += (_, e) =>
        {
            if (e.Node is AngleSharp.Dom.IElement { LocalName: "a" } a && a.HasAttribute("href"))
            {
                a.SetAttribute("target", "_blank");
                a.SetAttribute("rel", "noopener noreferrer nofollow");
            }
        };
        return sanitizer;
    }

    // A tag the editor produces; plain text written before the editor does not contain one
    [GeneratedRegex(@"</?(p|br|div|h[1-6]|strong|b|em|i|u|s|ul|ol|li|blockquote|a|table|tr|td|th)\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex KnownTag();

    // Tags after which the text continues on a new line
    [GeneratedRegex(@"<(br\s*/?|/p|/div|/h[1-6]|/li|/blockquote|/tr|/table|/td|/th)\s*>", RegexOptions.IgnoreCase)]
    private static partial Regex LineEnd();

    [GeneratedRegex(@"<[^>]*>")]
    private static partial Regex AnyTag();

    [GeneratedRegex(@"[ \t ]+")]
    private static partial Regex Spaces();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex BlankLines();

    [GeneratedRegex(@"\r?\n\s*\r?\n")]
    private static partial Regex ParagraphBreak();

    /// <summary>Whether <paramref name="stored"/> is HTML from the editor (as opposed to older plain text).</summary>
    public static bool LooksLikeHtml(string? stored) => !string.IsNullOrEmpty(stored) && KnownTag().IsMatch(stored);

    /// <summary>
    /// The HTML to save for what the editor posted: only the allowed tags, trimmed; null when there is no text
    /// (an editor left empty posts <c>&lt;p&gt;&lt;/p&gt;</c> or similar). Plain text is kept as it is.
    /// </summary>
    public static string? Clean(string? posted)
    {
        if (string.IsNullOrWhiteSpace(posted)) return null;
        if (!LooksLikeHtml(posted)) return posted.Trim();
        var html = Sanitize(posted).Trim();
        return ToPlainText(html).Length == 0 ? null : html;
    }

    /// <summary>Removes everything but the allowed tags and attributes.</summary>
    public static string Sanitize(string html) => Sanitizer.Sanitize(html);

    /// <summary>
    /// The HTML to show for a stored text: editor HTML sanitized again, older plain text encoded and put into
    /// paragraphs. Null when there is nothing to show.
    /// </summary>
    public static string? ToHtml(string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored)) return null;
        if (LooksLikeHtml(stored)) return Sanitize(stored);
        var html = new StringBuilder();
        foreach (var paragraph in ParagraphBreak().Split(stored.Trim()))
        {
            var lines = paragraph.Split('\n').Select(l => WebUtility.HtmlEncode(l.TrimEnd('\r')));
            html.Append("<p>").Append(string.Join("<br>", lines)).Append("</p>");
        }
        return html.ToString();
    }

    /// <summary>
    /// For a view: the stored text as formatted HTML in a <c>div.rich-text</c>, or <paramref name="empty"/> (encoded,
    /// in a paragraph) when there is none. Safe to write without encoding: the HTML is sanitized here.
    /// </summary>
    public static IHtmlContent Show(string? stored, string empty = "–", string? cssClass = null)
    {
        var html = ToHtml(stored);
        var classes = string.IsNullOrEmpty(cssClass) ? "rich-text" : "rich-text " + cssClass;
        return html == null
            ? new HtmlString($"<p class=\"{WebUtility.HtmlEncode(cssClass ?? "")}\">{WebUtility.HtmlEncode(empty)}</p>")
            : new HtmlString($"<div class=\"{WebUtility.HtmlEncode(classes)}\">{html}</div>");
    }

    /// <summary>
    /// Whether two stored texts show the same: compared as the HTML they are shown with, ignoring the line breaks the
    /// editor puts between blocks (older plain text and the same text saved from the editor are the same).
    /// </summary>
    public static bool SameContent(string? a, string? b) => Canonical(a) == Canonical(b);

    private static string Canonical(string? stored) => BetweenTags().Replace(ToHtml(stored) ?? "", "><").Trim();

    [GeneratedRegex(@">\s+<")]
    private static partial Regex BetweenTags();

    /// <summary>The text without formatting: one line per paragraph / list item, entities decoded.</summary>
    public static string ToPlainText(string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored)) return "";
        if (!LooksLikeHtml(stored)) return stored.Trim();
        var text = LineEnd().Replace(stored, "\n");
        text = WebUtility.HtmlDecode(AnyTag().Replace(text, ""));
        var lines = text.Replace("\r", "").Split('\n').Select(l => Spaces().Replace(l, " ").Trim());
        return BlankLines().Replace(string.Join("\n", lines), "\n\n").Trim();
    }

    /// <summary>
    /// A short text for a card or a list: the plain text on one line, cut at a word near <paramref name="max"/>
    /// characters with "…". Null when there is no text.
    /// </summary>
    public static string? Excerpt(string? stored, int max = 300)
    {
        var text = Spaces().Replace(ToPlainText(stored).Replace('\n', ' '), " ").Trim();
        if (text.Length == 0) return null;
        if (text.Length <= max) return text;
        var cut = text.LastIndexOf(' ', max);
        return text[..(cut > max / 2 ? cut : max)].TrimEnd(',', '.', ';', ':', ' ') + "…";
    }
}
