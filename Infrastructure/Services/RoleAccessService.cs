using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Core.Common;
using Core.Common.Interfaces;
using Core.Interfaces.Repositories;
using Core.Interfaces.Services;
using Core.ViewModel.Common;
using Core.ViewModel.RoleAccess;
using DomainPersistence.Entities;

namespace Infrastructure.Services;

/// <summary>
/// Role-level access. The Permissions table is the only source of truth for what
/// the portal can show — one row per menu or submenu, nested by ParentId — and
/// RolePermissions is the only source of truth for who may see and change it.
/// </summary>
public class RoleAccessService(IUnitOfWork _unitOfWork, ICurrentUser _currentUser) : IRoleAccessService
{
    // ── Reads ────────────────────────────────────────────────────────────

    public async Task<ApiResponse<List<PermissionNode>>> GetPermissionTreeAsync(CancellationToken ct = default)
    {
        var all = await ActivePermissionsAsync(ct);
        return ApiResponse<List<PermissionNode>>.SuccessResponse(BuildTree(all, null));
    }

    public async Task<ApiResponse<RoleAccessResponse>> GetRoleAccessAsync(Guid roleId, CancellationToken ct = default)
    {
        var role = await _unitOfWork.Roles.QueryNoTracking().FirstOrDefaultAsync(r => r.PublicId == roleId, ct);
        if (role == null)
            return ApiResponse<RoleAccessResponse>.NotFoundResponse("Role not found");

        var all = await ActivePermissionsAsync(ct);
        var access = await AccessByPermissionIdAsync(role.Id, ct);

        return ApiResponse<RoleAccessResponse>.SuccessResponse(new RoleAccessResponse
        {
            RoleId = role.PublicId,
            RoleName = role.Name,
            RoleCode = role.Code,
            // Every row, flags included — the admin screen needs the ones it can turn ON.
            Permissions = BuildAccessTree(all, null, access, pruneInaccessible: false),
        });
    }

    public async Task<ApiResponse<MyAccessResponse>> GetMyAccessAsync(CancellationToken ct = default)
    {
        if (!_currentUser.IsAuthenticated)
            return ApiResponse<MyAccessResponse>.UnauthorizedResponse("Not authenticated");

        var role = _currentUser.RoleId == 0
            ? null
            : await _unitOfWork.Roles.QueryNoTracking().FirstOrDefaultAsync(r => r.Id == _currentUser.RoleId, ct);

        // A user with no role sees nothing, rather than everything.
        if (role == null)
            return ApiResponse<MyAccessResponse>.SuccessResponse(new MyAccessResponse());

        var all = await ActivePermissionsAsync(ct);
        var access = await AccessByPermissionIdAsync(role.Id, ct);

        // DMS has no per-service permission rows (GMS parents one row per Service
        // under Services to gate individual tabs, kept in step by its
        // ServiceCatalogService). Nothing to filter out of the navigation here —
        // if that mechanism is ported later, this is where those rows get hidden
        // from the sidebar while staying tickable on the Role Access screen.
        return ApiResponse<MyAccessResponse>.SuccessResponse(new MyAccessResponse
        {
            RoleId = role.PublicId,
            RoleName = role.Name,
            RoleCode = role.Code,
            Permissions = BuildAccessTree(all, null, access, pruneInaccessible: true),
        });
    }

    public async Task<Dictionary<string, (bool Read, bool Write)>> GetRoleAccessMapAsync(int roleId, CancellationToken ct = default)
    {
        if (roleId == 0) return new Dictionary<string, (bool, bool)>();

        // Nullable IsActive on purpose: the Permission query filter can leave the
        // join empty for a soft-deleted row, and a non-null bool would not materialise.
        var rows = await _unitOfWork.RolePermissions.QueryNoTracking()
            .Where(a => a.RoleId == roleId && (a.CanRead || a.CanWrite))
            .Select(a => new { a.Permission.Code, IsActive = (bool?)a.Permission.IsActive, a.CanRead, a.CanWrite })
            .ToListAsync(ct);

        return rows
            .Where(r => r.IsActive == true && !string.IsNullOrWhiteSpace(r.Code))
            .GroupBy(r => r.Code)
            .ToDictionary(
                g => g.Key,
                g => (Read: g.Any(x => x.CanRead), Write: g.Any(x => x.CanWrite)),
                StringComparer.OrdinalIgnoreCase);
    }

    // ── Write ────────────────────────────────────────────────────────────

