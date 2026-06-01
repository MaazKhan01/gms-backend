using System;

namespace DomainPersistence.Entities;

public class OtpVerification : Entity
{
    public string Email { get; set; }
    public string OtpCode { get; set; }
    public string Purpose { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsUsed { get; set; }
    public DateTime? UsedAt { get; set; }
}
