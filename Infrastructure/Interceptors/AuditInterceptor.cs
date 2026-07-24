using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using DomainPersistence.Entities;

namespace Infrastructure.Interceptors;

/// <summary>
/// Interceptor that automatically populates audit fields for entities inheriting from AuditableEntity
/// </summary>
public class AuditInterceptor : SaveChangesInterceptor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditInterceptor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ApplyAuditInformation(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ApplyAuditInformation(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void ApplyAuditInformation(DbContext context)
    {
        if (context == null) return;

        var currentUserId = GetCurrentUserId();
        var entries = context.ChangeTracker.Entries<AuditEntity>();

        foreach (var entry in entries)
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    // Set creation audit fields
                    entry.Entity.CreatedAt = DateTime.UtcNow;
                    entry.Entity.CreatedBy = currentUserId;
                    entry.Entity.IsDeleted = false; // Ensure new entities are not marked as deleted
                    break;

                case EntityState.Modified:
                    // Set update audit fields
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                    entry.Entity.UpdatedBy = currentUserId;

                    // Prevent overwriting creation fields
                    entry.Property(x => x.CreatedAt).IsModified = false;
                    entry.Property(x => x.CreatedBy).IsModified = false;

                    // Prevent overwriting deletion fields if not being deleted
                    if (entry.Entity.IsDeleted != true)
                    {
                        entry.Property(x => x.DeletedAt).IsModified = false;
                        entry.Property(x => x.DeletedBy).IsModified = false;
                    }
                    break;

                case EntityState.Deleted:
                    // Implement soft delete - convert Delete to Modified
                    entry.State = EntityState.Modified;
                    entry.Entity.IsDeleted = true;
                    entry.Entity.DeletedAt = DateTime.UtcNow;
                    entry.Entity.DeletedBy = currentUserId;

                    // Also set update audit fields
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                    entry.Entity.UpdatedBy = currentUserId;
                    break;
            }
        }
    }

    /// <summary>
    /// Gets the current user ID from the HTTP context claims
    /// </summary>
    private int? GetCurrentUserId()
    {
        var userIdClaim = _httpContextAccessor.HttpContext?.User
            ?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userIdClaim))
        {
            // Try alternative claim types
            userIdClaim = _httpContextAccessor.HttpContext?.User
                ?.FindFirst("sub")?.Value;
        }

        if (string.IsNullOrEmpty(userIdClaim))
        {
            userIdClaim = _httpContextAccessor.HttpContext?.User
                ?.FindFirst("userId")?.Value;
        }

        return int.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}
