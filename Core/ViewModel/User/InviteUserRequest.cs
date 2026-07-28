using System;
using DomainPersistence.Enums;

namespace Core.ViewModel.User;

public class InviteUserRequest
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Email { get; set; }
    public string Phone { get; set; }
    public Guid RoleId { get; set; }

    // Present when RoleId resolves to the driver role.
    public DriverProfileInput DriverProfile { get; set; }
}

public class DriverProfileInput
{
    // DriverType enum value: 1 = fixed, 2 = open (GET /v1/lookups/enums/driver-types).
    public DriverType? DriverType { get; set; }
    public string LicenseNumber { get; set; }
    public DateOnly? LicenseExpiry { get; set; }
    public Guid? NationalityId { get; set; }
    public string PhotoUrl { get; set; }
}

public class PendingUserResponse
{
    public Guid Id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Email { get; set; }
    public string RoleName { get; set; }
    public DateTime? InviteSentAt { get; set; }
}

public class AcceptInviteRequest
{
    public string Password { get; set; }
    public string ConfirmPassword { get; set; }
}

public class InviteDetailsResponse
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Email { get; set; }
    public string RoleName { get; set; }
}

public class AdminSetPasswordRequest
{
    public string NewPassword { get; set; }
    public string ConfirmPassword { get; set; }
}
