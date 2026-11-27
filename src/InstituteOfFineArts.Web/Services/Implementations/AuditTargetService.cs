using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace InstituteOfFineArts.Web.Services.Implementations;

/// <summary>
/// Reads the object of a request from its path and looks up where it belongs (see <see cref="IAuditTargetService"/>).
/// <para>
/// Paths follow one pattern on the web and in the API: <c>…/{collection}/{id}/{verb}</c>, e.g.
/// <c>/staff/exhibition-entries/7/sold</c>, <c>/staff/competitions/5/awards/new</c>, <c>/api/staff/submissions/9/evaluation</c>.
/// The last "collection/id" pair is the object; a collection after it (<c>awards</c>, <c>entries</c>, <c>submissions</c>)
/// is something created inside it; the word after it is the verb.
/// </para>
/// </summary>
public class AuditTargetService : IAuditTargetService
{
    /// <summary>Path words that name a collection, with the kind of object they hold.</summary>
    private static readonly Dictionary<string, string> Collections = new()
    {
        ["competitions"] = "Competition",
        ["awards"] = "AwardIssued", // CompetitionAward when it follows competitions/{id} outside "awarding"
        ["submissions"] = "Submission",
        ["submission-images"] = "SubmissionImage", // one picture of an entry: logged against the entry
        ["images"] = "SubmissionImage",
        ["evaluations"] = "Submission",
        ["exhibitions"] = "Exhibition",
        ["exhibition-entries"] = "ExhibitionEntry",
        ["entries"] = "ExhibitionEntry",
        ["customers"] = "Customer",
        ["students"] = "StudentProfile",
        ["staff"] = "StaffProfile",
        ["team"] = "StaffProfile",
    };

    /// <summary>Words used in the sentence for each kind.</summary>
    private static readonly Dictionary<string, string> Nouns = new()
    {
        ["Competition"] = "the competition",
        ["CompetitionAward"] = "the award",
        ["AwardIssued"] = "the award",
        ["Submission"] = "the entry",
        ["SubmissionImage"] = "a picture of the entry",
        ["Exhibition"] = "the exhibition",
        ["ExhibitionEntry"] = "the artwork",
        ["Customer"] = "the customer",
        ["StudentProfile"] = "the student account",
        ["StaffProfile"] = "the staff account",
    };

    /// <summary>Verb words of the paths and their sentence.</summary>
    private static readonly Dictionary<string, string> Verbs = new()
    {
        ["edit"] = "Edited",
        ["delete"] = "Deleted",
        ["lock"] = "Locked",
        ["unlock"] = "Unlocked",
        ["reset-password"] = "Reset the password of",
        ["accept"] = "Accepted",
        ["reject"] = "Rejected",
        ["review"] = "Reviewed",
        ["sold"] = "Recorded the sale of",
        ["sell"] = "Recorded the sale of",
        ["sale"] = "Changed the sale of",
        ["unsold"] = "Marked as unsold",
        ["paid"] = "Recorded the payment to the student for",
        ["pay"] = "Recorded the payment to the student for",
        ["publish"] = "Published the results of",
        ["revoke"] = "Revoked",
        ["cancel"] = "Cancelled",
        ["finish"] = "Finished",
        ["delivery"] = "Updated the delivery of",
        ["adjust-evaluation"] = "Adjusted the evaluation of",
    };

    /// <summary>Verbs that are about money rather than the exhibition itself.</summary>
    private static readonly HashSet<string> SaleVerbs = ["sold", "sell", "sale", "unsold", "paid", "pay"];

    /// <summary>Database access.</summary>
    private readonly AppDbContext _db;

