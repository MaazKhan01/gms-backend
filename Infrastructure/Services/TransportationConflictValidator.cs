using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Core.Constants;
using Core.Interfaces.Repositories;
using Core.Interfaces.Services;
using DomainPersistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

// Today's conflict rule: exact Date+Hour+Minute match, no buffer either side.
public class ZeroBufferConflictWindowPolicy : IConflictWindowPolicy
{
    public TimeSpan BufferBefore => TimeSpan.Zero;
    public TimeSpan BufferAfter => TimeSpan.Zero;
    public TimeSpan DefaultRideDuration => TimeSpan.FromMinutes(60);
}

public class TransportationConflictValidator(IUnitOfWork _unitOfWork, IConflictWindowPolicy _windowPolicy)
    : ITransportationConflictValidator
{
    public async Task<ConflictCheckResult> CheckGuestConflictAsync(
        int guestId, DateTime candidateTime, int? excludeTransportId = null, CancellationToken ct = default)
    {
        var dayStart = candidateTime.Date;
        var dayEnd = dayStart.AddDays(1);

        // Open (Live) covers both admin-scheduled rides and guest-requested ones
        // still waiting for a driver — a guest can't be in two cars at one time
        // regardless of which flow booked them.
        var bookedTimes = await _unitOfWork.Transports.Query()
            .Where(t => t.GuestId == guestId
                && TransportStatuses.Live.Contains(t.TripStatus)
                && t.PickupTime >= dayStart && t.PickupTime < dayEnd
                && (excludeTransportId == null || t.Id != excludeTransportId))
            .Select(t => t.PickupTime.Value)
            .ToListAsync(ct);

        return CheckOverlap(candidateTime, bookedTimes, guest: true);
    }

    public async Task<ConflictCheckResult> CheckDriverConflictAsync(
        int driverId, DateTime candidateTime, int? excludeTransportId = null, CancellationToken ct = default)
    {
        var dayStart = candidateTime.Date;
        var dayEnd = dayStart.AddDays(1);

        var scheduledTimes = await _unitOfWork.Transports.Query()
            .Where(t => t.DriverId == driverId
                && TransportStatuses.Live.Contains(t.TripStatus)
                && t.PickupTime >= dayStart && t.PickupTime < dayEnd
                && (excludeTransportId == null || t.Id != excludeTransportId))
            .Select(t => t.PickupTime.Value)
            .ToListAsync(ct);

        return CheckOverlap(candidateTime, scheduledTimes, guest: false);
    }

    public async Task<ConflictCheckResult> CheckVehicleConflictAsync(
        int vehicleId, DateTime start, DateTime? end, int? excludeTransportId = null, CancellationToken ct = default)
    {
        var clash = await BookedVehicles(start, end, excludeTransportId)
            .Where(t => t.VehicleId == vehicleId)
            .OrderBy(t => t.PickupTime)
            .Select(t => new { From = t.PickupTime.Value, t.DropoffTime, t.Vehicle.VehicleNumber })
            .FirstOrDefaultAsync(ct);

        if (clash == null) return ConflictCheckResult.Ok();

        var until = clash.DropoffTime ?? clash.From + _windowPolicy.DefaultRideDuration;
        return ConflictCheckResult.Conflict(
            $"Vehicle {clash.VehicleNumber} is already booked from {clash.From:h:mm tt} to {until:h:mm tt} " +
            $"on {clash.From:dd-MMM-yyyy}. Choose another vehicle or a different time.");
    }

    public async Task<List<int>> GetBusyVehicleIdsAsync(
        DateTime start, DateTime? end, int? excludeTransportId = null, CancellationToken ct = default)
        => await BookedVehicles(start, end, excludeTransportId)
            .Select(t => t.VehicleId.Value)
            .Distinct()
            .ToListAsync(ct);

    public async Task<List<int>> GetBusyDriverIdsAsync(
        DateTime start, DateTime? end, int? excludeTransportId = null, CancellationToken ct = default)
        => await Overlapping(start, end, excludeTransportId)
            .Where(t => t.DriverId != null)
            .Select(t => t.DriverId.Value)
            .Distinct()
            .ToListAsync(ct);

    private IQueryable<Transport> BookedVehicles(DateTime start, DateTime? end, int? excludeTransportId)
        => Overlapping(start, end, excludeTransportId).Where(t => t.VehicleId != null);

    // Every open ride whose window overlaps [start, end) — the one predicate the
    // single-vehicle check and both availability feeds run on, so they can never
    // disagree about what "busy" means.
    private IQueryable<Transport> Overlapping(DateTime start, DateTime? end, int? excludeTransportId)
    {
        var from = start - _windowPolicy.BufferBefore;
        var to = (end ?? start + _windowPolicy.DefaultRideDuration) + _windowPolicy.BufferAfter;
        // Local, not a property access — EF has to translate it to a constant.
        var fallbackMinutes = _windowPolicy.DefaultRideDuration.TotalMinutes;

        return _unitOfWork.Transports.Query()
            .Where(t => TransportStatuses.Live.Contains(t.TripStatus)
                && t.PickupTime != null
                && (excludeTransportId == null || t.Id != excludeTransportId)
                // Half-open overlap: back-to-back rides (10:00–10:30, 10:30–11:00)
                // don't collide — add BufferAfter if turnaround time is needed.
                && t.PickupTime < to
                && (t.DropoffTime ?? t.PickupTime.Value.AddMinutes(fallbackMinutes)) > from);
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
