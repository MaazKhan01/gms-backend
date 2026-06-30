using System;
using System.Threading;
using System.Threading.Tasks;
using DomainPersistence.Entities;

namespace Core.Interfaces.Repositories;

public interface IUnitOfWork : IDisposable
{
    // Auth / RBAC
    IGenericRepository<User> Users { get; }
    IGenericRepository<Role> Roles { get; }
    IGenericRepository<Permission> Permissions { get; }
    IGenericRepository<RolePermission> RolePermissions { get; }
    IGenericRepository<OtpVerification> OtpVerifications { get; }

    // Auth tokens
    IGenericRepository<UserRefreshToken> UserRefreshTokens { get; }

    // GMS — Events
    IGenericRepository<Event> Events { get; }
    IGenericRepository<Session> Sessions { get; }

    // GMS — Account requests
    IGenericRepository<AccountRequest> AccountRequests { get; }

    // Notifications
    IGenericRepository<Notification> Notifications { get; }

    // Audit
    IGenericRepository<UserLoginLog> UserLoginLogs { get; }
    IGenericRepository<SystemErrorLog> SystemErrorLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task BeginTransactionAsync();
    Task CommitTransactionAsync();
    Task RollbackTransactionAsync();
}
