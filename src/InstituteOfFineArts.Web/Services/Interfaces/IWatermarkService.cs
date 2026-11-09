namespace InstituteOfFineArts.Web.Services.Interfaces;

/// <summary>
/// Draws the institute's name over publicly shown paintings (F-M13-02), in memory, so the stored files never change.
/// </summary>
public interface IWatermarkService
{
    /// <summary>
    /// Reads a JPEG from <paramref name="source"/> and returns a new JPEG of the same size (EXIF rotation applied)
    /// with "Institute of Fine Arts" drawn semi-transparently across it, or null when the image cannot be decoded.
    /// </summary>
    /// <param name="source">Stored JPEG (read to the end; not disposed)</param>
    Task<byte[]?> ApplyAsync(Stream source);
}
