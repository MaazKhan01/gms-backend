using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Core.ViewModel.Common;
using Core.ViewModel.Department;

namespace Core.Interfaces.Services;

public interface IDepartmentService
{
    Task<ApiResponse<List<DepartmentResponse>>> GetAllAsync(CancellationToken ct = default);
    Task<ApiResponse<DepartmentResponse>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ApiResponse<DepartmentResponse>> CreateAsync(CreateDepartmentRequest request, int userId, CancellationToken ct = default);
    Task<ApiResponse<DepartmentResponse>> UpdateAsync(Guid id, UpdateDepartmentRequest request, int userId, CancellationToken ct = default);
    Task<ApiResponse<bool>> DeleteAsync(Guid id, int userId, CancellationToken ct = default);
}
