using System;
using System.Collections.Generic;

namespace Core.ViewModel.Transportation;

// ── Admin — pre-scheduled transportation (Day 1) ─────────────────────────────
public class CreateScheduleRequest
{
    /// <summary>EventGuest.PublicId — transport belongs to one event participation,
    /// which is what makes "which event is this ride for?" answerable.</summary>
    public Guid EventGuestId { get; set; }
    public Guid? DriverId { get; set; }
    public Guid? VehicleId { get; set; }
    public Guid? PickupLocationId { get; set; }
    public Guid? DropoffLocationId { get; set; }
    // Date + time of pickup. Guest/driver conflict checks only ever look at
    // Date+Hour+Minute; the vehicle check uses the full ScheduledTime→DropoffTime
    // window.
    public DateTime ScheduledTime { get; set; }
    // Required: without it the vehicle's busy window is a guess, so two rides
    // could be booked into the same car (see IConflictWindowPolicy).
    public DateTime? DropoffTime { get; set; }
    public string Notes { get; set; }
}

public class AssignDriversRequest
{
    public List<Guid> DriverIds { get; set; } = new();
}

public class AssignedDriverDto
{
    public Guid DriverId { get; set; }
    public string DriverName { get; set; }
}

public class ScheduleRow
{
    public Guid Id { get; set; }
    /// <summary>EventGuest.PublicId of the ride's participation.</summary>
    public Guid EventGuestId { get; set; }
    /// <summary>Guest.PublicId — the person behind it.</summary>
    public Guid PersonId { get; set; }
    public string GuestName { get; set; }
    public Guid EventId { get; set; }
    public Guid? DriverId { get; set; }
    public string DriverName { get; set; }
    public Guid? VehicleId { get; set; }
    public string Vehicle { get; set; }
    public string Pickup { get; set; }
    public string Dropoff { get; set; }
    public DateTime? ScheduledTime { get; set; }
    public DateTime? DropoffTime { get; set; }
    public DateTime? ActualPickupTime { get; set; }
    public DateTime? ActualDropOffTime { get; set; }
    public string Status { get; set; }
    public string RideSource { get; set; }
    public string Notes { get; set; }
    public decimal? TotalFare { get; set; }
    public string Currency { get; set; }
}
