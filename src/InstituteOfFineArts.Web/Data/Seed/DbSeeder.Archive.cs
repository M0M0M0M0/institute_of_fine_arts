using InstituteOfFineArts.Web.Helpers;
using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace InstituteOfFineArts.Web.Data.Seed;

/// <summary>
/// Archive sample data: two years of past, graded competitions filled with ~1,500 real paintings of the National
/// Gallery of Art (open access, CC0) - covers, winning entries, the public gallery and past exhibitions.
/// The pictures are not in Git: database/seed-images/nga/fetch-images.ps1 downloads them. Without them this step
/// is skipped; with them it runs once, on a new or an existing database (the 7 main sample competitions are untouched).
/// </summary>
public static partial class DbSeeder
{
    /// <summary>One painting of database/seed-images/nga/manifest.csv.</summary>
    internal sealed record ArchivePainting(string Uuid, string Theme, string Title, int Width, int Height, string PageUrl, string Description);

    /// <summary>One archive competition: title, description and the manifest theme its paintings come from.</summary>
    internal sealed record ArchiveCompetition(string Title, string Description, string Theme);

    /// <summary>Number of archive students (alumni); a competition never has more entries than this.</summary>
    internal const int ArchiveStudentCount = 80;

    /// <summary>
    /// Archive competitions, newest first. Competitions sharing a theme split that theme's paintings between them.
    /// </summary>
    internal static readonly ArchiveCompetition[] ArchiveCompetitions =
    [
        new("Rivers and Lakes", "Still water, fast rivers and the light on a lake.", "landscape"),
        new("Feathered Friends", "Birds of the garden, the forest and the shore.", "animals"),
        new("Harbours and Boats", "Ports, fishing boats and the life of the harbour.", "seascape"),
        new("Mountains and Valleys", "High peaks, deep valleys and the sky above them.", "landscape"),
        new("Faces and Figures", "Portraits and figures: people as the painter sees them.", "portrait"),
        new("Life on the Farm", "Horses, cattle and the animals that share our work.", "animals"),
        new("Forest Light", "Paths, trees and the light falling through the leaves.", "landscape"),
        new("Streets and Squares", "Town life: streets, squares, markets and churches.", "city"),
        new("Wings and Branches", "A bird study: perched, nesting or in flight.", "animals"),
        new("The Countryside", "Fields, farms and the quiet roads between villages.", "landscape"),
        new("By the Sea", "Coasts, beaches and the changing moods of the sea.", "seascape"),
        new("Animal Kingdom", "Wild and tame animals in their natural surroundings.", "animals"),
        new("Summer Fields", "Meadows, harvests and long summer afternoons.", "landscape"),
        new("Still Life", "Flowers, fruit and the everyday objects on a table.", "still life"),
        new("Hills and Horizons", "Rolling hills and the wide view to the horizon.", "landscape"),
        new("Birds of America", "Careful studies of birds in their habitat.", "animals"),
        new("Bridges and Buildings", "Architecture, bridges and the shape of the town.", "city"),
        new("Wild Places", "Untouched nature: rocks, waterfalls and wilderness.", "landscape"),
        new("Horses and Riders", "Horses at work, at rest and on the race course.", "animals"),
        new("Beyond the Visible", "Abstract and imaginative painting: colour, line and form.", "abstract"),
        new("Pastoral Scenes", "Shepherds, flocks and the calm of the countryside.", "landscape"),
        new("Creatures Great and Small", "From insects to elephants: any animal, any medium.", "animals"),
        new("River Valleys", "Valleys shaped by rivers, in every season.", "landscape"),
        new("Animal Portraits", "One animal, one portrait: character above all.", "animals"),
        new("Landscapes of Memory", "A landscape remembered from childhood or a journey.", "landscape"),
    ];

