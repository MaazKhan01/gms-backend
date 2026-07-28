using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Core.Interfaces.Repositories;
using Core.Interfaces.Services;
using Core.ViewModel.Common;
using Core.ViewModel.Organization;
using DomainPersistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

// Organisations + their address. The address is a Location row of type
// "organization" written in the same unit of work, so an organisation is never
// persisted without its pin (and vice versa).
public class OrganizationService(IUnitOfWork _unitOfWork, ILogger<OrganizationService> _logger) : IOrganizationService
{
    // Location.Type value this module owns. Lowercase to match the existing
    // hotel/venue/airport rows.
    private const string LocationType = "organization";

    // Arabic script plus whitespace, digits and punctuation — the Arabic name is
    // optional, but when supplied it must not contain Latin letters.
    private static readonly Regex ArabicOnly = new(
        @"^[؀-ۿݐ-ݿࢠ-ࣿﭐ-﷿ﹰ-﻿\s\d\p{P}]+$",
        RegexOptions.Compiled);

    private static readonly Regex HasArabicLetter = new(@"[؀-ۿ]", RegexOptions.Compiled);

    public async Task<ApiResponse<List<OrganizationResponse>>> GetAllAsync(CancellationToken ct = default)
    {
        var data = await _unitOfWork.Organizations.Query()
            .OrderBy(x => x.Name)
            .Select(Project)
            .ToListAsync(ct);

        return ApiResponse<List<OrganizationResponse>>.SuccessResponse(data);
    }

    public async Task<ApiResponse<OrganizationResponse>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var org = await _unitOfWork.Organizations.Query()
            .Where(x => x.PublicId == id)
            .Select(Project)
            .FirstOrDefaultAsync(ct);

