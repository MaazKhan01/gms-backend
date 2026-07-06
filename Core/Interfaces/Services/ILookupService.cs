using System.Collections.Generic;
using Core.ViewModel.Common;
using Core.ViewModel.Lookup;

namespace Core.Interfaces.Services;

public interface ILookupService
{
    Task<ApiResponse<List<LookupCategoryResponse>>> GetCategoriesAsync(CancellationToken ct = default);

    /// <summary>Returns items for a category, addressed by the category's code (e.g. "AIRPORT").</summary>
    Task<ApiResponse<List<LookupItemResponse>>> GetItemsByCategoryCodeAsync(string categoryCode, bool includeInactive = false, CancellationToken ct = default);

    Task<ApiResponse<LookupItemResponse>> GetItemByIdAsync(Guid id, CancellationToken ct = default);
    Task<ApiResponse<LookupItemResponse>> CreateItemAsync(LookupItemRequest request, Guid userId, CancellationToken ct = default);
    Task<ApiResponse<LookupItemResponse>> UpdateItemAsync(LookupItemRequest request, Guid userId, CancellationToken ct = default);
    Task<ApiResponse<bool>> DeleteItemAsync(Guid id, Guid userId, CancellationToken ct = default);
}
