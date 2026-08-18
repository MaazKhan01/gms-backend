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
    IGenericRepository<EventGuest> EventGuests { get; }
    IGenericRepository<GuestSession> GuestSessions { get; }
    IGenericRepository<Role> Roles { get; }
    IGenericRepository<Permission> Permissions { get; }
    IGenericRepository<RolePermission> RolePermissions { get; }
    IGenericRepository<OtpVerification> OtpVerifications { get; }

    // Auth tokens
    IGenericRepository<UserRefreshToken> UserRefreshTokens { get; }

    // GMS — Events
    IGenericRepository<Event> Events { get; }
    IGenericRepository<EventType> EventTypes { get; }
    IGenericRepository<Session> Sessions { get; }

    // GMS — Bulk import jobs (Events/Guests, processed by Hangfire)
    IGenericRepository<ImportBatch> ImportBatches { get; }
    IGenericRepository<ImportBatchRow> ImportBatchRows { get; }

    // GMS — Guests
    IGenericRepository<Nationality> Nationalities { get; }
    IGenericRepository<InvitationTemplate> InvitationTemplates { get; }

    // GMS — Venue reference data
    IGenericRepository<VenueType> VenueTypes { get; }
    IGenericRepository<ElementType> ElementTypes { get; }

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
    // Travel — Flights / Accommodation / Transport
    IGenericRepository<Location> Locations { get; }
    IGenericRepository<Invitation> Invitations { get; }
    IGenericRepository<FlightClass> FlightClasses { get; }
    IGenericRepository<Flight> Flights { get; }
    IGenericRepository<FlightLeg> FlightLegs { get; }
    IGenericRepository<AirportData> AirportData { get; }
    IGenericRepository<AccommodationHotel> AccommodationHotels { get; }
    IGenericRepository<AccommodationRoomType> AccommodationRoomTypes { get; }
    IGenericRepository<Accommodation> Accommodations { get; }
    IGenericRepository<EventHotelContract> EventHotelContracts { get; }
    IGenericRepository<HotelRoomInventory> HotelRoomInventories { get; }
    IGenericRepository<Transport> Transports { get; }
    IGenericRepository<VehicleType> VehicleTypes { get; }
    IGenericRepository<Vehicle> Vehicles { get; }
    IGenericRepository<FleetProvider> FleetProviders { get; }
    IGenericRepository<DriverProfile> DriverProfiles { get; }
    IGenericRepository<TransportStatusHistory> TransportStatusHistories { get; }
    IGenericRepository<Organization> Organizations { get; }

    // Per-event service catalog + guest grades
    IGenericRepository<Service> Services { get; }
    IGenericRepository<ServiceLevel> ServiceLevels { get; }
    IGenericRepository<ServiceLevelService> ServiceLevelServices { get; }
    IGenericRepository<GuestServiceEntry> GuestServiceEntries { get; }

    // Notifications
    IGenericRepository<Notification> Notifications { get; }

    // VIP guest app
    IGenericRepository<SupportMessage> SupportMessages { get; }
    IGenericRepository<SupportConversation> SupportConversations { get; }
    IGenericRepository<Device> Devices { get; }

    // Audit
    IGenericRepository<UserLoginLog> UserLoginLogs { get; }
    IGenericRepository<SystemErrorLog> SystemErrorLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task BeginTransactionAsync();
    Task CommitTransactionAsync();
    Task RollbackTransactionAsync();
}
