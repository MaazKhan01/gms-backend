using System;
using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Core.Interfaces.Repositories;
using Core.ViewModel.Common;
using DomainPersistence.Entities;

namespace Core.Middlewares;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IWebHostEnvironment _env;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger, IWebHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context, IUnitOfWork unitOfWork)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception on {Method} {Path}", context.Request.Method, context.Request.Path);

            // Log to database — swallow secondary errors so the primary error is still returned
            try
            {
                var userIdStr = context.User?.FindFirst("sub")?.Value
                    ?? context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                Guid? userId = Guid.TryParse(userIdStr, out var uid) ? uid : null;

                await unitOfWork.SystemErrorLogs.AddAsync(new SystemErrorLog
                {
                    Id = Guid.NewGuid(),
                    ErrorMessage = ex.Message,
                    StackTrace = ex.StackTrace,
                    Source = ex.Source,
                    RequestPath = context.Request.Path,
                    RequestMethod = context.Request.Method,
                    UserId = userId,
                    IpAddress = context.Connection.RemoteIpAddress?.ToString(),
                    RequestId = context.TraceIdentifier,
                    OccurredAt = DateTime.UtcNow
                });
                await unitOfWork.SaveChangesAsync();
            }
            catch (Exception logEx)
            {
                _logger.LogError(logEx, "Failed to persist error log to database");
            }

            await WriteErrorResponseAsync(context, ex);
        }
    }

    private Task WriteErrorResponseAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

        // Fix 7: Never expose raw exception details outside Development
        var errors = _env.IsDevelopment()
            ? new List<string> { exception.Message }
            : new List<string> { "An unexpected error occurred. Please try again later." };

        var response = ApiResponse<object>.ServerErrorResponse("An internal server error occurred.", errors);

        return context.Response.WriteAsync(
            JsonSerializer.Serialize(response, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
    }
}
