namespace InstituteOfFineArts.Web.Exceptions;

/// <summary>
/// Business rule violation (e.g. "The submission deadline has passed").
/// Services throw it; controllers catch it and show the message to the user:
/// <c>ModelState.AddModelError(ex.Field ?? string.Empty, ex.Message)</c>.
/// </summary>
public class BusinessException : Exception
{
    /// <summary>Creates a business error with a user-facing (English) message.</summary>
    /// <param name="message">Message shown to the user</param>
    /// <param name="field">Optional form field the error belongs to (null = general error)</param>
    public BusinessException(string message, string? field = null) : base(message)
    {
        Field = field;
    }

    /// <summary>Form field the error belongs to, or null for a general error.</summary>
    public string? Field { get; }
}
