using System.ComponentModel.DataAnnotations;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.Validation;

namespace InstituteOfFineArts.Web.ViewModels.Student.Submissions;

/// <summary>
/// Text fields shared by the submit and edit forms of an entry (BR-19): title, description, text type and text.
/// The competition fields are display only and always reloaded from the database.
/// </summary>
public abstract class SubmissionFieldsVm
{
    /// <summary>Longest title (column submission.title).</summary>
    public const int TitleMaxLength = 150;

    /// <summary>Longest description (column submission.description).</summary>
    public const int DescriptionMaxLength = 2000;

    /// <summary>Longest poem / quotation / story (column submission.text_content).</summary>
    public const int TextMaxLength = 4000;

    /// <summary>Competition id (from the query string on submit, from the entry on edit).</summary>
    public long CompetitionId { get; set; }

    /// <summary>Competition title (display only).</summary>
    public string CompetitionTitle { get; set; } = string.Empty;

    /// <summary>Last day to submit / edit, deadline 23:59:59 (display only).</summary>
    public DateOnly Deadline { get; set; }

    /// <summary>Painting title.</summary>
    [Required(ErrorMessage = "Please enter a title")]
    [StringLength(TitleMaxLength, ErrorMessage = "Title must be at most 150 characters")]
    [Display(Name = "Title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>Why it was painted, why it deserves to win…</summary>
    [Required(ErrorMessage = "Please enter a description")]
    [RichTextLength(DescriptionMaxLength, ErrorMessage = "Description must be at most 2000 characters")]
    [Display(Name = "Description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>Type of the accompanying text.</summary>
    [Required(ErrorMessage = "Please choose the text type")]
    [Display(Name = "Text type")]
    public TextType? TextType { get; set; }

    /// <summary>Poem / quotation / story that goes with the painting.</summary>
    [Required(ErrorMessage = "Please enter the poem, quotation or story")]
    [RichTextLength(TextMaxLength, ErrorMessage = "The text must be at most 4000 characters")]
    [Display(Name = "Poem / quotation / story")]
    public string TextContent { get; set; } = string.Empty;
}

/// <summary>
/// Submit an entry (F-M06-01, SCR-43): /student/submissions/new?competitionId={id}. The image is required.
/// </summary>
public class SubmissionFormVm : SubmissionFieldsVm
{
    /// <summary>
    /// Pictures of the artwork (BR-16..18, widened 16/11/2026): 1 to 5 JPG / PNG / WebP files of at most 10 MB each –
    /// the same artwork from another angle, in another light, a close-up. Checked again by the file storage.
    /// </summary>
    [Display(Name = "Pictures (1 to 5 – JPG, PNG or WebP, up to 10 MB each)")]
    public List<IFormFile> Images { get; set; } = new();

    /// <summary>Which of <see cref="Images"/> is the cover (0 = the first).</summary>
    public int CoverIndex { get; set; }

    /// <summary>One picture, kept for the older mobile app (form field "image"); added to <see cref="Images"/>.</summary>
    public IFormFile? Image { get; set; }
}

/// <summary>
/// Edit an entry (F-M06-02, F-M06-03, SCR-43): text fields plus an optional new image that replaces the current one.
/// </summary>
public class SubmissionEditVm : SubmissionFieldsVm
{
    /// <summary>Submission id.</summary>
    public long Id { get; set; }

    /// <summary>File name of the current cover (display only).</summary>
    public string OriginalFileName { get; set; } = string.Empty;

    /// <summary>Display only: the current pictures, cover first.</summary>
    public List<SubmissionImageVm> CurrentImages { get; set; } = new();

    /// <summary>Pictures to add (JPG / PNG / WebP, up to 10 MB each); the entry keeps 1 to 5 pictures in total.</summary>
    [Display(Name = "Add pictures (JPG, PNG or WebP, up to 10 MB each)")]
    public List<IFormFile> NewImages { get; set; } = new();

    /// <summary>Current pictures to remove (submission_image ids).</summary>
    public List<long> RemoveImageIds { get; set; } = new();

    /// <summary>Current picture to make the cover (null = keep the cover, or the first one left).</summary>
    public long? CoverImageId { get; set; }

    /// <summary>Older mobile app: one new picture that replaces all the current ones (BR-18).</summary>
    public IFormFile? NewImage { get; set; }
}

/// <summary>One current picture on the edit form.</summary>
/// <param name="Id">submission_image.id</param>
/// <param name="FileName">File name chosen by the student</param>
/// <param name="FileSize">Size in bytes</param>
/// <param name="IsCover">The cover picture</param>
public record SubmissionImageVm(long Id, string FileName, long FileSize, bool IsCover);

/// <summary>Result of saving an entry.</summary>
/// <param name="Id">Submission id</param>
/// <param name="Warning">Non-blocking advice about the image (small image), or null</param>
public record SubmissionSaveResult(long Id, string? Warning);