    /// <summary>Receives the database context through Dependency Injection.</summary>
    public AuditTargetService(AppDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<AuditTarget> ResolveAsync(string method, string path, IReadOnlyDictionary<string, long>? formIds = null)
    {
        var segments = path.Trim('/').ToLowerInvariant().Split('/', StringSplitOptions.RemoveEmptyEntries);

        // Pages that are not about one object
        if (segments.Contains("import"))
        {
            return new AuditTarget
            {
                Category = AuditCategory.ACCOUNT,
                Summary = segments[^1] == "confirm" ? "Imported student accounts from a file" : "Uploaded a student import file",
            };
        }
        if (segments.Length > 1 && segments[0] == "admin" && segments[1] == "grading")
        {
            return new AuditTarget
            {
                Category = AuditCategory.OTHER,
                Summary = segments[^1] switch
                {
                    "thresholds" => "Changed the rating thresholds",
                    "new" => "Added a grading criterion",
                    "delete" => "Deleted or hid a grading criterion",
                    _ => "Edited a grading criterion",
                },
            };
        }
        if (segments.Length > 0 && segments[0] == "profile")
        {
            return new AuditTarget
            {
                Category = AuditCategory.ACCOUNT,
                Summary = segments[^1] == "change-password" ? "Changed their own password" : "Edited their own profile",
            };
        }
        if (segments.Length == 1 && segments[0] is "forgot-password" or "reset-password")
        {
            return new AuditTarget
            {
                Category = AuditCategory.ACCOUNT,
                Summary = segments[0] == "forgot-password" ? "Asked for a password reset link" : "Set a new password with a reset link",
            };
        }

        // "collection/id" pairs; the last one is the object
        var pairs = new List<(int Index, string Word, long Id)>();
        for (var i = 1; i < segments.Length; i++)
        {
            if (long.TryParse(segments[i], out var n) && Collections.ContainsKey(segments[i - 1]))
            {
                pairs.Add((i, segments[i - 1], n));
            }
        }

        string? kind;
        long? id;
        string[] rest;
        if (pairs.Count > 0)
        {
            var last = pairs[^1];
            kind = KindOf(last.Word, pairs.Take(pairs.Count - 1).Select(p => p.Word), segments);
            id = last.Id;
            rest = segments[(last.Index + 1)..];
        }
        else
        {
            // No id in the path (a create): the last collection word gives the kind
            var at = Array.FindLastIndex(segments, s => Collections.ContainsKey(s));
            if (at < 0)
            {
                return new AuditTarget { Summary = Sentence(method, null, []) };
            }
            kind = KindOf(segments[at], [], segments);
            id = null;
            rest = segments[(at + 1)..];
        }

        // Something created inside the object: /competitions/5/awards/new, /exhibitions/3/entries, /competitions/5/submissions
        var child = rest.Length > 0 && Collections.ContainsKey(rest[0]) ? rest[0] : null;
        var verb = child == null ? rest.FirstOrDefault() : null;

        var target = new AuditTarget { Kind = kind, Id = id };
        target = await FillAsync(target, formIds);
        var awarding = segments.Contains("awarding");
        var grading = segments.Contains("evaluations");
        return target with
        {
            Category = CategoryOf(kind, child, verb, segments, awarding),
            Summary = Sentence(method, target, rest, child, verb, awarding, grading),
        };
    }

    /// <inheritdoc />
    public async Task<AuditTarget> WithCreatedIdAsync(AuditTarget target, long createdId)
    {
        var filled = await FillAsync(target with { Id = createdId }, null);
        // "Created the competition" → "Created the competition “Lunar New Year 2027”"
        return filled with { Summary = filled.Label == null ? target.Summary : $"{target.Summary} “{filled.Label}”" };
    }

    /// <summary>Kind of object of a collection word ("awards" depends on where it is).</summary>
    private static string KindOf(string word, IEnumerable<string> before, string[] segments)
        => word == "awards" && before.Contains("competitions") && !segments.Contains("awarding")
            ? "CompetitionAward"
            : Collections[word];

    /// <summary>Kind of work of the request.</summary>
    private static AuditCategory CategoryOf(string? kind, string? child, string? verb, string[] segments, bool awarding)
    {
        if (verb is "publish" or "revoke" or "delivery" || awarding)
        {
            return AuditCategory.AWARD;
        }
        if (segments.Contains("evaluations") || verb is "evaluation" or "adjust-evaluation")
        {
            return AuditCategory.GRADING;
        }
        if (verb != null && SaleVerbs.Contains(verb))
        {
            return AuditCategory.SALE;
        }
        return (kind, child) switch
        {
            ("Competition", "submissions") => AuditCategory.SUBMISSION,
            ("Competition" or "CompetitionAward", _) => AuditCategory.COMPETITION,
            ("AwardIssued", _) => AuditCategory.AWARD,
            ("Submission", _) => AuditCategory.SUBMISSION,
            ("Exhibition" or "ExhibitionEntry", _) => AuditCategory.EXHIBITION,
            ("Customer", _) => AuditCategory.SALE,
            ("StudentProfile" or "StaffProfile", _) => AuditCategory.ACCOUNT,
            _ => AuditCategory.OTHER,
        };
    }

    /// <summary>Readable sentence of the request, without the user.</summary>
    private static string Sentence(string method, AuditTarget? target, string[] rest, string? child = null, string? verb = null,
        bool awarding = false, bool grading = false)
    {
        var noun = target?.Kind is { } k && Nouns.TryGetValue(k, out var n) ? n : "the item";
        var label = target?.Label is { } l ? $" “{l}”" : string.Empty;
        var obj = noun + label;

        if (child != null)
        {
            return child switch
            {
                "submissions" => $"Submitted an entry to {obj}",
                "awards" when awarding => $"Gave an award in {obj}",
                "awards" => $"Added an award to {obj}",
                "entries" => $"Added an artwork to {obj}",
                _ => $"Added an item to {obj}",
            };
        }

        // The grading form of the web: POST /teacher/evaluations/{id} saves, …/delete removes the evaluation
        if (grading && target?.Id != null && !HttpMethods.IsGet(method))
        {
            return verb == "delete" ? $"Deleted the evaluation of {obj}" : $"Graded {obj}";
        }

        // Create: "/new", or a POST to the collection itself (API)
        if (target?.Id == null && (verb == "new" || (verb == null && HttpMethods.IsPost(method))))
        {
            return target?.Kind switch
            {
                "AwardIssued" => "Gave an award",
                "Submission" => "Submitted an entry",
                _ => $"Created {noun}",
            };
        }

        if (verb == "evaluation")
        {
            return HttpMethods.IsDelete(method) ? $"Deleted the evaluation of {obj}" : $"Graded {obj}";
        }
        if (verb != null && Verbs.TryGetValue(verb, out var sentence))
        {
            return $"{sentence} {obj}";
        }
        if (verb == null)
        {
            if (HttpMethods.IsDelete(method)) return $"Deleted {obj}";
            if (HttpMethods.IsPut(method)) return $"Edited {obj}";
            if (HttpMethods.IsGet(method)) return $"Opened {obj}";
            // POST /teacher/evaluations/{id} saves the evaluation
            return target?.Kind == "Submission" ? $"Graded {obj}" : $"Updated {obj}";
        }
        // Unknown verb: "Something" with the dashes removed
        return $"{char.ToUpperInvariant(verb[0])}{verb[1..].Replace('-', ' ')} {obj}";
    }

    /// <summary>Looks up the label and the parents of the object (one small query).</summary>
    private async Task<AuditTarget> FillAsync(AuditTarget target, IReadOnlyDictionary<string, long>? formIds)
    {
        long? Form(string key) => formIds != null && formIds.TryGetValue(key, out var v) ? v : null;

        if (target.Id is not { } id)
        {
            // A create: the parents may come from the form (e.g. the exhibition an artwork is added to)
            return target with
            {
                CompetitionId = Form("CompetitionId"),
                ExhibitionId = Form("ExhibitionId"),
                SubmissionId = Form("SubmissionId"),
            };
        }

        var q = _db;
        switch (target.Kind)
        {
            case "Competition":
                return target with
                {
                    CompetitionId = id,
                    Label = await q.Competitions.Where(c => c.Id == id).Select(c => c.Title).FirstOrDefaultAsync(),
                };
            case "CompetitionAward":
                var award = await q.CompetitionAwards.Where(a => a.Id == id)
                    .Select(a => new { a.AwardName, a.CompetitionId }).FirstOrDefaultAsync();
                return target with { Label = award?.AwardName, CompetitionId = award?.CompetitionId };
            case "AwardIssued":
                var issued = await q.AwardsIssued.Where(a => a.Id == id)
                    .Select(a => new { a.CompetitionAward.AwardName, a.SubmissionId, a.Submission.CompetitionId, a.Submission.Title })
                    .FirstOrDefaultAsync();
                return target with
                {
                    Label = issued == null ? null : $"{issued.AwardName} – {issued.Title}",
                    SubmissionId = issued?.SubmissionId, CompetitionId = issued?.CompetitionId,
                };
            case "Submission":
                var entry = await q.Submissions.Where(s => s.Id == id)
                    .Select(s => new { s.Title, s.CompetitionId }).FirstOrDefaultAsync();
                return target with { Label = entry?.Title, SubmissionId = id, CompetitionId = entry?.CompetitionId };
            case "SubmissionImage":
                var picture = await q.SubmissionImages.Where(i => i.Id == id)
                    .Select(i => new { i.SubmissionId, i.Submission.Title, i.Submission.CompetitionId }).FirstOrDefaultAsync();
                return target with { Label = picture?.Title, SubmissionId = picture?.SubmissionId, CompetitionId = picture?.CompetitionId };
            case "Exhibition":
                return target with
                {
                    ExhibitionId = id,
                    Label = await q.Exhibitions.Where(e => e.Id == id).Select(e => e.Name).FirstOrDefaultAsync(),
                };
            case "ExhibitionEntry":
                var artwork = await q.ExhibitionEntries.Where(e => e.Id == id)
                    .Select(e => new { e.ExhibitionId, e.SubmissionId, e.Submission.CompetitionId, e.Submission.Title })
                    .FirstOrDefaultAsync();
                return target with
                {
                    Label = artwork?.Title, ExhibitionId = artwork?.ExhibitionId,
                    SubmissionId = artwork?.SubmissionId, CompetitionId = artwork?.CompetitionId,
                };
            case "Customer":
                return target with { Label = await q.Customers.Where(c => c.Id == id).Select(c => c.FullName).FirstOrDefaultAsync() };
            case "StudentProfile":
                var student = await q.StudentProfiles.Where(s => s.Id == id)
                    .Select(s => new { s.AccountId, s.Account.FullName }).FirstOrDefaultAsync();
                return target with { Label = student?.FullName, TargetAccountId = student?.AccountId };
            case "StaffProfile":
                var staff = await q.StaffProfiles.Where(s => s.Id == id)
                    .Select(s => new { s.AccountId, s.Account.FullName }).FirstOrDefaultAsync();
                return target with { Label = staff?.FullName, TargetAccountId = staff?.AccountId };
            default:
                return target;
        }
    }
}
