using InstituteOfFineArts.Web.Helpers;
using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Theme = InstituteOfFineArts.Web.Data.Seed.SampleArtwork.Theme;

namespace InstituteOfFineArts.Web.Data.Seed;

/// <summary>
/// Sample data part: competitions, submissions, evaluations, awards, exhibitions, customers (task BE2-01,
/// docs/plan/04 sections 4.2 – 4.4). All dates are relative to the day the seed runs, so statuses are always right.
/// Runs after SeedAccountsAsync, so staff and student accounts already exist.
/// </summary>
public static partial class DbSeeder
{
    /// <summary>One sample entry: student number, rating (null = not evaluated), days after the competition start.</summary>
    private sealed record EntrySpec(int Student, Rating? Rating, int Day, string Title, TextType TextType, string Text,
        string Description, string FileName, int Hour = 10, bool Late = false);

    /// <summary>
    /// Inserts sample business data so modules can be built and tested in parallel.
    /// </summary>
    /// <param name="db">DbContext used to insert the data</param>
    /// <param name="services">Scoped services (file storage and thumbnails for the sample images)</param>
    internal static async Task SeedSampleDataAsync(AppDbContext db, IServiceProvider services)
    {
        var today = DateTimeHelper.Today;
        var files = services.GetRequiredService<IFileStorageService>();
        var thumbnails = services.GetRequiredService<IThumbnailService>();
        var env = services.GetRequiredService<IWebHostEnvironment>();
        var seedImageFolder = Path.GetFullPath(Path.Combine(env.ContentRootPath, "..", "..", "database", "seed-images"));

        // Accounts created by SeedAccountsAsync
        var staff = await db.StaffProfiles.Include(s => s.Account).ToDictionaryAsync(s => s.Account.Username);
        var students = await db.StudentProfiles.Include(s => s.Account).ToDictionaryAsync(s => s.Account.Username);
        StudentProfile Student(int n) => students[$"student{n:D2}"];
        var staff01 = staff["staff01"].Id;
        var staff02 = staff["staff02"].Id;
        var staff03 = staff["staff03"].Id;

        // ------------------------------------------------------------ (1) competitions + awards
        var hometown = NewCompetition("Hometown Landscapes", -60, -30, staff01,
            "Paint the landscape of the place where you grew up: rivers, rice fields, hills or streets.",
            "One painting per student. JPEG, up to 5 MB. A short poem, quotation or story must accompany the painting.",
            ("First Prize", 1, 300m, 1, "Certificate and art supplies voucher"),
            ("Second Prize", 2, 200m, 1, "Certificate and sketchbook set"),
            ("Third Prize", 3, 100m, 1, "Certificate"));
        var wildlife = NewCompetition("Wildlife", -45, -15, staff02,
            "Animals in their natural habitat. Any medium: oil, watercolor, digital.",
            "Original work only. No photos traced or copied. One painting per student.",
            ("First Prize", 1, 300m, 1, "Certificate and art supplies voucher"),
            ("Second Prize", 2, 200m, 1, "Certificate"),
            ("Third Prize", 3, 100m, 1, "Certificate"),
            ("Honorable Mention", 4, 50m, 2, "Certificate"));
        var cityAtNight = NewCompetition("City at Night", -25, -1, staff03,
            "Lights, streets and buildings after sunset.",
            "One painting per student. Evaluation starts after the deadline.",
            ("First Prize", 1, 250m, 1, "Certificate and voucher"),
            ("Second Prize", 2, 150m, 1, "Certificate"));
        var autumn = NewCompetition("Colors of Autumn", -10, 10, staff02,
            "The warm colors of autumn: falling leaves, harvest and golden light.",
            "One painting per student. You may edit or replace your entry until the deadline.",
            ("First Prize", 1, 300m, 1, "Certificate and art supplies voucher"),
            ("Second Prize", 2, 200m, 1, "Certificate"),
            ("Third Prize", 3, 100m, 1, "Certificate"));
        var deadlineToday = NewCompetition("Deadline Today", -7, 0, staff01,
            "A quick sketch challenge – today is the last day to submit.",
            "Entries are accepted until 23:59:59 today.",
            ("First Prize", 1, 150m, 1, "Certificate"));
        var lunarNewYear = NewCompetition("Lunar New Year 2027", 5, 30, staff01,
            "Celebrate Tet: lanterns, peach blossoms, family reunions and fireworks.",
            "One painting per student. Traditional or modern styles are welcome.",
            ("First Prize", 1, 300m, 1, "Certificate and lucky money envelope"),
            ("Second Prize", 2, 200m, 1, "Certificate"),
            ("Third Prize", 3, 100m, 1, "Certificate"));
        var familyPortrait = NewCompetition("Family Portrait", 15, 45, staff03,
            "A portrait of your family or of someone you love.",
            "One painting per student. Do not use photos of real faces as the final work.",
            ("First Prize", 1, 200m, 1, "Certificate"));

        var competitions = new[] { hometown, wildlife, cityAtNight, autumn, deadlineToday, lunarNewYear, familyPortrait };
        AssignCodes(competitions);
        db.Competitions.AddRange(competitions);
        await db.SaveChangesAsync();

        // Cover images for all but the last competition (so the UI also shows a competition without cover):
        // database/seed-images/cover-<title slug>.jpg when the QA set provides one (QA-01), otherwise drawn
        var coverThemes = new[] { Theme.Landscape, Theme.Wildlife, Theme.Night, Theme.Autumn, Theme.Abstract, Theme.Festival };
        for (var i = 0; i < coverThemes.Length; i++)
        {
            var coverBytes = LoadSeedImage(seedImageFolder, $"cover-{Slug(competitions[i].Title)}.jpg")
                             ?? SampleArtwork.Create(coverThemes[i], 1000 + i, 1600, 600);
            competitions[i].CoverImagePath = await WriteImageAsync(files, $"competitions/{competitions[i].Id}/cover-{Guid.NewGuid()}.jpg", coverBytes);
        }
        await db.SaveChangesAsync();

        // ------------------------------------------------------------ (2) submissions + images
        var plan = new (Competition Competition, Theme Theme, EntrySpec[] Entries)[]
        {
            (hometown, Theme.Landscape, new EntrySpec[]
            {
                new(1, Rating.BEST, 2, "River of My Childhood", TextType.POEM,
                    "The river bends where the bamboo sleeps,\nit carries the songs my grandmother keeps.",
                    "Oil painting of the river behind my grandparents' house at sunset.", "river-of-my-childhood.jpg"),
                new(2, Rating.BEST, 10, "Golden Rice Fields", TextType.QUOTATION,
                    "\"To see a World in a Grain of Sand, and a Heaven in a Wild Flower.\" – William Blake",
                    "Watercolor of rice fields just before the harvest.", "golden-rice-fields.jpg", 14),
                new(3, Rating.BEST, 25, "Morning Mist Over the Hills", TextType.POEM,
                    "Sương sớm vương trên đồi chè xanh,\nquê nhà thức dậy trong nắng mong manh.",
                    "Acrylic painting of tea hills in the early morning.", "morning-mist.jpg", 9),
                new(4, Rating.BETTER, 6, "Old Market Street", TextType.STORY,
                    "Every Sunday my father took me to this street to buy bread. The shops are still there.",
                    "Pencil and watercolor street scene.", "old-market-street.jpg", 16),
                new(5, Rating.BETTER, 12, "Fishing Village", TextType.POEM,
                    "Boats asleep on silver sand,\nnets still warm from father's hand.",
                    "Gouache painting of a fishing village at dawn.", "fishing-village.jpg", 11),
                new(6, Rating.GOOD, 18, "Banyan Tree", TextType.OTHER,
                    "The banyan tree at the village gate is older than anyone can remember.",
                    "Ink drawing of the village banyan tree.", "banyan-tree.jpg", 15),
                new(8, Rating.MODERATE, 22, "Rainy Season", TextType.QUOTATION,
                    "\"Hope is the thing with feathers that perches in the soul.\" – Emily Dickinson",
                    "Digital painting of my street during the rainy season.", "rainy-season.jpg", 20),
                // Late entry added by staff after the deadline (TC-M07-04) → DISQUALIFIED
                new(9, Rating.DISQUALIFIED, 31, "Sunset Bridge", TextType.POEM,
                    "The bridge holds the sun\nuntil the river lets it go.",
                    "Watercolor of the bridge near my school.", "sunset-bridge.jpg", 9, Late: true),
            }),
            (wildlife, Theme.Wildlife, new EntrySpec[]
            {
                new(1, Rating.BEST, 4, "Fox in the Snow", TextType.POEM,
                    "Quiet paws on silver ground,\nthe fox goes home without a sound.",
                    "Oil painting of a red fox in winter.", "fox-in-the-snow.jpg"),
                new(2, Rating.BEST, 9, "The Old Elephant", TextType.STORY,
                    "I watched an elephant at the national park for an hour. It never hurried.",
                    "Charcoal and pastel study of an elephant.", "old-elephant.jpg", 13),
                new(3, Rating.BETTER, 14, "Kingfisher", TextType.OTHER,
                    "A flash of blue above the pond.",
                    "Watercolor of a kingfisher diving.", "kingfisher.jpg", 8),
                new(4, Rating.BETTER, 20, "Langur Family", TextType.POEM,
                    "Đàn voọc nhỏ chuyền cành lá biếc,\nrừng già ơi xin mãi xanh tươi.",
                    "Acrylic painting of Delacour's langurs.", "langur-family.jpg", 17),
                new(5, Rating.GOOD, 24, "Owl at Midnight", TextType.QUOTATION,
                    "\"The owl, for all his feathers, was a-cold.\" – John Keats",
                    "Digital painting of an owl on a branch.", "owl-at-midnight.jpg", 21),
                new(6, Rating.NORMAL, 27, "Butterflies", TextType.OTHER,
                    "Butterflies in the school garden.",
                    "Colored pencil drawing.", "butterflies.jpg", 10),
            }),
            (cityAtNight, Theme.Night, new EntrySpec[]
            {
                new(1, null, 3, "Night Market", TextType.POEM,
                    "Lanterns hum and noodles steam,\nthe city wakes inside a dream.",
                    "Gouache painting of the night market.", "night-market.jpg", 19),
                new(2, null, 8, "Bridge Lights", TextType.OTHER,
                    "The bridge changes color every ten minutes.",
                    "Digital painting of the city bridge at night.", "bridge-lights.jpg", 22),
                new(3, null, 12, "Last Bus Home", TextType.STORY,
                    "On the last bus the city looks like a river of lights.",
                    "Acrylic painting from the bus window.", "last-bus-home.jpg", 23),
                new(5, null, 17, "Rooftop View", TextType.QUOTATION,
                    "\"The night has a thousand eyes.\" – Francis William Bourdillon",
                    "Watercolor of the skyline from a rooftop.", "rooftop-view.jpg", 20),
                new(8, null, 22, "Street Food Corner", TextType.OTHER,
                    "My favorite corner after school.",
                    "Ink and watercolor sketch.", "street-food-corner.jpg", 18),
            }),
            (autumn, Theme.Autumn, new EntrySpec[]
            {
                new(2, null, 1, "Falling Maple Leaves", TextType.POEM,
                    "Red letters falling from the trees,\nthe autumn writes in every breeze.",
                    "Oil painting of maple leaves in the park.", "falling-maple-leaves.jpg"),
                new(3, null, 2, "Harvest Moon", TextType.OTHER,
                    "The full moon over the harvested fields.",
                    "Acrylic painting.", "harvest-moon.jpg", 21),
                new(4, null, 4, "Grandma's Persimmons", TextType.STORY,
                    "Every autumn my grandmother dries persimmons on the balcony.",
                    "Watercolor still life.", "grandmas-persimmons.jpg", 15),
                new(5, null, 5, "Golden Path", TextType.QUOTATION,
                    "\"Autumn is a second spring when every leaf is a flower.\" – Albert Camus",
                    "Digital painting of a path covered with leaves.", "golden-path.jpg", 9),
                new(6, null, 7, "Hanoi in Autumn", TextType.POEM,
                    "Hà Nội mùa thu, hoa sữa thơm nồng,\nlá vàng rơi nhẹ trên phố đông.",
                    "Gouache painting of an old Hanoi street.", "hanoi-in-autumn.jpg", 16),
                new(8, null, 9, "Autumn Rain", TextType.OTHER,
                    "Rain on orange leaves.",
                    "Watercolor.", "autumn-rain.jpg", 12),
            }),
            (deadlineToday, Theme.Abstract, new EntrySpec[]
            {
                new(2, null, 1, "Quick Sketch: Cafe", TextType.OTHER,
                    "Ten-minute sketch at my favorite cafe.",
                    "Pencil sketch.", "quick-sketch-cafe.jpg", 8),
                new(3, null, 6, "Quick Sketch: Park", TextType.POEM,
                    "Five quick lines,\na bench, a tree, a summer sky.",
                    "Ink sketch.", "quick-sketch-park.jpg", 18),
            }),
        };

        var created = new Dictionary<(Competition, int), Submission>();
        var imageNumber = 0;
        foreach (var (competition, theme, entries) in plan)
        {
            foreach (var e in entries)
            {
                imageNumber++;
                var bytes = LoadSeedImage(seedImageFolder, e.FileName)
                            ?? SampleArtwork.Create(theme, imageNumber, imageNumber % 3 == 0 ? 900 : 1200, imageNumber % 3 == 0 ? 1200 : 900);
                var guid = Guid.NewGuid();
                var imagePath = await WriteImageAsync(files, $"submissions/{competition.Id}/{guid}.jpg", bytes);
                var thumbPath = $"thumbnails/{competition.Id}/{guid}.jpg";
                await thumbnails.CreateAsync(files.ResolvePath(imagePath), files.ResolvePath(thumbPath), 400);

                // Submission time: the given day after the start (late entry: the day after the deadline)
                var day = e.Late ? competition.EndDate.AddDays(1) : competition.StartDate.AddDays(e.Day);
                var submission = new Submission
                {
                    CompetitionId = competition.Id,
                    StudentId = Student(e.Student).Id,
                    Title = e.Title,
                    ImagePath = imagePath,
                    ThumbnailPath = thumbPath,
                    OriginalFileName = e.FileName,
                    FileSize = bytes.Length,
                    TextType = e.TextType,
                    TextContent = e.Text,
                    Description = e.Description,
                    SubmittedAt = day.ToDateTime(new TimeOnly(e.Hour, 15 + imageNumber % 40)),
                };
                db.Submissions.Add(submission);
                created[(competition, e.Student)] = submission;
            }
        }
        await db.SaveChangesAsync();

        // ------------------------------------------------------------ (3) evaluations (all 6 ratings)
        foreach (var (competition, _, entries) in plan)
        {
            foreach (var e in entries.Where(x => x.Rating != null))
            {
                db.Evaluations.Add(NewEvaluation(created[(competition, e.Student)], e.Rating!.Value,
                    e.Student % 3 == 0 ? staff02 : staff01, competition.EndDate.AddDays(1 + e.Student % 3)));
            }
        }
        await db.SaveChangesAsync();

        // ------------------------------------------------------------ (4) issued awards + publish results
        // Hometown: First and Second given; Third Prize left open to demo issuing an award (TC-M08-01)
        IssueAward(db, hometown, "First Prize", created[(hometown, 1)], staff01, -26, "Outstanding use of light and color.");
        IssueAward(db, hometown, "Second Prize", created[(hometown, 2)], staff01, -26, "A calm, well balanced composition.");
        IssueAward(db, wildlife, "First Prize", created[(wildlife, 2)], staff02, -12, "Powerful texture and a strong sense of age.");
        IssueAward(db, wildlife, "Second Prize", created[(wildlife, 1)], staff02, -12, "Lovely atmosphere and soft palette.");
        Publish(hometown, staff01, -25, "Thank you to all 8 participants. The level this year was very high.");
        Publish(wildlife, staff02, -11, "Congratulations to the winners and to everyone who took part.");
        await db.SaveChangesAsync();

        // ------------------------------------------------------------ (5) customers, exhibitions, entries
        var customers = new[]
        {
            new Customer { FullName = "Hoang Anh Tuan", Email = "khachhang1@example.com", Phone = "0901000001", Address = "District 1, Ho Chi Minh City" },
            new Customer { FullName = "Nguyen Thi Lan", Email = "khachhang2@example.com", Phone = "0901000002", Address = "Hoan Kiem District, Hanoi" },
            new Customer { FullName = "Pham Gia Huy", Email = "khachhang3@example.com", Phone = "0901000003", Address = "Hai Chau District, Da Nang", Notes = "Collector of watercolor paintings" },
        };
        db.Customers.AddRange(customers);

        var hanoi = new Exhibition
        {
            Name = "Young Artists Exhibition (Hanoi)", Venue = "Vietnam Fine Arts Museum, 66 Nguyen Thai Hoc", City = "Hanoi",
            Organizer = "Institute of Fine Arts", Description = "Selected works from the Hometown Landscapes competition.",
            StartDate = today.AddDays(-20), EndDate = today.AddDays(-5), CreatedBy = staff01,
        };
        var saigon = new Exhibition
        {
            Name = "Saigon Spring Exhibition", Venue = "Ho Chi Minh City Fine Arts Museum, 97A Pho Duc Chinh", City = "Ho Chi Minh City",
            Organizer = "Institute of Fine Arts", Description = "Spring showcase of the best Wildlife entries.",
            StartDate = today.AddDays(20), EndDate = today.AddDays(27), CreatedBy = staff02,
        };
        hanoi.Status = CompetitionStatusResolver.Resolve(hanoi, today);
        saigon.Status = CompetitionStatusResolver.Resolve(saigon, today);
        db.Exhibitions.AddRange(hanoi, saigon);
        await db.SaveChangesAsync();

        // Hanoi (finished): 5 entries from Hometown BEST/BETTER – 3 sold (student01's not paid yet: TC-M09-07), 2 unsold
        db.ExhibitionEntries.AddRange(
            Sold(hanoi, created[(hometown, 1)], 250m, 250m, today.AddDays(-12), customers[0], paidOn: null, staff01),
            Sold(hanoi, created[(hometown, 2)], 300m, 320m, today.AddDays(-10), customers[1], paidOn: today.AddDays(-4), staff01),
            Sold(hanoi, created[(hometown, 4)], 150m, 180m, today.AddDays(-8), customers[2], paidOn: today.AddDays(-3), staff01),
            Entry(hanoi, created[(hometown, 3)], 220m, EntryStatus.UNSOLD, staff01),
            Entry(hanoi, created[(hometown, 5)], 120m, EntryStatus.UNSOLD, staff01));
        // Saigon (upcoming): 3 entries from Wildlife; student01's BEST is left free to propose (TC-M09-02)
        db.ExhibitionEntries.AddRange(
            Entry(saigon, created[(wildlife, 2)], 350m, EntryStatus.ACCEPTED, staff02),
            Entry(saigon, created[(wildlife, 3)], 200m, EntryStatus.ACCEPTED, staff02),
            Entry(saigon, created[(wildlife, 4)], 180m, EntryStatus.PENDING, null));
        await db.SaveChangesAsync();
    }

