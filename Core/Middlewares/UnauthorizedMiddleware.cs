using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Core.Middlewares
{
    public class UnauthorizedMiddleware
    {
        private readonly RequestDelegate _next;

        public UnauthorizedMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            await _next(context);

            if (context.Response.StatusCode == StatusCodes.Status401Unauthorized ||
                context.Response.StatusCode == StatusCodes.Status403Forbidden)
            {
                // Customize the error message here
                var errorMessage = new
                {
                    Message = "You are not authorized to access this resource.",
                    ErrorCode = "UNAUTHORIZED"
                };

                context.Response.ContentType = "application/json";
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;

                await context.Response.WriteAsync(JsonSerializer.Serialize(errorMessage));
            }
        }
    }

    public static class UnauthorizedMiddlewareExtensions
    {
        public static IApplicationBuilder UseUnauthorizedMiddleware(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<UnauthorizedMiddleware>();
        }
    }
}
