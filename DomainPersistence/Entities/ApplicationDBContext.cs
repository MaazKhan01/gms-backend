using System;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace DomainPersistence.Entities;

public partial class ApplicationDBContext : DbContext
{
    public ApplicationDBContext(DbContextOptions<ApplicationDBContext> options)
        : base(options)
    {
    }

    // User & Auth
    public virtual DbSet<User> Users { get; set; }
    public virtual DbSet<Role> Roles { get; set; }
    public virtual DbSet<Permission> Permissions { get; set; }
    public virtual DbSet<RolePermission> RolePermissions { get; set; }
    public virtual DbSet<OtpVerification> OtpVerifications { get; set; }
    public virtual DbSet<UserRefreshToken> UserRefreshTokens { get; set; }

    // Venue reference data
    public virtual DbSet<VenueType> VenueTypes { get; set; }
    public virtual DbSet<ElementType> ElementTypes { get; set; }

    // Notifications
    public virtual DbSet<Notification> Notifications { get; set; }

    // Audit
    public virtual DbSet<UserLoginLog> UserLoginLogs { get; set; }
    public virtual DbSet<SystemErrorLog> SystemErrorLogs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // User
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(e => e.Id);
            // Filtered, not a plain unique index — Users has no IsDeleted query
            // filter (soft-deleted rows stay queryable), so an unfiltered unique
            // index would keep a deleted user's email permanently unusable. This
            // scopes uniqueness to active rows only, so it's free again once deleted.
            // SQL Server filtered-index predicates don't support OR, only AND —
            // "= 0" alone is safe here since EF always sends the DB default (0),
            // never NULL, when IsDeleted isn't explicitly set on insert.
            // "AND [Email] IS NOT NULL" is required too: a plain unique index in
            // SQL Server treats multiple NULLs as duplicates (unlike ANSI), and
            // an invited-but-unfinished User can still have Email = NULL.
            // A guest-linked User now carries the Guest's email (and the same
            // value as its UserName) — Guests.Email is itself unique among active
            // rows, so the two constraints agree rather than fight.
            entity.HasIndex(e => e.Email).IsUnique().HasFilter("[IsDeleted] = 0 AND [Email] IS NOT NULL");
            // Same active-rows-only scoping as Email above: without it a deleted
            // guest's email would stay permanently unusable as a UserName.
            entity.HasIndex(e => e.UserName).IsUnique().HasFilter("[IsDeleted] = 0 AND [UserName] IS NOT NULL");
            entity.Property(e => e.UserName).HasMaxLength(100);
            entity.Property(e => e.Email).HasMaxLength(255);
            entity.Property(e => e.FirstName).HasMaxLength(150);
            entity.Property(e => e.LastName).HasMaxLength(150);
            entity.Property(e => e.Phone).HasMaxLength(20);
            entity.Property(e => e.PasswordHash).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.IsDeleted).HasDefaultValueSql("((0))");
            entity.HasOne(e => e.Role)
                .WithMany(r => r.Users)
                .HasForeignKey(e => e.RoleId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Role
        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Code).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(500);
            // Defaults to allowed so existing roles keep working; the driver role
            // has to be flipped off explicitly.
            entity.Property(e => e.PortalAccess).HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
        });

        // Permission
        modelBuilder.Entity<Permission>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Code).IsUnique();
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Code).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Module).HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
        });

        // RolePermission
        modelBuilder.Entity<RolePermission>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.RoleId, e.PermissionId }).IsUnique();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.HasOne(d => d.Role)
                .WithMany(p => p.RolePermissions)
                .HasForeignKey(d => d.RoleId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(d => d.Permission)
                .WithMany(p => p.RolePermissions)
                .HasForeignKey(d => d.PermissionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // OtpVerification
        modelBuilder.Entity<OtpVerification>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(255);
            entity.Property(e => e.OtpCode).IsRequired().HasMaxLength(10);
            entity.Property(e => e.Purpose).IsRequired().HasMaxLength(50);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
        });

        // UserRefreshToken
        modelBuilder.Entity<UserRefreshToken>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Jti).IsUnique();
            entity.Property(e => e.Jti).IsRequired().HasMaxLength(100);
            entity.Property(e => e.IsRevoked).HasDefaultValueSql("((0))");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.HasOne(d => d.User)
                .WithMany()
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Notification
        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(300);
            entity.Property(e => e.Type).HasMaxLength(100);
            entity.Property(e => e.RedirectUrl).HasMaxLength(500);
            entity.Property(e => e.Data).HasColumnType("nvarchar(max)");
            entity.Property(e => e.Read).HasDefaultValueSql("((0))");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.IsDeleted).HasDefaultValueSql("((0))");
            // Unread-first, newest-first is the dominant query shape (bell list + badge count).
            entity.HasIndex(e => new { e.UserId, e.Read, e.CreatedAt });
            entity.HasOne(d => d.User)
                .WithMany(p => p.Notifications)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // UserLoginLog
        modelBuilder.Entity<UserLoginLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.LoginAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.IpAddress).HasMaxLength(45);
            entity.Property(e => e.UserAgent).HasMaxLength(500);
            entity.Property(e => e.FailureReason).HasMaxLength(500);
            entity.HasOne(d => d.User)
                .WithMany(p => p.UserLoginLogs)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // SystemErrorLog
        modelBuilder.Entity<SystemErrorLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ErrorMessage).IsRequired().HasColumnType("nvarchar(max)");
            entity.Property(e => e.StackTrace).HasColumnType("nvarchar(max)");
            entity.Property(e => e.Source).HasMaxLength(500);
            entity.Property(e => e.RequestPath).HasMaxLength(500);
            entity.Property(e => e.RequestMethod).HasMaxLength(10);
            entity.Property(e => e.IpAddress).HasMaxLength(45);
            entity.Property(e => e.RequestId).HasMaxLength(100);
            entity.Property(e => e.OccurredAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.HasOne(d => d.User)
                .WithMany(p => p.SystemErrorLogs)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Global conventions applied to every mapped entity:
        //  - AuditEntity types get the soft-delete query filter.
        //  - Any entity exposing a Guid PublicId gets a newid() default + unique index.
        //    Int Id keys are identity by EF default — no explicit config needed.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var clrType = entityType.ClrType;

            if (typeof(AuditEntity).IsAssignableFrom(clrType))
            {
                var parameter = Expression.Parameter(clrType, "e");
                var property = Expression.Property(parameter, nameof(AuditEntity.IsDeleted));
                var compareToNull = Expression.Equal(property, Expression.Constant(null, typeof(bool?)));
                var compareToFalse = Expression.Equal(property, Expression.Constant(false, typeof(bool?)));
                var filter = Expression.Lambda(Expression.OrElse(compareToNull, compareToFalse), parameter);
                entityType.SetQueryFilter(filter);
            }

            var publicId = clrType.GetProperty("PublicId");
            if (publicId != null && publicId.PropertyType == typeof(Guid))
            {
                modelBuilder.Entity(clrType).Property("PublicId").HasDefaultValueSql("(newid())");
                modelBuilder.Entity(clrType).HasIndex("PublicId").IsUnique();
            }
        }

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
