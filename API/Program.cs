using API.Configurations;
using Core.Helpers;
using Core.Interfaces.Services;
using Core.Middlewares;
using Core.Serialization;
using Hangfire;
using Infrastructure.Auth;
using Infrastructure.Hangfire;
using Microsoft.AspNetCore.ResponseCompression;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("Logs/log-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddControllers()
    // Every DateTime this API returns is UTC — see UtcDateTimeConverters for why
    // this is necessary despite that (EF Core + SQL Server's datetime2 losing the
    // Kind tag on every read).
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.Converters.Add(new UtcDateTimeConverter());
        o.JsonSerializerOptions.Converters.Add(new UtcNullableDateTimeConverter());
    });
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddApplicationServices(builder.Configuration);
builder.Services.AddOpenApiDocumentation();

builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new Microsoft.AspNetCore.Mvc.ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
});
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
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
app.UseResponseCompression();

// Fix 5: Apply rate limiting middleware
app.UseRateLimiter();

app.MapHub<RealTimeHubService>("/realtimehub");

app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new HangfireAuthorizationFilter() }
});
RecurringJob.AddOrUpdate<INotificationCleanupJob>(
    "notification-cleanup",
    job => job.PurgeOldNotificationsAsync(CancellationToken.None),
    Cron.Daily(3)); // 03:00 UTC — off-peak

// OpenAPI document + Scalar API reference UI (replaces Swagger).
app.MapOpenApi("/openapi/{documentName}.json");
app.MapScalarApiReference("/scalar", options =>
{
    options
        .WithTitle("GMS API")
        .WithOpenApiRoutePattern("/openapi/{documentName}.json")
        .AddPreferredSecuritySchemes("Bearer");
});

// Swagger UI served against the same native OpenAPI doc.
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/openapi/v1.json", "GMS API v1");
    options.RoutePrefix = "swagger";
    // Keeps the Authorize token in browser localStorage, so a reload doesn't
    // log you out of the UI. Dev convenience only — the token is in the browser.
    options.ConfigObject.PersistAuthorization = true;
});

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseAuth(); // UnauthorizedMiddleware + UseAuthentication + UseAuthorization

app.UseSerilogRequestLogging();

app.UseMiddleware<Core.Middlewares.BlobSasMiddleware>();

app.MapControllers();

app.Run();
