using System;

namespace DomainPersistence.Entities;

public class Device : Entity
{
    public int UserId { get; set; }
    public string Token { get; set; }
    public string Platform { get; set; } // ios | android | web
    public string DeviceIdentifier { get; set; }
    public string DeviceModel { get; set; }
    public string OsVersion { get; set; }
    public string AppVersion { get; set; }
    public bool NotificationsEnabled { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public DateTime? LastActiveAt { get; set; }
    public DateTime? TokenUpdatedAt { get; set; }
    public virtual User User { get; set; }
}
