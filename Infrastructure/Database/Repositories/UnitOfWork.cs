using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Storage;
using Core.Interfaces.Repositories;
using DomainPersistence.Entities;

namespace Infrastructure.Database.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly ApplicationDBContext _context;
    private IDbContextTransaction _transaction;

    public UnitOfWork(ApplicationDBContext context)
    {
        _context = context;

        Users = new GenericRepository<User>(_context);
        Roles = new GenericRepository<Role>(_context);
        Permissions = new GenericRepository<Permission>(_context);
        RolePermissions = new GenericRepository<RolePermission>(_context);
        OtpVerifications = new GenericRepository<OtpVerification>(_context);
        UserRefreshTokens = new GenericRepository<UserRefreshToken>(_context);

        Events = new GenericRepository<Event>(_context);
        Sessions = new GenericRepository<Session>(_context);
        AccountRequests = new GenericRepository<AccountRequest>(_context);
        UserModuleGrants = new GenericRepository<UserModuleGrant>(_context);

        Guests = new GenericRepository<Guest>(_context);
        GuestSessions = new GenericRepository<GuestSession>(_context);
        Nationalities = new GenericRepository<Nationality>(_context);
        InvitationTemplates = new GenericRepository<InvitationTemplate>(_context);

        Notifications = new GenericRepository<Notification>(_context);

        UserLoginLogs = new GenericRepository<UserLoginLog>(_context);
        SystemErrorLogs = new GenericRepository<SystemErrorLog>(_context);
    }

    public IGenericRepository<User> Users { get; private set; }
    public IGenericRepository<Guest> Guests { get; private set; }
    public IGenericRepository<GuestSession> GuestSessions { get; private set; }
    public IGenericRepository<Nationality> Nationalities { get; private set; }
    public IGenericRepository<InvitationTemplate> InvitationTemplates { get; private set; }
    public IGenericRepository<Role> Roles { get; private set; }
    public IGenericRepository<Permission> Permissions { get; private set; }
    public IGenericRepository<RolePermission> RolePermissions { get; private set; }
    public IGenericRepository<OtpVerification> OtpVerifications { get; private set; }
    public IGenericRepository<UserRefreshToken> UserRefreshTokens { get; private set; }
    public IGenericRepository<Event> Events { get; private set; }
    public IGenericRepository<Session> Sessions { get; private set; }
    public IGenericRepository<AccountRequest> AccountRequests { get; private set; }
    public IGenericRepository<UserModuleGrant> UserModuleGrants { get; private set; }
    public IGenericRepository<Notification> Notifications { get; private set; }
    public IGenericRepository<UserLoginLog> UserLoginLogs { get; private set; }
    public IGenericRepository<SystemErrorLog> SystemErrorLogs { get; private set; }

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
        => await _context.SaveChangesAsync(ct);

    public async Task BeginTransactionAsync()
        => _transaction = await _context.Database.BeginTransactionAsync();

    public async Task CommitTransactionAsync()
    {
        try
        {
            await _context.SaveChangesAsync();
            await _transaction.CommitAsync();
        }
        catch
        {
            await RollbackTransactionAsync();
            throw;
        }
        finally
        {
            if (_transaction != null)
            {
                await _transaction.DisposeAsync();
                _transaction = null;
            }
        }
    }

    public async Task RollbackTransactionAsync()
    {
        if (_transaction != null)
        {
            await _transaction.RollbackAsync();
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public void Dispose()
    {
        _transaction?.Dispose();
        _context?.Dispose();
    }
}
