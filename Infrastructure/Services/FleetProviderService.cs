using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Core.Interfaces.Repositories;
using Core.Interfaces.Services;
using Core.ViewModel.Common;
using Core.ViewModel.FleetProvider;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using FleetProviderEntity = DomainPersistence.Entities.FleetProvider;

namespace Infrastructure.Services;

// Fleet providers — the companies vehicles are sourced from, contracted per
// event. Referenced by their public Guid at the API boundary, resolved to the
// internal int id by callers.
public class FleetProviderService(IUnitOfWork _unitOfWork, ILogger<FleetProviderService> _logger) : IFleetProviderService
{
    public async Task<ApiResponse<List<FleetProviderResponse>>> GetAllAsync(Guid eventId, CancellationToken ct = default)
    {
        var data = await _unitOfWork.FleetProviders.Query()
            .Where(x => x.Event.PublicId == eventId)
            .OrderBy(x => x.Name)
            .Select(Project)
            .ToListAsync(ct);

        return ApiResponse<List<FleetProviderResponse>>.SuccessResponse(data);
    }

    public async Task<ApiResponse<FleetProviderResponse>> GetByIdAsync(Guid eventId, Guid id, CancellationToken ct = default)
    {
        var provider = await _unitOfWork.FleetProviders.Query()
            .Where(x => x.PublicId == id && x.Event.PublicId == eventId)
            .Select(Project)
            .FirstOrDefaultAsync(ct);

        return provider == null
            ? ApiResponse<FleetProviderResponse>.NotFoundResponse("Fleet provider not found")
            : ApiResponse<FleetProviderResponse>.SuccessResponse(provider);
    }

    public async Task<ApiResponse<FleetProviderResponse>> CreateAsync(
        Guid eventId, CreateFleetProviderRequest request, int userId, CancellationToken ct = default)
    {
        try
        {
            var ev = await _unitOfWork.Events.GetByPublicIdAsync(eventId, ct);
            if (ev == null) return ApiResponse<FleetProviderResponse>.NotFoundResponse("Event not found");

            var error = await ValidateAsync(request, ev.Id, null, ct);
            if (error != null)
                return ApiResponse<FleetProviderResponse>.ErrorResponse(error);

            var provider = new FleetProviderEntity { EventId = ev.Id };
            Apply(provider, request);

            provider.SetCreationAudit(userId);
            await _unitOfWork.FleetProviders.AddAsync(provider, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            return await GetByIdAsync(eventId, provider.PublicId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating fleet provider for event {EventId}", eventId);
            return ApiResponse<FleetProviderResponse>.ServerErrorResponse("An error occurred while creating the fleet provider");
        }
    }

    public async Task<ApiResponse<FleetProviderResponse>> UpdateAsync(
        Guid eventId, Guid id, UpdateFleetProviderRequest request, int userId, CancellationToken ct = default)
    {
        try
        {
            // Matched on the event too, so one event can never edit another's
            // provider. EventId itself is never reassigned — a provider moving
            // event is a new contract, i.e. a new row.
            var provider = await _unitOfWork.FleetProviders.Query()
                .FirstOrDefaultAsync(x => x.PublicId == id && x.Event.PublicId == eventId, ct);
            if (provider == null)
                return ApiResponse<FleetProviderResponse>.NotFoundResponse("Fleet provider not found");

            var error = await ValidateAsync(request, provider.EventId, provider.Id, ct);
            if (error != null)
                return ApiResponse<FleetProviderResponse>.ErrorResponse(error);

            Apply(provider, request);

            provider.SetUpdateAudit(userId);
            _unitOfWork.FleetProviders.Update(provider);
            await _unitOfWork.SaveChangesAsync(ct);

            return await GetByIdAsync(eventId, provider.PublicId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating fleet provider {FleetProviderId}", id);
            return ApiResponse<FleetProviderResponse>.ServerErrorResponse("An error occurred while updating the fleet provider");
        }
    }

    public async Task<ApiResponse<bool>> DeleteAsync(Guid eventId, Guid id, int userId, CancellationToken ct = default)
    {
        try
        {
            var provider = await _unitOfWork.FleetProviders.Query()
                .FirstOrDefaultAsync(x => x.PublicId == id && x.Event.PublicId == eventId, ct);
            if (provider == null)
                return ApiResponse<bool>.NotFoundResponse("Fleet provider not found");

            // The FK is Restrict, so refuse before the DB does — the message is better.
            var inUse = await _unitOfWork.Vehicles.Query().AnyAsync(x => x.FleetProviderId == provider.Id, ct);
            if (inUse)
                return ApiResponse<bool>.ErrorResponse("This provider still has vehicles assigned to it");

            provider.MarkAsDeleted(userId);
            _unitOfWork.FleetProviders.Update(provider);
            await _unitOfWork.SaveChangesAsync(ct);

            return ApiResponse<bool>.SuccessResponse(true, "Fleet provider deleted");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting fleet provider {FleetProviderId}", id);
            return ApiResponse<bool>.ServerErrorResponse("An error occurred while deleting the fleet provider");
        }
    }

    private static void Apply(FleetProviderEntity provider, CreateFleetProviderRequest request)
    {
        provider.Name = request.Name.Trim();
        provider.ContactPerson = Clean(request.ContactPerson);
        provider.Phone = Clean(request.Phone);
        provider.Email = Clean(request.Email);
        provider.Notes = Clean(request.Notes);
    }

    private static string Clean(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    // Returns null when valid, otherwise the message to send back.
    private async Task<string> ValidateAsync(CreateFleetProviderRequest request, int eventId, int? excludeId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return "Provider name is required";

        var name = request.Name.Trim();
        var duplicate = await _unitOfWork.FleetProviders.Query()
            .AnyAsync(x => x.EventId == eventId && x.Name == name
                && (!excludeId.HasValue || x.Id != excludeId.Value), ct);

        return duplicate ? "A fleet provider with this name already exists for this event" : null;
    }

    // Shared projection. Must stay an expression tree (not a method) so EF can
    // translate it into the SELECT instead of pulling rows into memory.
    private static readonly Expression<Func<FleetProviderEntity, FleetProviderResponse>> Project = x => new FleetProviderResponse
    {
        Id = x.PublicId,
        EventId = x.Event.PublicId,
        Name = x.Name,
        ContactPerson = x.ContactPerson,
        Phone = x.Phone,
        Email = x.Email,
        Notes = x.Notes,
        VehicleCount = x.Vehicles.Count,
    };
}
