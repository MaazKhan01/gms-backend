using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace DomainPersistence.Entities;

/// <summary>
/// GMS domain model â€” kept in a partial class so the generated/boilerplate
/// context stays untouched. New modules add their DbSets + configuration here.
/// </summary>
public partial class ApplicationDBContext
{
    public virtual DbSet<Event> Events { get; set; }
    public virtual DbSet<EventType> EventTypes { get; set; }
    public virtual DbSet<Session> Sessions { get; set; }
    public virtual DbSet<ImportBatch> ImportBatches { get; set; }
    public virtual DbSet<ImportBatchRow> ImportBatchRows { get; set; }
    public virtual DbSet<AccountRequest> AccountRequests { get; set; }
    public virtual DbSet<UserModuleGrant> UserModuleGrants { get; set; }
    public virtual DbSet<Guest> Guests { get; set; }
    public virtual DbSet<GuestSession> GuestSessions { get; set; }
    public virtual DbSet<Nationality> Nationalities { get; set; }
    public virtual DbSet<InvitationTemplate> InvitationTemplates { get; set; }
    public virtual DbSet<Meeting> Meetings { get; set; }
    public virtual DbSet<Location> Locations { get; set; }
    public virtual DbSet<Organization> Organizations { get; set; }

    // Per-event service catalog + guest grades (replaces the old hardcoded
    // Guest.Tier string). See Service.cs for why these are NOT the same concept
    // as Core.Constants.GuestServiceType.
    public virtual DbSet<Service> Services { get; set; }
    public virtual DbSet<ServiceLevel> ServiceLevels { get; set; }
    public virtual DbSet<ServiceLevelService> ServiceLevelServices { get; set; }

    // Invitation / accreditation
    public virtual DbSet<Invitation> Invitations { get; set; }

    // Flights
    public virtual DbSet<FlightClass> FlightClasses { get; set; }
    public virtual DbSet<Flight> Flights { get; set; }
    public virtual DbSet<FlightLeg> FlightLegs { get; set; }
    public virtual DbSet<AirportData> AirportData { get; set; }

    // Accommodation
    public virtual DbSet<AccommodationHotel> AccommodationHotels { get; set; }
    public virtual DbSet<AccommodationRoomType> AccommodationRoomTypes { get; set; }
    public virtual DbSet<Accommodation> Accommodations { get; set; }
    public virtual DbSet<EventHotelContract> EventHotelContracts { get; set; }
    public virtual DbSet<HotelRoomInventory> HotelRoomInventories { get; set; }

    // Transport
    public virtual DbSet<Transport> Transports { get; set; }
    public virtual DbSet<VehicleType> VehicleTypes { get; set; }
    public virtual DbSet<Vehicle> Vehicles { get; set; }
    public virtual DbSet<FleetProvider> FleetProviders { get; set; }
    public virtual DbSet<DriverProfile> DriverProfiles { get; set; }
    public virtual DbSet<TransportStatusHistory> TransportStatusHistories { get; set; }
    public virtual DbSet<GuestDriverAssignment> GuestDriverAssignments { get; set; }
    public virtual DbSet<Venue> Venues { get; set; }
    public virtual DbSet<VenueLayout> VenueLayouts { get; set; }
    public virtual DbSet<VenueLayoutProp> VenueLayoutProps { get; set; }
    public virtual DbSet<VenueBlock> VenueBlocks { get; set; }
    public virtual DbSet<SeatProperties> SeatProperties { get; set; }
    public virtual DbSet<Seating> Seatings { get; set; }
    public virtual DbSet<SeatAssign> SeatAssigns { get; set; }
    public virtual DbSet<VenueBox> VenueBoxs { get; set; }

    // VIP guest app
    public virtual DbSet<GuestRefreshToken> GuestRefreshTokens { get; set; }
    public virtual DbSet<SupportMessage> SupportMessages { get; set; }
    public virtual DbSet<SupportConversation> SupportConversations { get; set; }

