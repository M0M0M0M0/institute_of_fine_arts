using System.Text;

namespace InstituteOfFineArts.Web.Helpers;

/// <summary>One CSV record with the file line on which it starts (1 = first line of the file).</summary>
/// <param name="Line">Line number where the record starts (a quoted value may span several lines)</param>
/// <param name="Fields">Field values, quotes removed</param>
public sealed record CsvRecord(int Line, IReadOnlyList<string> Fields);

/// <summary>
/// Small RFC 4180 CSV reader (task BE2-OPT 4, F-M04-08): comma separator, values in double quotes may contain
/// commas, line breaks and doubled quotes (""), records end with CRLF, LF or CR. A leading UTF-8 BOM is ignored.
/// Hand-written so the project needs no extra package.
/// </summary>
public static class CsvParser
{
    /// <summary>Error for a quoted value that is never closed.</summary>
    public const string UnclosedQuoteMessage = "Line {0}: a quoted value is not closed";

    /// <summary>Error for text right after a closing quote (e.g. "abc"x).</summary>
    public const string TextAfterQuoteMessage = "Line {0}: unexpected character after a closing quote";

    /// <summary>
    /// Splits CSV text into records. Completely empty lines are skipped.
    /// Throws <see cref="FormatException"/> (with the line number) when the text is not valid CSV.
    /// </summary>
    /// <param name="text">Whole file content</param>
    public static List<CsvRecord> Parse(string text)
    {
        var records = new List<CsvRecord>();
        var fields = new List<string>();
        var value = new StringBuilder();
        var line = 1;          // current physical line
        var recordLine = 1;    // line where the current record started
        var inQuotes = false;  // inside "..."
        var afterQuote = false; // a quoted value has just been closed
        var fieldStarted = false; // the current record has content (so an empty line is not a record)

        // A BOM written by Excel / Notepad is not part of the first header name
        var i = text.Length > 0 && text[0] == '﻿' ? 1 : 0;
        for (; i < text.Length; i++)
        {
            var c = text[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    // "" inside quotes is one literal quote; a single quote closes the value
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        value.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                        afterQuote = true;
                    }
                }
                else
                {
                    // Line breaks inside quotes belong to the value but still count as file lines
                    if (c == '\n' || (c == '\r' && (i + 1 >= text.Length || text[i + 1] != '\n')))
                    {
                        line++;
                    }
                    value.Append(c);
                }
                continue;
            }

            switch (c)
            {
                case ',':
                    // End of a field
                    fields.Add(value.ToString());
                    value.Clear();
                    afterQuote = false;
                    fieldStarted = true;
                    break;
                case '\r':
                case '\n':
                    // CRLF counts as one line break
                    if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                    {
                        i++;
                    }
                    EndRecord();
                    line++;
                    recordLine = line;
                    break;
                case '"' when value.Length == 0 && !afterQuote:
                    // Opening quote at the start of a field
                    inQuotes = true;
                    fieldStarted = true;
                    break;
                default:
                    if (afterQuote)
                    {
                        throw new FormatException(string.Format(TextAfterQuoteMessage, line));
                    }
                    // A quote in the middle of an unquoted value is kept as a normal character (lenient)
                    value.Append(c);
                    fieldStarted = true;
                    break;
            }
        }

        if (inQuotes)
        {
            throw new FormatException(string.Format(UnclosedQuoteMessage, recordLine));
        }
        // Last record without a final line break
        EndRecord();
        return records;

        // Closes the current record; a line with no content at all is ignored
        void EndRecord()
        {
            if (fieldStarted || value.Length > 0)
            {
                fields.Add(value.ToString());
                records.Add(new CsvRecord(recordLine, fields.ToArray()));
            }
            fields.Clear();
            value.Clear();
            afterQuote = false;
            fieldStarted = false;
        }
    }

    /// <summary>Formats one value for a CSV file: quoted when it contains a comma, quote or line break.</summary>
    /// <param name="value">Raw value</param>
    public static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }
        return value.IndexOfAny([',', '"', '\r', '\n']) >= 0 ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
    }
}
