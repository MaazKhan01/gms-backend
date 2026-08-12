using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Core.Interfaces.Repositories;
using Core.Interfaces.Services;
using Google.Cloud.Firestore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

// Firestore projection of a ride. One document per Transports row, keyed by its
// PublicId, in the "rideRequests" collection:
//
//   guest requests   -> doc created with status "new", driverId null
//   driver accepts   -> same doc, status "assigned" + driver{...} filled in
//   driver advances  -> same doc, status walks to "completed"
//
// so the driver app can query the open pool (status == "new") and the VIP app
// can snapshot-listen on its own ride, neither of them polling.
//
// Credentials reuse the existing "Firebase" config section (see
// FirebaseNotificationProvider) — a service-account key by path or inline JSON.
// Nothing configured => this quietly no-ops, same as push, so a dev machine
// without a key still runs.
public class RideMirror(
    IUnitOfWork _unitOfWork,
    IConfiguration _configuration,
    ILogger<RideMirror> _logger) : IRideMirror
{
    private const string Collection = "rideRequests";

    private static readonly object InitLock = new();
    private static FirestoreDb _db;
    private static bool _initAttempted;

    public async Task SyncAsync(Guid transportPublicId, CancellationToken ct = default)
    {
        try
        {
            var db = GetOrInitializeDb();
            if (db is null) return;

            var t = await _unitOfWork.Transports.QueryNoTracking()
                .Where(x => x.PublicId == transportPublicId)
                .Select(x => new
                {
                    x.Id,
                    x.PublicId,
                    x.TripStatus,
                    x.RideSource,
                    x.Notes,
                    x.PickupTime,
                    x.DropoffTime,
                    x.ActualPickupTime,
                    x.ActualDropOffTime,

                    EventId = x.Guest.Event == null ? (Guid?)null : x.Guest.Event.PublicId,
                    EventName = x.Guest.Event == null ? null : x.Guest.Event.Title,

                    GuestId = x.Guest.PublicId,
                    GuestName = (x.Guest.FirstName + " " + x.Guest.LastName).Trim(),
                    GuestTier = x.Guest.Tier,
                    GuestPhone = x.Guest.User == null ? null : x.Guest.User.Phone,
                    GuestPhotoUrl = x.Guest.PhotoUrl,

                    Pickup = x.PickupLocation == null ? null : x.PickupLocation.Address,
                    PickupLat = x.PickupLocation == null ? null : x.PickupLocation.Latitude,
                    PickupLng = x.PickupLocation == null ? null : x.PickupLocation.Longitude,
                    Dropoff = x.DropoffLocation == null ? null : x.DropoffLocation.Address,
                    DropoffLat = x.DropoffLocation == null ? null : x.DropoffLocation.Latitude,
                    DropoffLng = x.DropoffLocation == null ? null : x.DropoffLocation.Longitude,

                    VehicleNumber = x.Vehicle == null ? null : x.Vehicle.VehicleNumber,
                    VehicleModel = x.Vehicle == null ? null : x.Vehicle.VehicleModel,

                    DriverId = x.Driver == null ? (Guid?)null : x.Driver.PublicId,
                    DriverName = x.Driver == null || x.Driver.User == null
                        ? null
                        : (x.Driver.User.FirstName + " " + x.Driver.User.LastName).Trim(),
                    DriverPhone = x.Driver == null || x.Driver.User == null ? null : x.Driver.User.Phone,
                    DriverPhotoUrl = x.Driver == null ? null : x.Driver.PhotoUrl,
                })
                .FirstOrDefaultAsync(ct);

            if (t is null) return;

            var doc = new Dictionary<string, object>
            {
                ["id"] = t.PublicId.ToString(),
                ["jobNumber"] = "VIP-" + t.Id,
                ["status"] = t.TripStatus,
                ["rideSource"] = t.RideSource,
                ["notes"] = t.Notes,

                ["eventId"] = t.EventId?.ToString(),
                ["eventName"] = t.EventName,

                ["guest"] = new Dictionary<string, object>
                {
                    ["id"] = t.GuestId.ToString(),
                    ["name"] = t.GuestName,
                    ["tier"] = t.GuestTier,
                    ["phone"] = t.GuestPhone,
                    ["photoUrl"] = t.GuestPhotoUrl,
                },

                ["pickup"] = new Dictionary<string, object>
                {
                    ["address"] = t.Pickup,
                    ["lat"] = t.PickupLat,
                    ["lng"] = t.PickupLng,
                },
                ["dropoff"] = new Dictionary<string, object>
                {
                    ["address"] = t.Dropoff,
                    ["lat"] = t.DropoffLat,
                    ["lng"] = t.DropoffLng,
                },

                ["pickupTime"] = Utc(t.PickupTime),
                ["dropoffTime"] = Utc(t.DropoffTime),
                ["actualPickupTime"] = Utc(t.ActualPickupTime),
                ["actualDropOffTime"] = Utc(t.ActualDropOffTime),

                ["vehicle"] = new Dictionary<string, object>
                {
                    ["number"] = t.VehicleNumber,
                    ["model"] = t.VehicleModel,
                },

                // Flat too, not just inside driver{} — the driver app's "my jobs"
                // listener filters on a single field, and Firestore can't index
                // into a map for an equality query without the full path anyway.
                ["driverId"] = t.DriverId?.ToString(),
                ["driver"] = t.DriverId is null ? null : (object)new Dictionary<string, object>
                {
                    ["id"] = t.DriverId?.ToString(),
                    ["name"] = t.DriverName,
                    ["phone"] = t.DriverPhone,
                    ["photoUrl"] = t.DriverPhotoUrl,
                },

                ["updatedAt"] = DateTime.UtcNow,
            };

            // MergeAll, not a plain overwrite. Every field above is present on each
            // sync, so one that went null (driver unassigned) still clears — but
            // `currentLocation` is owned by the driver app, which writes it straight
            // into this document from the client SDK. A plain SetAsync would wipe
            // the driver's live position on the next status change.
            await db.Collection(Collection).Document(t.PublicId.ToString())
                .SetAsync(doc, SetOptions.MergeAll, ct);
        }
        catch (Exception ex)
        {
            // Never fail the caller's request over the mirror — SQL already holds
            // the truth, and the next write on this ride re-syncs the document.
            _logger.LogError(ex, "Failed to mirror ride {TransportId} to Firestore", transportPublicId);
        }
    }

    // Firestore rejects a DateTime that isn't UTC. Every timestamp on Transports
    // is written as UTC but comes back Unspecified from SQL Server, so stamp it.
    private static DateTime? Utc(DateTime? value)
        => value is null ? null : DateTime.SpecifyKind(value.Value, DateTimeKind.Utc);

    // Lazy, once per process — same shape (and same config keys) as
    // FirebaseNotificationProvider's app init.
    private FirestoreDb GetOrInitializeDb()
    {
        if (_db != null) return _db;
        lock (InitLock)
        {
            if (_db != null || _initAttempted) return _db;
            _initAttempted = true;

            try
            {
                var projectId = _configuration["Firebase:ProjectId"];
                if (string.IsNullOrWhiteSpace(projectId))
                {
                    _logger.LogWarning("Firebase:ProjectId is not set — ride mirroring to Firestore is disabled.");
                    return null;
                }

                // Resolved against the assembly's folder, not the working directory:
                // deployed as a service the CWD can be System32, where the relative
                // path from config never finds the key that publish did copy.
                var keyPath = _configuration["Firebase:ServiceAccountKeyPath"];
                if (!string.IsNullOrWhiteSpace(keyPath) && !Path.IsPathRooted(keyPath))
                    keyPath = Path.Combine(AppContext.BaseDirectory, keyPath);

                var inlineJson = _configuration["Firebase:ServiceAccountJson"];

                // Unset => "(default)", which is what the console creates unless you
                // name the database. A named one (staging, say) goes in config.
                var builder = new FirestoreDbBuilder { ProjectId = projectId };
                var databaseId = _configuration["Firebase:FirestoreDatabaseId"];
                if (!string.IsNullOrWhiteSpace(databaseId)) builder.DatabaseId = databaseId;

                if (!string.IsNullOrWhiteSpace(keyPath) && File.Exists(keyPath))
                    builder.CredentialsPath = keyPath;
                else if (!string.IsNullOrWhiteSpace(inlineJson))
                    builder.JsonCredentials = inlineJson;
                else
                {
                    _logger.LogWarning(
                        "Firebase credentials are not configured (Firebase:ServiceAccountKeyPath / Firebase:ServiceAccountJson) — ride mirroring to Firestore is disabled.");
                    return null;
                }

                _db = builder.Build();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Firestore — ride mirroring is disabled.");
            }

            return _db;
        }
    }
}
