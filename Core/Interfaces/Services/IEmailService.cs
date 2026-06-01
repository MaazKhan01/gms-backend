using System.Threading;
using System.Threading.Tasks;

namespace Core.Interfaces.Services;

public interface IEmailService
{
    Task SendOtpEmailAsync(string email, string otpCode, CancellationToken ct = default);
    Task SendResetPasswordLinkAsync(string email, string resetPasswordLink, CancellationToken ct = default);
}
