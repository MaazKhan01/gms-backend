using API.Configurations;
using Scalar.AspNetCore;
using Serilog;
using Core.Helpers;
using Core.Middlewares;
using Infrastructure.Auth;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("Logs/log-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddApplicationServices(builder.Configuration);
builder.Services.AddOpenApiDocumentation();

builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new Microsoft.AspNetCore.Mvc.ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.WithOrigins(builder.Configuration["AllowedOrigins"]?.Split(',')
                  ?? new[] { "http://localhost:3000", "http://localhost:5173" })
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

var app = builder.Build();

// Apply migrations + seed roles/permissions/admin on startup (idempotent).
using (var scope = app.Services.CreateScope())
{
    var sp = scope.ServiceProvider;
    var db = sp.GetRequiredService<DomainPersistence.Entities.ApplicationDBContext>();
    var seedLogger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("DataSeeder");
    await Infrastructure.Data.DataSeeder.SeedAsync(db, app.Configuration, seedLogger);
}

app.UseRouting();
app.UseCors("AllowAll");

// Fix 5: Apply rate limiting middleware
app.UseRateLimiter();

app.MapHub<RealTimeHubService>("/realtimehub");

// OpenAPI document + Scalar API reference UI (replaces Swagger).
app.MapOpenApi("/openapi/{documentName}.json");
app.MapScalarApiReference("/scalar", options =>
{
    options
        .WithTitle("GMS API")
        .WithOpenApiRoutePattern("/openapi/{documentName}.json")
        .AddPreferredSecuritySchemes("Bearer");
});

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseAuth(); // UnauthorizedMiddleware + UseAuthentication + UseAuthorization

app.UseSerilogRequestLogging();

app.MapControllers();

app.Run();
