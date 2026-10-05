using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Core.ViewModel.Common;
using Core.ViewModel.Group;

namespace Core.Interfaces.Services;

public interface IGroupService
{
    Task<ApiResponse<List<GroupResponse>>> GetAllAsync(CancellationToken ct = default);
    Task<ApiResponse<GroupResponse>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ApiResponse<GroupResponse>> CreateAsync(CreateGroupRequest request, int userId, CancellationToken ct = default);
    /// <summary>Renaming also re-mirrors EventGuest.Subgroup on every member, so
    /// the roster screens that still group by that string follow the rename.</summary>
    Task<ApiResponse<GroupResponse>> UpdateAsync(Guid id, UpdateGroupRequest request, int userId, CancellationToken ct = default);
    Task<ApiResponse<bool>> DeleteAsync(Guid id, int userId, CancellationToken ct = default);
}
