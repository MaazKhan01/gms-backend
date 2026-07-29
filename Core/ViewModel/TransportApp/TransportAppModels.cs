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
    public string JobNumber { get; set; }
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

/// <summary>One job, everything the detail screen needs — including map coordinates.</summary>
public class DriverJobDetailResponse
{
    public Guid Id { get; set; }
    public string JobNumber { get; set; }
    public string Status { get; set; }
    /// <summary>The only status this job may be moved to, or null when it's finished.</summary>
    public string NextStatus { get; set; }

    public Guid? EventId { get; set; }
    public string EventName { get; set; }

    public string GuestName { get; set; }
    public string GuestTier { get; set; }
    public string GuestType { get; set; }
    public string GuestOrganization { get; set; }
    public string GuestEmail { get; set; }
    public string GuestPhotoUrl { get; set; }

    public JobLocationResponse Pickup { get; set; }
    public JobLocationResponse Dropoff { get; set; }

    public DateTime? PickupTime { get; set; }
    public DateTime? DropoffTime { get; set; }
    public DateTime? ActualPickupTime { get; set; }
    public DateTime? ActualDropOffTime { get; set; }

    public string VehicleNumber { get; set; }
    public string VehicleModel { get; set; }
    public string VehicleType { get; set; }
    public string VehicleImage { get; set; }
    public int? VehicleCapacity { get; set; }
}

/// <summary>A pickup or drop-off point. Latitude/Longitude are strings because
/// that's how Locations stores them; null when the point isn't geocoded.</summary>
public class JobLocationResponse
{
    public Guid? Id { get; set; }
    public string Address { get; set; }
    public string Type { get; set; }
    public string Latitude { get; set; }
    public string Longitude { get; set; }
}

/// <summary>Completed-job performance for the driver: the three tiles plus the
/// per-job cards behind them.</summary>
public class DriverSummaryResponse
{
    public int CompletedJobs { get; set; }
    public int OnTimeJobs { get; set; }
    public int DelayJobs { get; set; }
    public List<DriverJobPerformanceResponse> Jobs { get; set; } = new();
}

/// <summary>One completed job, planned vs actual.</summary>
public class DriverJobPerformanceResponse
{
    public Guid Id { get; set; }
    /// <summary>Display number on the card, e.g. "VIP-1054".</summary>
    public string JobNumber { get; set; }
    /// <summary>"on-time" or "delayed" — drives the status pill.</summary>
    public string Status { get; set; }
    public Guid? EventId { get; set; }
    public string EventName { get; set; }
    public string GuestName { get; set; }
    public string Pickup { get; set; }
    public string Dropoff { get; set; }

    public DateTime? PickupTime { get; set; }
    public DateTime? ActualPickupTime { get; set; }
    /// <summary>Actual minus planned, in minutes. Negative = early. Null = not measurable.</summary>
    public int? PickupDeltaMinutes { get; set; }

    public DateTime? DropoffTime { get; set; }
    public DateTime? ActualDropOffTime { get; set; }
    public int? DropoffDeltaMinutes { get; set; }

    public int? EstimatedDurationMinutes { get; set; }
    public int? ActualDurationMinutes { get; set; }
    public int? DurationDeltaMinutes { get; set; }
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
    /// <summary>Dial code, e.g. "+971". Stored joined with PhoneNumber in one column.</summary>
    public string CountryCode { get; set; }
    /// <summary>National number without the dial code, e.g. "501234567".</summary>
    public string PhoneNumber { get; set; }
    public bool IsActive { get; set; }
    public string Role { get; set; }

    // From DriverProfiles
    public Guid? DriverProfileId { get; set; }
    /// <summary>Enum name ("Fixed"/"Open"), not its int value.</summary>
    public string DriverType { get; set; }
    /// <summary>Only Open drivers can change this — see POST profile/toggle-online.</summary>
    public bool IsOnline { get; set; }
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
    /// <summary>Dial code, e.g. "+971". Sent alongside PhoneNumber; saved as one value.</summary>
    public string CountryCode { get; set; }
    public string PhoneNumber { get; set; }
    /// <summary>Blob URL from POST /api/v1/upload.</summary>
    public string PhotoUrl { get; set; }
}

// Status moves are four dedicated POSTs now (start-job / arrived / start-trip /
// complete) — no request body, so no DTO.
