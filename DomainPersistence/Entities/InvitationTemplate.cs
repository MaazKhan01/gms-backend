namespace DomainPersistence.Entities;

public class InvitationTemplate : Entity
{
    public int EventId { get; set; }
    public string Name { get; set; }
    public string? NameAr { get; set; }
    public string Language { get; set; }      // "en" | "ar" | "both"
    public string Subject { get; set; }
    public string? SubjectAr { get; set; }
    public string Body { get; set; }
    public string? BodyAr { get; set; }
    public string? TargetTiers { get; set; }  // comma-separated: "VIP,VVIP"
    public string? Color { get; set; }         // "#1aaec4"
    public bool IsActive { get; set; } = true;

    public virtual Event Event { get; set; }
}
