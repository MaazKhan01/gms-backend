using System.Threading;
using System.Threading.Tasks;
using Core.ViewModel.Invitation;

namespace Core.Interfaces.Services;

public interface IEmailService
{
    Task SendOtpEmailAsync(string email, string otpCode, CancellationToken ct = default);
    Task SendResetPasswordLinkAsync(string email, string resetPasswordLink, CancellationToken ct = default);
    Task SendAccountApprovedAsync(string email, string firstName, string loginUrl, CancellationToken ct = default);
    Task SendAccountRejectedAsync(string email, string firstName, string reviewNote, CancellationToken ct = default);
    Task SendGuestInvitationAsync(string toEmail, GuestInvitationEmailModel model, CancellationToken ct = default);
    Task SendUserInviteAsync(string email, string firstName, string roleName, string acceptUrl, CancellationToken ct = default);
}
