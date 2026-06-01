using System.Reflection;
using System.Text;
using System.Threading.RateLimiting;
using Azure.Storage.Blobs;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Core.Authorization;
using Core.Common.Interfaces;
using Core.Interfaces;
using Core.Interfaces.Repositories;
using Core.Interfaces.Services;
using Core.Mappings;
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
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<IBlobService, BlobService>();

        // Notification services
        services.AddScoped<INotificationService, NotificationService>();
        services.AddSignalR();
        services.AddScoped<IRealTimeAlertService, RealTimeAlertService>();
        services.AddScoped<INotificationManagerService, NotificationManagerService>();

        services.AddAutoMapper(typeof(MappingProfile));

        services.AddFluentValidationAutoValidation();
        services.AddValidatorsFromAssembly(Assembly.Load("Core"));

        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddAuthorization(ConfigureAuthorization);

        // Fix 5: Rate limiting for auth endpoints
        services.AddRateLimiter(options =>
        {
            options.AddSlidingWindowLimiter("auth", opt =>
            {
                opt.PermitLimit = 10;
                opt.Window = TimeSpan.FromMinutes(1);
                opt.SegmentsPerWindow = 4;
                opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                opt.QueueLimit = 0;
            });
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        });

        return services;
    }

    public static IServiceCollection AddSwaggerDocumentation(this IServiceCollection services)
    {
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo { Title = "API", Version = "v1" });

            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Description = "JWT Authorization header. Enter: Bearer {token}",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.ApiKey,
                Scheme = "Bearer"
            });

            c.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                    },
                    Array.Empty<string>()
                }
            });
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
