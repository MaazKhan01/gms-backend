using Core.Constants;
using Core.Interfaces.Services;
using Core.ViewModel.Common;
using Core.ViewModel.Lookup;
using DomainPersistence.Enums;

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

    public ApiResponse<List<EnumIntOption>> GetDriverTypes()
        => ApiResponse<List<EnumIntOption>>.SuccessResponse(new List<EnumIntOption>
        {
            new((int)DriverType.Fixed, "Fixed", "ثابت"),
            new((int)DriverType.Open,  "Open",  "مفتوح"),
        });

    // Code order = lifecycle order: pending → assigned → arrived → in-progress → completed.
    public ApiResponse<List<LookupEnumOption>> GetTransportStatuses()
        => ApiResponse<List<LookupEnumOption>>.SuccessResponse(new List<LookupEnumOption>
        {
            new(TransportStatuses.New,        "New",         "جديد"),
            new(TransportStatuses.Pending,    "Pending",     "قيد الانتظار"),
            new(TransportStatuses.Assigned,   "Assigned",    "تم التعيين"),
            new(TransportStatuses.InProgress, "In Progress", "قيد التنفيذ"),
            new(TransportStatuses.Arrived,    "Arrived",     "وصل"),
            new(TransportStatuses.InTransit,  "In Transit",  "في الطريق"),
            new(TransportStatuses.Completed,  "Completed",   "مكتمل"),
        });
}
