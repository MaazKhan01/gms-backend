using System;

namespace DomainPersistence.Entities;

public class UserRefreshToken
{
    public int Id { get; set; }
    public Guid PublicId { get; set; }
    public int UserId { get; set; }
    public string Jti { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsRevoked { get; set; }
    public DateTime CreatedAt { get; set; }

    public virtual User User { get; set; }
}
