using System.ComponentModel.DataAnnotations;
using InstituteOfFineArts.Web.Models.Enums;

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
    [StringLength(DescriptionMaxLength, ErrorMessage = "Description must be at most 2000 characters")]
    [Display(Name = "Description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>Type of the accompanying text.</summary>
    [Required(ErrorMessage = "Please choose the text type")]
    [Display(Name = "Text type")]
    public TextType? TextType { get; set; }

    /// <summary>Poem / quotation / story that goes with the painting.</summary>
    [Required(ErrorMessage = "Please enter the poem, quotation or story")]
    [StringLength(TextMaxLength, ErrorMessage = "The text must be at most 4000 characters")]
    [Display(Name = "Poem / quotation / story")]
    public string TextContent { get; set; } = string.Empty;
}

/// <summary>
/// Submit an entry (F-M06-01, SCR-43): /student/submissions/new?competitionId={id}. The image is required.
/// </summary>
public class SubmissionFormVm : SubmissionFieldsVm
{
    /// <summary>The painting: JPEG (.jpg / .jpeg), at most 5 MB (BR-16, BR-17). Checked again by the file storage.</summary>
    [Required(ErrorMessage = "Please choose an image file.")]
    [Display(Name = "Image (JPEG, up to 5 MB)")]
    public IFormFile? Image { get; set; }
}

/// <summary>
/// Edit an entry (F-M06-02, F-M06-03, SCR-43): text fields plus an optional new image that replaces the current one.
/// </summary>
public class SubmissionEditVm : SubmissionFieldsVm
{
    /// <summary>Submission id.</summary>
    public long Id { get; set; }

    /// <summary>File name of the current image (display only).</summary>
    public string OriginalFileName { get; set; } = string.Empty;

    /// <summary>Optional new image; when chosen it replaces the current file (BR-18).</summary>
    [Display(Name = "Replace the image (optional, JPEG up to 5 MB)")]
    public IFormFile? NewImage { get; set; }
}

/// <summary>Result of saving an entry.</summary>
/// <param name="Id">Submission id</param>
/// <param name="Warning">Non-blocking advice about the image (small image), or null</param>
public record SubmissionSaveResult(long Id, string? Warning);
