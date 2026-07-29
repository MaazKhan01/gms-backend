using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Core.ViewModel.Auth;

public class TokenResponse
{
    public string AccessToken { get; set; }
    public string RefreshToken { get; set; }
    public UserInfo User { get; set; }
}

public class UserInfo
{
    public Guid Id { get; set; }
    public string UserName { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string FullName => $"{FirstName} {LastName}";
    public string Email { get; set; }
    public string Role { get; set; }
    public Guid? RoleId { get; set; }
    public string RoleCode { get; set; }
    // Drivers get driverDetails instead of permissions — the driver app has no
    // permission-gated nav, so both are omitted when they don't apply.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string> Permissions { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DriverDetails DriverDetails { get; set; }
}

public class DriverDetails
{
    public Guid Id { get; set; }
    /// <summary>Name of the DriverType enum ("Fixed"/"Open"), not its int value.</summary>
    public string DriverType { get; set; }
    public string LicenseNumber { get; set; }
    public DateOnly? LicenseExpiry { get; set; }
    public Guid? NationalityId { get; set; }
    public string Nationality { get; set; }
    public string PhotoUrl { get; set; }
}
