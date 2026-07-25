using Core.Constants;
using Core.Interfaces.Services;
using Core.ViewModel.Common;
using Core.ViewModel.Lookup;

namespace Infrastructure.Services;

// Only surviving lookup concern: code-defined guest enums (tier/type/statuses).
// All DB-backed reference data now lives in dedicated tables (FlightType,
// AccommodationHotel, VenueType, ElementType, ...) with their own endpoints.
public class LookupService : ILookupService
{
    public ApiResponse<Dictionary<string, List<LookupEnumOption>>> GetGuestEnums()
    {
        var sets = GuestEnumCatalog.All.ToDictionary(kv => kv.Key, kv => kv.Value);
        return ApiResponse<Dictionary<string, List<LookupEnumOption>>>.SuccessResponse(sets);
    }
}
