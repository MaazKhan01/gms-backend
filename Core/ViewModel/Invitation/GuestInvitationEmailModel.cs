using System;

namespace Core.ViewModel.Invitation;

// Everything the branded invitation email needs to render its header meta
// pills and detail card — separate from the admin's own custom template body.
public class GuestInvitationEmailModel
{
    public string GuestName { get; set; }
    public string Subject { get; set; }
    public string BodyHtml { get; set; }
    public string CtaUrl { get; set; }
    public string EventTitle { get; set; }
    public string EventVenue { get; set; }
    public DateOnly? EventStartDate { get; set; }
    public DateOnly? EventEndDate { get; set; }
    public string Tier { get; set; }
    public string Reference { get; set; }
}
