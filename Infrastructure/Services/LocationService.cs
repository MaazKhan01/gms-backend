using System;
using System.Threading;
using System.Threading.Tasks;
using Core.Common.Interfaces;
using Core.Interfaces.Repositories;
using Core.Interfaces.Services;
using Core.ViewModel.Common;
using Core.ViewModel.Location;
using Microsoft.Extensions.Logging;
using LocationEntity = DomainPersistence.Entities.Location;

namespace Infrastructure.Services
{
    public class LocationService(IUnitOfWork _unitOfWork, ILogger<LocationService> _logger, ICurrentUser _currentUser) : ILocationService
    {
        public async Task<ApiResponse<LocationResponse>> CreateLocationAsync(CreateLocationDto request, CancellationToken ct)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Latitude) || string.IsNullOrWhiteSpace(request.Longitude))
                    return ApiResponse<LocationResponse>.ErrorResponse("Latitude and longitude are required.");

                var location = new LocationEntity
                {
                    Id = Guid.NewGuid(),
                    Latitude = request.Latitude,
                    Longitude = request.Longitude,
                    Address = request.Address,
                };
                location.SetCreationAudit(_currentUser.UserId);

                await _unitOfWork.Locations.AddAsync(location, ct);
                await _unitOfWork.SaveChangesAsync(ct);

                var response = new LocationResponse
                {
                    Id = location.Id,
                    Latitude = location.Latitude,
                    Longitude = location.Longitude,
                    Address = location.Address,
                };

                return ApiResponse<LocationResponse>.SuccessResponse(response, "Location created successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating location");
                return ApiResponse<LocationResponse>.ServerErrorResponse("An error occurred while creating the location.");
            }
        }
    }
}
