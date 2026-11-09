namespace InstituteOfFineArts.Web.Models.Entities;

/// <summary>
/// One-time token of the "forgot password" e-mail link (F-M16-01).
/// Only the SHA-256 hash of the token is stored, so a database leak does not reveal usable links.
/// </summary>
public class PasswordResetToken : BaseEntity
{
    /// <summary>Account whose password may be reset.</summary>
    public long AccountId { get; set; }

    /// <summary>SHA-256 hash (hex) of the token sent by e-mail.</summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>The link stops working after this time (institute time).</summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>When the token was used; a used token cannot be used again.</summary>
    public DateTime? UsedAt { get; set; }

    /// <summary>Account whose password may be reset.</summary>
    public Account Account { get; set; } = null!;
}
