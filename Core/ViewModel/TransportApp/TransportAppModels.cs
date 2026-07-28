using System;

namespace Core.ViewModel.TransportApp;

/// <summary>Today's workload for the signed-in driver.</summary>
public class DriverStatsResponse
{
    // Jobs whose pickup falls on today (driver's assigned jobs only).
    public int Today { get; set; }
    public int Completed { get; set; }
    // Not started yet — assigned or arrived at pickup.
    public int Pending { get; set; }
    public int InProgress { get; set; }
}

/// <summary>One transfer as the driver app shows it.</summary>
public class DriverJobResponse
{
    public Guid Id { get; set; }
    public string Status { get; set; }
    public string GuestName { get; set; }
    public string GuestTier { get; set; }
    public string Pickup { get; set; }
    public string Dropoff { get; set; }
    public DateTime? PickupTime { get; set; }
    public DateTime? DropoffTime { get; set; }
    public DateTime? ActualPickupTime { get; set; }
    public DateTime? ActualDropOffTime { get; set; }
    public string VehicleNumber { get; set; }
    public string VehicleModel { get; set; }
}

/// <summary>Driver moving a job along: arrived → in-progress → completed.</summary>
public class UpdateJobStatusRequest
{
    public string Status { get; set; }
}
