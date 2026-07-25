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
    public virtual DbSet<Session> Sessions { get; set; }
    public virtual DbSet<AccountRequest> AccountRequests { get; set; }
    public virtual DbSet<UserModuleGrant> UserModuleGrants { get; set; }
    public virtual DbSet<Guest> Guests { get; set; }
    public virtual DbSet<GuestSession> GuestSessions { get; set; }
    public virtual DbSet<Nationality> Nationalities { get; set; }
    public virtual DbSet<InvitationTemplate> InvitationTemplates { get; set; }
    public virtual DbSet<Meeting> Meetings { get; set; }
    public virtual DbSet<Location> Locations { get; set; }

    // Invitation / accreditation
    public virtual DbSet<Invitation> Invitations { get; set; }

    // Flights
    public virtual DbSet<FlightType> FlightTypes { get; set; }
    public virtual DbSet<FlightClass> FlightClasses { get; set; }
    public virtual DbSet<Flight> Flights { get; set; }
    public virtual DbSet<FlightLeg> FlightLegs { get; set; }

    // Accommodation
    public virtual DbSet<AccommodationHotel> AccommodationHotels { get; set; }
    public virtual DbSet<AccommodationRoomType> AccommodationRoomTypes { get; set; }
    public virtual DbSet<Accommodation> Accommodations { get; set; }

    // Transport
    public virtual DbSet<Transport> Transports { get; set; }
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
    public virtual DbSet<GuestDevice> GuestDevices { get; set; }
    public virtual DbSet<GuestNotification> GuestNotifications { get; set; }

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
            g.HasQueryFilter(x => x.IsDeleted == null || x.IsDeleted == false);
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
            m.Property(x => x.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            m.Property(x => x.IsDeleted).HasDefaultValueSql("((0))");
            m.HasIndex(x => new { x.GuestId, x.SentAt });
            m.HasOne(x => x.Guest).WithMany().HasForeignKey(x => x.GuestId).OnDelete(DeleteBehavior.Cascade);
            m.HasQueryFilter(x => x.IsDeleted == null || x.IsDeleted == false);
        });

        modelBuilder.Entity<GuestDevice>(d =>
        {
            d.ToTable("GuestDevices");
            d.HasKey(x => x.Id);
            d.Property(x => x.Token).IsRequired().HasMaxLength(500);
            d.Property(x => x.Platform).HasMaxLength(20);
            d.Property(x => x.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            d.Property(x => x.IsDeleted).HasDefaultValueSql("((0))");
            d.HasIndex(x => x.Token).IsUnique();
            d.HasOne(x => x.Guest).WithMany().HasForeignKey(x => x.GuestId).OnDelete(DeleteBehavior.Cascade);
            d.HasQueryFilter(x => x.IsDeleted == null || x.IsDeleted == false);
        });

        modelBuilder.Entity<GuestNotification>(n =>
        {
            n.ToTable("GuestNotifications");
            n.HasKey(x => x.Id);
            n.Property(x => x.Title).IsRequired().HasMaxLength(300);
            n.Property(x => x.Message).HasColumnType("nvarchar(max)");
            n.Property(x => x.Type).HasMaxLength(50);
            n.Property(x => x.RedirectUrl).HasMaxLength(500);
            n.Property(x => x.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            n.Property(x => x.IsDeleted).HasDefaultValueSql("((0))");
            n.HasIndex(x => new { x.GuestId, x.Read });
            n.HasOne(x => x.Guest).WithMany().HasForeignKey(x => x.GuestId).OnDelete(DeleteBehavior.Cascade);
            n.HasQueryFilter(x => x.IsDeleted == null || x.IsDeleted == false);
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
        modelBuilder.Entity<FlightType>(ft =>
        {
            ft.ToTable("FlightTypes");
            ft.HasKey(x => x.Id);
            ft.Property(x => x.Name).IsRequired().HasMaxLength(50);
            ft.Property(x => x.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            ft.Property(x => x.IsDeleted).HasDefaultValueSql("((0))");
        });

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
            f.HasOne(x => x.FlightType).WithMany().HasForeignKey(x => x.FlightTypeId).OnDelete(DeleteBehavior.Restrict);
            f.HasOne(x => x.FlightClass).WithMany().HasForeignKey(x => x.FlightClassId).OnDelete(DeleteBehavior.Restrict);
            f.HasQueryFilter(x => x.IsDeleted == null || x.IsDeleted == false);
        });

        modelBuilder.Entity<FlightLeg>(fl =>
        {
            fl.ToTable("FlightLegs");
            fl.HasKey(x => x.Id);
            fl.Property(x => x.FlightNumber).HasMaxLength(20);
            fl.Property(x => x.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            fl.Property(x => x.IsDeleted).HasDefaultValueSql("((0))");
            fl.HasOne(x => x.Flight).WithMany(x => x.Legs).HasForeignKey(x => x.FlightId).OnDelete(DeleteBehavior.Cascade);
            fl.HasQueryFilter(x => x.IsDeleted == null || x.IsDeleted == false);
        });

        // ── Accommodation ───────────────────────────────────────────────────
        modelBuilder.Entity<AccommodationHotel>(h =>
        {
            h.ToTable("AccommodationHotels");
            h.HasKey(x => x.Id);
            h.Property(x => x.Name).IsRequired().HasMaxLength(300);
            h.Property(x => x.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            h.Property(x => x.IsDeleted).HasDefaultValueSql("((0))");
        });

        modelBuilder.Entity<AccommodationRoomType>(rt =>
        {
            rt.ToTable("AccommodationRoomTypes");
            rt.HasKey(x => x.Id);
            rt.Property(x => x.Name).IsRequired().HasMaxLength(100);
            rt.Property(x => x.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            rt.Property(x => x.IsDeleted).HasDefaultValueSql("((0))");
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
            t.Property(x => x.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            t.Property(x => x.IsDeleted).HasDefaultValueSql("((0))");
            t.HasOne(x => x.Guest).WithMany().HasForeignKey(x => x.GuestId).OnDelete(DeleteBehavior.Cascade);
            t.HasOne(x => x.PickupLocation).WithMany().HasForeignKey(x => x.PickupLocationId).OnDelete(DeleteBehavior.Restrict);
            t.HasOne(x => x.DropoffLocation).WithMany().HasForeignKey(x => x.DropoffLocationId).OnDelete(DeleteBehavior.Restrict);
            t.HasQueryFilter(x => x.IsDeleted == null || x.IsDeleted == false);
        });
    }
}
