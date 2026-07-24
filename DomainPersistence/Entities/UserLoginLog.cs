using System;

namespace DomainPersistence.Entities;

public partial class UserLoginLog
{
    public int Id { get; set; }
    public Guid PublicId { get; set; }
    public int UserId { get; set; }
    public DateTime LoginAt { get; set; }
    public string IpAddress { get; set; }
    public string UserAgent { get; set; }
    public bool IsSuccessful { get; set; }
    public string FailureReason { get; set; }

    public virtual User User { get; set; }
}