    public async Task<ApiResponse<RoleAccessResponse>> SetRoleAccessAsync(
        Guid roleId, SetRoleAccessRequest request, int adminId, CancellationToken ct = default)
    {
        var role = await _unitOfWork.Roles.Query().FirstOrDefaultAsync(r => r.PublicId == roleId, ct);
        if (role == null)
            return ApiResponse<RoleAccessResponse>.NotFoundResponse("Role not found");

        var items = request?.Items ?? new List<RoleAccessItem>();

        // Resolve the public ids in one round trip and reject unknown ones outright —
        // a silently dropped id would look like a saved permission.
        var requestedIds = items.Select(i => i.PermissionId).Distinct().ToList();
        var known = await _unitOfWork.Permissions.QueryNoTracking()
            .Where(p => requestedIds.Contains(p.PublicId))
            .Select(p => new { p.Id, p.PublicId })
            .ToListAsync(ct);

        var unknown = requestedIds.Except(known.Select(p => p.PublicId)).ToList();
        if (unknown.Count > 0)
            return ApiResponse<RoleAccessResponse>.ErrorResponse(
                $"Unknown permission ids: {string.Join(", ", unknown)}");

        var internalIdByPublic = known.ToDictionary(p => p.PublicId, p => p.Id);

        // Last item wins if the same permission appears twice, so a duplicated
        // payload cannot produce two rows for one (role, permission).
        var desired = items
            .GroupBy(i => i.PermissionId)
            .ToDictionary(g => internalIdByPublic[g.Key], g => g.Last());

        // ponytail: no "write implies read" fixup on save — the UI ticks both
        // boxes, and AccessEvaluator already lets a write claim satisfy a read.

        var existing = await _unitOfWork.RolePermissions.Query()
            .Where(a => a.RoleId == role.Id)
            .ToListAsync(ct);
        var existingByPermission = existing.ToDictionary(a => a.PermissionId);

        // A revoke clears the flags rather than deleting the row. Deletes here are
        // soft (AuditInterceptor), and a soft-deleted row would still occupy the
        // unique (RoleId, PermissionId) slot and block the next grant.
        foreach (var row in existing)
        {
            desired.TryGetValue(row.PermissionId, out var item);
            var read = item?.Read == true;
            var write = item?.Write == true;
            if (row.CanRead == read && row.CanWrite == write) continue;

            row.CanRead = read;
            row.CanWrite = write;
            row.SetUpdateAudit(adminId);
            _unitOfWork.RolePermissions.Update(row);
        }

        foreach (var (permissionId, item) in desired)
        {
            // Both flags false with no existing row means nothing to store.
            if (existingByPermission.ContainsKey(permissionId) || (!item.Read && !item.Write)) continue;

            await _unitOfWork.RolePermissions.AddAsync(new RolePermission
            {
                RoleId = role.Id,
                PermissionId = permissionId,
                CanRead = item.Read,
                CanWrite = item.Write,
                CreatedBy = adminId,
                CreatedAt = DateTime.UtcNow,
            }, ct);
        }

        await _unitOfWork.SaveChangesAsync(ct);

        var saved = await GetRoleAccessAsync(roleId, ct);
        return saved.Success
            ? ApiResponse<RoleAccessResponse>.SuccessResponse(saved.Data, "Role access updated")
            : saved;
    }

    // ── Tree building ────────────────────────────────────────────────────

    private async Task<List<Permission>> ActivePermissionsAsync(CancellationToken ct) =>
        await _unitOfWork.Permissions.QueryNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.SortOrder).ThenBy(p => p.Name)
            .ToListAsync(ct);

    private async Task<Dictionary<int, RolePermission>> AccessByPermissionIdAsync(int roleId, CancellationToken ct)
    {
        var rows = await _unitOfWork.RolePermissions.QueryNoTracking()
            .Where(a => a.RoleId == roleId)
            .ToListAsync(ct);
        // The unique index gives one row per permission, but group anyway so a
        // stray duplicate left by a hand-run script cannot throw here.
        return rows.GroupBy(a => a.PermissionId).ToDictionary(g => g.Key, g => g.First());
    }

    private static List<PermissionNode> BuildTree(List<Permission> all, int? parentId) =>
        all.Where(p => p.ParentId == parentId)
           .OrderBy(p => p.SortOrder)
           .Select(p => new PermissionNode
           {
               PermissionId = p.PublicId,
               Code = p.Code,
               Name = p.Name,
               NameAr = p.NameAr,
               Icon = p.Icon,
               Path = p.Path,
               SortOrder = p.SortOrder,
               Children = BuildTree(all, p.Id),
           })
           .ToList();

    /// <summary>
    /// Builds the access tree depth first. With <paramref name="pruneInaccessible"/>
    /// a node survives when the role can read it OR any descendant survived — that
    /// is the "show the parent for the sake of its accessible submenu" rule, and it
    /// needs no access record on the parent itself. A parent kept only for its
    /// children carries read=false, so it stays a header and never becomes a page
    /// grant, and the children it exposes are only the permitted ones.
    /// </summary>
    private static List<PermissionAccessNode> BuildAccessTree(
        List<Permission> all, int? parentId,
        Dictionary<int, RolePermission> access,
        bool pruneInaccessible)
    {
        var result = new List<PermissionAccessNode>();

        foreach (var p in all.Where(x => x.ParentId == parentId).OrderBy(x => x.SortOrder))
        {
            var children = BuildAccessTree(all, p.Id, access, pruneInaccessible);
            access.TryGetValue(p.Id, out var row);
            var read = row?.CanRead == true;
            var write = row?.CanWrite == true;

            if (pruneInaccessible && !read && !write && children.Count == 0)
                continue;

            result.Add(new PermissionAccessNode
            {
                PermissionId = p.PublicId,
                Code = p.Code,
                Name = p.Name,
                NameAr = p.NameAr,
                Icon = p.Icon,
                Path = p.Path,
                SortOrder = p.SortOrder,
                Read = read,
                Write = write,
                Children = children,
            });
        }

        return result;
    }
}