    /// <summary>Competition with dates relative to today, its status from the dates, and its awards.</summary>
    private static Competition NewCompetition(string title, int startIn, int endIn, long createdBy, string description, string rules,
        params (string Name, int Rank, decimal Prize, int Quantity, string Description)[] awards)
    {
        var today = DateTimeHelper.Today;
        var c = new Competition
        {
            Title = title, Description = description, Rules = rules,
            StartDate = today.AddDays(startIn), EndDate = today.AddDays(endIn), CreatedBy = createdBy,
        };
        c.Status = CompetitionStatusResolver.Resolve(c, today);
        foreach (var a in awards)
        {
            c.Awards.Add(new CompetitionAward { AwardName = a.Name, Rank = a.Rank, PrizeValue = a.Prize, Quantity = a.Quantity, PrizeDescription = a.Description });
        }
        return c;
    }

    /// <summary>Codes CT{year}-{nn} numbered by start date within each year (same format as CompetitionService).</summary>
    private static void AssignCodes(IEnumerable<Competition> competitions)
    {
        foreach (var year in competitions.GroupBy(c => c.StartDate.Year))
        {
            var n = 0;
            foreach (var c in year.OrderBy(c => c.StartDate))
            {
                c.Code = $"CT{year.Key}-{++n:D2}";
            }
        }
    }

