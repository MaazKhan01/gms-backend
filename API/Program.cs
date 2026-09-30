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

// Serverless/container hosts give the app a READ-ONLY filesystem and collect
// logs from stdout — there is no disk to roll files onto and nothing reading
// them if there were. Writing to Logs/ there fails at startup, which presents
// as a container that never becomes healthy.
//
// So: files when running on a real box (unchanged local behaviour), console
// only in a container. The Microsoft base images set DOTNET_RUNNING_IN_CONTAINER,
// so this needs no configuration to be right; Serilog:WriteToFile overrides it
// either way.
var runningInContainer = builder.Configuration.GetValue<bool>("DOTNET_RUNNING_IN_CONTAINER");
var writeFileLogs = builder.Configuration.GetValue<bool?>("Serilog:WriteToFile") ?? !runningInContainer;

var loggerConfiguration = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console();

if (writeFileLogs)
    loggerConfiguration = loggerConfiguration.WriteTo.File("Logs/log-.txt", rollingInterval: RollingInterval.Day);

Log.Logger = loggerConfiguration.CreateLogger();

builder.Host.UseSerilog();

// The host assigns the port and routes to it; Kestrel's own default (or a
// launchSettings URL baked into the image) would leave it listening on a port
// nothing forwards to, which looks like a hung deploy rather than a bad bind.
// Also 0.0.0.0, not localhost — the request arrives from outside the container.
var listenPort = builder.Configuration["PORT"];
if (!string.IsNullOrWhiteSpace(listenPort))
    builder.WebHost.UseUrls($"http://0.0.0.0:{listenPort}");

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
    //await Infrastructure.Data.DataSeeder.SeedAsync(db, app.Configuration, seedLogger);
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
        .WithTitle("DMS API")
        .WithOpenApiRoutePattern("/openapi/{documentName}.json")
        .AddPreferredSecuritySchemes("Bearer");
});

// Swagger UI served against the same native OpenAPI doc.
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/openapi/v1.json", "DMS API v1");
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

// Liveness, for the host's health check. Anonymous and dependency-free on
// purpose: it answers "is this container up and serving", which is the question
// a platform restarts on. It deliberately does NOT touch the database — a
// health check that fails on a slow query gets the container killed and
// restarted, which fixes nothing and takes the API down with it.
//
// It does pass through the normal middleware pipeline, so a misconfigured blob
// connection string still shows up here rather than only on the first real
// request.
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();
