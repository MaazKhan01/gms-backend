using System.Collections.Generic;
using Core.ViewModel.Common;
using Core.ViewModel.Lookup;

namespace Core.Interfaces.Services;

public interface ILookupService
{
    /// <summary>Code-defined guest option sets (tier, type, statuses) — no DB access.</summary>
    ApiResponse<Dictionary<string, List<LookupEnumOption>>> GetGuestEnums();

    /// <summary>Driver engagement types (fixed / open) — no DB access.</summary>
    ApiResponse<List<EnumIntOption>> GetDriverTypes();

    /// <summary>Transfer lifecycle statuses (Transports.TripStatus) — no DB access.</summary>
    ApiResponse<List<LookupEnumOption>> GetTransportStatuses();

    /// <summary>Flight directions (Flights.FlightType) — no DB access.</summary>
    ApiResponse<List<LookupEnumOption>> GetFlightTypes();
}
