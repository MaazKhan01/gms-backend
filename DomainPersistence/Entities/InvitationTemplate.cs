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
    // JSON blob of the visual builder's settings (background, image, base font,
    // font size, button label/color, etc.) so the editor can reload exact state.
    // The email HTML itself is self-contained in Body; this is for round-tripping.
    public string? DesignConfig { get; set; }
    public bool IsActive { get; set; } = true;

    public virtual Event Event { get; set; }
}
