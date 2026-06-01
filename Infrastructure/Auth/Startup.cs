using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Text;
using Core.Middlewares;

namespace Infrastructure.Auth
{
    public static class Startup
    {
        public static IServiceCollection InitializeAuth(this IServiceCollection services, IConfiguration config)
        {
            var jwtSection = config.GetSection("Authentication:Jwt");
            var secret = jwtSection["JwtSecretKey"];
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
