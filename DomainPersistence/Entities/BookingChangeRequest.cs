using System;

namespace DomainPersistence.Entities;

/// <summary>
/// A delegate asking their coordinator to change a flight or a stay — "date
/// change", "extra night", "change hotel" and so on, from the delegate app.
/// It never edits the booking itself: the coordinator actions it in the portal
/// (rebooking through the normal travel form) and closes the request.
/// </summary>
public class BookingChangeRequest : Entity
{
    public int EventGuestId { get; set; }

    /// <summary>Core.Constants.BookingChangeKinds — flight / stay.</summary>
    public string BookingKind { get; set; }

    /// <summary>The booking it is about — exactly one, matching BookingKind.</summary>
    public int? FlightId { get; set; }
    public int? AccommodationId { get; set; }

    /// <summary>Core.Constants.BookingChangeTypes for the kind.</summary>
    public string ChangeType { get; set; }
    public string Notes { get; set; }

    /// <summary>Route change only.</summary>
    public int? FromAirportId { get; set; }
    public int? ToAirportId { get; set; }

    /// <summary>Change hotel only — must be contracted for the mission.</summary>
    public int? HotelId { get; set; }

    /// <summary>Core.Constants.BookingChangeStatuses — requested / done / declined.</summary>
    public string Status { get; set; }
    public string ResolutionNote { get; set; }
    public int? ResolvedBy { get; set; }
    public DateTime? ResolvedAt { get; set; }

    public virtual EventGuest EventGuest { get; set; }
    public virtual Flight Flight { get; set; }
    public virtual Accommodation Accommodation { get; set; }
    public virtual AirportData FromAirport { get; set; }
    public virtual AirportData ToAirport { get; set; }
    public virtual AccommodationHotel Hotel { get; set; }
    public virtual User ResolvedByUser { get; set; }
}
