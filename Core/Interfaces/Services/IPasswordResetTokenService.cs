using System;
using System.Threading.Tasks;

namespace Core.Interfaces.Services
{
    public interface IPasswordResetTokenService
    {
        Task<string> GenerateToken(Guid userId, string email);
        Task<Guid> ValidateToken(string token);
    }
}
