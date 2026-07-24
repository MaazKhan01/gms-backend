using System;

namespace DomainPersistence.Entities;

public class Notification
{
    public int Id { get; set; }
    public Guid PublicId { get; set; }
    public int UserId { get; set; }
    public string Title { get; set; }
    public string? Message { get; set; }
    public string? Type { get; set; }
    public bool? Read { get; set; }
    public DateTime? CreatedAt { get; set; }
    public string? RedirectUrl { get; set; }

    public virtual User User { get; set; }
}
