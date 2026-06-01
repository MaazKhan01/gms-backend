using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Core.Interfaces.Services;

namespace Infrastructure.Services;

public class PasswordResetTokenService : IPasswordResetTokenService
{
    private readonly IConfiguration _configuration;

    public PasswordResetTokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public Task<string> GenerateToken(Guid userId, string email)
    {
        var jwtSection = _configuration.GetSection("Authentication:Jwt");
        var secret = jwtSection["JwtSecretKey"];

        if (string.IsNullOrEmpty(secret))
            throw new InvalidOperationException("Authentication:Jwt:JwtSecretKey is not configured.");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("purpose", "reset-password")
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(60),
            Issuer = jwtSection["Issuer"],
            Audience = jwtSection["Audience"],
            SigningCredentials = credentials
        };

        var handler = new JwtSecurityTokenHandler();
        return Task.FromResult(handler.WriteToken(handler.CreateToken(tokenDescriptor)));
    }

    public Task<Guid> ValidateToken(string token)
    {
        var jwtSection = _configuration.GetSection("Authentication:Jwt");
        var secret = jwtSection["JwtSecretKey"];

        if (string.IsNullOrEmpty(secret))
            throw new InvalidOperationException("Authentication:Jwt:JwtSecretKey is not configured.");

        var handler = new JwtSecurityTokenHandler();
        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidAudience = jwtSection["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
            ClockSkew = TimeSpan.Zero,
            RequireSignedTokens = true,
            RequireExpirationTime = true
        };

        try
        {
            var principal = handler.ValidateToken(token, parameters, out _);
            var userIdClaim = principal.FindFirst(JwtRegisteredClaimNames.Sub)
                           ?? principal.FindFirst(ClaimTypes.NameIdentifier);

            if (userIdClaim == null)
                throw new SecurityTokenException("Invalid token: sub claim missing.");

            return Task.FromResult(Guid.Parse(userIdClaim.Value));
        }
        catch (Exception ex) when (ex is not SecurityTokenException)
        {
            throw new SecurityTokenException("Token validation failed.", ex);
        }
    }
}
