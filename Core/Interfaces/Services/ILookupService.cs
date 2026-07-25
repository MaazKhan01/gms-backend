using System.Collections.Generic;
using Core.ViewModel.Common;
using Core.ViewModel.Lookup;

namespace Core.Interfaces.Services;

public interface ILookupService
{
    /// <summary>Code-defined guest option sets (tier, type, statuses) — no DB access.</summary>
    ApiResponse<Dictionary<string, List<LookupEnumOption>>> GetGuestEnums();
}