    /// <summary>Evaluation with English comments matching the rating (DISQUALIFIED always has a reason).</summary>
    private static Evaluation NewEvaluation(Submission s, Rating rating, long staffId, DateOnly day)
    {
        var (positive, negative, improve) = rating switch
        {
            Rating.BEST => ("Excellent composition and confident brushwork.", "Very little to criticise.", "Keep experimenting with bolder formats."),
            Rating.BETTER => ("Good color harmony and clear subject.", "The background is a little busy.", "Simplify the background to focus the eye."),
            Rating.GOOD => ("Nice idea and honest observation.", "Proportions are uneven in places.", "Practise quick proportion sketches."),
            Rating.MODERATE => ("The theme is understood.", "Colors are flat and the edges are unclear.", "Study light and shadow on simple objects."),
            Rating.NORMAL => ("The work is complete.", "Limited effort on details.", "Spend more time on planning the composition."),
            _ => ((string?)null, "Submitted after the deadline (late entry added by staff).", (string?)null),
        };
        return new Evaluation
        {
            SubmissionId = s.Id, Rating = rating, EvaluatedBy = staffId,
            PositivePoints = positive, NegativePoints = negative, ImprovementAreas = improve,
            EvaluatedAt = day.ToDateTime(new TimeOnly(10, 0)),
        };
    }

