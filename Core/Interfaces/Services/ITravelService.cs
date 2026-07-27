using Core.ViewModel.Common;
using Core.ViewModel.Travel;

namespace Core.Interfaces.Services;

public interface ITravelService
{
    Task<ApiResponse<List<IdNameDto>>> GetFlightTypesAsync(CancellationToken ct = default);
    Task<ApiResponse<List<IdNameDto>>> GetFlightClassesAsync(CancellationToken ct = default);
    Task<ApiResponse<List<IdNameDto>>> GetRoomTypesAsync(CancellationToken ct = default);
    Task<ApiResponse<List<HotelDto>>> GetHotelsAsync(CancellationToken ct = default);
    Task<ApiResponse<List<LocationDto>>> GetLocationsAsync(CancellationToken ct = default);
    Task<ApiResponse<List<IdNameDto>>> GetVehicleTypesAsync(CancellationToken ct = default);
    Task<ApiResponse<List<IdNameDto>>> GetDriversAsync(CancellationToken ct = default);
    Task<ApiResponse<List<AirportDto>>> GetAirportsAsync(CancellationToken ct = default);
    Task<ApiResponse<GuestTravelResponse>> GetGuestTravelAsync(Guid guestId, CancellationToken ct = default);

    // Per-event booking lists (one per travel tab).
    Task<ApiResponse<List<EventFlightRow>>> GetEventFlightsAsync(Guid eventId, CancellationToken ct = default);
    Task<ApiResponse<List<EventAccommodationRow>>> GetEventAccommodationsAsync(Guid eventId, CancellationToken ct = default);
    Task<ApiResponse<List<EventTransportRow>>> GetEventTransportsAsync(Guid eventId, CancellationToken ct = default);
    Task<ApiResponse<bool>> SaveGuestTravelAsync(Guid guestId, GuestTravelRequest request, int userId, CancellationToken ct = default);

    // Create wizard-dropdown lookup records.
    Task<ApiResponse<IdNameDto>> CreateFlightTypeAsync(CreateNamedLookupRequest request, int userId, CancellationToken ct = default);
    Task<ApiResponse<IdNameDto>> CreateFlightClassAsync(CreateNamedLookupRequest request, int userId, CancellationToken ct = default);
    Task<ApiResponse<IdNameDto>> CreateRoomTypeAsync(CreateNamedLookupRequest request, int userId, CancellationToken ct = default);
    Task<ApiResponse<HotelDto>> CreateHotelAsync(CreateHotelRequest request, int userId, CancellationToken ct = default);
    Task<ApiResponse<IdNameDto>> CreateVehicleTypeAsync(CreateNamedLookupRequest request, int userId, CancellationToken ct = default);
    Task<ApiResponse<AirportDto>> CreateAirportAsync(CreateAirportRequest request, int userId, CancellationToken ct = default);
    Task<ApiResponse<LocationDto>> CreateLocationAsync(LocationRequest request, int userId, CancellationToken ct = default);
    Task<ApiResponse<LocationDto>> UpdateLocationAsync(Guid id, LocationRequest request, int userId, CancellationToken ct = default);
}
