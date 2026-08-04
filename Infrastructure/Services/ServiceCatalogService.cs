using System;
using System.Collections.Generic;
using System.Linq;
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
/// Per-event service catalog + the guest grades built from it. Follows the
/// EventService/Session pattern: every read and write is scoped by the event's
/// public id, and children are always looked up by (childPublicId, eventPublicId)
/// so a level from event A can never be mutated through event B's route.
/// </summary>
public class ServiceCatalogService(
    IUnitOfWork _unitOfWork,
    ILogger<ServiceCatalogService> _logger) : IServiceCatalogService
{
    // ── Services ─────────────────────────────────────────────────────────────

    public async Task<ApiResponse<List<ServiceResponse>>> GetServicesAsync(Guid eventId, CancellationToken ct = default)
    {
        try
        {
            var ev = await _unitOfWork.Events.GetByPublicIdAsync(eventId, ct);
            if (ev == null)
                return ApiResponse<List<ServiceResponse>>.NotFoundResponse("Event not found");

            var services = await _unitOfWork.Services.QueryNoTracking()
                .Where(s => s.EventId == ev.Id)
                .OrderBy(s => s.SortOrder).ThenBy(s => s.Name)
                .Select(s => new
                {
                    s.PublicId, s.Name, s.NameAr, s.Description, s.SortOrder, s.FieldsSchema,
                    UsedBy = s.ServiceLevels.Count,
                })
                .ToListAsync(ct);

            var data = services.Select(s => new ServiceResponse
            {
                Id = s.PublicId,
                EventId = eventId,
                Name = s.Name,
                NameAr = s.NameAr,
                Description = s.Description,
                SortOrder = s.SortOrder,
                Fields = ServiceFieldSchema.Parse(s.FieldsSchema),
                UsedByLevelCount = s.UsedBy,
            }).ToList();

            return ApiResponse<List<ServiceResponse>>.SuccessResponse(data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading services for event {EventId}", eventId);
            return ApiResponse<List<ServiceResponse>>.ServerErrorResponse("An error occurred while loading services");
        }
    }

    public async Task<ApiResponse<ServiceResponse>> CreateServiceAsync(
        Guid eventId, CreateServiceRequest request, int userId, CancellationToken ct = default)
    {
        try
        {
            var ev = await _unitOfWork.Events.GetByPublicIdAsync(eventId, ct);
            if (ev == null)
                return ApiResponse<ServiceResponse>.NotFoundResponse("Event not found");

            // The catalogue only means anything on a fixed event. Creating levels
            // for a flexible one would build a configuration nothing enforces, so
            // it is refused with an explanation rather than silently accepted.
            // Reads stay open: an event switched to flexible keeps showing its
            // existing catalogue, and switching back restores it.
            if (!EventGuestModels.UsesServiceLevels(ev.GuestModel))
                return ApiResponse<ServiceResponse>.ConflictResponse(
                    "This event uses the flexible guest model, which doesn't use service levels. "
                    + "Switch the event to fixed to configure them.", "EVENT_MODEL_FLEXIBLE");

            var invalid = ValidateService(request);
            if (invalid != null)
                return ApiResponse<ServiceResponse>.ErrorResponse(invalid);

            var name = request.Name.Trim();
            if (await _unitOfWork.Services.Query().AnyAsync(s => s.EventId == ev.Id && s.Name == name, ct))
                return ApiResponse<ServiceResponse>.ConflictResponse("A service with this name already exists for this event");

            var service = new Service
            {
                EventId = ev.Id,
                Event = ev,
                Name = name,
                NameAr = request.NameAr?.Trim(),
                Description = request.Description?.Trim(),
                SortOrder = request.SortOrder,
                FieldsSchema = ServiceFieldSchema.Serialize(request.Fields),
            };
            service.SetCreationAudit(userId);

            await _unitOfWork.Services.AddAsync(service, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            return ApiResponse<ServiceResponse>.SuccessResponse(MapService(service, eventId, 0), "Service created");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating service for event {EventId}", eventId);
            return ApiResponse<ServiceResponse>.ServerErrorResponse("An error occurred while creating the service");
        }
    }

    public async Task<ApiResponse<ServiceResponse>> UpdateServiceAsync(
        Guid eventId, Guid serviceId, UpdateServiceRequest request, int userId, CancellationToken ct = default)
    {
        try
        {
            var service = await _unitOfWork.Services.Query()
                .FirstOrDefaultAsync(s => s.PublicId == serviceId && s.Event.PublicId == eventId, ct);
            if (service == null)
                return ApiResponse<ServiceResponse>.NotFoundResponse("Service not found");

            var invalid = ValidateService(request);
            if (invalid != null)
                return ApiResponse<ServiceResponse>.ErrorResponse(invalid);

            var name = request.Name.Trim();
            if (await _unitOfWork.Services.Query()
                    .AnyAsync(s => s.EventId == service.EventId && s.Name == name && s.Id != service.Id, ct))
                return ApiResponse<ServiceResponse>.ConflictResponse("A service with this name already exists for this event");

            service.Name = name;
            service.NameAr = request.NameAr?.Trim();
            service.Description = request.Description?.Trim();
            service.SortOrder = request.SortOrder;
            service.FieldsSchema = ServiceFieldSchema.Serialize(request.Fields);
            service.SetUpdateAudit(userId);

            _unitOfWork.Services.Update(service);
            await _unitOfWork.SaveChangesAsync(ct);

            var usedBy = await _unitOfWork.ServiceLevelServices.Query().CountAsync(x => x.ServiceId == service.Id, ct);
            return ApiResponse<ServiceResponse>.SuccessResponse(MapService(service, eventId, usedBy), "Service updated");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating service {ServiceId}", serviceId);
            return ApiResponse<ServiceResponse>.ServerErrorResponse("An error occurred while updating the service");
        }
    }

    public async Task<ApiResponse<bool>> DeleteServiceAsync(Guid eventId, Guid serviceId, int userId, CancellationToken ct = default)
    {
        try
        {
            var service = await _unitOfWork.Services.Query()
                .FirstOrDefaultAsync(s => s.PublicId == serviceId && s.Event.PublicId == eventId, ct);
            if (service == null)
                return ApiResponse<bool>.NotFoundResponse("Service not found");

            // Service -> ServiceLevelServices is Restrict (see the DbContext note on
            // multiple cascade paths), so detach the join rows explicitly. Soft
            // delete, matching every other delete in the codebase.
            var joins = await _unitOfWork.ServiceLevelServices.Query()
                .Where(x => x.ServiceId == service.Id)
                .ToListAsync(ct);
            foreach (var j in joins) j.MarkAsDeleted(userId);
            if (joins.Count > 0) _unitOfWork.ServiceLevelServices.UpdateRange(joins);

            service.MarkAsDeleted(userId);
            _unitOfWork.Services.Update(service);
            await _unitOfWork.SaveChangesAsync(ct);

            return ApiResponse<bool>.SuccessResponse(true,
                joins.Count > 0
                    ? $"Service deleted and removed from {joins.Count} service level{(joins.Count == 1 ? "" : "s")}"
                    : "Service deleted");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting service {ServiceId}", serviceId);
            return ApiResponse<bool>.ServerErrorResponse("An error occurred while deleting the service");
        }
    }

    // ── Service levels ───────────────────────────────────────────────────────

    public async Task<ApiResponse<List<ServiceLevelResponse>>> GetServiceLevelsAsync(Guid eventId, CancellationToken ct = default)
    {
        try
        {
            var ev = await _unitOfWork.Events.GetByPublicIdAsync(eventId, ct);
            if (ev == null)
                return ApiResponse<List<ServiceLevelResponse>>.NotFoundResponse("Event not found");

            var levels = await LevelQuery().Where(l => l.EventId == ev.Id)
                .OrderBy(l => l.SortOrder).ThenBy(l => l.Name)
                .ToListAsync(ct);

            var counts = await GuestCountsByLevelAsync(levels.Select(l => l.Id).ToList(), null, ct);

            var data = levels.Select(l => MapLevel(l, eventId, counts.GetValueOrDefault(l.Id))).ToList();
            return ApiResponse<List<ServiceLevelResponse>>.SuccessResponse(data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading service levels for event {EventId}", eventId);
            return ApiResponse<List<ServiceLevelResponse>>.ServerErrorResponse("An error occurred while loading service levels");
        }
    }

    public async Task<ApiResponse<ServiceLevelResponse>> GetServiceLevelByIdAsync(Guid eventId, Guid levelId, CancellationToken ct = default)
    {
        try
        {
            var level = await LevelQuery().FirstOrDefaultAsync(l => l.PublicId == levelId && l.Event.PublicId == eventId, ct);
            if (level == null)
                return ApiResponse<ServiceLevelResponse>.NotFoundResponse("Service level not found");

            var count = await _unitOfWork.Guests.Query().CountAsync(g => g.ServiceLevelId == level.Id, ct);
            return ApiResponse<ServiceLevelResponse>.SuccessResponse(MapLevel(level, eventId, count));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading service level {LevelId}", levelId);
            return ApiResponse<ServiceLevelResponse>.ServerErrorResponse("An error occurred while loading the service level");
        }
    }

    public async Task<ApiResponse<ServiceLevelResponse>> CreateServiceLevelAsync(
        Guid eventId, CreateServiceLevelRequest request, int userId, CancellationToken ct = default)
    {
        try
        {
            var ev = await _unitOfWork.Events.GetByPublicIdAsync(eventId, ct);
            if (ev == null)
                return ApiResponse<ServiceLevelResponse>.NotFoundResponse("Event not found");

            // The catalogue only means anything on a fixed event. Creating levels
            // for a flexible one would build a configuration nothing enforces, so
            // it is refused with an explanation rather than silently accepted.
            // Reads stay open: an event switched to flexible keeps showing its
            // existing catalogue, and switching back restores it.
            if (!EventGuestModels.UsesServiceLevels(ev.GuestModel))
                return ApiResponse<ServiceLevelResponse>.ConflictResponse(
                    "This event uses the flexible guest model, which doesn't use service levels. "
                    + "Switch the event to fixed to configure them.", "EVENT_MODEL_FLEXIBLE");

            var invalid = ValidateLevel(request);
            if (invalid != null)
                return ApiResponse<ServiceLevelResponse>.ErrorResponse(invalid);

            var name = request.Name.Trim();
            if (await _unitOfWork.ServiceLevels.Query().AnyAsync(l => l.EventId == ev.Id && l.Name == name, ct))
                return ApiResponse<ServiceLevelResponse>.ConflictResponse("A service level with this name already exists for this event");

            // Validate the requested services BEFORE the level row is written.
            // Doing it afterwards (as the join-sync does) would commit the level
            // and then fail, leaving an orphaned level behind on every bad payload.
            var (resolvedServices, servicesError) = await ResolveLevelServicesAsync(ev.Id, request.Services, ct);
            if (servicesError != null)
                return ApiResponse<ServiceLevelResponse>.ErrorResponse(servicesError);

            var level = new ServiceLevel
            {
                EventId = ev.Id,
                Event = ev,
                Name = name,
                NameAr = request.NameAr?.Trim(),
                Code = Slugify(request.Code, name),
                Description = request.Description?.Trim(),
                Color = request.Color?.Trim(),
                SortOrder = request.SortOrder,
                Capacity = request.Capacity is > 0 ? request.Capacity : null,
                RequiredGuestFieldsJson = ServiceLevelRules.SerializeRequiredFields(request.RequiredGuestFields),
            };
            level.SetCreationAudit(userId);

            await _unitOfWork.ServiceLevels.AddAsync(level, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            await WriteLevelServicesAsync(level, request.Services, resolvedServices, userId, ct);

            return await GetServiceLevelByIdAsync(eventId, level.PublicId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating service level for event {EventId}", eventId);
            return ApiResponse<ServiceLevelResponse>.ServerErrorResponse("An error occurred while creating the service level");
        }
    }

    public async Task<ApiResponse<ServiceLevelResponse>> UpdateServiceLevelAsync(
        Guid eventId, Guid levelId, UpdateServiceLevelRequest request, int userId, CancellationToken ct = default)
    {
        try
        {
            var level = await _unitOfWork.ServiceLevels.Query()
                .FirstOrDefaultAsync(l => l.PublicId == levelId && l.Event.PublicId == eventId, ct);
            if (level == null)
                return ApiResponse<ServiceLevelResponse>.NotFoundResponse("Service level not found");

            var invalid = ValidateLevel(request);
            if (invalid != null)
                return ApiResponse<ServiceLevelResponse>.ErrorResponse(invalid);

            var name = request.Name.Trim();
            if (await _unitOfWork.ServiceLevels.Query()
                    .AnyAsync(l => l.EventId == level.EventId && l.Name == name && l.Id != level.Id, ct))
                return ApiResponse<ServiceLevelResponse>.ConflictResponse("A service level with this name already exists for this event");

            // Validated up front for the same reason as create: a bad services
            // payload must not half-apply the level's own field changes.
            var (resolvedServices, servicesError) = await ResolveLevelServicesAsync(level.EventId, request.Services, ct);
            if (servicesError != null)
                return ApiResponse<ServiceLevelResponse>.ErrorResponse(servicesError);

            // Lowering capacity below the current headcount is allowed (the guests
            // are already there) — it just blocks further assignment. Warn, don't fail.
            var guestCount = await _unitOfWork.Guests.Query().CountAsync(g => g.ServiceLevelId == level.Id, ct);

            level.Name = name;
            level.NameAr = request.NameAr?.Trim();
            level.Code = Slugify(request.Code, name);
            level.Description = request.Description?.Trim();
            level.Color = request.Color?.Trim();
            level.SortOrder = request.SortOrder;
            level.Capacity = request.Capacity is > 0 ? request.Capacity : null;
            level.RequiredGuestFieldsJson = ServiceLevelRules.SerializeRequiredFields(request.RequiredGuestFields);
            level.SetUpdateAudit(userId);

            _unitOfWork.ServiceLevels.Update(level);
            await _unitOfWork.SaveChangesAsync(ct);

            await WriteLevelServicesAsync(level, request.Services, resolvedServices, userId, ct);

            // Guest.Tier mirrors ServiceLevel.Code — renaming a level has to
            // re-sync every guest on it or the legacy string goes stale.
            await ResyncGuestTierAsync(level, ct);

            var result = await GetServiceLevelByIdAsync(eventId, level.PublicId, ct);
            if (result.Success && level.Capacity.HasValue && guestCount > level.Capacity.Value)
                result.Message = $"Service level updated — note it now holds {guestCount} guests, over its {level.Capacity} capacity";
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating service level {LevelId}", levelId);
            return ApiResponse<ServiceLevelResponse>.ServerErrorResponse("An error occurred while updating the service level");
        }
    }

    public async Task<ApiResponse<bool>> DeleteServiceLevelAsync(Guid eventId, Guid levelId, int userId, CancellationToken ct = default)
    {
        try
        {
            var level = await _unitOfWork.ServiceLevels.Query()
                .FirstOrDefaultAsync(l => l.PublicId == levelId && l.Event.PublicId == eventId, ct);
            if (level == null)
                return ApiResponse<bool>.NotFoundResponse("Service level not found");

            var guestCount = await _unitOfWork.Guests.Query().CountAsync(g => g.ServiceLevelId == level.Id, ct);
            if (guestCount > 0)
                return ApiResponse<bool>.ConflictResponse(
                    $"{guestCount} guest{(guestCount == 1 ? " is" : "s are")} still on this service level — reassign them first");

            var joins = await _unitOfWork.ServiceLevelServices.Query()
                .Where(x => x.ServiceLevelId == level.Id)
                .ToListAsync(ct);
            foreach (var j in joins) j.MarkAsDeleted(userId);
            if (joins.Count > 0) _unitOfWork.ServiceLevelServices.UpdateRange(joins);

            level.MarkAsDeleted(userId);
            _unitOfWork.ServiceLevels.Update(level);
            await _unitOfWork.SaveChangesAsync(ct);

            return ApiResponse<bool>.SuccessResponse(true, "Service level deleted");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting service level {LevelId}", levelId);
            return ApiResponse<bool>.ServerErrorResponse("An error occurred while deleting the service level");
        }
    }

    public async Task<ApiResponse<ServiceLevelRuleCheckResponse>> CheckRulesAsync(
        Guid levelId, Guid? excludeGuestId, CancellationToken ct = default)
    {
        try
        {
            var level = await _unitOfWork.ServiceLevels.QueryNoTracking()
                .FirstOrDefaultAsync(l => l.PublicId == levelId, ct);
            if (level == null)
                return ApiResponse<ServiceLevelRuleCheckResponse>.NotFoundResponse("Service level not found");

            var result = new ServiceLevelRuleCheckResponse { Passes = true };

            if (level.Capacity.HasValue)
            {
                var q = _unitOfWork.Guests.Query().Where(g => g.ServiceLevelId == level.Id);
                if (excludeGuestId.HasValue)
                    q = q.Where(g => g.PublicId != excludeGuestId.Value);

                var count = await q.CountAsync(ct);
                if (count >= level.Capacity.Value)
                {
                    result.Passes = false;
                    result.Violations.Add($"\"{level.Name}\" is at capacity ({count} / {level.Capacity}).");
                }
            }

            // Required-field violations are guest-specific, so they're reported by
            // the guest save path (ValidateGuestAgainstLevel below). Here we only
            // surface WHICH fields the level demands, so the form can mark them.
            result.MissingFields = ServiceLevelRules.ParseRequiredFields(level.RequiredGuestFieldsJson);

            return ApiResponse<ServiceLevelRuleCheckResponse>.SuccessResponse(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking rules for service level {LevelId}", levelId);
            return ApiResponse<ServiceLevelRuleCheckResponse>.ServerErrorResponse("An error occurred while checking the service level rules");
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private IQueryable<ServiceLevel> LevelQuery() =>
        _unitOfWork.ServiceLevels.Query()
            .Include(l => l.Services).ThenInclude(s => s.Service);

    private async Task<Dictionary<int, int>> GuestCountsByLevelAsync(
        List<int> levelIds, Guid? excludeGuestId, CancellationToken ct)
    {
        if (levelIds.Count == 0) return new Dictionary<int, int>();

        var q = _unitOfWork.Guests.Query().Where(g => g.ServiceLevelId != null && levelIds.Contains(g.ServiceLevelId.Value));
        if (excludeGuestId.HasValue) q = q.Where(g => g.PublicId != excludeGuestId.Value);

        return await q.GroupBy(g => g.ServiceLevelId.Value)
            .Select(grp => new { LevelId = grp.Key, Count = grp.Count() })
            .ToDictionaryAsync(x => x.LevelId, x => x.Count, ct);
    }

    /// <summary>
    /// Resolves + fully validates the requested services without writing anything,
    /// so callers can reject a bad payload before persisting the level itself.
    /// Returns (null, null) when <paramref name="inputs"/> is null, which means
    /// "leave the existing set as-is" (a PUT that only renames a level must not
    /// wipe its services).
    /// </summary>
    private async Task<(List<Service> services, string error)> ResolveLevelServicesAsync(
        int eventInternalId, List<ServiceLevelServiceInput> inputs, CancellationToken ct)
    {
        if (inputs == null) return (null, null);

        // Only services from the SAME event may be attached.
        var wantedIds = inputs.Select(i => i.ServiceId).Where(id => id != Guid.Empty).Distinct().ToList();
        var services = await _unitOfWork.Services.Query()
            .Where(s => s.EventId == eventInternalId && wantedIds.Contains(s.PublicId))
            .ToListAsync(ct);

        if (wantedIds.Except(services.Select(s => s.PublicId)).Any())
            return (null, "One or more selected services do not belong to this event");

        // Validate every value set against its own service's schema.
        foreach (var input in inputs)
        {
            var svc = services.FirstOrDefault(s => s.PublicId == input.ServiceId);
            if (svc == null) continue;
            var error = ServiceFieldSchema.ValidateValues(svc.FieldsSchema, input.Values);
            if (error != null) return (null, $"{svc.Name}: {error}");
        }

        return (services, null);
    }

    /// <summary>Applies the already-validated set of included services to the level:
    /// drops rows no longer wanted, upserts the rest. Assumes
    /// <see cref="ResolveLevelServicesAsync"/> has passed.</summary>
    private async Task WriteLevelServicesAsync(
        ServiceLevel level, List<ServiceLevelServiceInput> inputs,
        List<Service> services, int userId, CancellationToken ct)
    {
        if (inputs == null || services == null) return;

        var existing = await _unitOfWork.ServiceLevelServices.Query()
            .Where(x => x.ServiceLevelId == level.Id)
            .ToListAsync(ct);

        var byServiceId = services.ToDictionary(s => s.PublicId, s => s.Id);
        var keepInternalIds = inputs
            .Select(i => i.ServiceId)
            .Where(id => byServiceId.ContainsKey(id))
            .Select(id => byServiceId[id])
            .ToHashSet();

        // Drop rows no longer wanted.
        var toRemove = existing.Where(x => !keepInternalIds.Contains(x.ServiceId)).ToList();
        foreach (var r in toRemove) r.MarkAsDeleted(userId);
        if (toRemove.Count > 0) _unitOfWork.ServiceLevelServices.UpdateRange(toRemove);

        // Upsert the rest.
        foreach (var input in inputs)
        {
            if (!byServiceId.TryGetValue(input.ServiceId, out var internalServiceId)) continue;
            var json = ServiceFieldSchema.SerializeValues(input.Values);
            var row = existing.FirstOrDefault(x => x.ServiceId == internalServiceId);

            if (row == null)
            {
                row = new ServiceLevelService
                {
                    ServiceLevelId = level.Id,
                    ServiceId = internalServiceId,
                    FieldValuesJson = json,
                };
                row.SetCreationAudit(userId);
                await _unitOfWork.ServiceLevelServices.AddAsync(row, ct);
            }
            else
            {
                row.FieldValuesJson = json;
                row.SetUpdateAudit(userId);
                _unitOfWork.ServiceLevelServices.Update(row);
            }
        }

        await _unitOfWork.SaveChangesAsync(ct);
    }

    /// <summary>Keeps the legacy Guest.Tier string aligned with the level's code
    /// after a rename. Same sync contract as Organization/OrganizationId.</summary>
    private async Task ResyncGuestTierAsync(ServiceLevel level, CancellationToken ct)
    {
        var guests = await _unitOfWork.Guests.Query()
            .Where(g => g.ServiceLevelId == level.Id && g.Tier != level.Code)
            .ToListAsync(ct);
        if (guests.Count == 0) return;

        foreach (var g in guests) g.Tier = level.Code;
        _unitOfWork.Guests.UpdateRange(guests);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    private static string ValidateService(CreateServiceRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.Name))
            return "Service name is required";

        var fields = request.Fields ?? new List<ServiceFieldDefinition>();
        foreach (var f in fields)
        {
            if (string.IsNullOrWhiteSpace(f?.Key))
                return "Every field needs a key";
            if (!ServiceFieldTypes.IsValid(f.Type))
                return $"\"{f.Type}\" is not a supported field type";
            if (f.Type == ServiceFieldTypes.Select && (f.Options == null || f.Options.Count == 0))
                return $"Field \"{f.Label ?? f.Key}\" is a dropdown, so it needs at least one option";
        }

        var dupe = fields.Where(f => !string.IsNullOrWhiteSpace(f?.Key))
            .GroupBy(f => f.Key.Trim(), StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(g => g.Count() > 1);
        return dupe != null ? $"Duplicate field key \"{dupe.Key}\"" : null;
    }

    private static string ValidateLevel(CreateServiceLevelRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.Name))
            return "Service level name is required";
        if (request.Capacity is < 0)
            return "Capacity cannot be negative";

        var bad = (request.RequiredGuestFields ?? new List<string>())
            .FirstOrDefault(k => !string.IsNullOrWhiteSpace(k) && !GuestRequirableFields.IsValid(k));
        return bad != null ? $"\"{bad}\" is not a guest field that can be required" : null;
    }

    /// <summary>Code is what lands on Guest.Tier — derive it from the name when the
    /// caller doesn't supply one, and keep it slug-safe either way.</summary>
    private static string Slugify(string code, string fallbackName)
    {
        var source = string.IsNullOrWhiteSpace(code) ? fallbackName : code;
        if (string.IsNullOrWhiteSpace(source)) return null;

        var slug = new string(source.Trim().ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray());

        while (slug.Contains("--")) slug = slug.Replace("--", "-");
        slug = slug.Trim('-');
        return slug.Length > 50 ? slug[..50] : slug;
    }

    private static ServiceResponse MapService(Service s, Guid eventId, int usedByLevelCount) => new()
    {
        Id = s.PublicId,
        EventId = eventId,
        Name = s.Name,
        NameAr = s.NameAr,
        Description = s.Description,
        SortOrder = s.SortOrder,
        Fields = ServiceFieldSchema.Parse(s.FieldsSchema),
        UsedByLevelCount = usedByLevelCount,
    };

    private static ServiceLevelResponse MapLevel(ServiceLevel l, Guid eventId, int guestCount) => new()
    {
        Id = l.PublicId,
        EventId = eventId,
        Name = l.Name,
        NameAr = l.NameAr,
        Code = l.Code,
        Description = l.Description,
        Color = l.Color,
        SortOrder = l.SortOrder,
        Capacity = l.Capacity,
        RequiredGuestFields = ServiceLevelRules.ParseRequiredFields(l.RequiredGuestFieldsJson),
        GuestCount = guestCount,
        Services = (l.Services ?? new List<ServiceLevelService>())
            .Where(x => x.Service != null)
            .OrderBy(x => x.Service.SortOrder).ThenBy(x => x.Service.Name)
            .Select(x => new ServiceLevelServiceResponse
            {
                ServiceId = x.Service.PublicId,
                ServiceName = x.Service.Name,
                ServiceNameAr = x.Service.NameAr,
                Fields = ServiceFieldSchema.Parse(x.Service.FieldsSchema),
                Values = ServiceFieldSchema.ParseValues(x.FieldValuesJson),
            })
            .ToList(),
    };
}
