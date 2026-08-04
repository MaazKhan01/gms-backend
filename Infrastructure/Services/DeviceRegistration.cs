using System;
using System.Threading;
using System.Threading.Tasks;
using Core.Interfaces.Repositories;
using DomainPersistence.Entities;

namespace Infrastructure.Services;

// Single Device upsert used by every path that can register a push token:
// portal/driver login, VIP app verify-otp, and POST /notifications/devices.
// Static + IUnitOfWork rather than a service, because two of those three
// callers run before authentication completes, so ICurrentUser isn't available
// to resolve the owner — the userId has to be passed in either way.
public static class DeviceRegistration
{
    // One row per token: an existing token is re-pointed at whichever User most
    // recently signed in with it (a device only belongs to one signed-in User at
    // a time). Null detail fields are left alone, so a lean login payload doesn't
    // wipe what a fuller POST /notifications/devices call stored earlier.
    public static async Task<Device> UpsertAsync(
        IUnitOfWork unitOfWork,
        int userId,
        string token,
        string platform = null,
        string deviceIdentifier = null,
        string deviceModel = null,
        string osVersion = null,
        string appVersion = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;

        var now = DateTime.UtcNow;
        var device = await unitOfWork.Devices.FindFirstOrDefaultAsync(d => d.Token == token, ct);
        if (device is null)
        {
            device = new Device { Token = token, NotificationsEnabled = true };
            await unitOfWork.Devices.AddAsync(device, ct);
        }
        else
        {
            unitOfWork.Devices.Update(device);
        }

        device.UserId = userId;
        device.IsActive = true;
        device.LastActiveAt = now;
        device.TokenUpdatedAt = now;
        if (platform != null) device.Platform = platform;
        if (deviceIdentifier != null) device.DeviceIdentifier = deviceIdentifier;
        if (deviceModel != null) device.DeviceModel = deviceModel;
        if (osVersion != null) device.OsVersion = osVersion;
        if (appVersion != null) device.AppVersion = appVersion;

        await unitOfWork.SaveChangesAsync(ct);
        return device;
    }
}
