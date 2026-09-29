using System;

namespace Core.ViewModel.HostInvitation;

public class HostInvitationResponse
{
    public Guid Id { get; set; }
    /// <summary>The Organizations row. Null for invitations logged before the
    /// host became a directory entry — those still carry a name.</summary>
    public Guid? HostOrganizationId { get; set; }
    /// <summary>The organisation's name, resolved at save time.</summary>
    public string HostOrganization { get; set; }
    public string HostEmail { get; set; }
    public string MissionTitle { get; set; }

    public Guid? DestinationId { get; set; }
    public string DestinationAddress { get; set; }

    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public int? HeadcountCap { get; set; }
    public DateOnly? ResponseDeadline { get; set; }

    public string AttachmentUrl { get; set; }
    public string Status { get; set; }
    public string Notes { get; set; }

    /// <summary>The mission this became. Null while still logged.</summary>
    public Guid? ConvertedEventId { get; set; }
    public string ConvertedEventTitle { get; set; }

    public DateTime CreatedAt { get; set; }
}

public class CreateHostInvitationRequest
{
    /// <summary>An Organizations row — required. The name is copied from it,
    /// so the client never sends one.</summary>
    public Guid? HostOrganizationId { get; set; }
    public string HostEmail { get; set; }
    public string MissionTitle { get; set; }
    /// <summary>A Locations row. Create the location first if it is new.</summary>
    public Guid? DestinationId { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public int? HeadcountCap { get; set; }
    public DateOnly? ResponseDeadline { get; set; }
    public string AttachmentUrl { get; set; }
    public string Notes { get; set; }
}

public class UpdateHostInvitationRequest : CreateHostInvitationRequest { }

/// <summary>Turns a logged invitation into a mission. Everything here defaults
/// from the invitation when omitted.</summary>
public class ConvertInvitationRequest
{
    public string Title { get; set; }
    public string Type { get; set; }
    public Guid? VenueId { get; set; }
    public string VenueName { get; set; }
    public Guid? DestinationId { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public int? DelegationCap { get; set; }

    // The host's letter says nothing about these — they are ours to decide at
    // conversion, which is why they have no invitation counterpart to default from.
    /// <summary>Core.Constants.DestinationTiers — regional / international_a|b|c.</summary>
    public string DestinationTier { get; set; }
    /// <summary>The budget line the mission is charged to.</summary>
    public string CostCenter { get; set; }
    /// <summary>Core.Constants.FundingModels — org_paid / hosted / mixed.</summary>
    public string FundingModel { get; set; }

    /// <summary>fixed | flexible. Omit for the default (flexible).</summary>
    public string GuestModel { get; set; }

    // The conversion form is the New Mission form, so it collects a cover image
    // and an attachment like any other mission. Without these the two silently
    // vanished on save: the form gathered them and the convert request had
    // nowhere to put them.
    public string ImageUrl { get; set; }
    public string AttachmentUrl { get; set; }
}
