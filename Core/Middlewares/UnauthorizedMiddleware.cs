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

            // 401 and 403 mean different things and must stay different on the
            // wire. 401 = "no valid session" and the client should refresh or
            // sign in again; 403 = "signed in, but this role may not do that" and
            // refreshing achieves nothing.
            //
            // This used to rewrite 403 into 401. That was survivable when denials
            // were rare, but under role-based read/write they are routine — and
            // the frontend retries once on 401 after refreshing the token, so a
            // permission denial turned into a refresh and then a sign-out instead
            // of a "forbidden" message.
            var status = context.Response.StatusCode;
            if (status == StatusCodes.Status401Unauthorized ||
                status == StatusCodes.Status403Forbidden)
            {
                var isForbidden = status == StatusCodes.Status403Forbidden;
                var payload = new
                {
                    Message = isForbidden
                        ? "You do not have permission to perform this action."
                        : "You are not authorized to access this resource.",
                    ErrorCode = isForbidden ? "FORBIDDEN" : "UNAUTHORIZED"
                };

                context.Response.ContentType = "application/json";
                context.Response.StatusCode = status;   // preserved, not collapsed

                await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
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