    // Push notification devices — shared by every User (staff, driver, guest).
    public virtual DbSet<Device> Devices { get; set; }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AccountRequest>(a =>
        {
            a.ToTable("AccountRequests");
            a.HasKey(x => x.Id);
            a.Property(x => x.FirstName).HasMaxLength(150);
            a.Property(x => x.LastName).HasMaxLength(150);
            a.Property(x => x.Email).IsRequired().HasMaxLength(255);
            a.Property(x => x.Phone).HasMaxLength(20);
            a.Property(x => x.PasswordHash).HasMaxLength(500);
            a.Property(x => x.Note).HasMaxLength(1000);
            a.Property(x => x.RequestedRoleName).HasMaxLength(100);
            a.Property(x => x.Status).HasMaxLength(20);
            a.Property(x => x.ReviewNote).HasMaxLength(1000);
            a.Property(x => x.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            a.Property(x => x.IsDeleted).HasDefaultValueSql("((0))");
            a.HasIndex(x => x.Email);
            a.HasQueryFilter(x => x.IsDeleted == null || x.IsDeleted == false);
        });

        modelBuilder.Entity<Nationality>(n =>
        {
            n.ToTable("Nationalities");
            n.HasKey(x => x.Id);
            n.Property(x => x.Name).IsRequired().HasMaxLength(100);
            n.Property(x => x.NameAr).HasMaxLength(100);
            n.Property(x => x.Code).IsRequired().HasMaxLength(3);
            n.Property(x => x.Flag).HasMaxLength(10);
            n.HasIndex(x => x.Code).IsUnique();
        });

        modelBuilder.Entity<InvitationTemplate>(t =>
        {
            t.ToTable("InvitationTemplates");
            t.HasKey(x => x.Id);
            t.Property(x => x.Name).IsRequired().HasMaxLength(200);
            t.Property(x => x.NameAr).HasMaxLength(200);
            t.Property(x => x.Language).IsRequired().HasMaxLength(10);
            t.Property(x => x.Subject).IsRequired().HasMaxLength(500);
            t.Property(x => x.SubjectAr).HasMaxLength(500);
            t.Property(x => x.Body).HasColumnType("nvarchar(max)");
            t.Property(x => x.BodyAr).HasColumnType("nvarchar(max)");
            t.Property(x => x.TargetTiers).HasMaxLength(200);
            t.Property(x => x.Color).HasMaxLength(20);
            t.Property(x => x.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            t.Property(x => x.IsDeleted).HasDefaultValueSql("((0))");
            t.HasOne(x => x.Event)
                .WithMany()
                .HasForeignKey(x => x.EventId)
                .OnDelete(DeleteBehavior.Cascade);
            t.HasQueryFilter(x => x.IsDeleted == null || x.IsDeleted == false);
        });

        modelBuilder.Entity<Guest>(g =>
        {
            g.ToTable("Guests");
            g.HasKey(x => x.Id);
            g.Property(x => x.FirstName).IsRequired().HasMaxLength(150);
            g.Property(x => x.LastName).IsRequired().HasMaxLength(150);
            g.Property(x => x.Email).HasMaxLength(255);
            g.Property(x => x.GuestType).HasMaxLength(50);
            g.Property(x => x.Organization).HasMaxLength(300);
            g.Property(x => x.Tier).HasMaxLength(50);
            g.Property(x => x.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            g.Property(x => x.IsDeleted).HasDefaultValueSql("((0))");
            g.HasOne(x => x.Event)
                .WithMany()
                .HasForeignKey(x => x.EventId)
                .OnDelete(DeleteBehavior.Restrict);
            g.HasOne(x => x.Nationality)
                .WithMany(x => x.Guests)
                .HasForeignKey(x => x.NationalityId)
                .OnDelete(DeleteBehavior.SetNull);
            g.HasOne(x => x.OrganizationRef)
                .WithMany()
                .HasForeignKey(x => x.OrganizationId)
                .OnDelete(DeleteBehavior.SetNull);
            // SetNull, not Cascade: deleting a level must never take its guests
            // with it — they fall back to the legacy Tier string until reassigned.
            g.HasOne(x => x.ServiceLevel)
                .WithMany(x => x.Guests)
                .HasForeignKey(x => x.ServiceLevelId)
                .OnDelete(DeleteBehavior.SetNull);
            // 1:1, same convention as DriverProfile <-> User below: EF creates the
            // unique index on Guests.UserId for us from WithOne.
            g.HasOne(x => x.User)
                .WithOne(u => u.GuestProfile)
                .HasForeignKey<Guest>(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            g.HasQueryFilter(x => x.IsDeleted == null || x.IsDeleted == false);
        });

        // ── Per-event service catalog + guest grades ──────────────────────────
        // Event edge cascades (same as Session): deleting an event takes its
        // catalog with it. Everything else Restrict/SetNull per the convention
        // noted in the Venue/Seating block below.
        modelBuilder.Entity<Service>(s =>
        {
            s.ToTable("Services");
            s.HasKey(x => x.Id);
            s.Property(x => x.Name).IsRequired().HasMaxLength(200);
            s.Property(x => x.NameAr).HasMaxLength(200);
            s.Property(x => x.Description).HasMaxLength(1000);
            s.Property(x => x.FieldsSchema).HasColumnType("nvarchar(max)");
            s.Property(x => x.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            s.Property(x => x.IsDeleted).HasDefaultValueSql("((0))");
            s.HasOne(x => x.Event)
                .WithMany()
                .HasForeignKey(x => x.EventId)
                .OnDelete(DeleteBehavior.Cascade);
            s.HasQueryFilter(x => x.IsDeleted == null || x.IsDeleted == false);
        });

        modelBuilder.Entity<ServiceLevel>(sl =>
        {
            sl.ToTable("ServiceLevels");
            sl.HasKey(x => x.Id);
            sl.Property(x => x.Name).IsRequired().HasMaxLength(200);
            sl.Property(x => x.NameAr).HasMaxLength(200);
            sl.Property(x => x.Code).HasMaxLength(50);
            sl.Property(x => x.Description).HasMaxLength(1000);
            sl.Property(x => x.Color).HasMaxLength(20);
            sl.Property(x => x.RequiredGuestFieldsJson).HasColumnType("nvarchar(max)");
            sl.Property(x => x.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            sl.Property(x => x.IsDeleted).HasDefaultValueSql("((0))");
            sl.HasOne(x => x.Event)
                .WithMany()
                .HasForeignKey(x => x.EventId)
                .OnDelete(DeleteBehavior.Cascade);
            sl.HasQueryFilter(x => x.IsDeleted == null || x.IsDeleted == false);
        });

        modelBuilder.Entity<ServiceLevelService>(sls =>
        {
            sls.ToTable("ServiceLevelServices");
            sls.HasKey(x => x.Id);
            sls.Property(x => x.FieldValuesJson).HasColumnType("nvarchar(max)");
            sls.Property(x => x.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            sls.Property(x => x.IsDeleted).HasDefaultValueSql("((0))");
            sls.HasOne(x => x.ServiceLevel)
                .WithMany(x => x.Services)
                .HasForeignKey(x => x.ServiceLevelId)
                .OnDelete(DeleteBehavior.Cascade);
            // Restrict, not Cascade: ServiceLevels -> ServiceLevelServices is
            // already a cascade path from Events, and a second one via Services
            // would trip SQL Server's multiple-cascade-path rule. The service
            // delete flow detaches its join rows explicitly instead.
            sls.HasOne(x => x.Service)
                .WithMany(x => x.ServiceLevels)
                .HasForeignKey(x => x.ServiceId)
                .OnDelete(DeleteBehavior.Restrict);
            // One row per (level, service) — a level can't include the same
            // service twice with two different value sets.
            sls.HasIndex(x => new { x.ServiceLevelId, x.ServiceId }).IsUnique();
            sls.HasQueryFilter(x => x.IsDeleted == null || x.IsDeleted == false);
        });

        modelBuilder.Entity<GuestSession>(gs =>
        {
            gs.ToTable("GuestSessions");
            gs.HasKey(x => new { x.GuestId, x.SessionId });
            gs.HasOne(x => x.Guest)
                .WithMany(x => x.GuestSessions)
                .HasForeignKey(x => x.GuestId)
                .OnDelete(DeleteBehavior.Cascade);
            gs.HasOne(x => x.Session)
                .WithMany()
                .HasForeignKey(x => x.SessionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Event>(e =>
        {
            e.ToTable("Events");
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).IsRequired().HasMaxLength(300);
            e.Property(x => x.Type).HasMaxLength(50);
            e.Property(x => x.Theme).HasMaxLength(300);
            e.Property(x => x.VenueName).HasMaxLength(300);
            e.Property(x => x.Status).HasMaxLength(30);
            e.Property(x => x.AppKey).HasMaxLength(150);
            // Defaulted in the database as well as in code so rows written by
            // anything that bypasses the service layer still land valid.
            e.Property(x => x.GuestModel).HasMaxLength(20).HasDefaultValue("flexible");
            // nvarchar(max): may hold a URL or an uploaded base64 data URI.
            e.Property(x => x.ImageUrl).HasColumnType("nvarchar(max)");
            e.Property(x => x.ThemeAccent).HasMaxLength(20);
            e.Property(x => x.ThemeSecondary).HasMaxLength(20);
            e.Property(x => x.LogoDarkUrl).HasColumnType("nvarchar(max)");
            e.Property(x => x.LogoLightUrl).HasColumnType("nvarchar(max)");
            e.Property(x => x.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            e.Property(x => x.IsDeleted).HasDefaultValueSql("((0))");
            e.HasQueryFilter(x => x.IsDeleted == null || x.IsDeleted == false);
        });

        modelBuilder.Entity<Session>(s =>
        {
            s.ToTable("Sessions");
            s.HasKey(x => x.Id);
            s.Property(x => x.Title).IsRequired().HasMaxLength(300);
            s.Property(x => x.Time).HasMaxLength(10);
            s.Property(x => x.VenueName).HasMaxLength(300);
            s.Property(x => x.Room).HasMaxLength(200);
            s.Property(x => x.Speaker).HasMaxLength(200);
            s.Property(x => x.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            s.Property(x => x.IsDeleted).HasDefaultValueSql("((0))");
            s.HasOne(x => x.Event)
                .WithMany(x => x.Sessions)
                .HasForeignKey(x => x.EventId)
                .OnDelete(DeleteBehavior.Cascade);
            s.HasOne(x => x.Venue)
                .WithMany()
                .HasForeignKey(x => x.VenueId)
                .OnDelete(DeleteBehavior.Restrict);
            s.HasQueryFilter(x => x.IsDeleted == null || x.IsDeleted == false);
        });

        modelBuilder.Entity<UserModuleGrant>(g =>
        {
            g.ToTable("UserModuleGrants");
            g.HasKey(x => x.Id);
            g.Property(x => x.Module).IsRequired().HasMaxLength(50);
            g.Property(x => x.GrantedAt).HasDefaultValueSql("(sysutcdatetime())");
            g.HasIndex(x => new { x.UserId, x.Module }).IsUnique();
            g.HasOne(x => x.User)
                .WithMany(x => x.ModuleGrants)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // â”€â”€ Venue / Seating module â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        // Most relationships use Restrict to avoid SQL Server multiple-cascade-path
        // errors; the app relies on soft-delete anyway. Only the two leaf ownership
        // edges (propâ†’seats, seatingâ†’assignments) cascade.
        modelBuilder.Entity<ImportBatch>(ib =>
        {
            ib.ToTable("ImportBatches");
            ib.HasKey(x => x.Id);
            ib.Property(x => x.Kind).IsRequired().HasMaxLength(30);
            ib.Property(x => x.Status).IsRequired().HasMaxLength(30);
            ib.Property(x => x.FileUrl).HasMaxLength(1000);
            ib.Property(x => x.ErrorMessage).HasMaxLength(1000);
            ib.Property(x => x.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            ib.Property(x => x.IsDeleted).HasDefaultValueSql("((0))");
            ib.HasOne(x => x.Event).WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ImportBatchRow>(ibr =>
        {
            ibr.ToTable("ImportBatchRows");
            ibr.HasKey(x => x.Id);
            ibr.Property(x => x.Title).HasMaxLength(300);
            ibr.Property(x => x.Error).HasMaxLength(1000);
            ibr.Property(x => x.ErrorCategory).HasMaxLength(30);
            ibr.Property(x => x.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            ibr.Property(x => x.IsDeleted).HasDefaultValueSql("((0))");
            ibr.HasOne(x => x.ImportBatch).WithMany(x => x.Rows).HasForeignKey(x => x.ImportBatchId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EventType>(et2 =>
        {
            et2.ToTable("EventTypes");
            et2.HasKey(x => x.Id);
            et2.Property(x => x.Name).IsRequired().HasMaxLength(150);
            et2.Property(x => x.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            et2.Property(x => x.IsDeleted).HasDefaultValueSql("((0))");
        });

        modelBuilder.Entity<VenueType>(vt =>
        {
            vt.ToTable("VenueTypes");
            vt.HasKey(x => x.Id);
            vt.Property(x => x.Name).IsRequired().HasMaxLength(150);
            vt.Property(x => x.NameAr).HasMaxLength(150);
            vt.Property(x => x.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            vt.Property(x => x.IsDeleted).HasDefaultValueSql("((0))");
        });

        modelBuilder.Entity<ElementType>(et =>
        {
            et.ToTable("ElementTypes");
            et.HasKey(x => x.Id);
            et.Property(x => x.Code).HasMaxLength(50);
            et.Property(x => x.Name).IsRequired().HasMaxLength(150);
            et.Property(x => x.NameAr).HasMaxLength(150);
            et.Property(x => x.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            et.Property(x => x.IsDeleted).HasDefaultValueSql("((0))");
        });

        modelBuilder.Entity<Venue>(v =>
        {
            v.ToTable("Venues");
            v.HasKey(x => x.Id);
            v.Property(x => x.Name).IsRequired().HasMaxLength(300);
            v.Property(x => x.Color).HasMaxLength(20);
            v.Property(x => x.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            v.Property(x => x.IsDeleted).HasDefaultValueSql("((0))");
            v.HasOne(x => x.Type)
                .WithMany()
                .HasForeignKey(x => x.TypeId)
                .OnDelete(DeleteBehavior.Restrict);
            v.HasOne(x => x.Location)
                .WithMany()
                .HasForeignKey(x => x.LocationId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Location>(l =>
        {
            l.ToTable("Locations");
            l.HasKey(x => x.Id);
            l.Property(x => x.Type).HasMaxLength(50);
        });

        modelBuilder.Entity<Organization>(o =>
        {
            o.ToTable("Organizations");
            o.HasKey(x => x.Id);
            o.Property(x => x.Name).IsRequired().HasMaxLength(300);
            o.Property(x => x.NameAr).HasMaxLength(300);
            o.Property(x => x.Code).IsRequired().HasMaxLength(50);
            o.HasIndex(x => x.Code).IsUnique().HasFilter("[IsDeleted] = 0");
            o.Property(x => x.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            o.Property(x => x.IsDeleted).HasDefaultValueSql("((0))");
            // Restrict: an organisation's address row must be detached before the
            // location itself can go, so a delete never silently orphans the org.
            o.HasOne(x => x.Location).WithMany().HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<Meeting>(v =>
        {
            v.ToTable("Meetings");
            v.HasKey(x => x.Id);
            v.Property(x => x.Name).IsRequired().HasMaxLength(300);
            v.Property(x => x.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            v.Property(x => x.IsDeleted).HasDefaultValueSql("((0))");
            v.HasMany(x => x.Guests)
            .WithMany()
            .UsingEntity(j => j.ToTable("MeetingGuests"));
        });

        modelBuilder.Entity<VenueBox>(b =>
        {
            b.ToTable("VenueBoxes");
            b.HasKey(x => x.Id);
            b.Property(x => x.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            b.Property(x => x.IsDeleted).HasDefaultValueSql("((0))");
            b.HasOne(x => x.Venue)
                .WithMany(x => x.VenueBoxes)
                .HasForeignKey(x => x.VenueId)
                .OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.Event)
                .WithMany()
                .HasForeignKey(x => x.EventId)
                .OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.Session)
                .WithMany()
                .HasForeignKey(x => x.SessionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<VenueBlock>(bl =>
        {
            bl.ToTable("VenueBlocks");
            bl.HasKey(x => x.Id);
            bl.Property(x => x.Type).HasMaxLength(50);
            bl.Property(x => x.Label).HasMaxLength(200);
            bl.Property(x => x.Category).HasMaxLength(100);
            bl.Property(x => x.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            bl.Property(x => x.IsDeleted).HasDefaultValueSql("((0))");
            bl.HasOne(x => x.VenueBox)
                .WithMany(x => x.Blocks)
                .HasForeignKey(x => x.VenueBoxId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<VenueLayout>(l =>
        {
            l.ToTable("VenueLayouts");
            l.HasKey(x => x.Id);
            l.Property(x => x.Type).HasMaxLength(50);
            l.Property(x => x.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            l.Property(x => x.IsDeleted).HasDefaultValueSql("((0))");
            l.HasOne(x => x.VenueBox)
                .WithMany(x => x.VenueLayouts)
                .HasForeignKey(x => x.VenueBoxId)
                .OnDelete(DeleteBehavior.Restrict);
            l.HasOne(x => x.Block)
                .WithMany(x => x.VenueLayouts)
                .HasForeignKey(x => x.VenueBlockId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<VenueLayoutProp>(p =>
        {
            p.ToTable("VenueLayoutProps");
            p.HasKey(x => x.Id);
            p.Property(x => x.Code).HasMaxLength(100);
            p.Property(x => x.Label).HasMaxLength(300);
            p.Property(x => x.Color).HasMaxLength(20);
            p.Property(x => x.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            p.Property(x => x.IsDeleted).HasDefaultValueSql("((0))");
            p.HasOne(x => x.Layout)
                .WithMany(x => x.VenueLayoutProps)
                .HasForeignKey(x => x.VenueLayoutId)
                .OnDelete(DeleteBehavior.Restrict);
            p.HasOne(x => x.Block)
                .WithMany(x => x.Props)
                .HasForeignKey(x => x.VenueBlockId)
                .OnDelete(DeleteBehavior.Restrict);
            p.HasMany(x => x.Seats)
                .WithOne()
                .HasForeignKey("VenueLayoutPropId")
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SeatProperties>(sp =>
        {
            sp.ToTable("SeatProperties");
            sp.HasKey(x => x.Id);
            sp.Property(x => x.Code).HasMaxLength(100);
            sp.Property(x => x.Color).HasMaxLength(20);
            sp.Property(x => x.Status).HasMaxLength(30);
            sp.Property(x => x.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            sp.Property(x => x.IsDeleted).HasDefaultValueSql("((0))");
        });

        modelBuilder.Entity<Seating>(se =>
        {
            se.ToTable("Seatings");
            se.HasKey(x => x.Id);
            se.Property(x => x.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            se.Property(x => x.IsDeleted).HasDefaultValueSql("((0))");
            se.HasOne(x => x.Event).WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Restrict);
            se.HasOne(x => x.Venue).WithMany().HasForeignKey(x => x.VenueId).OnDelete(DeleteBehavior.Restrict);
            se.HasOne(x => x.VenueBox).WithMany().HasForeignKey(x => x.VenueBoxId).OnDelete(DeleteBehavior.Restrict);
            se.HasOne(x => x.Session).WithMany().HasForeignKey(x => x.EventSessionId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SeatAssign>(sa =>
        {
            sa.ToTable("SeatAssigns");
            sa.HasKey(x => x.Id);
            sa.Property(x => x.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            sa.Property(x => x.IsDeleted).HasDefaultValueSql("((0))");
            sa.HasOne(x => x.Seating).WithMany(x => x.SeatsDetail).HasForeignKey(x => x.SeatingId).OnDelete(DeleteBehavior.Cascade);
            sa.HasOne(x => x.Seat).WithMany().HasForeignKey(x => x.SeatId).OnDelete(DeleteBehavior.Restrict);
            sa.HasOne(x => x.Guest).WithMany().HasForeignKey(x => x.GuestId).OnDelete(DeleteBehavior.Restrict);
            sa.HasIndex(x => new { x.SeatingId, x.SeatId }).IsUnique();
        });

        // â”€â”€ VIP guest app â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        modelBuilder.Entity<GuestRefreshToken>(t =>
        {
            t.ToTable("GuestRefreshTokens");
            t.HasKey(x => x.Id);
            t.Property(x => x.Jti).IsRequired().HasMaxLength(100);
            t.Property(x => x.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            t.HasIndex(x => x.Jti);
            t.HasOne(x => x.Guest).WithMany().HasForeignKey(x => x.GuestId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SupportMessage>(m =>
        {
            m.ToTable("SupportMessages");
            m.HasKey(x => x.Id);
            m.Property(x => x.Body).IsRequired().HasColumnType("nvarchar(max)");
            m.Property(x => x.AttachmentUrl).HasMaxLength(500);
            m.Property(x => x.AttachmentType).HasMaxLength(100);
            m.Property(x => x.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            m.Property(x => x.IsDeleted).HasDefaultValueSql("((0))");
            m.HasIndex(x => new { x.UserId, x.SentAt });
            m.HasIndex(x => new { x.ConversationId, x.SentAt });
            m.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            // Restrict, not SetNull/Cascade: Users -> SupportConversations (Cascade) ->
            // SupportMessages.ConversationId would otherwise be a second cascade path
            // alongside the direct Users -> SupportMessages.UserId cascade above — the
            // same multiple-cascade-path problem as SenderUserId below. The direct
            // UserId cascade already cleans up a guest's messages on delete regardless.
            m.HasOne(x => x.Conversation).WithMany().HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Restrict);
            // Restrict, not SetNull: Users -> SupportMessages.SenderUserId would otherwise
            // be a second cascade path alongside Users -> SupportMessages.UserId /
            // Users -> SupportConversations.*UserId -> SupportMessages.ConversationId,
            // which SQL Server rejects as a multiple cascade path. Same convention as
            // the Venue/Seating module above — the app relies on soft-delete anyway,
            // so Users are never hard-deleted in practice.
            m.HasOne(x => x.SenderUser).WithMany().HasForeignKey(x => x.SenderUserId).OnDelete(DeleteBehavior.Restrict);
            m.HasQueryFilter(x => x.IsDeleted == null || x.IsDeleted == false);
        });

        modelBuilder.Entity<SupportConversation>(c =>
        {
            c.ToTable("SupportConversations");
            c.HasKey(x => x.Id);
            // "AdminSupport"/"Open" mirror Core.Constants.SupportChatTypes/Statuses —
            // DomainPersistence doesn't reference Core, so the literals are duplicated here on purpose.
            c.Property(x => x.Type).IsRequired().HasMaxLength(20).HasDefaultValue("AdminSupport");
            c.Property(x => x.Status).IsRequired().HasMaxLength(20).HasDefaultValue("Open");
            c.Property(x => x.LastMessagePreview).HasMaxLength(300);
            c.Property(x => x.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            c.Property(x => x.IsDeleted).HasDefaultValueSql("((0))");
            // Replaces the old "one conversation per guest" unique index: a guest now
            // gets exactly one AdminSupport thread AND one DriverGuest thread per
            // distinct driver. Two filtered indexes, not one composite index over
            // (UserId, Type, OtherUserId) — EF Core's SqlServer convention filters a
            // unique index to WHERE OtherUserId IS NOT NULL whenever it covers a
            // nullable column (ANSI NULL semantics), which would silently stop
            // enforcing "at most one AdminSupport row per guest" (OtherUserId is
            // always null there) entirely.
            c.HasIndex(x => new { x.UserId, x.Type }).IsUnique().HasFilter("[OtherUserId] IS NULL");
            c.HasIndex(x => new { x.UserId, x.Type, x.OtherUserId }).IsUnique().HasFilter("[OtherUserId] IS NOT NULL");
            c.HasIndex(x => new { x.Status, x.UnreadByAdminCount, x.LastMessageAt });
            c.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            // Restrict — see SupportMessage.SenderUserId comment above; these paths
            // into Users are what SQL Server flags as a multiple cascade path.
            c.HasOne(x => x.OtherUser).WithMany().HasForeignKey(x => x.OtherUserId).OnDelete(DeleteBehavior.Restrict);
            c.HasOne(x => x.AssignedAdmin).WithMany().HasForeignKey(x => x.AssignedAdminUserId).OnDelete(DeleteBehavior.Restrict);
            c.HasOne(x => x.ClosedByUser).WithMany().HasForeignKey(x => x.ClosedByUserId).OnDelete(DeleteBehavior.Restrict);
            c.HasQueryFilter(x => x.IsDeleted == null || x.IsDeleted == false);
        });

        modelBuilder.Entity<Device>(d =>
        {
            d.ToTable("Devices");
            d.HasKey(x => x.Id);
            d.Property(x => x.Token).IsRequired().HasMaxLength(500);
            d.Property(x => x.Platform).HasMaxLength(20);
            d.Property(x => x.DeviceIdentifier).HasMaxLength(200);
            d.Property(x => x.DeviceModel).HasMaxLength(200);
            d.Property(x => x.OsVersion).HasMaxLength(50);
            d.Property(x => x.AppVersion).HasMaxLength(50);
            d.Property(x => x.NotificationsEnabled).HasDefaultValue(true);
            d.Property(x => x.IsActive).HasDefaultValue(true);
            d.Property(x => x.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            d.Property(x => x.IsDeleted).HasDefaultValueSql("((0))");
            d.HasIndex(x => x.Token).IsUnique();
            d.HasIndex(x => new { x.UserId, x.IsActive });
            d.HasOne(x => x.User).WithMany(x => x.Devices).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            d.HasQueryFilter(x => x.IsDeleted == null || x.IsDeleted == false);
        });

        // ── Invitation / accreditation ──────────────────────────────────────
        modelBuilder.Entity<Invitation>(i =>
        {
            i.ToTable("Invitations");
            i.HasKey(x => x.Id);
            i.Property(x => x.InvitationStatus).HasMaxLength(30);
            i.Property(x => x.AccreditationStatus).HasMaxLength(30);
            i.Property(x => x.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            i.Property(x => x.IsDeleted).HasDefaultValueSql("((0))");
            i.HasOne(x => x.Guest).WithMany().HasForeignKey(x => x.GuestId).OnDelete(DeleteBehavior.Cascade);
            i.HasOne(x => x.InvitationTemplate).WithMany().HasForeignKey(x => x.InvitationTemplateId).OnDelete(DeleteBehavior.SetNull);
            i.HasQueryFilter(x => x.IsDeleted == null || x.IsDeleted == false);
        });

        // ── Flights ─────────────────────────────────────────────────────────
        modelBuilder.Entity<FlightClass>(fc =>
        {
            fc.ToTable("FlightClasses");
            fc.HasKey(x => x.Id);
            fc.Property(x => x.Name).IsRequired().HasMaxLength(50);
            fc.Property(x => x.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            fc.Property(x => x.IsDeleted).HasDefaultValueSql("((0))");
        });

        modelBuilder.Entity<Flight>(f =>
        {
            f.ToTable("Flights");
            f.HasKey(x => x.Id);
            f.Property(x => x.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            f.Property(x => x.IsDeleted).HasDefaultValueSql("((0))");
            f.HasOne(x => x.Guest).WithMany().HasForeignKey(x => x.GuestId).OnDelete(DeleteBehavior.Cascade);
            f.HasOne(x => x.FlightClass).WithMany().HasForeignKey(x => x.FlightClassId).OnDelete(DeleteBehavior.Restrict);
            f.HasQueryFilter(x => x.IsDeleted == null || x.IsDeleted == false);
        });

        modelBuilder.Entity<AirportData>(a =>
        {
            a.ToTable("AirportData");
            a.HasKey(x => x.Id);
            a.Property(x => x.Code).IsRequired().HasMaxLength(10);
            a.Property(x => x.City).HasMaxLength(100);
            a.Property(x => x.Country).HasMaxLength(100);
            a.Property(x => x.Continent).HasMaxLength(50);
            a.HasIndex(x => x.Code).IsUnique();
            a.Property(x => x.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            a.Property(x => x.IsDeleted).HasDefaultValueSql("((0))");
            a.HasOne(x => x.Location).WithMany().HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<FlightLeg>(fl =>
        {
            fl.ToTable("FlightLegs");
            fl.HasKey(x => x.Id);
            fl.Property(x => x.FlightNumber).HasMaxLength(20);
            fl.Property(x => x.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            fl.Property(x => x.IsDeleted).HasDefaultValueSql("((0))");
            fl.HasOne(x => x.Flight).WithMany(x => x.Legs).HasForeignKey(x => x.FlightId).OnDelete(DeleteBehavior.Cascade);
            fl.HasOne(x => x.FromAirport).WithMany().HasForeignKey(x => x.FromAirportId).OnDelete(DeleteBehavior.Restrict);
            fl.HasOne(x => x.ToAirport).WithMany().HasForeignKey(x => x.ToAirportId).OnDelete(DeleteBehavior.Restrict);
            fl.HasOne(x => x.FlightClass).WithMany().HasForeignKey(x => x.FlightClassId).OnDelete(DeleteBehavior.Restrict);
            fl.HasQueryFilter(x => x.IsDeleted == null || x.IsDeleted == false);
        });

        // ── Accommodation ───────────────────────────────────────────────────
        modelBuilder.Entity<AccommodationHotel>(h =>
        {
            h.ToTable("AccommodationHotels");
            h.HasKey(x => x.Id);
            h.Property(x => x.Name).IsRequired().HasMaxLength(300);
            h.Property(x => x.ImageUrl).HasMaxLength(1000);
            h.Property(x => x.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            h.Property(x => x.IsDeleted).HasDefaultValueSql("((0))");
            h.HasOne(x => x.Location).WithMany().HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<AccommodationRoomType>(rt =>
        {
            rt.ToTable("AccommodationRoomTypes");
            rt.HasKey(x => x.Id);
            rt.Property(x => x.Name).IsRequired().HasMaxLength(100);
            rt.Property(x => x.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            rt.Property(x => x.IsDeleted).HasDefaultValueSql("((0))");
        });

        modelBuilder.Entity<EventHotelContract>(c =>
        {
            c.ToTable("EventHotelContracts");
            c.HasKey(x => x.Id);
            c.Property(x => x.Notes).HasMaxLength(1000);
            c.Property(x => x.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            c.Property(x => x.IsDeleted).HasDefaultValueSql("((0))");
            c.HasOne(x => x.Event).WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Restrict);
            c.HasOne(x => x.Hotel).WithMany().HasForeignKey(x => x.AccommodationHotelId).OnDelete(DeleteBehavior.Restrict);
            // One contract per hotel per event — a second one would split the same
            // hotel's room blocks across two rows for no reason.
            c.HasIndex(x => new { x.EventId, x.AccommodationHotelId }).IsUnique().HasFilter("[IsDeleted] = 0");
            c.HasQueryFilter(x => x.IsDeleted == null || x.IsDeleted == false);
        });

        modelBuilder.Entity<HotelRoomInventory>(i =>
        {
            i.ToTable("HotelRoomInventories");
            i.HasKey(x => x.Id);
            i.Property(x => x.Notes).HasMaxLength(500);
            i.Property(x => x.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            i.Property(x => x.IsDeleted).HasDefaultValueSql("((0))");
            i.HasOne(x => x.Contract).WithMany(x => x.Inventory).HasForeignKey(x => x.EventHotelContractId).OnDelete(DeleteBehavior.Cascade);
            i.HasOne(x => x.RoomType).WithMany().HasForeignKey(x => x.RoomTypeId).OnDelete(DeleteBehavior.Restrict);
            // No unique key on the window: overlapping blocks for one room type are
            // legal and their counts add up (see HotelRoomInventory).
            i.HasIndex(x => new { x.EventHotelContractId, x.RoomTypeId, x.FromDate });
            i.HasQueryFilter(x => x.IsDeleted == null || x.IsDeleted == false);
        });

        modelBuilder.Entity<Accommodation>(a =>
        {
            a.ToTable("Accommodations");
            a.HasKey(x => x.Id);
            a.Property(x => x.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            a.Property(x => x.IsDeleted).HasDefaultValueSql("((0))");
            a.HasOne(x => x.Guest).WithMany().HasForeignKey(x => x.GuestId).OnDelete(DeleteBehavior.Cascade);
            a.HasOne(x => x.Hotel).WithMany().HasForeignKey(x => x.AccommodationHotelId).OnDelete(DeleteBehavior.Restrict);
            a.HasOne(x => x.RoomType).WithMany().HasForeignKey(x => x.RoomTypeId).OnDelete(DeleteBehavior.Restrict);
            a.HasQueryFilter(x => x.IsDeleted == null || x.IsDeleted == false);
        });

        // ── Transport ───────────────────────────────────────────────────────
        modelBuilder.Entity<Transport>(t =>
        {
            t.ToTable("Transports");
            t.HasKey(x => x.Id);
            t.Property(x => x.Notes).HasMaxLength(1000);
            t.Property(x => x.RideSource).HasMaxLength(20);
            t.Property(x => x.Currency).HasMaxLength(3);
            t.Property(x => x.BaseFare).HasColumnType("decimal(18,2)");
            t.Property(x => x.DistanceFare).HasColumnType("decimal(18,2)");
            t.Property(x => x.WaitingFare).HasColumnType("decimal(18,2)");
            t.Property(x => x.TotalFare).HasColumnType("decimal(18,2)");
            t.Property(x => x.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            t.Property(x => x.IsDeleted).HasDefaultValueSql("((0))");
            t.HasOne(x => x.Guest).WithMany().HasForeignKey(x => x.GuestId).OnDelete(DeleteBehavior.Cascade);
            t.HasOne(x => x.PickupLocation).WithMany().HasForeignKey(x => x.PickupLocationId).OnDelete(DeleteBehavior.Restrict);
            t.HasOne(x => x.DropoffLocation).WithMany().HasForeignKey(x => x.DropoffLocationId).OnDelete(DeleteBehavior.Restrict);
            t.HasOne(x => x.Vehicle).WithMany().HasForeignKey(x => x.VehicleId).OnDelete(DeleteBehavior.Restrict);
            t.HasOne(x => x.Driver).WithMany().HasForeignKey(x => x.DriverId).OnDelete(DeleteBehavior.Restrict);
            // The vehicle double-booking check filters on exactly this pair.
            t.HasIndex(x => new { x.VehicleId, x.PickupTime });
            t.HasQueryFilter(x => x.IsDeleted == null || x.IsDeleted == false);
        });

        modelBuilder.Entity<TransportStatusHistory>(h =>
        {
            h.ToTable("TransportStatusHistories");
            h.HasKey(x => x.Id);
            h.Property(x => x.Status).IsRequired().HasMaxLength(20);
            h.Property(x => x.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            h.Property(x => x.IsDeleted).HasDefaultValueSql("((0))");
            h.HasOne(x => x.Transport).WithMany().HasForeignKey(x => x.TransportId).OnDelete(DeleteBehavior.Cascade);
            h.HasIndex(x => new { x.TransportId, x.CreatedAt });
            h.HasQueryFilter(x => x.IsDeleted == null || x.IsDeleted == false);
        });

        modelBuilder.Entity<GuestDriverAssignment>(a =>
        {
            a.ToTable("GuestDriverAssignments");
            a.HasKey(x => x.Id);
            a.Property(x => x.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            a.Property(x => x.IsDeleted).HasDefaultValueSql("((0))");
            a.HasOne(x => x.Guest).WithMany().HasForeignKey(x => x.GuestId).OnDelete(DeleteBehavior.Cascade);
            a.HasOne(x => x.Driver).WithMany().HasForeignKey(x => x.DriverId).OnDelete(DeleteBehavior.Restrict);
            a.HasIndex(x => new { x.GuestId, x.DriverId }).IsUnique().HasFilter("[IsDeleted] = 0");
            a.HasQueryFilter(x => x.IsDeleted == null || x.IsDeleted == false);
        });

        modelBuilder.Entity<VehicleType>(vt =>
        {
            vt.ToTable("VehicleTypes");
            vt.HasKey(x => x.Id);
            vt.Property(x => x.Name).IsRequired().HasMaxLength(50);
            vt.Property(x => x.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            vt.Property(x => x.IsDeleted).HasDefaultValueSql("((0))");
        });

        modelBuilder.Entity<Vehicle>(v =>
        {
            v.ToTable("Vehicles");
            v.HasKey(x => x.Id);
            v.Property(x => x.VehicleModel).IsRequired().HasMaxLength(100);
            v.Property(x => x.VehicleNumber).IsRequired().HasMaxLength(50);
            v.Property(x => x.VehicleImage).HasMaxLength(500);
            v.Property(x => x.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            v.Property(x => x.IsDeleted).HasDefaultValueSql("((0))");
            v.HasOne(x => x.VehicleType).WithMany().HasForeignKey(x => x.VehicleTypeId).OnDelete(DeleteBehavior.Restrict);
            v.HasOne(x => x.FleetProvider).WithMany(x => x.Vehicles).HasForeignKey(x => x.FleetProviderId).OnDelete(DeleteBehavior.Restrict);
            v.HasQueryFilter(x => x.IsDeleted == null || x.IsDeleted == false);
        });

        modelBuilder.Entity<FleetProvider>(fp =>
        {
            fp.ToTable("FleetProviders");
            fp.HasKey(x => x.Id);
            fp.Property(x => x.Name).IsRequired().HasMaxLength(150);
            fp.Property(x => x.ContactPerson).HasMaxLength(150);
            fp.Property(x => x.Phone).HasMaxLength(30);
            fp.Property(x => x.Email).HasMaxLength(150);
            fp.Property(x => x.Notes).HasMaxLength(1000);
            fp.Property(x => x.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            fp.Property(x => x.IsDeleted).HasDefaultValueSql("((0))");
            fp.HasOne(x => x.Event).WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Restrict);
            // Name is unique within one event's provider list, not globally —
            // two events may both contract "Al Fardan Rent A Car".
            fp.HasIndex(x => new { x.EventId, x.Name }).IsUnique().HasFilter("[IsDeleted] = 0");
            fp.HasQueryFilter(x => x.IsDeleted == null || x.IsDeleted == false);
        });

        modelBuilder.Entity<DriverProfile>(d =>
        {
            d.ToTable("DriverProfiles");
            d.HasKey(x => x.Id);
            d.Property(x => x.LicenseNumber).HasMaxLength(50);
            d.Property(x => x.PhotoUrl).HasMaxLength(500);
            d.Property(x => x.IsOnline).HasDefaultValue(false);
            d.Property(x => x.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            d.Property(x => x.IsDeleted).HasDefaultValueSql("((0))");
            d.HasOne(x => x.User).WithOne(u => u.DriverProfile).HasForeignKey<DriverProfile>(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            d.HasOne(x => x.Nationality).WithMany().HasForeignKey(x => x.NationalityId).OnDelete(DeleteBehavior.Restrict);
            d.HasQueryFilter(x => x.IsDeleted == null || x.IsDeleted == false);
        });
    }
}
