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

// Fleet providers — the companies vehicles are sourced from. Referenced by their
// public Guid at the API boundary, resolved to the internal int id by callers.
public class FleetProviderService(IUnitOfWork _unitOfWork, ILogger<FleetProviderService> _logger) : IFleetProviderService
{
    public async Task<ApiResponse<List<FleetProviderResponse>>> GetAllAsync(CancellationToken ct = default)
    {
        var data = await _unitOfWork.FleetProviders.Query()
            .OrderBy(x => x.Name)
            .Select(Project)
            .ToListAsync(ct);

        return ApiResponse<List<FleetProviderResponse>>.SuccessResponse(data);
    }

    public async Task<ApiResponse<FleetProviderResponse>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var provider = await _unitOfWork.FleetProviders.Query()
            .Where(x => x.PublicId == id)
            .Select(Project)
            .FirstOrDefaultAsync(ct);

        return provider == null
            ? ApiResponse<FleetProviderResponse>.NotFoundResponse("Fleet provider not found")
            : ApiResponse<FleetProviderResponse>.SuccessResponse(provider);
    }

    public async Task<ApiResponse<FleetProviderResponse>> CreateAsync(
        CreateFleetProviderRequest request, int userId, CancellationToken ct = default)
    {
        try
        {
            var error = await ValidateAsync(request, null, ct);
            if (error != null)
                return ApiResponse<FleetProviderResponse>.ErrorResponse(error);

            var provider = new FleetProviderEntity();
            Apply(provider, request);

            provider.SetCreationAudit(userId);
            await _unitOfWork.FleetProviders.AddAsync(provider, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            return await GetByIdAsync(provider.PublicId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating fleet provider");
            return ApiResponse<FleetProviderResponse>.ServerErrorResponse("An error occurred while creating the fleet provider");
        }
    }

    public async Task<ApiResponse<FleetProviderResponse>> UpdateAsync(
        Guid id, UpdateFleetProviderRequest request, int userId, CancellationToken ct = default)
    {
        try
        {
            var provider = await _unitOfWork.FleetProviders.GetByPublicIdAsync(id, ct);
            if (provider == null)
                return ApiResponse<FleetProviderResponse>.NotFoundResponse("Fleet provider not found");

            var error = await ValidateAsync(request, provider.Id, ct);
            if (error != null)
                return ApiResponse<FleetProviderResponse>.ErrorResponse(error);

            Apply(provider, request);

            provider.SetUpdateAudit(userId);
            _unitOfWork.FleetProviders.Update(provider);
            await _unitOfWork.SaveChangesAsync(ct);

            return await GetByIdAsync(provider.PublicId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating fleet provider {FleetProviderId}", id);
            return ApiResponse<FleetProviderResponse>.ServerErrorResponse("An error occurred while updating the fleet provider");
        }
    }

    public async Task<ApiResponse<bool>> DeleteAsync(Guid id, int userId, CancellationToken ct = default)
    {
        try
        {
            var provider = await _unitOfWork.FleetProviders.GetByPublicIdAsync(id, ct);
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
    private async Task<string> ValidateAsync(CreateFleetProviderRequest request, int? excludeId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return "Provider name is required";

        var name = request.Name.Trim();
        var duplicate = await _unitOfWork.FleetProviders.Query()
            .AnyAsync(x => x.Name == name && (!excludeId.HasValue || x.Id != excludeId.Value), ct);

        return duplicate ? "A fleet provider with this name already exists" : null;
    }

    // Shared projection. Must stay an expression tree (not a method) so EF can
    // translate it into the SELECT instead of pulling rows into memory.
    private static readonly Expression<Func<FleetProviderEntity, FleetProviderResponse>> Project = x => new FleetProviderResponse
    {
        Id = x.PublicId,
        Name = x.Name,
        ContactPerson = x.ContactPerson,
        Phone = x.Phone,
        Email = x.Email,
        Notes = x.Notes,
        VehicleCount = x.Vehicles.Count,
    };
}