        return org == null
            ? ApiResponse<OrganizationResponse>.NotFoundResponse("Organization not found")
            : ApiResponse<OrganizationResponse>.SuccessResponse(org);
    }

    public async Task<ApiResponse<OrganizationResponse>> CreateAsync(
        CreateOrganizationRequest request, int userId, CancellationToken ct = default)
    {
        try
        {
            var error = await ValidateAsync(request, null, ct);
            if (error != null)
                return ApiResponse<OrganizationResponse>.ErrorResponse(error);

            var org = new Organization
            {
                Name = request.Name.Trim(),
                NameAr = string.IsNullOrWhiteSpace(request.NameAr) ? null : request.NameAr.Trim(),
                Code = request.Code.Trim(),
            };

            if (request.Location != null)
            {
                var location = new Location
                {
                    Latitude = request.Location.Latitude.Trim(),
                    Longitude = request.Location.Longitude.Trim(),
                    Address = string.IsNullOrWhiteSpace(request.Location.Address) ? null : request.Location.Address.Trim(),
                    Type = LocationType,
                };
                location.SetCreationAudit(userId);
                await _unitOfWork.Locations.AddAsync(location, ct);
                // Saved before the org so EF has assigned the location's int Id.
                await _unitOfWork.SaveChangesAsync(ct);
                org.LocationId = location.Id;
            }

            org.SetCreationAudit(userId);
            await _unitOfWork.Organizations.AddAsync(org, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            return await GetByIdAsync(org.PublicId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating organization");
            return ApiResponse<OrganizationResponse>.ServerErrorResponse("An error occurred while creating the organization");
        }
    }

    public async Task<ApiResponse<OrganizationResponse>> UpdateAsync(
        Guid id, UpdateOrganizationRequest request, int userId, CancellationToken ct = default)
    {
        try
        {
            var org = await _unitOfWork.Organizations.GetByPublicIdAsync(id, ct);
            if (org == null)
                return ApiResponse<OrganizationResponse>.NotFoundResponse("Organization not found");

            var error = await ValidateAsync(request, org.Id, ct);
            if (error != null)
                return ApiResponse<OrganizationResponse>.ErrorResponse(error);

            org.Name = request.Name.Trim();
            org.NameAr = string.IsNullOrWhiteSpace(request.NameAr) ? null : request.NameAr.Trim();
            org.Code = request.Code.Trim();

            if (request.Location != null)
            {
                // Move the existing pin when there is one, otherwise attach a new
                // one — an organisation keeps at most a single address row.
                var location = org.LocationId.HasValue
                    ? await _unitOfWork.Locations.Query().FirstOrDefaultAsync(l => l.Id == org.LocationId.Value, ct)
                    : null;

                if (location == null)
                {
                    location = new Location { Type = LocationType };
                    location.SetCreationAudit(userId);
                    await _unitOfWork.Locations.AddAsync(location, ct);
                }
                else
                {
                    location.SetUpdateAudit(userId);
                }

                location.Latitude = request.Location.Latitude.Trim();
                location.Longitude = request.Location.Longitude.Trim();
                location.Address = string.IsNullOrWhiteSpace(request.Location.Address) ? null : request.Location.Address.Trim();

                await _unitOfWork.SaveChangesAsync(ct);
                org.LocationId = location.Id;
            }

            org.SetUpdateAudit(userId);
            _unitOfWork.Organizations.Update(org);
            await _unitOfWork.SaveChangesAsync(ct);

            return await GetByIdAsync(org.PublicId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating organization {OrganizationId}", id);
            return ApiResponse<OrganizationResponse>.ServerErrorResponse("An error occurred while updating the organization");
        }
    }

    public async Task<ApiResponse<bool>> DeleteAsync(Guid id, int userId, CancellationToken ct = default)
    {
        try
        {
            var org = await _unitOfWork.Organizations.GetByPublicIdAsync(id, ct);
            if (org == null)
                return ApiResponse<bool>.NotFoundResponse("Organization not found");

            org.MarkAsDeleted(userId);
            _unitOfWork.Organizations.Update(org);

            // Retire the address row with it — nothing else points at an
            // organisation's location.
            if (org.LocationId.HasValue)
            {
                var location = await _unitOfWork.Locations.Query()
                    .FirstOrDefaultAsync(l => l.Id == org.LocationId.Value, ct);
                if (location != null)
                {
                    location.MarkAsDeleted(userId);
                    _unitOfWork.Locations.Update(location);
                }
            }

            await _unitOfWork.SaveChangesAsync(ct);
            return ApiResponse<bool>.SuccessResponse(true, "Organization deleted");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting organization {OrganizationId}", id);
            return ApiResponse<bool>.ServerErrorResponse("An error occurred while deleting the organization");
        }
    }

    // Returns an error message, or null when the request is valid.
    private async Task<string> ValidateAsync(CreateOrganizationRequest request, int? excludeId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return "Organization name is required";
        if (string.IsNullOrWhiteSpace(request.Code))
            return "Organization code is required";

        if (!string.IsNullOrWhiteSpace(request.NameAr))
        {
            var nameAr = request.NameAr.Trim();
            if (!ArabicOnly.IsMatch(nameAr) || !HasArabicLetter.IsMatch(nameAr))
                return "Arabic name must contain Arabic characters only";
        }

        if (request.Location != null &&
            (string.IsNullOrWhiteSpace(request.Location.Latitude) || string.IsNullOrWhiteSpace(request.Location.Longitude)))
            return "Location latitude and longitude are required";

        var code = request.Code.Trim();
        var duplicate = await _unitOfWork.Organizations.Query()
            .AnyAsync(x => x.Code == code && (!excludeId.HasValue || x.Id != excludeId.Value), ct);
        if (duplicate)
            return "An organization with this code already exists";

        return null;
    }

    // Shared projection. Must stay an expression tree (not a method) so EF can
    // translate it into the SELECT instead of pulling rows into memory.
    private static readonly Expression<Func<Organization, OrganizationResponse>> Project = x => new OrganizationResponse
    {
        Id = x.PublicId,
        Name = x.Name,
        NameAr = x.NameAr,
        Code = x.Code,
        LocationId = x.Location != null ? (Guid?)x.Location.PublicId : null,
        Address = x.Location != null ? x.Location.Address : null,
        Latitude = x.Location != null ? x.Location.Latitude : null,
        Longitude = x.Location != null ? x.Location.Longitude : null,
    };
}
