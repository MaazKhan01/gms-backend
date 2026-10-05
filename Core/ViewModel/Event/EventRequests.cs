using System;

namespace Core.ViewModel.Event;

public class CreateEventRequest
{
    public string Title { get; set; }
    public string Type { get; set; }
    public string VenueName { get; set; }
    public Guid? VenueId { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string Status { get; set; } = "planning";
    /// <summary>fixed | flexible. Omit to keep the default (flexible), which is
    /// the unrestricted pre-service-level flow.</summary>
    public string GuestModel { get; set; }
    public string ImageUrl { get; set; }
    public string AttachmentUrl { get; set; }

    // ── Mission fields ───────────────────────────────────────────────────
    /// <summary>A Locations row — where the delegation travels to.</summary>
    public Guid? DestinationId { get; set; }
    /// <summary>Headcount cap the host set. Enforced when nominating.</summary>
    public int? DelegationCap { get; set; }
    /// <summary>Core.Constants.DestinationTiers — regional / international_a|b|c.</summary>
    public string DestinationTier { get; set; }
    /// <summary>The budget line the mission is charged to. Free text — finance
    /// lives outside DMS.</summary>
    public string CostCenter { get; set; }
    /// <summary>Core.Constants.FundingModels — org_paid / hosted / mixed.</summary>
    public string FundingModel { get; set; }
    /// <summary>An Organizations row. The name is copied from it, so
    /// <c>hostName</c> is never sent by the client.</summary>
    public Guid? HostOrganizationId { get; set; }
    public string HostEmail { get; set; }
}

public class UpdateEventRequest
{
    /// <summary>fixed | flexible. Omit to keep the default (flexible), which is
    /// the unrestricted pre-service-level flow.</summary>
    public string GuestModel { get; set; }
    public string Title { get; set; }
    public string Type { get; set; }
    public string VenueName { get; set; }
    public Guid? VenueId { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string Status { get; set; }
    public string ImageUrl { get; set; }
    public string AttachmentUrl { get; set; }

    // ── Mission fields. Null leaves the current value alone. ─────────────
    public Guid? DestinationId { get; set; }
    public int? DelegationCap { get; set; }
    /// <summary>Core.Constants.DestinationTiers — regional / international_a|b|c.</summary>
    public string DestinationTier { get; set; }
    /// <summary>The budget line the mission is charged to. Free text — finance
    /// lives outside DMS.</summary>
    public string CostCenter { get; set; }
    /// <summary>Core.Constants.FundingModels — org_paid / hosted / mixed.</summary>
    public string FundingModel { get; set; }
    /// <summary>An Organizations row. The name is copied from it, so
    /// <c>hostName</c> is never sent by the client.</summary>
    public Guid? HostOrganizationId { get; set; }
    public string HostEmail { get; set; }
}

public class UpdateEventStatusRequest
{
    public string Status { get; set; }
}

public class CreateSessionRequest
{
    public string Title { get; set; }
    public DateOnly? Date { get; set; }
    public string Time { get; set; }
    public string VenueName { get; set; }
    public Guid? VenueId { get; set; }
    public string Room { get; set; }
    public string Speaker { get; set; }
    public int Capacity { get; set; }
    public string ImageUrl { get; set; }
}

public class UpdateSessionRequest : CreateSessionRequest { }
