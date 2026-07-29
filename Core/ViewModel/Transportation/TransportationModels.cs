using System;
using System.Collections.Generic;

namespace Core.ViewModel.Transportation;

// ── Admin — pre-scheduled transportation (Day 1) ─────────────────────────────
public class CreateScheduleRequest
{
    public Guid GuestId { get; set; }
    public Guid? DriverId { get; set; }
    public Guid? VehicleId { get; set; }
    public Guid? PickupLocationId { get; set; }
    public Guid? DropoffLocationId { get; set; }
    // Date + time of pickup. Conflict checks only ever look at Date+Hour+Minute.
    public DateTime ScheduledTime { get; set; }
    public string Notes { get; set; }
}

public class AssignDriversRequest
{
    public List<Guid> DriverIds { get; set; } = new();
}

public class SetAvailabilityRequest
{
    public bool IsAvailable { get; set; }
}

public class AssignedDriverDto
{
    public Guid DriverId { get; set; }
    public string DriverName { get; set; }
}

public class ScheduleRow
{
    public Guid Id { get; set; }
    public Guid GuestId { get; set; }
    public string GuestName { get; set; }
    public Guid? DriverId { get; set; }
    public string DriverName { get; set; }
    public Guid? VehicleId { get; set; }
    public string Vehicle { get; set; }
    public string Pickup { get; set; }
    public string Dropoff { get; set; }
    public DateTime? ScheduledTime { get; set; }
    public DateTime? ActualPickupTime { get; set; }
    public DateTime? ActualDropOffTime { get; set; }
    public string Status { get; set; }
    public string RideSource { get; set; }
    public string Notes { get; set; }
    public decimal? TotalFare { get; set; }
    public string Currency { get; set; }
}
