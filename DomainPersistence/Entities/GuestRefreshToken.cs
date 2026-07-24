using System;

namespace DomainPersistence.Entities;

// Guest-app refresh token (mirror of UserRefreshToken). Guests log in via OTP,
// so there's no password — the refresh token is the only long-lived credential.
public class GuestRefreshToken
{
    public int Id { get; set; }
    public Guid PublicId { get; set; }
    public int GuestId { get; set; }
    public string Jti { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsRevoked { get; set; }
    public DateTime CreatedAt { get; set; }

    public virtual Guest Guest { get; set; }
}
