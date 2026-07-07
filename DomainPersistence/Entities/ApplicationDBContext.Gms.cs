using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace DomainPersistence.Entities;

/// <summary>
/// GMS domain model — kept in a partial class so the generated/boilerplate
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
    public virtual DbSet<Travel_logistics> Travel_logistics { get; set; }
    public virtual DbSet<Location> Locations { get; set; }
    public virtual DbSet<Venue> Venues { get; set; }
    public virtual DbSet<VenueLayout> VenueLayouts { get; set; }
    public virtual DbSet<VenueLayoutProp> VenueLayoutProps { get; set; }
    public virtual DbSet<VenueBlock> VenueBlocks { get; set; }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AccountRequest>(a =>
        {
            a.ToTable("AccountRequests");
            a.HasKey(x => x.Id);
            a.Property(x => x.Id).HasDefaultValueSql("(newid())");
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
            n.Property(x => x.Id).HasDefaultValueSql("(newid())");
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
            t.Property(x => x.Id).HasDefaultValueSql("(newid())");
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
            g.Property(x => x.Id).HasDefaultValueSql("(newid())");
            g.Property(x => x.FirstName).IsRequired().HasMaxLength(150);
            g.Property(x => x.LastName).IsRequired().HasMaxLength(150);
            g.Property(x => x.Email).HasMaxLength(255);
            g.Property(x => x.GuestType).HasMaxLength(50);
            g.Property(x => x.Organization).HasMaxLength(300);
            g.Property(x => x.Tier).HasMaxLength(50);
            g.Property(x => x.InvitationStatus).HasMaxLength(30);
            g.Property(x => x.FlightNumber).HasMaxLength(20);
            g.Property(x => x.Hotel).HasMaxLength(300);
            g.Property(x => x.AccreditationStatus).HasMaxLength(30);
            g.Property(x => x.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            g.Property(x => x.IsDeleted).HasDefaultValueSql("((0))");
            g.HasOne<Event>()
                .WithMany()
                .HasForeignKey(x => x.EventId)
                .OnDelete(DeleteBehavior.Restrict);
            g.HasOne(x => x.Nationality)
                .WithMany(x => x.Guests)
                .HasForeignKey(x => x.NationalityId)
                .OnDelete(DeleteBehavior.SetNull);
            g.HasOne(x => x.InvitationTemplate)
                .WithMany()
                .HasForeignKey(x => x.InvitationTemplateId)
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
            e.Property(x => x.Id).HasDefaultValueSql("(newid())");
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
            s.Property(x => x.Id).HasDefaultValueSql("(newid())");
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
            s.HasQueryFilter(x => x.IsDeleted == null || x.IsDeleted == false);
        });

        modelBuilder.Entity<UserModuleGrant>(g =>
        {
            g.ToTable("UserModuleGrants");
            g.HasKey(x => x.Id);
            g.Property(x => x.Id).HasDefaultValueSql("(newid())");
            g.Property(x => x.Module).IsRequired().HasMaxLength(50);
            g.Property(x => x.GrantedAt).HasDefaultValueSql("(sysutcdatetime())");
            g.HasIndex(x => new { x.UserId, x.Module }).IsUnique();
            g.HasOne(x => x.User)
                .WithMany(x => x.ModuleGrants)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
