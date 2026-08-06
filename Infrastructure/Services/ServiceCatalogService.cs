using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Core.Constants;
using Core.Interfaces.Repositories;
using Core.Interfaces.Services;
using Core.ViewModel.Common;
using Core.ViewModel.ServiceCatalog;
using DomainPersistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

/// <summary>
/// Global service catalogue plus per-guest service plans.
/// Design and rationale: docs/service-levels-v2.md.
/// </summary>
public class ServiceCatalogService(
    IUnitOfWork _unitOfWork,
    ILogger<ServiceCatalogService> _logger) : IServiceCatalogService
{
    // ═══════════════════════════════════════════════════════════════════════
    //  Services
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<ApiResponse<List<ServiceResponse>>> GetServicesAsync(
        bool includeInactive, CancellationToken ct = default)
    {
        try
        {
            var query = _unitOfWork.Services.Query();
            if (!includeInactive) query = query.Where(s => s.IsActive);

            var services = await query
                .OrderBy(s => s.SortOrder).ThenBy(s => s.Name)
                .Select(s => new
                {
                    Entity = s,
                    LevelCount = s.ServiceLevels.Count(l => l.IsDeleted != true),
                })
                .ToListAsync(ct);

            var result = services.Select(x => ToServiceResponse(x.Entity, x.LevelCount)).ToList();
            return ApiResponse<List<ServiceResponse>>.SuccessResponse(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing services");
            return ApiResponse<List<ServiceResponse>>.ServerErrorResponse("Could not load services.");
        }
    }

    public async Task<ApiResponse<ServiceResponse>> GetServiceByIdAsync(Guid serviceId, CancellationToken ct = default)
    {
        var service = await _unitOfWork.Services.Query()
            .FirstOrDefaultAsync(s => s.PublicId == serviceId, ct);

        return service == null
            ? ApiResponse<ServiceResponse>.NotFoundResponse("Service not found.")
            : ApiResponse<ServiceResponse>.SuccessResponse(ToServiceResponse(service, 0));
    }

    public async Task<ApiResponse<ServiceResponse>> CreateServiceAsync(
        CreateServiceRequest request, int userId, CancellationToken ct = default)
    {
        try
        {
            var invalid = await ValidateServiceAsync(request, null, ct);
            if (invalid != null) return ApiResponse<ServiceResponse>.ErrorResponse(invalid);

            var service = new Service
            {
                Code = Slugify(request.Code, request.Name),
                Name = request.Name.Trim(),
                NameAr = request.NameAr?.Trim(),
                Description = request.Description?.Trim(),
                Icon = request.Icon?.Trim(),
                SortOrder = request.SortOrder,
                IsActive = request.IsActive,
                FormSchemaJson = ServiceFormSchema.Serialize(request.Form),
            };
            service.SetCreationAudit(userId);

            await _unitOfWork.Services.AddAsync(service, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            return ApiResponse<ServiceResponse>.SuccessResponse(ToServiceResponse(service, 0));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating service");
            return ApiResponse<ServiceResponse>.ServerErrorResponse("Could not create the service.");
        }
    }

    public async Task<ApiResponse<ServiceResponse>> UpdateServiceAsync(
        Guid serviceId, UpdateServiceRequest request, int userId, CancellationToken ct = default)
    {
        try
        {
            var service = await _unitOfWork.Services.Query()
                .FirstOrDefaultAsync(s => s.PublicId == serviceId, ct);
            if (service == null) return ApiResponse<ServiceResponse>.NotFoundResponse("Service not found.");

            var invalid = await ValidateServiceAsync(request, service.Id, ct);
            if (invalid != null) return ApiResponse<ServiceResponse>.ErrorResponse(invalid);

            service.Code = Slugify(request.Code, request.Name);
            service.Name = request.Name.Trim();
            service.NameAr = request.NameAr?.Trim();
            service.Description = request.Description?.Trim();
            service.Icon = request.Icon?.Trim();
            service.SortOrder = request.SortOrder;
            service.IsActive = request.IsActive;
            service.FormSchemaJson = ServiceFormSchema.Serialize(request.Form);
            service.SetUpdateAudit(userId);

            _unitOfWork.Services.Update(service);
            await _unitOfWork.SaveChangesAsync(ct);

            return ApiResponse<ServiceResponse>.SuccessResponse(ToServiceResponse(service, 0));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating service {ServiceId}", serviceId);
            return ApiResponse<ServiceResponse>.ServerErrorResponse("Could not update the service.");
        }
    }

    public async Task<ApiResponse<bool>> DeleteServiceAsync(Guid serviceId, int userId, CancellationToken ct = default)
    {
        try
        {
            var service = await _unitOfWork.Services.Query()
                .FirstOrDefaultAsync(s => s.PublicId == serviceId, ct);
            if (service == null) return ApiResponse<bool>.NotFoundResponse("Service not found.");

            // Guest data outlives the catalogue: deleting a service whose form
            // people have already filled in would orphan those answers, so it is
            // deactivated instead and stays readable.
            var entryCount = await _unitOfWork.GuestServiceEntries.Query()
                .CountAsync(e => e.ServiceId == service.Id, ct);
            if (entryCount > 0)
            {
                return ApiResponse<bool>.ConflictResponse(
                    $"{entryCount} guest record(s) already use \"{service.Name}\". Deactivate it instead — "
                    + "it will stop being assignable but existing records stay intact.");
            }

            var assignments = await _unitOfWork.ServiceLevelServices.Query()
                .Where(a => a.ServiceId == service.Id).ToListAsync(ct);
            foreach (var a in assignments) a.MarkAsDeleted(userId);

            service.MarkAsDeleted(userId);
            _unitOfWork.Services.Update(service);
            await _unitOfWork.SaveChangesAsync(ct);

            return ApiResponse<bool>.SuccessResponse(true, "Service deleted.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting service {ServiceId}", serviceId);
            return ApiResponse<bool>.ServerErrorResponse("Could not delete the service.");
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Service levels
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<ApiResponse<List<ServiceLevelResponse>>> GetServiceLevelsAsync(
        bool includeInactive, CancellationToken ct = default)
    {
        try
        {
            var query = _unitOfWork.ServiceLevels.Query();
            if (!includeInactive) query = query.Where(l => l.IsActive);

            var levels = await query
                .Include(l => l.Services).ThenInclude(a => a.Service)
                .OrderBy(l => l.SortOrder).ThenBy(l => l.Name)
                .ToListAsync(ct);

            var counts = await _unitOfWork.Guests.Query()
                .Where(g => g.ServiceLevelId != null)
                .GroupBy(g => g.ServiceLevelId!.Value)
                .Select(gr => new { LevelId = gr.Key, Count = gr.Count() })
                .ToDictionaryAsync(x => x.LevelId, x => x.Count, ct);

            var result = levels
                .Select(l => ToLevelResponse(l, counts.TryGetValue(l.Id, out var c) ? c : 0))
                .ToList();

            return ApiResponse<List<ServiceLevelResponse>>.SuccessResponse(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing service levels");
            return ApiResponse<List<ServiceLevelResponse>>.ServerErrorResponse("Could not load service levels.");
        }
    }

    public async Task<ApiResponse<ServiceLevelResponse>> GetServiceLevelByIdAsync(
        Guid levelId, CancellationToken ct = default)
    {
        var level = await _unitOfWork.ServiceLevels.Query()
            .Include(l => l.Services).ThenInclude(a => a.Service)
            .FirstOrDefaultAsync(l => l.PublicId == levelId, ct);
        if (level == null) return ApiResponse<ServiceLevelResponse>.NotFoundResponse("Service level not found.");

        var count = await _unitOfWork.Guests.Query().CountAsync(g => g.ServiceLevelId == level.Id, ct);
        return ApiResponse<ServiceLevelResponse>.SuccessResponse(ToLevelResponse(level, count));
    }

    public async Task<ApiResponse<ServiceLevelResponse>> CreateServiceLevelAsync(
        CreateServiceLevelRequest request, int userId, CancellationToken ct = default)
    {
        try
        {
            var invalid = await ValidateLevelAsync(request, null, ct);
            if (invalid != null) return ApiResponse<ServiceLevelResponse>.ErrorResponse(invalid);

            var (services, resolveError) = await ResolveServicesAsync(request.ServiceIds, ct);
            if (resolveError != null) return ApiResponse<ServiceLevelResponse>.ErrorResponse(resolveError);

            var level = new ServiceLevel
            {
                Code = Slugify(request.Code, request.Name),
                Name = request.Name.Trim(),
                NameAr = request.NameAr?.Trim(),
                Description = request.Description?.Trim(),
                Color = request.Color?.Trim(),
                SortOrder = request.SortOrder,
                IsActive = request.IsActive,
                RequiredGuestFieldsJson = ServiceLevelRules.SerializeRequiredFields(request.RequiredGuestFields),
            };
            level.SetCreationAudit(userId);

            await _unitOfWork.ServiceLevels.AddAsync(level, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            await WriteAssignmentsAsync(level, services, userId, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            return await GetServiceLevelByIdAsync(level.PublicId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating service level");
            return ApiResponse<ServiceLevelResponse>.ServerErrorResponse("Could not create the service level.");
        }
    }

    public async Task<ApiResponse<ServiceLevelResponse>> UpdateServiceLevelAsync(
        Guid levelId, UpdateServiceLevelRequest request, int userId, CancellationToken ct = default)
    {
        try
        {
            var level = await _unitOfWork.ServiceLevels.Query()
                .Include(l => l.Services)
                .FirstOrDefaultAsync(l => l.PublicId == levelId, ct);
            if (level == null) return ApiResponse<ServiceLevelResponse>.NotFoundResponse("Service level not found.");

            var invalid = await ValidateLevelAsync(request, level.Id, ct);
            if (invalid != null) return ApiResponse<ServiceLevelResponse>.ErrorResponse(invalid);

            // Resolved before anything is written, so a bad service id cannot
            // leave the level half-updated.
            var (services, resolveError) = await ResolveServicesAsync(request.ServiceIds, ct);
            if (resolveError != null) return ApiResponse<ServiceLevelResponse>.ErrorResponse(resolveError);

            level.Code = Slugify(request.Code, request.Name);
            level.Name = request.Name.Trim();
            level.NameAr = request.NameAr?.Trim();
            level.Description = request.Description?.Trim();
            level.Color = request.Color?.Trim();
            level.SortOrder = request.SortOrder;
            level.IsActive = request.IsActive;
            level.RequiredGuestFieldsJson = ServiceLevelRules.SerializeRequiredFields(request.RequiredGuestFields);
            level.SetUpdateAudit(userId);

            _unitOfWork.ServiceLevels.Update(level);
            await WriteAssignmentsAsync(level, services, userId, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            return await GetServiceLevelByIdAsync(level.PublicId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating service level {LevelId}", levelId);
            return ApiResponse<ServiceLevelResponse>.ServerErrorResponse("Could not update the service level.");
        }
    }

    public async Task<ApiResponse<bool>> DeleteServiceLevelAsync(Guid levelId, int userId, CancellationToken ct = default)
    {
        try
        {
            var level = await _unitOfWork.ServiceLevels.Query()
                .Include(l => l.Services)
                .FirstOrDefaultAsync(l => l.PublicId == levelId, ct);
            if (level == null) return ApiResponse<bool>.NotFoundResponse("Service level not found.");

            var guestCount = await _unitOfWork.Guests.Query().CountAsync(g => g.ServiceLevelId == level.Id, ct);
            if (guestCount > 0)
            {
                return ApiResponse<bool>.ConflictResponse(
                    $"{guestCount} guest(s) are on \"{level.Name}\". Move them to another level first, "
                    + "or deactivate this one.");
            }

            foreach (var a in level.Services) a.MarkAsDeleted(userId);
            level.MarkAsDeleted(userId);
            _unitOfWork.ServiceLevels.Update(level);
            await _unitOfWork.SaveChangesAsync(ct);

            return ApiResponse<bool>.SuccessResponse(true, "Service level deleted.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting service level {LevelId}", levelId);
            return ApiResponse<bool>.ServerErrorResponse("Could not delete the service level.");
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Guest service plan
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<ApiResponse<GuestServicePlanResponse>> GetGuestServicePlanAsync(
        Guid guestId, CancellationToken ct = default)
    {
        try
        {
            var guest = await LoadGuestAsync(guestId, ct);
            if (guest == null) return ApiResponse<GuestServicePlanResponse>.NotFoundResponse("Guest not found.");

            var plan = await BuildPlanAsync(guest, ct);
            return ApiResponse<GuestServicePlanResponse>.SuccessResponse(plan);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error building service plan for guest {GuestId}", guestId);
            return ApiResponse<GuestServicePlanResponse>.ServerErrorResponse("Could not load the guest's services.");
        }
    }

    public async Task<ApiResponse<GuestServiceEntryResponse>> SaveGuestServiceEntryAsync(
        Guid guestId, SaveGuestServiceEntryRequest request, int userId, CancellationToken ct = default)
    {
        try
        {
            var guest = await LoadGuestAsync(guestId, ct);
            if (guest == null) return ApiResponse<GuestServiceEntryResponse>.NotFoundResponse("Guest not found.");

            var plan = await BuildPlanAsync(guest, ct);
            var slot = plan.Slots.FirstOrDefault(s => s.ServiceId == request.ServiceId);
            if (slot == null)
            {
                return ApiResponse<GuestServiceEntryResponse>.ErrorResponse(
                    "That service is not part of this guest's service level.");
            }

            // Re-checked here rather than trusted from the client: the UI greys
            // locked services out, but the order is a rule, not a hint.
            var editingExisting = request.Id.HasValue && request.Id.Value != Guid.Empty;
            if (!slot.IsUnlocked && !editingExisting)
            {
                return ApiResponse<GuestServiceEntryResponse>.ConflictResponse(
                    slot.LockedReason ?? "An earlier service must be completed first.",
                    "SERVICE_SEQUENCE");
            }

            var service = await _unitOfWork.Services.Query()
                .FirstOrDefaultAsync(s => s.PublicId == request.ServiceId, ct);
            if (service == null) return ApiResponse<GuestServiceEntryResponse>.NotFoundResponse("Service not found.");

            var form = ServiceFormSchema.Parse(service.FormSchemaJson);
            var values = ServiceFormSchema.StripUnknown(form, request.Values);

            // Constraints apply to whatever was supplied, draft or not: a bad
            // value is wrong even in a half-finished form, and catching it now
            // avoids storing something that can never be completed.
            var constraintErrors = ServiceFormSchema.ConstraintErrors(
                form, values, guest.Event?.StartDate, guest.Event?.EndDate);
            if (constraintErrors.Count > 0)
                return ApiResponse<GuestServiceEntryResponse>.ErrorResponse(string.Join(" ", constraintErrors));

            // Required fields gate completion only — a half-filled form can
            // still be parked as pending.
            if (request.MarkCompleted)
            {
                var missing = ServiceFormSchema.MissingRequired(form, values);
                if (missing.Count > 0)
                {
                    return ApiResponse<GuestServiceEntryResponse>.ErrorResponse(
                        $"Fill in {string.Join(", ", missing)} before marking \"{service.Name}\" complete.");
                }
            }

            GuestServiceEntry entry;
            if (editingExisting)
            {
                entry = await _unitOfWork.GuestServiceEntries.Query()
                    .FirstOrDefaultAsync(e => e.PublicId == request.Id.Value && e.GuestId == guest.Id, ct);
                if (entry == null)
                    return ApiResponse<GuestServiceEntryResponse>.NotFoundResponse("Service entry not found.");
                entry.SetUpdateAudit(userId);
            }
            else
            {
                entry = new GuestServiceEntry { GuestId = guest.Id, ServiceId = service.Id };
                entry.SetCreationAudit(userId);
                await _unitOfWork.GuestServiceEntries.AddAsync(entry, ct);
            }

            entry.ValuesJson = ServiceFormSchema.SerializeValues(values);
            entry.Status = request.MarkCompleted ? GuestServiceStatus.Completed : GuestServiceStatus.Pending;
            entry.CompletedAt = request.MarkCompleted ? DateTime.UtcNow : null;
            entry.CompletedBy = request.MarkCompleted ? userId : null;

            if (editingExisting) _unitOfWork.GuestServiceEntries.Update(entry);
            await _unitOfWork.SaveChangesAsync(ct);

            return ApiResponse<GuestServiceEntryResponse>.SuccessResponse(
                ToEntryResponse(entry, service));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving service entry for guest {GuestId}", guestId);
            return ApiResponse<GuestServiceEntryResponse>.ServerErrorResponse("Could not save the service.");
        }
    }

    public async Task<ApiResponse<bool>> DeleteGuestServiceEntryAsync(
        Guid guestId, Guid entryId, int userId, CancellationToken ct = default)
    {
        try
        {
            var guest = await LoadGuestAsync(guestId, ct);
            if (guest == null) return ApiResponse<bool>.NotFoundResponse("Guest not found.");

            var entry = await _unitOfWork.GuestServiceEntries.Query()
                .Include(e => e.Service)
                .FirstOrDefaultAsync(e => e.PublicId == entryId && e.GuestId == guest.Id, ct);
            if (entry == null) return ApiResponse<bool>.NotFoundResponse("Service entry not found.");

            // Removing the last completed entry for a service re-locks everything
            // after it in a Fixed sequence. Blocked, because silently invalidating
            // work already done further down the chain is worse than refusing.
            if (EventGuestModels.UsesServiceLevels(guest.Event?.GuestModel)
                && entry.Status == GuestServiceStatus.Completed)
            {
                var siblings = await _unitOfWork.GuestServiceEntries.Query()
                    .CountAsync(e => e.GuestId == guest.Id
                                     && e.ServiceId == entry.ServiceId
                                     && e.PublicId != entryId
                                     && e.Status == GuestServiceStatus.Completed, ct);

                if (siblings == 0 && await HasLaterCompletedAsync(guest, entry.ServiceId, ct))
                {
                    return ApiResponse<bool>.ConflictResponse(
                        $"\"{entry.Service?.Name}\" comes before services that are already completed. "
                        + "Remove those first.", "SERVICE_SEQUENCE");
                }
            }

            entry.MarkAsDeleted(userId);
            _unitOfWork.GuestServiceEntries.Update(entry);
            await _unitOfWork.SaveChangesAsync(ct);

            return ApiResponse<bool>.SuccessResponse(true, "Service entry removed.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting service entry {EntryId}", entryId);
            return ApiResponse<bool>.ServerErrorResponse("Could not remove the service entry.");
        }
    }

    public async Task<ApiResponse<PaginatedResponse<ServiceEntryRow>>> GetServiceEntriesAsync(
        Guid serviceId, Guid eventId, PagedRequest request, CancellationToken ct = default)
    {
        try
        {
            var service = await _unitOfWork.Services.Query()
                .FirstOrDefaultAsync(s => s.PublicId == serviceId, ct);
            if (service == null)
                return ApiResponse<PaginatedResponse<ServiceEntryRow>>.NotFoundResponse("Service not found.");

            var ev = await _unitOfWork.Events.GetByPublicIdAsync(eventId, ct);
            if (ev == null)
                return ApiResponse<PaginatedResponse<ServiceEntryRow>>.NotFoundResponse("Event not found.");

            var query = _unitOfWork.GuestServiceEntries.Query()
                .Where(e => e.ServiceId == service.Id && e.Guest.EventId == ev.Id)
                .Include(e => e.Guest).ThenInclude(g => g.ServiceLevel);

            if (!string.IsNullOrWhiteSpace(request.SearchTerm))
            {
                var term = request.SearchTerm.Trim();
                query = (Microsoft.EntityFrameworkCore.Query.IIncludableQueryable<GuestServiceEntry, ServiceLevel>)
                    query.Where(e =>
                        (e.Guest.FirstName + " " + e.Guest.LastName).Contains(term)
                        || (e.Guest.Email != null && e.Guest.Email.Contains(term))
                        || (e.Guest.Organization != null && e.Guest.Organization.Contains(term)));
            }

            var total = await query.CountAsync(ct);

            var rows = await query
                .OrderBy(e => e.Guest.FirstName).ThenBy(e => e.Guest.LastName).ThenBy(e => e.CreatedAt)
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync(ct);

            var items = rows.Select(e => new ServiceEntryRow
            {
                EntryId = e.PublicId,
                GuestId = e.Guest.PublicId,
                GuestName = ($"{e.Guest.FirstName} {e.Guest.LastName}").Trim(),
                Email = e.Guest.Email,
                Organization = e.Guest.Organization,
                ServiceLevelName = e.Guest.ServiceLevel?.Name,
                ServiceLevelColor = e.Guest.ServiceLevel?.Color,
                Status = e.Status,
                CompletedAt = e.CompletedAt,
                Values = ServiceFormSchema.ParseValues(e.ValuesJson),
            }).ToList();

            return ApiResponse<PaginatedResponse<ServiceEntryRow>>.SuccessResponse(
                new PaginatedResponse<ServiceEntryRow>(items, total, request.PageNumber, request.PageSize));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing entries for service {ServiceId}", serviceId);
            return ApiResponse<PaginatedResponse<ServiceEntryRow>>.ServerErrorResponse("Could not load the service entries.");
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Internals
    // ═══════════════════════════════════════════════════════════════════════

    private async Task<Guest> LoadGuestAsync(Guid guestId, CancellationToken ct) =>
        await _unitOfWork.Guests.Query()
            .Include(g => g.Event)
            .Include(g => g.ServiceLevel)
            .FirstOrDefaultAsync(g => g.PublicId == guestId, ct);

    /// <summary>
    /// The guest's checklist. Slot order is the level's configured sequence; on a
    /// Fixed event a slot unlocks only once every earlier slot has a completed
    /// entry, which is also what makes every service mandatory — the chain simply
    /// cannot be finished by skipping one.
    /// </summary>
    private async Task<GuestServicePlanResponse> BuildPlanAsync(Guest guest, CancellationToken ct)
    {
        var isFixed = EventGuestModels.UsesServiceLevels(guest.Event?.GuestModel);

        var plan = new GuestServicePlanResponse
        {
            GuestId = guest.PublicId,
            ServiceLevelId = guest.ServiceLevel?.PublicId,
            ServiceLevelName = guest.ServiceLevel?.Name,
            ServiceLevelColor = guest.ServiceLevel?.Color,
            GuestModel = isFixed ? EventGuestModels.Fixed : EventGuestModels.Flexible,
            IsComplete = true,
        };

        if (guest.ServiceLevelId == null) return plan;

        var assignments = await _unitOfWork.ServiceLevelServices.Query()
            .Where(a => a.ServiceLevelId == guest.ServiceLevelId.Value)
            .Include(a => a.Service)
            .OrderBy(a => a.SortOrder)
            .ToListAsync(ct);

        var entries = await _unitOfWork.GuestServiceEntries.Query()
            .Where(e => e.GuestId == guest.Id)
            .Include(e => e.Service)
            .OrderBy(e => e.CreatedAt)
            .ToListAsync(ct);

        var blockedBy = (string)null;

        foreach (var a in assignments)
        {
            if (a.Service == null) continue;

            var mine = entries.Where(e => e.ServiceId == a.ServiceId).ToList();
            var done = mine.Any(e => e.Status == GuestServiceStatus.Completed);

            var slot = new GuestServiceSlotResponse
            {
                ServiceId = a.Service.PublicId,
                Code = a.Service.Code,
                Name = a.Service.Name,
                NameAr = a.Service.NameAr,
                Icon = a.Service.Icon,
                SortOrder = a.SortOrder,
                Form = ServiceFormSchema.Parse(a.Service.FormSchemaJson),
                Entries = mine.Select(e => ToEntryResponse(e, a.Service)).ToList(),
                Status = done ? GuestServiceStatus.Completed : GuestServiceStatus.Pending,
                IsRequired = isFixed,
                IsUnlocked = !isFixed || blockedBy == null,
            };

            if (!slot.IsUnlocked)
                slot.LockedReason = $"Complete \"{blockedBy}\" first.";

            // The first incomplete service in a Fixed sequence blocks the rest.
            if (isFixed && !done && blockedBy == null)
                blockedBy = a.Service.Name;

            if (slot.IsRequired && !done) plan.IsComplete = false;

            plan.Slots.Add(slot);
        }

        return plan;
    }

    /// <summary>True when any service after <paramref name="serviceId"/> in the guest's level already has a completed entry.</summary>
    private async Task<bool> HasLaterCompletedAsync(Guest guest, int serviceId, CancellationToken ct)
    {
        var assignments = await _unitOfWork.ServiceLevelServices.Query()
            .Where(a => a.ServiceLevelId == guest.ServiceLevelId)
            .OrderBy(a => a.SortOrder)
            .ToListAsync(ct);

        var position = assignments.FindIndex(a => a.ServiceId == serviceId);
        if (position < 0) return false;

        var laterIds = assignments.Skip(position + 1).Select(a => a.ServiceId).ToList();
        if (laterIds.Count == 0) return false;

        return await _unitOfWork.GuestServiceEntries.Query()
            .AnyAsync(e => e.GuestId == guest.Id
                           && laterIds.Contains(e.ServiceId)
                           && e.Status == GuestServiceStatus.Completed, ct);
    }

    private async Task<(List<Service> services, string error)> ResolveServicesAsync(
        List<Guid> serviceIds, CancellationToken ct)
    {
        var ids = (serviceIds ?? new List<Guid>()).Where(id => id != Guid.Empty).Distinct().ToList();
        if (ids.Count == 0) return (new List<Service>(), null);

        var found = await _unitOfWork.Services.Query()
            .Where(s => ids.Contains(s.PublicId))
            .ToListAsync(ct);

        if (found.Count != ids.Count)
            return (null, "One or more selected services no longer exist.");

        // Returned in the order the client sent them: that order becomes the
        // completion sequence, so it must not be re-sorted here.
        var byId = found.ToDictionary(s => s.PublicId);
        return (ids.Select(id => byId[id]).ToList(), null);
    }

    /// <summary>
    /// Replaces a level's assignments with exactly the ones given, reusing rows
    /// that already exist so re-ordering a level does not churn the table.
    /// </summary>
    private async Task WriteAssignmentsAsync(
        ServiceLevel level, List<Service> services, int userId, CancellationToken ct)
    {
        var existing = await _unitOfWork.ServiceLevelServices.Query()
            .Where(a => a.ServiceLevelId == level.Id)
            .ToListAsync(ct);

        var wanted = services.Select(s => s.Id).ToHashSet();

        foreach (var stale in existing.Where(a => !wanted.Contains(a.ServiceId)))
        {
            stale.MarkAsDeleted(userId);
            _unitOfWork.ServiceLevelServices.Update(stale);
        }

        for (var i = 0; i < services.Count; i++)
        {
            var match = existing.FirstOrDefault(a => a.ServiceId == services[i].Id);
            if (match != null)
            {
                match.SortOrder = i;
                match.SetUpdateAudit(userId);
                _unitOfWork.ServiceLevelServices.Update(match);
            }
            else
            {
                var row = new ServiceLevelService
                {
                    ServiceLevelId = level.Id,
                    ServiceId = services[i].Id,
                    SortOrder = i,
                };
                row.SetCreationAudit(userId);
                await _unitOfWork.ServiceLevelServices.AddAsync(row, ct);
            }
        }
    }

    private async Task<string> ValidateServiceAsync(CreateServiceRequest r, int? excludeId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(r.Name)) return "Service name is required.";

        var formError = ServiceFormSchema.ValidateForm(r.Form);
        if (formError != null) return formError;

        var code = Slugify(r.Code, r.Name);
        var clash = await _unitOfWork.Services.Query()
            .AnyAsync(s => s.Code == code && (excludeId == null || s.Id != excludeId.Value), ct);
        return clash ? $"Another service already uses the code \"{code}\"." : null;
    }

    private async Task<string> ValidateLevelAsync(CreateServiceLevelRequest r, int? excludeId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(r.Name)) return "Service level name is required.";

        var unknown = (r.RequiredGuestFields ?? new List<string>())
            .Where(f => !GuestRequirableFields.IsValid(f)).ToList();
        if (unknown.Count > 0)
            return $"Unknown required guest field(s): {string.Join(", ", unknown)}.";

        var code = Slugify(r.Code, r.Name);
        var clash = await _unitOfWork.ServiceLevels.Query()
            .AnyAsync(l => l.Code == code && (excludeId == null || l.Id != excludeId.Value), ct);
        return clash ? $"Another service level already uses the code \"{code}\"." : null;
    }

    /// <summary>Lowercase, hyphenated slug; falls back to the name when no code is given.</summary>
    private static string Slugify(string code, string name)
    {
        var raw = string.IsNullOrWhiteSpace(code) ? name : code;
        var slug = Regex.Replace((raw ?? string.Empty).Trim().ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
        return string.IsNullOrEmpty(slug) ? "service" : slug;
    }

    private static ServiceResponse ToServiceResponse(Service s, int levelCount) => new()
    {
        Id = s.PublicId,
        Code = s.Code,
        Name = s.Name,
        NameAr = s.NameAr,
        Description = s.Description,
        Icon = s.Icon,
        SortOrder = s.SortOrder,
        IsActive = s.IsActive,
        Form = ServiceFormSchema.Parse(s.FormSchemaJson),
        LevelCount = levelCount,
    };

    private static ServiceLevelResponse ToLevelResponse(ServiceLevel l, int guestCount) => new()
    {
        Id = l.PublicId,
        Code = l.Code,
        Name = l.Name,
        NameAr = l.NameAr,
        Description = l.Description,
        Color = l.Color,
        SortOrder = l.SortOrder,
        IsActive = l.IsActive,
        RequiredGuestFields = ServiceLevelRules.ParseRequiredFields(l.RequiredGuestFieldsJson),
        GuestCount = guestCount,
        Services = l.Services
            .Where(a => a.IsDeleted != true && a.Service != null)
            .OrderBy(a => a.SortOrder)
            .Select(a => new ServiceLevelServiceResponse
            {
                ServiceId = a.Service.PublicId,
                Code = a.Service.Code,
                Name = a.Service.Name,
                NameAr = a.Service.NameAr,
                Icon = a.Service.Icon,
                SortOrder = a.SortOrder,
            })
            .ToList(),
    };

    private static GuestServiceEntryResponse ToEntryResponse(GuestServiceEntry e, Service s) => new()
    {
        Id = e.PublicId,
        ServiceId = s?.PublicId ?? Guid.Empty,
        ServiceName = s?.Name,
        Status = e.Status,
        Values = ServiceFormSchema.ParseValues(e.ValuesJson),
        CompletedAt = e.CompletedAt,
    };
}
