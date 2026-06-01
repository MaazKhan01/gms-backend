using API.Configurations;
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
builder.Services.AddSwaggerDocumentation();

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

app.UseRouting();
app.UseCors("AllowAll");

// Fix 5: Apply rate limiting middleware
app.UseRateLimiter();

app.MapHub<RealTimeHubService>("/realtimehub");

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "API v1");
    options.ConfigObject.AdditionalItems.Add("persistAuthorization", "true");
    options.ConfigObject.AdditionalItems["defaultModelsExpandDepth"] = -1;
});

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseAuth(); // UnauthorizedMiddleware + UseAuthentication + UseAuthorization

app.UseSerilogRequestLogging();

app.MapControllers();

app.Run();
