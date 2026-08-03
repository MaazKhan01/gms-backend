using System;
using System.Collections.Generic;
using Core.Constants;

// Namespace is ServiceCatalog, not ServiceLevel: a namespace sharing a name with
// the ServiceLevel entity makes every `ServiceLevel` reference ambiguous wherever
// both are in scope.
namespace Core.ViewModel.ServiceCatalog;

// ── Service catalog (per event) ──────────────────────────────────────────────

public class ServiceResponse
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public string Name { get; set; }
    public string NameAr { get; set; }
    public string Description { get; set; }
    public int SortOrder { get; set; }
    /// <summary>Parsed field definitions — the API never hands back the raw JSON
    /// string, so the frontend doesn't have to parse it defensively.</summary>
    public List<ServiceFieldDefinition> Fields { get; set; } = new();
    /// <summary>How many of this event's levels currently include this service —
    /// shown in the catalog list and used to warn before deleting.</summary>
    public int UsedByLevelCount { get; set; }
}

public class CreateServiceRequest
{
    public string Name { get; set; }
    public string NameAr { get; set; }
    public string Description { get; set; }
    public int SortOrder { get; set; }
    public List<ServiceFieldDefinition> Fields { get; set; } = new();
}

public class UpdateServiceRequest : CreateServiceRequest { }

// ── Service levels (per event) ───────────────────────────────────────────────

public class ServiceLevelResponse
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public string Name { get; set; }
    public string NameAr { get; set; }
    public string Code { get; set; }
    public string Description { get; set; }
    public string Color { get; set; }
    public int SortOrder { get; set; }

    // Rules
    public int? Capacity { get; set; }
    public List<string> RequiredGuestFields { get; set; } = new();

    /// <summary>Guests currently on this level (live count, drives the capacity
    /// rule and the "38 / 50" display).</summary>
    public int GuestCount { get; set; }

    /// <summary>The services this level includes, with their configured values.</summary>
    public List<ServiceLevelServiceResponse> Services { get; set; } = new();
}

public class ServiceLevelServiceResponse
{
    public Guid ServiceId { get; set; }
    public string ServiceName { get; set; }
    public string ServiceNameAr { get; set; }
    /// <summary>The service's own field definitions, echoed so the UI can render
    /// label + type alongside each value without a second lookup.</summary>
    public List<ServiceFieldDefinition> Fields { get; set; } = new();
    /// <summary>Configured values, keyed by field key.</summary>
    public Dictionary<string, string> Values { get; set; } = new();
}

public class CreateServiceLevelRequest
{
    public string Name { get; set; }
    public string NameAr { get; set; }
    public string Code { get; set; }
    public string Description { get; set; }
    public string Color { get; set; }
    public int SortOrder { get; set; }
    public int? Capacity { get; set; }
    public List<string> RequiredGuestFields { get; set; } = new();
    /// <summary>Full replacement set of included services on update — omit to
    /// leave the existing set untouched.</summary>
    public List<ServiceLevelServiceInput> Services { get; set; }
}

public class UpdateServiceLevelRequest : CreateServiceLevelRequest { }

public class ServiceLevelServiceInput
{
    public Guid ServiceId { get; set; }
    public Dictionary<string, string> Values { get; set; } = new();
}

/// <summary>Returned by the guest-assignment rule check so the UI can show what
/// would be violated and offer an override to whoever is allowed to use it.</summary>
public class ServiceLevelRuleCheckResponse
{
    public bool Passes { get; set; }
    /// <summary>Human-readable violations, e.g. "Gold is at capacity (50 / 50)."</summary>
    public List<string> Violations { get; set; } = new();
    /// <summary>Guest field keys that are required but missing, so the form can
    /// highlight them inline rather than only showing a message.</summary>
    public List<string> MissingFields { get; set; } = new();
}
