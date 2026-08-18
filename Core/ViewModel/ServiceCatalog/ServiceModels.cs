using System;
using System.Collections.Generic;
using Core.Constants;

namespace Core.ViewModel.ServiceCatalog;

// ── Service ──────────────────────────────────────────────────────────────────

public class CreateServiceRequest
{
    public string Code { get; set; }
    public string Name { get; set; }
    public string NameAr { get; set; }
    public string Description { get; set; }
    public string Icon { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public ServiceFormDefinition Form { get; set; } = new();
}

public class UpdateServiceRequest : CreateServiceRequest { }

public class ServiceResponse
{
    public Guid Id { get; set; }
    public string Code { get; set; }
    public string Name { get; set; }
    public string NameAr { get; set; }
    public string Description { get; set; }
    public string Icon { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
    public ServiceFormDefinition Form { get; set; } = new();

    /// <summary>How many levels currently include this service.</summary>
    public int LevelCount { get; set; }

    /// <summary>Flight / Accommodation / Transport: a built-in service with a
    /// hand-written form writing to its own table. Its code, form and existence
    /// are fixed — the UI hides the form builder and the delete action for these.
    /// See <see cref="Core.Constants.SystemServices"/>.</summary>
    public bool IsSystem { get; set; }
}

// ── Service level ────────────────────────────────────────────────────────────

public class CreateServiceLevelRequest
{
    public string Code { get; set; }
    public string Name { get; set; }
    public string NameAr { get; set; }
    public string Description { get; set; }
    public string Color { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public List<string> RequiredGuestFields { get; set; } = new();

    /// <summary>
    /// Assigned services, in the order given. Index becomes SortOrder, which is
    /// the completion sequence a Fixed event enforces.
    /// </summary>
    public List<Guid> ServiceIds { get; set; } = new();
}

public class UpdateServiceLevelRequest : CreateServiceLevelRequest { }

public class ServiceLevelServiceResponse
{
    public Guid ServiceId { get; set; }
    public string Code { get; set; }
    public string Name { get; set; }
    public string NameAr { get; set; }
    public string Icon { get; set; }
    public int SortOrder { get; set; }
}

public class ServiceLevelResponse
{
    public Guid Id { get; set; }
    public string Code { get; set; }
    public string Name { get; set; }
    public string NameAr { get; set; }
    public string Description { get; set; }
    public string Color { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
    public List<string> RequiredGuestFields { get; set; } = new();
    public List<ServiceLevelServiceResponse> Services { get; set; } = new();

    /// <summary>Guests currently on this level, across all events.</summary>
    public int GuestCount { get; set; }
}

// ── Guest service entries ────────────────────────────────────────────────────

public class SaveGuestServiceEntryRequest
{
    /// <summary>Omit to create a new entry; supply to update an existing one.</summary>
    public Guid? Id { get; set; }
    public Guid ServiceId { get; set; }
    public Dictionary<string, string> Values { get; set; } = new();

    /// <summary>
    /// False saves progress without completing, which leaves the next service in
    /// a Fixed sequence still locked.
    /// </summary>
    public bool MarkCompleted { get; set; } = true;
}

public class GuestServiceEntryResponse
{
    public Guid Id { get; set; }
    public Guid ServiceId { get; set; }
    public string ServiceName { get; set; }
    public string Status { get; set; }
    public Dictionary<string, string> Values { get; set; } = new();
    public DateTime? CompletedAt { get; set; }
}

/// <summary>
/// One row of the guest's service checklist: the service, its form, what has
/// been filled in, and whether the user may open it yet.
/// </summary>
public class GuestServiceSlotResponse
{
    public Guid ServiceId { get; set; }
    public string Code { get; set; }
    public string Name { get; set; }
    public string NameAr { get; set; }
    public string Icon { get; set; }
    public int SortOrder { get; set; }

    public ServiceFormDefinition Form { get; set; } = new();
    public List<GuestServiceEntryResponse> Entries { get; set; } = new();

    /// <summary>
    /// True for flight / accommodation / transport. The client must edit these
    /// through the travel endpoints (POST /v1/travel/guest/{id}) and render the
    /// static form — <c>Form</c> is empty and <c>Entries</c> are the guest's real
    /// booking rows, whose <c>Id</c> is the booking's public id.
    /// </summary>
    public bool IsSystem { get; set; }

    /// <summary>pending until at least one entry is completed.</summary>
    public string Status { get; set; }

    /// <summary>
    /// False on a Fixed event while an earlier service is still pending. Always
    /// true on a Flexible event.
    /// </summary>
    public bool IsUnlocked { get; set; }

    /// <summary>Mandatory on Fixed events, optional on Flexible ones.</summary>
    public bool IsRequired { get; set; }

    /// <summary>Why it is locked, ready to show in a tooltip.</summary>
    public string LockedReason { get; set; }
}

public class GuestServicePlanResponse
{
    /// <summary>EventGuest.PublicId — a service plan is per participation.</summary>
    public Guid EventGuestId { get; set; }
    public Guid? ServiceLevelId { get; set; }
    public string ServiceLevelName { get; set; }
    public string ServiceLevelColor { get; set; }

    /// <summary>fixed | flexible — the event's guest model.</summary>
    public string GuestModel { get; set; }

    /// <summary>True once every required service has a completed entry.</summary>
    public bool IsComplete { get; set; }

    public List<GuestServiceSlotResponse> Slots { get; set; } = new();
}


// ── Service entries across an event (Travel & Logistics style listings) ─────

/// <summary>
/// One guest's completed entry for a service, flattened for a table. Values are
/// the raw {fieldKey: value} map — the client already has the form schema, so
/// it knows how to label and format each column.
/// </summary>
public class ServiceEntryRow
{
    public Guid EntryId { get; set; }
    /// <summary>EventGuest.PublicId this entry belongs to.</summary>
    public Guid EventGuestId { get; set; }
    public string GuestName { get; set; }
    public string PhotoUrl { get; set; }
    public string Email { get; set; }
    public string Organization { get; set; }
    public string ServiceLevelName { get; set; }
    public string ServiceLevelColor { get; set; }
    public string Status { get; set; }
    public DateTime? CompletedAt { get; set; }
    public Dictionary<string, string> Values { get; set; } = new();
}