    /// <summary>Accompanying texts of archive entries (poems / quotations in the public domain).</summary>
    private static readonly (TextType Type, string Text)[] ArchiveTexts =
    [
        (TextType.QUOTATION, "\"Painting is poetry that is seen rather than felt.\" – Leonardo da Vinci"),
        (TextType.QUOTATION, "\"Every artist dips his brush in his own soul.\" – Henry Ward Beecher"),
        (TextType.QUOTATION, "\"Look deep into nature, and then you will understand everything better.\" – Albert Einstein"),
        (TextType.QUOTATION, "\"In every walk with nature one receives far more than he seeks.\" – John Muir"),
        (TextType.QUOTATION, "\"Colour is my day-long obsession, joy and torment.\" – Claude Monet"),
        (TextType.QUOTATION, "\"I dream my painting and I paint my dream.\" – Vincent van Gogh"),
        (TextType.QUOTATION, "\"Nature always wears the colours of the spirit.\" – Ralph Waldo Emerson"),
        (TextType.POEM, "I wandered lonely as a cloud\nthat floats on high o'er vales and hills. – William Wordsworth"),
        (TextType.POEM, "The woods are lovely, dark and deep,\nbut I have promises to keep. – Robert Frost"),
        (TextType.POEM, "My heart leaps up when I behold\na rainbow in the sky. – William Wordsworth"),
        (TextType.POEM, "Nature's first green is gold,\nher hardest hue to hold. – Robert Frost"),
        (TextType.STORY, "I painted this after a long walk; I wanted to keep the light exactly as it was that afternoon."),
        (TextType.STORY, "My teacher told me to paint what I see, not what I know. This was my first try."),
        (TextType.STORY, "I made three studies before this one. The colours changed every time I looked again."),
        (TextType.OTHER, "A study in light and colour."),
        (TextType.OTHER, "Painted from life over several sessions."),
    ];

    /// <summary>
    /// Reads the manifest (comma separated, double quotes around fields that contain commas, "" inside quotes).
    /// </summary>
    /// <param name="path">Path of manifest.csv</param>
    internal static List<ArchivePainting> ReadArchiveManifest(string path)
    {
        var lines = File.ReadAllLines(path);
        var header = ParseCsvLine(lines[0]);
        int Col(string name) => Array.IndexOf(header, name);
        int uuid = Col("uuid"), theme = Col("theme"), title = Col("title"), width = Col("width"), height = Col("height"),
            page = Col("page_url"), description = Col("description");
        return lines.Skip(1).Where(l => l.Length > 0).Select(ParseCsvLine).Select(f => new ArchivePainting(
            f[uuid], f[theme], f[title], int.Parse(f[width]), int.Parse(f[height]), f[page], f[description])).ToList();
    }

