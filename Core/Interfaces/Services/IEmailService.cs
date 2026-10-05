using System.Threading;
using System.Threading.Tasks;
using Core.ViewModel.Invitation;
using Core.ViewModel.NominationLetter;

namespace Core.Interfaces.Services;

public interface IEmailService
{
    Task SendOtpEmailAsync(string email, string otpCode, CancellationToken ct = default);
    Task SendResetPasswordLinkAsync(string email, string resetPasswordLink, CancellationToken ct = default);
    Task SendAccountApprovedAsync(string email, string firstName, string loginUrl, CancellationToken ct = default);
    Task SendAccountRejectedAsync(string email, string firstName, string reviewNote, CancellationToken ct = default);
    Task SendGuestInvitationAsync(string toEmail, GuestInvitationEmailModel model, CancellationToken ct = default);
    Task SendUserInviteAsync(string email, string firstName, string roleName, string acceptUrl, CancellationToken ct = default);

    /// <summary>
    /// The nomination letter to the host organisation: the mission and the
    /// HR-verified delegates being put forward, as a table they can reply to.
    /// </summary>
    Task SendNominationLetterAsync(string toEmail, NominationLetterEmailModel model, CancellationToken ct = default);

    /// <summary>
    /// Chases a delegate whose post-mission report is still outstanding. Sent by
    /// the coordinator, one per delegate, so it names the mission and says what
    /// is wanted rather than being a generic "action required".
    /// </summary>
    Task SendReportReminderAsync(string toEmail, string delegateName, string missionTitle, string note, CancellationToken ct = default);
}
