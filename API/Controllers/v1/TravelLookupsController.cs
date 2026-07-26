using System.Collections.Generic;
using System.Linq;
using Core.Authorization;
using Core.Common;
using Core.Common.Interfaces;
using Core.Interfaces.Repositories;
using Core.Interfaces.Services;
using Core.ViewModel.Common;
using Core.ViewModel.Lookup;
using Core.ViewModel.Travel_Logistics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API.Controllers.v1
{
    // Guest-travel wizard reference data (flight types/classes, room types,
    // hotels, pickup/dropoff locations). Thin adapters over the existing,
    // already-seeded generic Lookup system (and the real Location table for
    // "locations") — exposed at the dedicated per-entity routes the frontend
    // expects, so no new tables/migrations are needed for this fix.
    [Route("api/v1/travel/lookups")]
    [Authorize]
    [ApiVersion("1.0")]
    public class TravelLookupsController(ILookupService _lookupService, IUnitOfWork _unitOfWork, ICurrentUser _currentUser) : Controllers.BaseApiController
    {
        [HttpGet("flight-types")]
        public Task<IActionResult> GetFlightTypes(CancellationToken ct) => GetNamed("FLIGHT_TYPE", ct);

        [HttpPost("flight-types")]
        [HasPermission(PermissionCodes.TravelManage)]
        public Task<IActionResult> CreateFlightType([FromBody] CreateNamedLookupDto request, CancellationToken ct) => CreateNamed("FLIGHT_TYPE", request.Name, ct);

        [HttpGet("flight-classes")]
        public Task<IActionResult> GetFlightClasses(CancellationToken ct) => GetNamed("FLIGHT_CLASS", ct);

        [HttpPost("flight-classes")]
        [HasPermission(PermissionCodes.TravelManage)]
        public Task<IActionResult> CreateFlightClass([FromBody] CreateNamedLookupDto request, CancellationToken ct) => CreateNamed("FLIGHT_CLASS", request.Name, ct);

        [HttpGet("room-types")]
        public Task<IActionResult> GetRoomTypes(CancellationToken ct) => GetNamed("ROOM_TYPE", ct);

        [HttpPost("room-types")]
        [HasPermission(PermissionCodes.TravelManage)]
        public Task<IActionResult> CreateRoomType([FromBody] CreateNamedLookupDto request, CancellationToken ct) => CreateNamed("ROOM_TYPE", request.Name, ct);

        [HttpGet("hotels")]
        public async Task<IActionResult> GetHotels(CancellationToken ct)
        {
            var result = await _lookupService.GetItemsByCategoryCodeAsync("HOTEL", false, ct);
            if (!result.Success) return ToResponse(result);

            var mapped = result.Data.Select(ToHotelResponse).ToList();
            return ToResponse(ApiResponse<List<HotelLookupResponse>>.SuccessResponse(mapped));
        }

        [HttpPost("hotels")]
        [HasPermission(PermissionCodes.TravelManage)]
        public async Task<IActionResult> CreateHotel([FromBody] CreateHotelDto request, CancellationToken ct)
        {
            var itemResult = await _lookupService.CreateItemAsync(new LookupItemRequest
            {
                CategoryCode = "HOTEL",
                Code = Slugify(request.Name),
                Name = request.Name,
                NameAr = request.Name,
                Metadata = string.IsNullOrWhiteSpace(request.Address)
                    ? new Dictionary<string, string>()
                    : new Dictionary<string, string> { ["address"] = request.Address },
            }, _currentUser.UserId, ct);

            if (!itemResult.Success)
                return ToResponse(itemResult);

            return ToResponse(ApiResponse<HotelLookupResponse>.SuccessResponse(ToHotelResponse(itemResult.Data)));
        }

        // Real Location rows (lat/lng/address) — created via POST /v1/Location
        // (e.g. the map picker); this is the list side for the travel wizard's
        // pickup/dropoff dropdown.
        [HttpGet("locations")]
        public async Task<IActionResult> GetLocations(CancellationToken ct)
        {
            var locations = await _unitOfWork.Locations.Query()
                .Select(l => new LocationLookupResponse { Id = l.Id, Address = l.Address, Latitude = l.Latitude, Longitude = l.Longitude })
                .ToListAsync(ct);

            return ToResponse(ApiResponse<List<LocationLookupResponse>>.SuccessResponse(locations));
        }

        private async Task<IActionResult> GetNamed(string categoryCode, CancellationToken ct)
        {
            var result = await _lookupService.GetItemsByCategoryCodeAsync(categoryCode, false, ct);
            if (!result.Success) return ToResponse(result);

            var mapped = result.Data.Select(i => new NamedLookupResponse { Id = i.Id, Name = i.Name }).ToList();
            return ToResponse(ApiResponse<List<NamedLookupResponse>>.SuccessResponse(mapped));
        }

        private async Task<IActionResult> CreateNamed(string categoryCode, string name, CancellationToken ct)
        {
            var result = await _lookupService.CreateItemAsync(new LookupItemRequest
            {
                CategoryCode = categoryCode,
                Code = Slugify(name),
                Name = name,
                NameAr = name,
            }, _currentUser.UserId, ct);

            if (!result.Success) return ToResponse(result);

            return ToResponse(ApiResponse<NamedLookupResponse>.SuccessResponse(new NamedLookupResponse { Id = result.Data.Id, Name = result.Data.Name }));
        }

        private static HotelLookupResponse ToHotelResponse(LookupItemResponse i) => new()
        {
            Id = i.Id,
            Name = i.Name,
            Address = i.Metadata != null && i.Metadata.TryGetValue("address", out var addr) ? addr : null,
        };

        private static string Slugify(string s) => (s ?? "").Trim().ToUpperInvariant().Replace(" ", "_");
    }
}