    /// <summary>Splits one CSV line into fields.</summary>
    internal static string[] ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var current = new System.Text.StringBuilder();
        var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (quoted)
            {
                if (ch == '"' && i + 1 < line.Length && line[i + 1] == '"') { current.Append('"'); i++; }
                else if (ch == '"') quoted = false;
                else current.Append(ch);
            }
            else if (ch == '"') quoted = true;
            else if (ch == ',') { fields.Add(current.ToString()); current.Clear(); }
            else current.Append(ch);
        }
        fields.Add(current.ToString());
        return fields.ToArray();
    }

    /// <summary>
    /// Shares the paintings out: each competition gets an equal part of its theme's paintings (manifest order).
    /// The widest painting of each part becomes the cover; the rest are the entries.
    /// </summary>
    /// <param name="paintings">Paintings that are present on disk</param>
    internal static List<(ArchiveCompetition Competition, ArchivePainting Cover, List<ArchivePainting> Entries)> PlanArchive(
        IReadOnlyList<ArchivePainting> paintings)
    {
        var plan = new List<(ArchiveCompetition, ArchivePainting, List<ArchivePainting>)>();
        foreach (var group in ArchiveCompetitions.GroupBy(c => c.Theme))
        {
            var pool = paintings.Where(p => p.Theme == group.Key).ToList();
            var parts = group.ToList();
            for (var i = 0; i < parts.Count; i++)
            {
                // Equal shares, the first parts take the remainder; never more entries than archive students
                var share = pool.Count / parts.Count + (i < pool.Count % parts.Count ? 1 : 0);
                var start = Enumerable.Range(0, i).Sum(k => pool.Count / parts.Count + (k < pool.Count % parts.Count ? 1 : 0));
                var part = pool.Skip(start).Take(share).ToList();
                if (part.Count < 6)
                {
                    continue; // too few pictures downloaded for a believable competition
                }
                var cover = part.OrderByDescending(p => (double)p.Width / p.Height).First();
                var entries = part.Where(p => p != cover).Take(ArchiveStudentCount).ToList();
                plan.Add((parts[i], cover, entries));
            }
        }
        // Newest first, in the order of ArchiveCompetitions
        return plan.OrderBy(p => Array.IndexOf(ArchiveCompetitions, p.Item1)).ToList();
    }

    /// <summary>
    /// Inserts the archive when its pictures are present and it is not in the database yet (all or nothing).
    /// </summary>
    /// <param name="db">DbContext used to insert the data</param>
    /// <param name="services">Scoped services (file storage, thumbnails, logging)</param>
    internal static async Task SeedArchiveAsync(AppDbContext db, IServiceProvider services)
    {
        var env = services.GetRequiredService<IWebHostEnvironment>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("DbSeeder.Archive");
        var folder = Path.GetFullPath(Path.Combine(env.ContentRootPath, "..", "..", "database", "seed-images", "nga"));
        var imageFolder = Path.Combine(folder, "images");
        var manifestPath = Path.Combine(folder, "manifest.csv");
        if (!File.Exists(manifestPath) || !Directory.Exists(imageFolder))
        {
            logger.LogInformation("Archive sample data skipped: run database/seed-images/nga/fetch-images.ps1 to download its pictures");
            return;
        }
        var titles = ArchiveCompetitions.Select(c => c.Title).ToList();
        if (await db.Competitions.AnyAsync(c => titles.Contains(c.Title)))
        {
            return; // already seeded
        }
        var paintings = ReadArchiveManifest(manifestPath).Where(p => File.Exists(Path.Combine(imageFolder, p.Uuid + ".jpg"))).ToList();
        var plan = PlanArchive(paintings);
        if (plan.Count == 0)
        {
            return;
        }

        var files = services.GetRequiredService<IFileStorageService>();
        var thumbnails = services.GetRequiredService<IThumbnailService>();
        var today = DateTimeHelper.Today;
        logger.LogInformation("Seeding the archive: {Competitions} competitions, {Entries} paintings (takes a minute)…",
            plan.Count, plan.Sum(p => p.Entries.Count + 1));

        var written = new List<string>();
        await using var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            var staffIds = await db.StaffProfiles.Where(s => s.Account.Role == Role.STAFF).OrderBy(s => s.Id).Select(s => s.Id).ToListAsync();
            var students = await AddArchiveStudentsAsync(db, today);

            // ---- (1) competitions: 30 days each, one ending every 4 weeks, the newest ended 10 weeks ago
            var codes = await db.Competitions.Select(c => c.Code).ToListAsync();
            var created = new List<(Competition Competition, List<Submission> Entries)>();
            for (var k = 0; k < plan.Count; k++)
            {
                var (spec, cover, entries) = plan[k];
                var end = today.AddDays(-70 - 28 * k);
                var staffId = staffIds[k % staffIds.Count];
                var competition = new Competition
                {
                    Title = spec.Title, Description = spec.Description,
                    Rules = "One original painting per student. JPEG, up to 5 MB. A short poem, quotation or story must accompany the painting.",
                    StartDate = end.AddDays(-30), EndDate = end, CreatedBy = staffId,
                    Code = NextArchiveCode(codes, end.AddDays(-30).Year),
                };
                foreach (var (name, rank, prize, quantity) in new[] { ("First Prize", 1, 300m, 1), ("Second Prize", 2, 200m, 1), ("Third Prize", 3, 100m, 1), ("Honorable Mention", 4, 50m, 3) })
                {
                    competition.Awards.Add(new CompetitionAward { AwardName = name, Rank = rank, PrizeValue = prize, Quantity = quantity, PrizeDescription = "Certificate" });
                }
                db.Competitions.Add(competition);
                await db.SaveChangesAsync();
                competition.CoverImagePath = await CopyImageAsync(files, imageFolder, cover, $"competitions/{competition.Id}/cover-{Guid.NewGuid()}.jpg", written);

                // ---- (2) entries: one per alumni student, spread over the 30 days
                var submissions = new List<Submission>();
                for (var i = 0; i < entries.Count; i++)
                {
                    var p = entries[i];
                    var guid = Guid.NewGuid();
                    var imagePath = await CopyImageAsync(files, imageFolder, p, $"submissions/{competition.Id}/{guid}.jpg", written);
                    var thumbPath = $"thumbnails/{competition.Id}/{guid}.jpg";
                    await thumbnails.CreateAsync(files.ResolvePath(imagePath), files.ResolvePath(thumbPath), 400);
                    written.Add(thumbPath);
                    var text = ArchiveTexts[(i + k) % ArchiveTexts.Length];
                    var student = students[(i + 7 * k) % students.Count];
                    var submission = new Submission
                    {
                        CompetitionId = competition.Id, StudentId = student.Id, Student = student,
                        Title = p.Title.Length > 150 ? p.Title[..150] : p.Title,
                        ImagePath = imagePath, ThumbnailPath = thumbPath, OriginalFileName = p.Uuid + ".jpg",
                        FileSize = new FileInfo(files.ResolvePath(imagePath)).Length,
                        TextType = text.Type, TextContent = text.Text,
                        Description = p.Description,
                        SubmittedAt = competition.StartDate.AddDays(i * 29 / Math.Max(1, entries.Count)).ToDateTime(new TimeOnly(8 + i % 13, i % 60)),
                    };
                    db.Submissions.Add(submission);
                    submissions.Add(submission);
                }
                await db.SaveChangesAsync();

                // ---- (3) every entry evaluated: 5 BEST (the winners), then BETTER / GOOD / MODERATE / NORMAL
                for (var i = 0; i < submissions.Count; i++)
                {
                    var share = (double)i / submissions.Count;
                    var rating = i < 5 ? Rating.BEST : share < 0.3 ? Rating.BETTER : share < 0.6 ? Rating.GOOD : share < 0.85 ? Rating.MODERATE : Rating.NORMAL;
                    db.Evaluations.Add(NewEvaluation(submissions[i], rating, staffIds[(k + i) % staffIds.Count], end.AddDays(1 + i % 5)));
                }

                // ---- (4) awards to the 5 BEST, then results published
                var awardNames = new[] { "First Prize", "Second Prize", "Third Prize", "Honorable Mention", "Honorable Mention" };
                var remarks = new[] { "Outstanding composition and mastery of light.", "A confident, beautifully balanced work.", "Rich colour and careful observation.", "A sensitive and personal interpretation.", "Promising work with a strong atmosphere." };
                for (var i = 0; i < Math.Min(5, submissions.Count); i++)
                {
                    db.AwardsIssued.Add(new AwardIssued
                    {
                        CompetitionAwardId = competition.Awards.First(a => a.AwardName == awardNames[i]).Id,
                        SubmissionId = submissions[i].Id, IssuedBy = staffId, IssuedDate = end.AddDays(8), Remarks = remarks[i],
                    });
                }
                competition.Status = CompetitionStatus.GRADED;
                competition.PublishedBy = staffId;
                competition.PublishedAt = end.AddDays(10).ToDateTime(new TimeOnly(9, 0));
                competition.ResultRemarks = $"Thank you to all {submissions.Count} participants. Congratulations to the winners!";
                await db.SaveChangesAsync();
                created.Add((competition, submissions));
                logger.LogInformation("Archive {Done}/{Total}: {Title}", k + 1, plan.Count, spec.Title);
            }

            // ---- (5) past exhibitions: one for every three competitions, a selection of their best works, most of them sold
            await AddArchiveExhibitionsAsync(db, created, staffIds, today);
            await transaction.CommitAsync();
            logger.LogInformation("Archive sample data seeded");
        }
        catch
        {
            await transaction.RollbackAsync();
            foreach (var path in written)
            {
                files.Delete(path);
            }
            throw;
        }
    }

    /// <summary>
    /// The 80 alumni students who entered the archive competitions (most have graduated): student11, student12…,
    /// skipping any number whose username, e-mail or student code is already taken.
    /// </summary>
    private static async Task<List<StudentProfile>> AddArchiveStudentsAsync(AppDbContext db, DateOnly today)
    {
        var usernames = (await db.Accounts.Select(a => a.Username).ToListAsync()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var emails = (await db.Accounts.Select(a => a.Email).ToListAsync()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var codes = (await db.StudentProfiles.Select(s => s.StudentCode).ToListAsync()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var no = 10;
        var hash = BCrypt.Net.BCrypt.HashPassword(StudentPassword);
        string[] family = ["Nguyen", "Tran", "Le", "Pham", "Hoang", "Vu", "Vo", "Dang", "Bui", "Do", "Ho", "Ngo", "Duong", "Ly"];
        string[] middle = ["Van", "Thi", "Minh", "Ngoc", "Duc", "Thu", "Gia", "Bao", "Thanh", "Hoai", "Quoc", "Khanh"];
        string[] given = ["An", "Binh", "Chi", "Dung", "Giang", "Ha", "Hieu", "Hung", "Khoa", "Lam", "Linh", "Mai", "Nam", "Nhi", "Phong",
            "Quan", "Quynh", "Son", "Tam", "Thao", "Trang", "Trung", "Tuan", "Uyen", "Viet", "Xuan", "Yen", "Long", "Hanh", "Phuong", "Duy"];
        var classes = new[] { ("Painting", "PT01"), ("Painting", "PT02"), ("Animation", "AN01"), ("Digital Art", "DA01") };
        var students = new List<StudentProfile>();
        for (var i = 0; i < ArchiveStudentCount; i++)
        {
            do { no++; } while (usernames.Contains($"student{no:00}") || emails.Contains($"student{no:00}@ifa.local") || codes.Contains($"HV{no:0000}"));
            var (course, className) = classes[i % classes.Length];
            var student = new StudentProfile
            {
                StudentCode = $"HV{no:0000}",
                EnrollmentDate = today.AddMonths(-(18 + i % 18)),
                CourseName = course, ClassName = className,
                EnrollmentStatus = i % 5 == 0 ? EnrollmentStatus.STUDYING : EnrollmentStatus.GRADUATED,
                Account = new Account
                {
                    Username = $"student{no:00}", PasswordHash = hash,
                    FullName = $"{family[i % family.Length]} {middle[i * 5 % middle.Length]} {given[i * 7 % given.Length]}",
                    Email = $"student{no:00}@ifa.local", Phone = $"09121{no:00000}",
                    DateOfBirth = today.AddYears(-(20 + i % 6)).AddDays(-11 * no),
                    Gender = i % 2 == 0 ? Gender.F : Gender.M, Address = "Ho Chi Minh City",
                    Role = Role.STUDENT, Active = true,
                },
            };
            db.StudentProfiles.Add(student);
            students.Add(student);
        }
        await db.SaveChangesAsync();
        return students;
    }

    /// <summary>Past exhibitions with their entries; 2 of 3 entries sold to an archive customer and paid to the student.</summary>
    private static async Task AddArchiveExhibitionsAsync(AppDbContext db, List<(Competition Competition, List<Submission> Entries)> created,
        List<long> staffIds, DateOnly today)
    {
        var customers = new[]
        {
            ("Tran Quoc Dat", "Hanoi"), ("Le Thi Hong", "Ho Chi Minh City"), ("Nguyen Duc Minh", "Da Nang"), ("Pham Thanh Hai", "Hue"),
            ("Vo Ngoc Tram", "Can Tho"), ("Bui Gia Bao", "Hai Phong"), ("Dang Minh Chau", "Nha Trang"), ("Hoang Kim Ngan", "Da Lat"),
        }.Select((c, i) => new Customer
        {
            FullName = c.Item1, Email = $"collector{i + 1}@example.com", Phone = $"090200000{i + 1}", Address = c.Item2,
            Notes = i % 2 == 0 ? "Regular buyer at the institute's exhibitions" : null,
        }).ToList();
        db.Customers.AddRange(customers);

        var venues = new[]
        {
            ("Hanoi", "Vietnam Fine Arts Museum, 66 Nguyen Thai Hoc"), ("Ho Chi Minh City", "Ho Chi Minh City Fine Arts Museum, 97A Pho Duc Chinh"),
            ("Da Nang", "Da Nang Fine Arts Museum, 78 Le Duan"), ("Hue", "Hue Museum of Fine Arts, 150 Nguyen Hue"),
            ("Hai Phong", "Hai Phong Exhibition House, 1 Dien Bien Phu"), ("Da Lat", "Da Lat Art Gallery, 2 Tran Quoc Toan"),
            ("Can Tho", "Can Tho Culture Center, 2 Hoa Binh"), ("Nha Trang", "Nha Trang Art Space, 16 Tran Phu"),
            ("Hanoi", "Hanoi Contemporary Art Centre, 17 Trang Tien"),
        };
        var n = 0;
        foreach (var group in created.Chunk(3))
        {
            var (city, venue) = venues[n % venues.Length];
            var published = DateOnly.FromDateTime(group.Max(g => g.Competition.PublishedAt!.Value));
            var exhibition = new Exhibition
            {
                Name = $"{string.Join(" · ", group.Select(g => g.Competition.Title))} ({city})",
                Venue = venue, City = city, Organizer = "Institute of Fine Arts",
                Description = "Selected works from the " + string.Join(", ", group.Select(g => g.Competition.Title)) + " competitions.",
                StartDate = published.AddDays(7), EndDate = published.AddDays(21), CreatedBy = staffIds[n % staffIds.Count],
            };
            exhibition.Status = CompetitionStatusResolver.Resolve(exhibition, today);
            db.Exhibitions.Add(exhibition);
            await db.SaveChangesAsync();

            var i = 0;
            foreach (var s in group.SelectMany(g => g.Entries.Take(5)))
            {
                var asking = 150m + 50m * (i % 6);
                var reviewer = staffIds[i % staffIds.Count];
                if (i % 3 != 2)
                {
                    var soldOn = exhibition.StartDate.AddDays(1 + i % 12);
                    db.ExhibitionEntries.Add(Sold(exhibition, s, asking, asking + 10m * (i % 4), soldOn, customers[(i + n) % customers.Count],
                        paidOn: soldOn.AddDays(7), reviewer));
                }
                else
                {
                    db.ExhibitionEntries.Add(Entry(exhibition, s, asking, EntryStatus.UNSOLD, reviewer));
                }
                i++;
            }
            await db.SaveChangesAsync();
            n++;
        }
    }

    /// <summary>Next free code CT{year}-{nn} (highest number of that year + 1, like CompetitionService); remembers it.</summary>
    private static string NextArchiveCode(List<string> codes, int year)
    {
        var prefix = $"CT{year}-";
        var max = codes.Where(c => c.StartsWith(prefix)).Select(c => int.TryParse(c[prefix.Length..], out var x) ? x : 0).DefaultIfEmpty(0).Max();
        var code = $"{prefix}{max + 1:D2}";
        codes.Add(code);
        return code;
    }

    /// <summary>Copies an archive picture below the upload root and returns its relative path.</summary>
    private static async Task<string> CopyImageAsync(IFileStorageService files, string imageFolder, ArchivePainting p, string relativePath, List<string> written)
    {
        var path = await WriteImageAsync(files, relativePath, await File.ReadAllBytesAsync(Path.Combine(imageFolder, p.Uuid + ".jpg")));
        written.Add(path);
        return path;
    }
}
