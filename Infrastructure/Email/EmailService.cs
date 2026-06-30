using System;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Communication.Email;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Core.Interfaces.Services;

namespace Infrastructure.Email;

public class EmailService : IEmailService
{
    private readonly ILogger<EmailService> _logger;
    private readonly IConfiguration _configuration;
    private readonly EmailClient _emailClient;
    private readonly string _senderEmail;
    private readonly string _appName;

    public EmailService(ILogger<EmailService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;

        var connectionString = configuration.GetValue<string>("AzureCommunicationServiceConfig:COMMUNICATION_SERVICES_CONNECTION_STRING");
        _senderEmail = configuration.GetValue<string>("AzureCommunicationServiceConfig:EmailSenderInfo");
        _appName = configuration.GetValue<string>("AppName") ?? "Application";

        if (string.IsNullOrEmpty(connectionString))
            throw new InvalidOperationException("AzureCommunicationServiceConfig:COMMUNICATION_SERVICES_CONNECTION_STRING is not configured.");

        if (string.IsNullOrEmpty(_senderEmail))
            throw new InvalidOperationException("AzureCommunicationServiceConfig:EmailSenderInfo is not configured.");

        _emailClient = new EmailClient(connectionString);
    }

    public async Task SendOtpEmailAsync(string email, string otpCode, CancellationToken ct = default)
    {
        var subject = $"Your Verification Code – {_appName}";
        var body = $@"
        <div style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
            <h2>Email Verification</h2>
            <p>Thank you for registering with <strong>{_appName}</strong>.</p>
            <p>Use the following verification code to complete your registration:</p>
            <div style='margin: 24px 0; text-align: center;'>
                <span style='font-size: 36px; font-weight: bold; letter-spacing: 8px; color: #4CAF50;
                             background: #f5f5f5; padding: 12px 24px; border-radius: 8px; display: inline-block;'>
                    {otpCode}
                </span>
            </div>
            <p><strong>This code expires in 10 minutes.</strong></p>
            <p>If you did not request this, please ignore this email.</p>
            <p>Regards,<br/>{_appName} Team</p>
        </div>";

        await SendEmailAsync(email, subject, body, ct);
        _logger.LogInformation("OTP email sent to {Email}", email);
    }

    public async Task SendResetPasswordLinkAsync(string email, string resetLink, CancellationToken ct = default)
    {
        var subject = $"Reset Your Password – {_appName}";
        var body = $@"
        <div style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
            <h2>Password Reset Request</h2>
            <p>We received a request to reset the password for your <strong>{_appName}</strong> account.</p>
            <p>Click the button below to reset your password:</p>
            <p>
                <a href='{resetLink}'
                   style='background-color:#4CAF50;color:#ffffff;padding:12px 24px;text-decoration:none;
                          border-radius:5px;display:inline-block;font-weight:bold;'>
                    Reset Password
                </a>
            </p>
            <p>If the button does not work, copy and paste this link into your browser:</p>
            <p style='word-break: break-all;'>{resetLink}</p>
            <p><strong>This link expires in 1 hour.</strong></p>
            <p>If you did not request a password reset, please ignore this email.</p>
            <p>Regards,<br/>{_appName} Team</p>
        </div>";

        await SendEmailAsync(email, subject, body, ct);
        _logger.LogInformation("Password reset email sent to {Email}", email);
    }

    public async Task SendAccountApprovedAsync(string email, string firstName, string loginUrl, CancellationToken ct = default)
    {
        var name = string.IsNullOrWhiteSpace(firstName) ? "there" : firstName;
        var subject = $"Your {_appName} account has been approved";
        var body = $@"
        <div style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
            <h2 style='color: #1aaec4;'>Welcome to {_appName}!</h2>
            <p>Hi {name},</p>
            <p>Your account request has been <strong>approved</strong>. You can now sign in using the email address you registered with.</p>
            <p style='margin: 24px 0;'>
                <a href='{loginUrl}'
                   style='background-color:#1aaec4;color:#ffffff;padding:12px 28px;text-decoration:none;
                          border-radius:6px;display:inline-block;font-weight:bold;letter-spacing:0.03em;'>
                    Sign In Now
                </a>
            </p>
            <p style='font-size:12px;color:#888;'>If the button doesn't work, copy and paste this link:<br/>
               <span style='word-break:break-all;'>{loginUrl}</span></p>
            <p>If you did not request this account, please ignore this email.</p>
            <p>Regards,<br/>{_appName} Team</p>
        </div>";

        await SendEmailAsync(email, subject, body, ct);
        _logger.LogInformation("Account-approved email sent to {Email}", email);
    }

    public async Task SendAccountRejectedAsync(string email, string firstName, string reviewNote, CancellationToken ct = default)
    {
        var name = string.IsNullOrWhiteSpace(firstName) ? "there" : firstName;
        var subject = $"Your {_appName} account request was not approved";
        var noteSection = string.IsNullOrWhiteSpace(reviewNote)
            ? string.Empty
            : $"<p><strong>Reason:</strong> {reviewNote}</p>";

        var body = $@"
        <div style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
            <h2>Account Request Update</h2>
            <p>Hi {name},</p>
            <p>Unfortunately, your account request for <strong>{_appName}</strong> was not approved at this time.</p>
            {noteSection}
            <p>If you believe this is an error, please contact your system administrator.</p>
            <p>Regards,<br/>{_appName} Team</p>
        </div>";

        await SendEmailAsync(email, subject, body, ct);
        _logger.LogInformation("Account-rejected email sent to {Email}", email);
    }

    private async Task SendEmailAsync(string toEmail, string subject, string body, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(toEmail))
            throw new ArgumentException("Recipient email cannot be empty.", nameof(toEmail));

        var bcc = _configuration.GetValue<string>("AzureCommunicationServiceConfig:BccEmail");
        var emailContent = new EmailContent(subject) { Html = body };
        var recipients = new EmailRecipients();

        foreach (var addr in toEmail.Split(',', StringSplitOptions.RemoveEmptyEntries))
            recipients.To.Add(new EmailAddress(addr.Trim()));

        if (!string.IsNullOrEmpty(bcc))
            foreach (var addr in bcc.Split(',', StringSplitOptions.RemoveEmptyEntries))
                recipients.BCC.Add(new EmailAddress(addr.Trim()));

        var message = new EmailMessage(_senderEmail, recipients, emailContent);
        var op = await _emailClient.SendAsync(WaitUntil.Completed, message, ct);

        if (op.Value.Status != EmailSendStatus.Succeeded)
            throw new InvalidOperationException($"Email send failed with status: {op.Value.Status}");
    }
}
