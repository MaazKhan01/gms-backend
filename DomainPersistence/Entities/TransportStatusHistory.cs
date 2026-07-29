namespace DomainPersistence.Entities;

/// <summary>One row per Transport.TripStatus transition — CreatedAt (audit
/// field) is the transition timestamp. Satisfies "store timestamps for every
/// state transition" without changing the existing TripStatus contract.</summary>
public class TransportStatusHistory : Entity
{
    public int TransportId { get; set; }
    public string Status { get; set; }
    // Null for system/guest-triggered transitions (e.g. an on-demand accept).
    public int? ChangedByUserId { get; set; }

    public virtual Transport Transport { get; set; }
}
