using System.Reflection;
using System.Text;
using System.Threading.RateLimiting;
using Azure.Storage.Blobs;
using FluentValidation;
using FluentValidation.AspNetCore;
using Hangfire;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using API.Configurations.OpenApi;
using Core.Authorization;
using Core.Common.Interfaces;
using Core.Interfaces;
using Core.Interfaces.Repositories;
using Core.Interfaces.Services;
using Core.Mappings;
using Core.Serialization;
using DomainPersistence.Entities;
using Infrastructure.Auth;
using Infrastructure.Database.Repositories;
using Infrastructure.Email;
using Infrastructure.Interceptors;
using Infrastructure.Services;

namespace API.Configurations;

public static class ServiceExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.InitializeAuth(configuration);

        services.AddHttpContextAccessor();

        services.AddScoped<AuditInterceptor>();

        services.AddDbContext<ApplicationDBContext>((sp, options) =>
        {
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection"));
            options.AddInterceptors(sp.GetRequiredService<AuditInterceptor>());
        });

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddScoped<ICurrentGuest, CurrentGuest>();

        // Fix 8: BlobServiceClient as singleton — not created per request
        services.AddSingleton(sp =>
        {
            var connStr = configuration["AzureStorage:BlobConnectionString"];
            if (string.IsNullOrEmpty(connStr))
                throw new InvalidOperationException("AzureStorage:BlobConnectionString is not configured.");
            return new BlobServiceClient(connStr);
        });

        // Auth services
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IPasswordResetTokenService, PasswordResetTokenService>();

        // Domain services
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IGuestService, GuestService>();
        services.AddScoped<INationalityService, NationalityService>();
        services.AddScoped<IOrganizationService, OrganizationService>();
        services.AddScoped<IServiceCatalogService, ServiceCatalogService>();
        services.AddScoped<IVehicleService, VehicleService>();
        services.AddScoped<IFleetProviderService, FleetProviderService>();
        services.AddScoped<ITransportAppService, TransportAppService>();
        services.AddScoped<ILookupService, LookupService>();
        services.AddScoped<IVenueService, VenueService>();
        services.AddScoped<ISeatingService, SeatingService>();
        services.AddScoped<IMeetingService, MeetingService>();
        services.AddScoped<ITravelService, TravelService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IInvitationService, InvitationService>();
        services.AddScoped<IInvitationTemplateService, InvitationTemplateService>();
        services.AddScoped<IEventService, EventService>();
        services.AddScoped<IAccountRequestService, AccountRequestService>();
        services.AddScoped<IUserAccessService, UserAccessService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<IBlobService, BlobService>();
        services.AddScoped<IVipAppService, VipAppService>();
        services.AddScoped<ISupportChatService, SupportChatService>();
        services.AddScoped<IConflictWindowPolicy, ZeroBufferConflictWindowPolicy>();
        services.AddScoped<ITransportationConflictValidator, TransportationConflictValidator>();
        services.AddScoped<ITransportationScheduleService, TransportationScheduleService>();

        // Notification services
        services.AddScoped<INotificationService, NotificationService>();
        // Same UTC timestamp format as the REST responses — hub payloads would
        // otherwise write dates through SignalR's own serializer.
        services.AddSignalR().AddJsonProtocol(o =>
        {
            o.PayloadSerializerOptions.Converters.Add(new UtcDateTimeConverter());
            o.PayloadSerializerOptions.Converters.Add(new UtcNullableDateTimeConverter());
        });
        services.AddScoped<IRealTimeAlertService, RealTimeAlertService>();
        services.AddScoped<INotificationManagerService, NotificationManagerService>();

        // Push notification providers — multi-registered on purpose:
        // NotificationManagerService fans out to ALL of them (IEnumerable<IPushNotificationProvider>).
        // ManualNotificationProvider = live in-app delivery over SignalR (works today).
        // FirebaseNotificationProvider = real device push, fanned out per GuestDevice;
        // the wire call is still a stub pending Firebase credentials (see its remarks).
        services.AddScoped<IPushNotificationProvider, ManualNotificationProvider>();
        services.AddScoped<IPushNotificationProvider, FirebaseNotificationProvider>();

        // Background jobs (Hangfire) — SQL Server storage, same DB as the app.
        // Dashboard mapping + recurring job registration happens in Program.cs
        // (needs the built IApplicationBuilder / a service scope for RecurringJob).
        services.AddScoped<INotificationCleanupJob, NotificationCleanupJob>();
        services.AddScoped<IImportBatchService, ImportBatchService>();
        services.AddHangfire(cfg => cfg
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseSqlServerStorage(configuration.GetConnectionString("DefaultConnection")));
        services.AddHangfireServer();

        services.AddAutoMapper(typeof(MappingProfile));

        services.AddFluentValidationAutoValidation();
        services.AddValidatorsFromAssembly(Assembly.Load("Core"));

        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddAuthorization(ConfigureAuthorization);

        // Fix 5: Rate limiting for auth endpoints
        services.AddRateLimiter(options =>
        {
            // Partitioned per caller IP. AddSlidingWindowLimiter has no partition
            // key, so the old version was one global 10/min bucket shared by every
            // user — a handful of logins/refreshes anywhere 429'd everyone else,
            // and a 429 on refresh reads as a dead session on the client.
            options.AddPolicy("auth", http => RateLimitPartition.GetSlidingWindowLimiter(
                partitionKey: http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = 20,
                    Window = TimeSpan.FromMinutes(1),
                    SegmentsPerWindow = 4,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0,
                }));
            // Refresh gets its own, far looser bucket. It already requires a signed
            // refresh token backed by a live DB row, so it needs no brute-force
            // guard — and mobile clients sit behind carrier NAT, where hundreds of
            // guests share one IP and the "auth" limit would throttle them into
            // what looks like an expired session.
            options.AddPolicy("auth-refresh", http => RateLimitPartition.GetSlidingWindowLimiter(
                partitionKey: http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = 300,
                    Window = TimeSpan.FromMinutes(1),
                    SegmentsPerWindow = 4,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0,
                }));
            // Support chat send/reply — cheap abuse guard against message flooding.
            options.AddSlidingWindowLimiter("chat", opt =>
            {
                opt.PermitLimit = 20;
                opt.Window = TimeSpan.FromMinutes(1);
                opt.SegmentsPerWindow = 4;
                opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                opt.QueueLimit = 0;
            });
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        });

        return services;
    }

    public static IServiceCollection AddOpenApiDocumentation(this IServiceCollection services)
    {
        // Native .NET 9 OpenAPI document generation (served to Scalar, not Swagger UI).
        services.AddOpenApi("v1", options =>
        {
            options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
        });

        return services;
    }

    // Fix 9: Dynamic policy registration — no manual update needed when adding new PermissionCodes
    private static void ConfigureAuthorization(AuthorizationOptions options)
    {
        var codes = typeof(Core.Common.PermissionCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(f => f.IsLiteral && !f.IsInitOnly)
            .Select(f => f.GetValue(null)?.ToString())
            .Where(v => !string.IsNullOrEmpty(v));

        foreach (var code in codes)
            options.AddPolicy(code, p => p.Requirements.Add(new PermissionRequirement(code)));
    }
}
