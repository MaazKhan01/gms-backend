using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Core.Constants;
using Core.Interfaces.Repositories;
using Core.Interfaces.Services;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

// Today's conflict rule: exact Date+Hour+Minute match, no buffer either side.
public class ZeroBufferConflictWindowPolicy : IConflictWindowPolicy
{
    public TimeSpan BufferBefore => TimeSpan.Zero;
    public TimeSpan BufferAfter => TimeSpan.Zero;
}

public class TransportationConflictValidator(IUnitOfWork _unitOfWork, IConflictWindowPolicy _windowPolicy)
    : ITransportationConflictValidator
{
    public async Task<ConflictCheckResult> CheckGuestConflictAsync(
        int guestId, DateTime candidateTime, int? excludeTransportId = null, CancellationToken ct = default)
    {
        var dayStart = candidateTime.Date;
        var dayEnd = dayStart.AddDays(1);

        var scheduledTimes = await _unitOfWork.Transports.Query()
            .Where(t => t.GuestId == guestId
                && TransportStatuses.Active.Contains(t.TripStatus)
                && t.PickupTime >= dayStart && t.PickupTime < dayEnd
                && (excludeTransportId == null || t.Id != excludeTransportId))
            .Select(t => t.PickupTime.Value)
            .ToListAsync(ct);

        var requestedTimes = await _unitOfWork.RideRequests.Query()
            .Where(r => r.GuestId == guestId
                && RideRequestStatuses.Active.Contains(r.Status)
                && r.RequestedTime >= dayStart && r.RequestedTime < dayEnd)
            .Select(r => r.RequestedTime.Value)
            .ToListAsync(ct);

        return CheckOverlap(candidateTime, scheduledTimes.Concat(requestedTimes), guest: true);
    }

    public async Task<ConflictCheckResult> CheckDriverConflictAsync(
        int driverId, DateTime candidateTime, int? excludeTransportId = null, CancellationToken ct = default)
    {
        var dayStart = candidateTime.Date;
        var dayEnd = dayStart.AddDays(1);

        var scheduledTimes = await _unitOfWork.Transports.Query()
            .Where(t => t.DriverId == driverId
                && TransportStatuses.Active.Contains(t.TripStatus)
                && t.PickupTime >= dayStart && t.PickupTime < dayEnd
                && (excludeTransportId == null || t.Id != excludeTransportId))
            .Select(t => t.PickupTime.Value)
            .ToListAsync(ct);

        return CheckOverlap(candidateTime, scheduledTimes, guest: false);
    }

    private ConflictCheckResult CheckOverlap(DateTime candidateTime, IEnumerable<DateTime> existingTimes, bool guest)
    {
        var candidate = TruncateToMinute(candidateTime);

        var hasOverlap = existingTimes.Any(existing =>
        {
            var diff = candidate - TruncateToMinute(existing);
            return diff >= -_windowPolicy.BufferBefore && diff <= _windowPolicy.BufferAfter;
        });

        if (!hasOverlap) return ConflictCheckResult.Ok();

        return ConflictCheckResult.Conflict(guest
            ? $"This guest already has a transportation booking scheduled for {candidate:h:mm tt} on {candidate:dd-MMM-yyyy}. Please select a different time."
            : "Driver is already assigned to another transportation schedule during the selected time.");
    }

    private static DateTime TruncateToMinute(DateTime dt) => new(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, 0, dt.Kind);
}
