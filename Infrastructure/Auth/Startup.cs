using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Text;
using System.Threading.Tasks;
using Core.Middlewares;

namespace Infrastructure.Auth
{
    public static class Startup
    {
        public static IServiceCollection InitializeAuth(this IServiceCollection services, IConfiguration config)
        {
            var jwtSection = config.GetSection("Authentication:Jwt");
            var secret = jwtSection["JwtSecretKey"];
            if (string.IsNullOrWhiteSpace(secret))
                throw new InvalidOperationException(
                    "Authentication:Jwt:JwtSecretKey is not configured. Copy API/appsettings.example.json " +
                    "to API/appsettings.json and set a secret of at least 32 characters.");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
            }).AddJwtBearer(o =>
            {
                o.SaveToken = true;
                o.RequireHttpsMetadata = false;
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = key,
                    ValidateIssuer = !string.IsNullOrEmpty(jwtSection["Issuer"]),
                    ValidIssuer = jwtSection["Issuer"],
                    ValidateAudience = !string.IsNullOrEmpty(jwtSection["Audience"]),
                    ValidAudience = jwtSection["Audience"],
                    ClockSkew = TimeSpan.Zero
                };
                // Browsers can't set an Authorization header on the WebSocket/SSE
                // handshake SignalR uses, so the JS client sends the token as
                // ?access_token=... instead — only honored for the hub path.
                o.Events = new JwtBearerEvents
                {
                    OnMessageReceived = ctx =>
                    {
                        var accessToken = ctx.Request.Query["access_token"];
                        if (!string.IsNullOrEmpty(accessToken) &&
                            ctx.HttpContext.Request.Path.StartsWithSegments("/realtimehub"))
                            ctx.Token = accessToken;
                        return Task.CompletedTask;
                    }
                };
            });

            return services;
        }

        public static IApplicationBuilder UseAuth(this WebApplication app)
        {
            app.UseUnauthorizedMiddleware();
            app.UseAuthentication();
            app.UseAuthorization();
            return app;
        }
    }
}
