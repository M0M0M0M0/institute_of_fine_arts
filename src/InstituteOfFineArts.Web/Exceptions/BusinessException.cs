namespace InstituteOfFineArts.Web.Exceptions;

/// <summary>
/// Business rule violation (e.g. "The submission deadline has passed").
/// Services throw it; controllers catch it and show the message to the user.
/// </summary>
public class BusinessException : Exception
{
    /// <summary>Creates a business error with a user-facing (English) message.</summary>
    public BusinessException(string message) : base(message)
    {
    }
}