    /// <summary>Gives an award of the competition to a (BEST) submission.</summary>
    private static void IssueAward(AppDbContext db, Competition competition, string awardName, Submission submission, long staffId, int dayOffset, string remarks)
    {
        db.AwardsIssued.Add(new AwardIssued
        {
            CompetitionAwardId = competition.Awards.Single(a => a.AwardName == awardName).Id,
            SubmissionId = submission.Id, IssuedBy = staffId,
            IssuedDate = DateTimeHelper.Today.AddDays(dayOffset), Remarks = remarks,
        });
    }

    /// <summary>Publishes the results of a competition (→ GRADED, BR-08).</summary>
    private static void Publish(Competition competition, long staffId, int dayOffset, string remarks)
    {
        competition.Status = CompetitionStatus.GRADED;
        competition.PublishedBy = staffId;
        competition.PublishedAt = DateTimeHelper.Today.AddDays(dayOffset).ToDateTime(new TimeOnly(9, 0));
        competition.ResultRemarks = remarks;
    }

    /// <summary>Exhibition entry proposed by the student at their asking price.</summary>
    private static ExhibitionEntry Entry(Exhibition exhibition, Submission s, decimal askingPrice, EntryStatus status, long? reviewedBy)
        => new()
        {
            ExhibitionId = exhibition.Id, SubmissionId = s.Id, AskingPrice = askingPrice, Status = status,
            ProposedBy = s.Student.AccountId, ReviewedBy = reviewedBy,
        };

