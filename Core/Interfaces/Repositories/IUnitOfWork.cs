using System;
using System.Threading;
using System.Threading.Tasks;
using DomainPersistence.Entities;

namespace Core.Interfaces.Repositories;

public interface IUnitOfWork : IDisposable
{
    // Auth / RBAC
    IGenericRepository<User> Users { get; }
    IGenericRepository<Guest> Guests { get; }
    IGenericRepository<GuestSession> GuestSessions { get; }
    IGenericRepository<Role> Roles { get; }
    IGenericRepository<Permission> Permissions { get; }
    IGenericRepository<RolePermission> RolePermissions { get; }
    IGenericRepository<OtpVerification> OtpVerifications { get; }

    // Auth tokens
    IGenericRepository<UserRefreshToken> UserRefreshTokens { get; }

    // GMS — Events
    IGenericRepository<Event> Events { get; }
    IGenericRepository<Session> Sessions { get; }

    // GMS — Guests
    IGenericRepository<Nationality> Nationalities { get; }
    IGenericRepository<InvitationTemplate> InvitationTemplates { get; }

    // GMS — Lookups (generic reference data)
    IGenericRepository<LookupCategory> LookupCategories { get; }
    IGenericRepository<LookupItem> LookupItems { get; }

    // GMS — Account requests
    IGenericRepository<AccountRequest> AccountRequests { get; }

    // GMS — Per-user module access
    IGenericRepository<UserModuleGrant> UserModuleGrants { get; }
    //Venue
    IGenericRepository<Venue> Venues { get; }
    IGenericRepository<VenueBox> VenueBoxes { get; }
    IGenericRepository<VenueBlock> VenueBlocks { get; }
    IGenericRepository<VenueLayout> VenueLayouts { get; }
    IGenericRepository<VenueLayoutProp> VenueLayoutProps { get; }
    IGenericRepository<SeatProperties> SeatProperties { get; }
    IGenericRepository<Seating> Seatings { get; }
    IGenericRepository<SeatAssign> SeatAssigns { get; }
    //Meeting
    IGenericRepository<Meeting> Meetings { get; }
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
