namespace InstituteOfFineArts.Web.Exceptions;

/// <summary>
/// Requested data does not exist (e.g. unknown competition id). Controllers return the 404 page.
/// </summary>
public class NotFoundException : Exception
{
    /// <summary>Creates a not-found error with a descriptive message.</summary>
    public NotFoundException(string message) : base(message)
    {
    }
}