    /// <summary>Sold entry; paid to the student when <paramref name="paidOn"/> is set (full amount, BR-45).</summary>
    private static ExhibitionEntry Sold(Exhibition exhibition, Submission s, decimal askingPrice, decimal soldPrice, DateOnly soldOn,
        Customer customer, DateOnly? paidOn, long reviewedBy)
    {
        var entry = Entry(exhibition, s, askingPrice, EntryStatus.SOLD, reviewedBy);
        entry.SoldPrice = soldPrice;
        entry.SoldDate = soldOn;
        entry.Customer = customer;
        if (paidOn != null)
        {
            entry.PaidToStudent = true;
            entry.PaidAmount = soldPrice;
            entry.PaidDate = paidOn;
        }
        return entry;
    }

    /// <summary>Picture from database/seed-images when QA has added one with this name (QA-01), otherwise null.</summary>
    private static byte[]? LoadSeedImage(string folder, string fileName)
    {
        var path = Path.Combine(folder, fileName);
        return File.Exists(path) ? File.ReadAllBytes(path) : null;
    }

    /// <summary>File-name slug of a title: "Lunar New Year 2027" → "lunar-new-year-2027".</summary>
    /// <param name="title">Competition title</param>
    internal static string Slug(string title)
        => System.Text.RegularExpressions.Regex.Replace(title.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');

    /// <summary>Writes image bytes below the upload root with File I/O and returns the relative path.</summary>
    private static async Task<string> WriteImageAsync(IFileStorageService files, string relativePath, byte[] bytes)
    {
        var fullPath = files.ResolvePath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllBytesAsync(fullPath, bytes);
        return relativePath;
    }
}
