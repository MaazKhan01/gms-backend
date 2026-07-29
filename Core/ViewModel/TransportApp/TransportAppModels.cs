using System;

namespace Core.ViewModel.TransportApp;

/// <summary>Today's workload for the signed-in driver.</summary>
public class DriverStatsResponse
{
    // Jobs whose pickup falls on today (driver's assigned jobs only).
    public int Today { get; set; }
    // Lifetime, not today: everything the driver has ever completed.
    public int Completed { get; set; }
    // Lifetime, not today: everything still to do — assigned or arrived at pickup.
    public int Pending { get; set; }
    public int InProgress { get; set; }
}

/// <summary>One transfer as the driver app shows it.</summary>
public class DriverJobResponse
{
    public Guid Id { get; set; }
    public string Status { get; set; }
    public Guid? EventId { get; set; }
    public string EventName { get; set; }
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
    /// <summary>True on at most one job in the list — the next assigned job the
    /// driver may start. False on everything else.</summary>
    public bool ShowStartButton { get; set; }
}

/// <summary>An event this driver has transfers on — feeds the eventId filter.</summary>
public class DriverEventResponse
{
    public Guid Id { get; set; }
    public string Title { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public int JobCount { get; set; }
}

/// <summary>The signed-in driver's own profile — User row plus DriverProfile row.</summary>
public class DriverProfileResponse
{
    // From Users
    public Guid Id { get; set; }
    public string UserName { get; set; }
    public string Email { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string FullName { get; set; }
    public string Phone { get; set; }
    public bool IsActive { get; set; }
    public string Role { get; set; }

    // From DriverProfiles
    public Guid? DriverProfileId { get; set; }
    /// <summary>Enum name ("Fixed"/"Open"), not its int value.</summary>
    public string DriverType { get; set; }
    public string LicenseNumber { get; set; }
    public DateOnly? LicenseExpiry { get; set; }
    public Guid? NationalityId { get; set; }
    public string Nationality { get; set; }
    public string PhotoUrl { get; set; }
}

/// <summary>Self-service edit from the driver app. Null field = leave unchanged.</summary>
public class UpdateDriverProfileRequest
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Phone { get; set; }
    /// <summary>Blob URL from POST /api/v1/upload.</summary>
    public string PhotoUrl { get; set; }
}

/// <summary>Driver moving a job along: arrived → in-progress → completed.</summary>
public class UpdateJobStatusRequest
{
    public string Status { get; set; }
}
