using System.Text.Json;
using Core.Interfaces.Repositories;
using Core.Interfaces.Services;
using Core.ViewModel.Common;
using Core.ViewModel.Lookup;
using DomainPersistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

public class LookupService(
    IUnitOfWork _unitOfWork,
    ILogger<LookupService> _logger) : ILookupService
{
    public async Task<ApiResponse<List<LookupCategoryResponse>>> GetCategoriesAsync(CancellationToken ct = default)
    {
        try
        {
            var categories = await _unitOfWork.LookupCategories.Query()
                .Where(c => c.IsActive)
                .OrderBy(c => c.Name)
                .Select(c => new LookupCategoryResponse
                {
                    Id = c.Id,
                    Code = c.Code,
                    Name = c.Name,
                    NameAr = c.NameAr,
                    Description = c.Description,
                    IsActive = c.IsActive,
                    IsSystem = c.IsSystem,
                    ItemCount = c.Items.Count(i => i.IsActive),
                })
                .ToListAsync(ct);

            return ApiResponse<List<LookupCategoryResponse>>.SuccessResponse(categories);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving lookup categories");
            return ApiResponse<List<LookupCategoryResponse>>.ServerErrorResponse("An error occurred while retrieving lookup categories");
        }
    }

    public async Task<ApiResponse<List<LookupItemResponse>>> GetItemsByCategoryCodeAsync(string categoryCode, bool includeInactive = false, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(categoryCode))
                return ApiResponse<List<LookupItemResponse>>.ErrorResponse("Category code is required");

            var code = categoryCode.Trim().ToUpper();
            var category = await _unitOfWork.LookupCategories.Query()
                .FirstOrDefaultAsync(c => c.Code == code, ct);

            if (category == null)
                return ApiResponse<List<LookupItemResponse>>.NotFoundResponse($"Lookup category '{categoryCode}' not found");

            var query = _unitOfWork.LookupItems.Query().Where(i => i.CategoryId == category.Id);
            if (!includeInactive)
                query = query.Where(i => i.IsActive);

            var items = await query
                .OrderBy(i => i.SortOrder).ThenBy(i => i.Name)
                .ToListAsync(ct);

            var mapped = items.Select(i => ToResponse(i, category.Code)).ToList();
            return ApiResponse<List<LookupItemResponse>>.SuccessResponse(mapped);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving lookup items for category {CategoryCode}", categoryCode);
            return ApiResponse<List<LookupItemResponse>>.ServerErrorResponse("An error occurred while retrieving lookup items");
        }
    }

    public async Task<ApiResponse<LookupItemResponse>> GetItemByIdAsync(Guid id, CancellationToken ct = default)
    {
        try
        {
            var item = await _unitOfWork.LookupItems.Query()
                .Include(i => i.Category)
                .FirstOrDefaultAsync(i => i.Id == id, ct);

            if (item == null)
                return ApiResponse<LookupItemResponse>.NotFoundResponse("Lookup item not found");

            return ApiResponse<LookupItemResponse>.SuccessResponse(ToResponse(item, item.Category?.Code));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving lookup item {ItemId}", id);
            return ApiResponse<LookupItemResponse>.ServerErrorResponse("An error occurred while retrieving the lookup item");
        }
    }

    public async Task<ApiResponse<LookupItemResponse>> CreateItemAsync(LookupItemRequest request, Guid userId, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return ApiResponse<LookupItemResponse>.ErrorResponse("Name is required");

            var category = await ResolveCategoryAsync(request.CategoryCode, ct);
            if (category == null)
                return ApiResponse<LookupItemResponse>.ErrorResponse("A valid categoryCode is required");

            // Enforce unique code within the category (when a code is provided)
            if (!string.IsNullOrWhiteSpace(request.Code))
            {
                var code = request.Code.Trim();
                var exists = await _unitOfWork.LookupItems.Query()
                    .AnyAsync(i => i.CategoryId == category.Id && i.Code == code, ct);
                if (exists)
                    return ApiResponse<LookupItemResponse>.ConflictResponse($"An item with code '{code}' already exists in this category");
            }

            var item = new LookupItem
            {
                Id = Guid.NewGuid(),
                CategoryId = category.Id,
                Code = request.Code?.Trim(),
                Name = request.Name.Trim(),
                NameAr = request.NameAr?.Trim(),
                SortOrder = request.SortOrder,
                IsActive = request.IsActive,
                Metadata = SerializeMetadata(request.Metadata),
                CreatedAt = DateTime.UtcNow,
            };
            item.SetCreationAudit(userId);

            await _unitOfWork.LookupItems.AddAsync(item, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            return ApiResponse<LookupItemResponse>.SuccessResponse(ToResponse(item, category.Code), "Lookup item created successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating lookup item");
            return ApiResponse<LookupItemResponse>.ServerErrorResponse("An error occurred while creating the lookup item");
        }
    }

    public async Task<ApiResponse<LookupItemResponse>> UpdateItemAsync(LookupItemRequest request, Guid userId, CancellationToken ct = default)
    {
        try
        {
            if (request.Id == null || request.Id == Guid.Empty)
                return ApiResponse<LookupItemResponse>.ErrorResponse("Item Id is required");

            if (string.IsNullOrWhiteSpace(request.Name))
                return ApiResponse<LookupItemResponse>.ErrorResponse("Name is required");

            var item = await _unitOfWork.LookupItems.Query()
                .Include(i => i.Category)
                .FirstOrDefaultAsync(i => i.Id == request.Id.Value, ct);

            if (item == null)
                return ApiResponse<LookupItemResponse>.NotFoundResponse("Lookup item not found");

            // Enforce unique code within the category (excluding this item)
            if (!string.IsNullOrWhiteSpace(request.Code))
            {
                var code = request.Code.Trim();
                var clash = await _unitOfWork.LookupItems.Query()
                    .AnyAsync(i => i.CategoryId == item.CategoryId && i.Code == code && i.Id != item.Id, ct);
                if (clash)
                    return ApiResponse<LookupItemResponse>.ConflictResponse($"An item with code '{code}' already exists in this category");
            }

            item.Code = request.Code?.Trim();
            item.Name = request.Name.Trim();
            item.NameAr = request.NameAr?.Trim();
            item.SortOrder = request.SortOrder;
            item.IsActive = request.IsActive;
            item.Metadata = SerializeMetadata(request.Metadata);
            item.SetUpdateAudit(userId);

            _unitOfWork.LookupItems.Update(item);
            await _unitOfWork.SaveChangesAsync(ct);

            return ApiResponse<LookupItemResponse>.SuccessResponse(ToResponse(item, item.Category?.Code), "Lookup item updated successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating lookup item {ItemId}", request.Id);
            return ApiResponse<LookupItemResponse>.ServerErrorResponse("An error occurred while updating the lookup item");
        }
    }

    public async Task<ApiResponse<bool>> DeleteItemAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        try
        {
            var item = await _unitOfWork.LookupItems.FindFirstOrDefaultAsync(i => i.Id == id);
            if (item == null)
                return ApiResponse<bool>.NotFoundResponse("Lookup item not found");

            item.MarkAsDeleted(userId);
            _unitOfWork.LookupItems.Update(item);
            await _unitOfWork.SaveChangesAsync(ct);

            return ApiResponse<bool>.SuccessResponse(true, "Lookup item deleted successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting lookup item {ItemId}", id);
            return ApiResponse<bool>.ServerErrorResponse("An error occurred while deleting the lookup item");
        }
    }

    // ── helpers ─────────────────────────────────────────────────────────────

    private async Task<LookupCategory> ResolveCategoryAsync(string categoryCode, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(categoryCode)) return null;
        var code = categoryCode.Trim().ToUpper();
        return await _unitOfWork.LookupCategories.Query().FirstOrDefaultAsync(c => c.Code == code, ct);
    }

    private static string SerializeMetadata(Dictionary<string, string> metadata)
    {
        if (metadata == null || metadata.Count == 0) return null;
        // Drop empty values so we don't persist noise
        var clean = metadata
            .Where(kv => !string.IsNullOrWhiteSpace(kv.Value))
            .ToDictionary(kv => kv.Key, kv => kv.Value);
        return clean.Count == 0 ? null : JsonSerializer.Serialize(clean);
    }

    private static Dictionary<string, string> DeserializeMetadata(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new Dictionary<string, string>();
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
        }
        catch
        {
            return new Dictionary<string, string>();
        }
    }

    private static LookupItemResponse ToResponse(LookupItem item, string categoryCode) => new()
    {
        Id = item.Id,
        CategoryId = item.CategoryId,
        CategoryCode = categoryCode,
        Code = item.Code,
        Name = item.Name,
        NameAr = item.NameAr,
        SortOrder = item.SortOrder,
        IsActive = item.IsActive,
        Metadata = DeserializeMetadata(item.Metadata),
        CreatedAt = item.CreatedAt,
    };
}
