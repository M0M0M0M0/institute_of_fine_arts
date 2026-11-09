using InstituteOfFineArts.Web.Helpers;

namespace InstituteOfFineArts.Tests.Helpers;

/// <summary>
/// CsvParser (task BE2-OPT 4, F-M04-08): RFC 4180 quotes, embedded commas / line breaks, CRLF, BOM, error lines.
/// </summary>
public class CsvParserTests
{
    /// <summary>Plain values separated by commas, CRLF line ends, last line without a line break.</summary>
    [Fact]
    public void Parse_Simple_CrLf()
    {
        var records = CsvParser.Parse("a,b,c\r\n1,2,3\r\n4,,6");

        Assert.Equal(3, records.Count);
        Assert.Equal(new[] { "a", "b", "c" }, records[0].Fields);
        Assert.Equal(new[] { "4", "", "6" }, records[2].Fields);
        Assert.Equal(new[] { 1, 2, 3 }, records.Select(r => r.Line));
    }

    /// <summary>Quoted values keep commas, doubled quotes become one quote.</summary>
    [Fact]
    public void Parse_QuotedCommaAndQuotes()
    {
        var records = CsvParser.Parse("name,address\n\"Le, Van\",\"12 \"\"Le Loi\"\" St\"\n");

        Assert.Equal(2, records.Count);
        Assert.Equal(new[] { "Le, Van", "12 \"Le Loi\" St" }, records[1].Fields);
    }

    /// <summary>A quoted line break belongs to the value; the next record starts on the right file line.</summary>
    [Fact]
    public void Parse_EmbeddedNewline_KeepsLineNumbers()
    {
        var records = CsvParser.Parse("h1,h2\r\n\"line one\r\nline two\",x\r\nlast,y\r\n");

        Assert.Equal(3, records.Count);
        Assert.Equal("line one\r\nline two", records[1].Fields[0]);
        Assert.Equal(2, records[1].Line);
        Assert.Equal(4, records[2].Line);
    }

    /// <summary>A UTF-8 BOM (Excel "CSV UTF-8") is not part of the first column name; Vietnamese text is kept.</summary>
    [Fact]
    public void Parse_Bom_Ignored()
    {
        var records = CsvParser.Parse("﻿FullName,Course\nNguyễn Văn An,Hội họa");

        Assert.Equal("FullName", records[0].Fields[0]);
        Assert.Equal("Nguyễn Văn An", records[1].Fields[0]);
    }

    /// <summary>Empty lines are skipped but still counted for the line numbers.</summary>
    [Fact]
    public void Parse_EmptyLines_Skipped()
    {
        var records = CsvParser.Parse("a,b\n\n1,2\n\n");

        Assert.Equal(2, records.Count);
        Assert.Equal(3, records[1].Line);
    }

    /// <summary>An empty quoted value and a line with only commas are records with empty fields.</summary>
    [Fact]
    public void Parse_EmptyQuotedValue()
    {
        var records = CsvParser.Parse("\"\",x\n,\n");

        Assert.Equal(new[] { "", "x" }, records[0].Fields);
        Assert.Equal(new[] { "", "" }, records[1].Fields);
    }

    /// <summary>A quote that is never closed is refused with the line where the record starts.</summary>
    [Fact]
    public void Parse_UnclosedQuote_Throws()
    {
        var ex = Assert.Throws<FormatException>(() => CsvParser.Parse("a,b\n1,\"open\n2,3"));
        Assert.Equal(string.Format(CsvParser.UnclosedQuoteMessage, 2), ex.Message);
    }

    /// <summary>Text after a closing quote is refused.</summary>
    [Fact]
    public void Parse_TextAfterQuote_Throws()
    {
        var ex = Assert.Throws<FormatException>(() => CsvParser.Parse("a\n\"x\"y"));
        Assert.Equal(string.Format(CsvParser.TextAfterQuoteMessage, 2), ex.Message);
    }

    /// <summary>Escape quotes only the values that need it, and Parse reads them back unchanged.</summary>
    [Fact]
    public void Escape_RoundTrip()
    {
        var values = new[] { "plain", "with, comma", "with \"quote\"", "two\nlines", "" };
        var line = string.Join(",", values.Select(CsvParser.Escape));

        Assert.StartsWith("plain,\"with, comma\"", line);
        Assert.Equal(values, CsvParser.Parse(line)[0].Fields);
    }
}
