using Core.ViewModel.Common;
using Core.ViewModel.Travel;

namespace Core.Interfaces.Services;

public interface ITravelService
{
    Task<ApiResponse<List<IdNameDto>>> GetFlightClassesAsync(CancellationToken ct = default);
    Task<ApiResponse<List<IdNameDto>>> GetRoomTypesAsync(CancellationToken ct = default);
    Task<ApiResponse<List<HotelDto>>> GetHotelsAsync(CancellationToken ct = default);
    Task<ApiResponse<List<LocationDto>>> GetLocationsAsync(CancellationToken ct = default);
    Task<ApiResponse<List<IdNameDto>>> GetVehicleTypesAsync(CancellationToken ct = default);
    Task<ApiResponse<List<IdNameDto>>> GetDriversAsync(CancellationToken ct = default);
    Task<ApiResponse<List<AirportDto>>> GetAirportsAsync(CancellationToken ct = default);
    // bookingId narrows the prefill to one specific Flight/Accommodation/Transport
    // (Services' per-booking Edit); omit it for the wizard's "most recent of each".
    Task<ApiResponse<GuestTravelResponse>> GetGuestTravelAsync(Guid guestId, Guid? bookingId = null, CancellationToken ct = default);

    // Per-event booking lists (one per travel tab).
    Task<ApiResponse<PaginatedResponse<EventFlightRow>>> GetEventFlightsAsync(Guid eventId, PagedRequest request, CancellationToken ct = default);
    Task<ApiResponse<PaginatedResponse<EventAccommodationRow>>> GetEventAccommodationsAsync(Guid eventId, PagedRequest request, CancellationToken ct = default);
    Task<ApiResponse<PaginatedResponse<EventTransportRow>>> GetEventTransportsAsync(Guid eventId, PagedRequest request, CancellationToken ct = default);

    // Read-only arrivals/departures board. Its own method (and endpoint) so it
    // can be permission-gated separately from the Flights tab later on.
    Task<ApiResponse<PaginatedResponse<ArrivalDepartureRow>>> GetEventArrivalsDeparturesAsync(Guid eventId, ArrivalsDeparturesRequest request, CancellationToken ct = default);
    Task<ApiResponse<bool>> SaveGuestTravelAsync(Guid guestId, GuestTravelRequest request, int userId, CancellationToken ct = default);

    // Remove one specific booking (a guest may have several of a kind).
    Task<ApiResponse<bool>> DeleteFlightAsync(Guid id, CancellationToken ct = default);
    Task<ApiResponse<bool>> DeleteAccommodationAsync(Guid id, CancellationToken ct = default);
    Task<ApiResponse<bool>> DeleteTransportAsync(Guid id, CancellationToken ct = default);

    // Create wizard-dropdown lookup records.
    Task<ApiResponse<IdNameDto>> CreateFlightClassAsync(CreateNamedLookupRequest request, int userId, CancellationToken ct = default);
    Task<ApiResponse<IdNameDto>> CreateRoomTypeAsync(CreateNamedLookupRequest request, int userId, CancellationToken ct = default);
    Task<ApiResponse<HotelDto>> CreateHotelAsync(CreateHotelRequest request, int userId, CancellationToken ct = default);
    /// <summary>Same request shape as create — a hotel's name, address, image and
    /// location are all editable. Address stays required: the VIP app shows it.</summary>
    Task<ApiResponse<HotelDto>> UpdateHotelAsync(Guid id, CreateHotelRequest request, int userId, CancellationToken ct = default);
    Task<ApiResponse<IdNameDto>> CreateVehicleTypeAsync(CreateNamedLookupRequest request, int userId, CancellationToken ct = default);
    Task<ApiResponse<AirportDto>> CreateAirportAsync(CreateAirportRequest request, int userId, CancellationToken ct = default);
    Task<ApiResponse<LocationDto>> CreateLocationAsync(LocationRequest request, int userId, CancellationToken ct = default);
    Task<ApiResponse<LocationDto>> UpdateLocationAsync(Guid id, LocationRequest request, int userId, CancellationToken ct = default);
}
