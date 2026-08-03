using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Storage;
using Core.Interfaces.Repositories;
using DomainPersistence.Entities;
using Infrastructure.Services;

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
        EventTypes = new GenericRepository<EventType>(_context);
        Sessions = new GenericRepository<Session>(_context);
        ImportBatches = new GenericRepository<ImportBatch>(_context);
        ImportBatchRows = new GenericRepository<ImportBatchRow>(_context);
        AccountRequests = new GenericRepository<AccountRequest>(_context);
        UserModuleGrants = new GenericRepository<UserModuleGrant>(_context);

        Venues = new GenericRepository<Venue>(_context);
        VenueBoxes = new GenericRepository<VenueBox>(_context);
        VenueBlocks = new GenericRepository<VenueBlock>(_context);
        VenueLayouts = new GenericRepository<VenueLayout>(_context);
        VenueLayoutProps = new GenericRepository<VenueLayoutProp>(_context);
        SeatProperties = new GenericRepository<SeatProperties>(_context);
        Seatings = new GenericRepository<Seating>(_context);
        SeatAssigns = new GenericRepository<SeatAssign>(_context);

        Meetings = new GenericRepository<Meeting>(_context);
        Locations = new GenericRepository<Location>(_context);
        Invitations = new GenericRepository<Invitation>(_context);
        FlightClasses = new GenericRepository<FlightClass>(_context);
        Flights = new GenericRepository<Flight>(_context);
        FlightLegs = new GenericRepository<FlightLeg>(_context);
        AirportData = new GenericRepository<AirportData>(_context);
        AccommodationHotels = new GenericRepository<AccommodationHotel>(_context);
        AccommodationRoomTypes = new GenericRepository<AccommodationRoomType>(_context);
        Accommodations = new GenericRepository<Accommodation>(_context);
        Transports = new GenericRepository<Transport>(_context);
        VehicleTypes = new GenericRepository<VehicleType>(_context);
        Vehicles = new GenericRepository<Vehicle>(_context);
        DriverProfiles = new GenericRepository<DriverProfile>(_context);
        TransportStatusHistories = new GenericRepository<TransportStatusHistory>(_context);
        GuestDriverAssignments = new GenericRepository<GuestDriverAssignment>(_context);
        Organizations = new GenericRepository<Organization>(_context);

        Guests = new GenericRepository<Guest>(_context);
        GuestSessions = new GenericRepository<GuestSession>(_context);
        Nationalities = new GenericRepository<Nationality>(_context);
        InvitationTemplates = new GenericRepository<InvitationTemplate>(_context);
        VenueTypes = new GenericRepository<VenueType>(_context);
        ElementTypes = new GenericRepository<ElementType>(_context);

        Notifications = new GenericRepository<Notification>(_context);

        GuestRefreshTokens = new GenericRepository<GuestRefreshToken>(_context);
        SupportMessages = new GenericRepository<SupportMessage>(_context);
        SupportConversations = new GenericRepository<SupportConversation>(_context);
        Devices = new GenericRepository<Device>(_context);

        UserLoginLogs = new GenericRepository<UserLoginLog>(_context);
        SystemErrorLogs = new GenericRepository<SystemErrorLog>(_context);
    }

    public IGenericRepository<User> Users { get; private set; }
    public IGenericRepository<Guest> Guests { get; private set; }
    public IGenericRepository<GuestSession> GuestSessions { get; private set; }
    public IGenericRepository<Venue> Venues { get; private set; }
    public IGenericRepository<VenueBox> VenueBoxes { get; private set; }
    public IGenericRepository<VenueBlock> VenueBlocks { get; private set; }
    public IGenericRepository<VenueLayout> VenueLayouts { get; private set; }
    public IGenericRepository<VenueLayoutProp> VenueLayoutProps { get; private set; }
    public IGenericRepository<SeatProperties> SeatProperties { get; private set; }
    public IGenericRepository<Seating> Seatings { get; private set; }
    public IGenericRepository<SeatAssign> SeatAssigns { get; private set; }
    public IGenericRepository<Meeting> Meetings { get; private set; }
    public IGenericRepository<Location> Locations { get; private set; }
    public IGenericRepository<Invitation> Invitations { get; private set; }
    public IGenericRepository<FlightClass> FlightClasses { get; private set; }
    public IGenericRepository<Flight> Flights { get; private set; }
    public IGenericRepository<FlightLeg> FlightLegs { get; private set; }
    public IGenericRepository<AirportData> AirportData { get; private set; }
    public IGenericRepository<AccommodationHotel> AccommodationHotels { get; private set; }
    public IGenericRepository<AccommodationRoomType> AccommodationRoomTypes { get; private set; }
    public IGenericRepository<Accommodation> Accommodations { get; private set; }
    public IGenericRepository<Transport> Transports { get; private set; }
    public IGenericRepository<VehicleType> VehicleTypes { get; private set; }
    public IGenericRepository<Vehicle> Vehicles { get; private set; }
    public IGenericRepository<DriverProfile> DriverProfiles { get; private set; }
    public IGenericRepository<TransportStatusHistory> TransportStatusHistories { get; private set; }
    public IGenericRepository<GuestDriverAssignment> GuestDriverAssignments { get; private set; }
    public IGenericRepository<Organization> Organizations { get; private set; }
    public IGenericRepository<Nationality> Nationalities { get; private set; }
    public IGenericRepository<InvitationTemplate> InvitationTemplates { get; private set; }
    public IGenericRepository<VenueType> VenueTypes { get; private set; }
    public IGenericRepository<ElementType> ElementTypes { get; private set; }
    public IGenericRepository<Role> Roles { get; private set; }
    public IGenericRepository<Permission> Permissions { get; private set; }
    public IGenericRepository<RolePermission> RolePermissions { get; private set; }
    public IGenericRepository<OtpVerification> OtpVerifications { get; private set; }
    public IGenericRepository<UserRefreshToken> UserRefreshTokens { get; private set; }
    public IGenericRepository<Event> Events { get; private set; }
    public IGenericRepository<EventType> EventTypes { get; private set; }
    public IGenericRepository<ImportBatch> ImportBatches { get; private set; }
    public IGenericRepository<ImportBatchRow> ImportBatchRows { get; private set; }
    public IGenericRepository<Session> Sessions { get; private set; }
    public IGenericRepository<AccountRequest> AccountRequests { get; private set; }
    public IGenericRepository<UserModuleGrant> UserModuleGrants { get; private set; }
    public IGenericRepository<Notification> Notifications { get; private set; }
    public IGenericRepository<GuestRefreshToken> GuestRefreshTokens { get; private set; }
    public IGenericRepository<SupportMessage> SupportMessages { get; private set; }
    public IGenericRepository<SupportConversation> SupportConversations { get; private set; }
    public IGenericRepository<Device> Devices { get; private set; }
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
