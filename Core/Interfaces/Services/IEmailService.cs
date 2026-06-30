using System.Threading;
using System.Threading.Tasks;

namespace Core.Interfaces.Services;

public interface IEmailService
{
    Task SendOtpEmailAsync(string email, string otpCode, CancellationToken ct = default);
    Task SendResetPasswordLinkAsync(string email, string resetPasswordLink, CancellationToken ct = default);
    Task SendAccountApprovedAsync(string email, string firstName, string loginUrl, CancellationToken ct = default);
    Task SendAccountRejectedAsync(string email, string firstName, string reviewNote, CancellationToken ct = default);
}
