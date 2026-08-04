namespace Core.ViewModel.Auth;

public class LoginModel
{
    public string? Email { get; set; }
    public string Password { get; set; }

    // Optional device registration, so a mobile client doesn't need a second
    // round-trip to POST /notifications/devices right after signing in. All
    // null (web/portal) => login stays read-only against Devices.
    // Same upsert semantics as NotificationService.RegisterDeviceForUserAsync:
    // keyed on DeviceToken, re-pointed at whichever User last logged in with it.
    public string? DeviceToken { get; set; }
    public string? Platform { get; set; } // ios | android | web
    public string? DeviceIdentifier { get; set; }
    public string? DeviceModel { get; set; }
}
