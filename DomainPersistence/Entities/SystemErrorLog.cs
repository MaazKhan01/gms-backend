using System;

namespace DomainPersistence.Entities;

public partial class SystemErrorLog
{
    public Guid Id { get; set; }
    public string ErrorMessage { get; set; }
    public string StackTrace { get; set; }
    public string Source { get; set; }
    public string RequestPath { get; set; }
    public string RequestMethod { get; set; }
    public Guid? UserId { get; set; }
    public DateTime OccurredAt { get; set; }
    public string IpAddress { get; set; }
    public string RequestId { get; set; }

    public virtual User User { get; set; }
}
