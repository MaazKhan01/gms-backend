using System;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Communication.Email;
using Azure.Core;
using Azure.Core.Pipeline;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Core.Interfaces.Services;
using Core.ViewModel.Invitation;
using Core.ViewModel.NominationLetter;

namespace Infrastructure.Email;

public class EmailService : IEmailService
{
    private readonly ILogger<EmailService> _logger;
    private readonly IConfiguration _configuration;
    private readonly EmailClient _emailClient;
    private readonly string _senderEmail;
    private readonly string _appName;

    // This transport exists to make .NET connect exactly the way `curl -4` does, since
    // that is what demonstrably works from the UAT server while the app times out from
    // the same box. Three deliberate differences from the default handler, all of them
    // things curl was doing implicitly:
    //
    //  1. UseProxy = false. curl ignores the system proxy; SocketsHttpHandler does not
    //     — it picks up the WinHTTP/IE proxy of whatever identity the process runs as.
    //     A stale or unreachable proxy configured for the app pool identity produces
    //     precisely the observed failure: a TaskCanceledException per attempt with no
    //     SocketException, because the connect that hangs is to the *proxy*, not to
    //     ACS. This is the change most likely to fix it.
    //  2. IPv4 only. The server's DNS returns an A and an AAAA record and .NET has no
    //     Happy Eyeballs, so a dead IPv6 path stalls the connect before IPv4 is ever
    //     tried. `curl -4` skipped that; so does this.
    //  3. HTTP/1.1 pinned. curl's successful handshake negotiated http/1.1 via ALPN.
    //     Middleboxes that mangle h2 are a known cause of hangs that look like this.
    //
    // ponytail: this is a client-side workaround for a server networking problem, and
    // it only covers ACS — Firebase fails the same way through its own HttpClient. The
    // real fix is the server's egress config (proxy or route). Delete this once the
    // host is sane; note it hard-codes IPv4, so an IPv6-only endpoint would break.
    //
    // Static and shared deliberately: one HttpClient per process, so repeated
    // EmailService instances (it is scoped) cannot exhaust sockets.
    private static readonly HttpClient DirectIpv4Client = new(new SocketsHttpHandler
    {
        UseProxy = false,
        Proxy = null,
        ConnectCallback = async (context, ct) =>
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
            {
                NoDelay = true,
            };
            try
            {
                // DnsEndPoint on an InterNetwork socket resolves to A records only.
                await socket.ConnectAsync(context.DnsEndPoint, ct).ConfigureAwait(false);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        },
    })
    {
        DefaultRequestVersion = HttpVersion.Version11,
        DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower,
    };

    public EmailService(ILogger<EmailService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;

        var connectionString = configuration.GetValue<string>("AzureCommunicationServiceConfig:COMMUNICATION_SERVICES_CONNECTION_STRING");
        _senderEmail = configuration.GetValue<string>("AzureCommunicationServiceConfig:EmailSenderInfo");
        _appName = configuration.GetValue<string>("AppName") ?? "QOC - GMS";

        if (string.IsNullOrEmpty(connectionString))
            throw new InvalidOperationException("AzureCommunicationServiceConfig:COMMUNICATION_SERVICES_CONNECTION_STRING is not configured.");

        if (string.IsNullOrEmpty(_senderEmail))
            throw new InvalidOperationException("AzureCommunicationServiceConfig:EmailSenderInfo is not configured.");

        // Bounded retries. The SDK defaults are MaxRetries=3 with a 100s network
        // timeout each, so when the ACS front door cannot be reached a single send can
        // occupy a thread for several minutes before it finally throws. That is what
        // made the invite endpoint time out. Sends run in Hangfire now, so failing fast
        // and letting the job retry with backoff beats blocking.
        var options = new EmailClientOptions();
        options.Retry.NetworkTimeout = TimeSpan.FromSeconds(15);
        options.Retry.MaxRetries = 2;
        options.Retry.Delay = TimeSpan.FromSeconds(1);
        options.Retry.Mode = RetryMode.Exponential;
        options.Transport = new HttpClientTransport(DirectIpv4Client);
        _emailClient = new EmailClient(connectionString, options);
    }

    public async Task SendOtpEmailAsync(string email, string otpCode, CancellationToken ct = default)
    {
        var subject = $"Your Verification Code – {_appName}";
        var inner = $@"
            <p style='{P}'>Thank you for registering with <strong style='color:{Ink};'>{_appName}</strong>.</p>
            <p style='{P}'>Use the following verification code to complete your registration:</p>
            <div style='margin:24px 0;text-align:center;'>
                <span style='font-size:32px;font-weight:700;letter-spacing:8px;color:{AccentSoft};background:rgba(141,1,52,0.12);border:1px solid rgba(141,1,52,0.35);padding:14px 26px;border-radius:10px;display:inline-block;font-family:{Mono};'>{otpCode}</span>
            </div>
            <p style='{Small}'><strong style='color:{InkDim};'>This code expires in 10 minutes.</strong></p>
            <p style='{Small}'>If you did not request this, please ignore this email.</p>";

        var body = Shell("Verify your email", "Confirm your<br>email address", inner);
        await SendEmailAsync(email, subject, body, ct);
        _logger.LogInformation("OTP email sent to {Email}", email);
    }

    public async Task SendResetPasswordLinkAsync(string email, string resetLink, CancellationToken ct = default)
    {
        var subject = $"Reset Your Password – {_appName}";
        var inner = $@"
            <p style='{P}'>We received a request to reset the password for your <strong style='color:{Ink};'>{_appName}</strong> account.</p>
            {Cta(resetLink, "Reset Password")}
            <p style='{Small}'><strong style='color:{InkDim};'>This link expires in 1 hour.</strong> If you did not request a password reset, please ignore this email.</p>";

        var body = Shell("Password reset", "Reset your<br>password", inner);
        await SendEmailAsync(email, subject, body, ct);
        _logger.LogInformation("Password reset email sent to {Email}", email);
    }

    public async Task SendAccountApprovedAsync(string email, string firstName, string loginUrl, CancellationToken ct = default)
    {
        var name = string.IsNullOrWhiteSpace(firstName) ? "there" : firstName;
        var subject = $"Your {_appName} account has been approved";
        var inner = $@"
            <p style='{P}'>Hi {name},</p>
            <p style='{P}'>Your account request has been <strong style='color:{Ink};'>approved</strong>. You can now sign in using the email address you registered with.</p>
            {Cta(loginUrl, "Sign In Now")}
            <p style='{Small}'>If you did not request this account, please ignore this email.</p>";

        var body = Shell("Account approved", $"Welcome to<br><em style='font-style:italic;color:{AccentSoft};'>{_appName}</em>", inner);
        await SendEmailAsync(email, subject, body, ct);
        _logger.LogInformation("Account-approved email sent to {Email}", email);
    }

    public async Task SendAccountRejectedAsync(string email, string firstName, string reviewNote, CancellationToken ct = default)
    {
        var name = string.IsNullOrWhiteSpace(firstName) ? "there" : firstName;
        var subject = $"Your {_appName} account request was not approved";
        var noteSection = string.IsNullOrWhiteSpace(reviewNote)
            ? string.Empty
            : $"<p style='{P}'><strong style='color:{Ink};'>Reason:</strong> {reviewNote}</p>";

        var inner = $@"
            <p style='{P}'>Hi {name},</p>
            <p style='{P}'>Unfortunately, your account request for <strong style='color:{Ink};'>{_appName}</strong> was not approved at this time.</p>
            {noteSection}
            <p style='{Small}'>If you believe this is an error, please contact your system administrator.</p>";

        var body = Shell("Account request update", "Request<br>update", inner);
        await SendEmailAsync(email, subject, body, ct);
        _logger.LogInformation("Account-rejected email sent to {Email}", email);
    }

    public async Task SendUserInviteAsync(string email, string firstName, string roleName, string acceptUrl, CancellationToken ct = default)
    {
        var name = string.IsNullOrWhiteSpace(firstName) ? "there" : firstName;
        var subject = $"You've been invited to {_appName}";
        var roleSection = string.IsNullOrWhiteSpace(roleName)
            ? string.Empty
            : $"<p style='{P}'>You've been added as a <strong style='color:{Ink};'>{roleName}</strong>.</p>";

        var inner = $@"
            <p style='{P}'>Hi {name},</p>
            <p style='{P}'>QOC administrator has created an account for you on <strong style='color:{Ink};'>{_appName}</strong>.</p>
            {roleSection}
            <p style='{P}'>Click the button below to set your password and activate your account:</p>
            {Cta(acceptUrl, "Set Up My Account")}
            <p style='{Small}'>If you weren't expecting this, please ignore this email.</p>";

        var body = Shell("Official invitation", $"Welcome to<br><em style='font-style:italic;color:{AccentSoft};'>{_appName}</em>", inner);
        await SendEmailAsync(email, subject, body, ct);
        _logger.LogInformation("User invite email sent to {Email}", email);
    }

    public async Task SendGuestInvitationAsync(string toEmail, GuestInvitationEmailModel model, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(toEmail)) return;

        var dateRange = model.EventStartDate is { } s
            ? (model.EventEndDate is { } e && e != s ? $"{s:d MMM} – {e:d MMM yyyy}" : $"{s:d MMM yyyy}")
            : null;

        var pills = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(dateRange)) pills.Append(Pill(dateRange));
        if (!string.IsNullOrWhiteSpace(model.EventVenue)) pills.Append(Pill(model.EventVenue));

        var detailRows = new StringBuilder();
        detailRows.Append(DetailRow("Guest name", model.GuestName));
        if (!string.IsNullOrWhiteSpace(model.EventTitle)) detailRows.Append(DetailRow("Event", model.EventTitle));

        var dateVenueParts = new System.Collections.Generic.List<string>();
        if (!string.IsNullOrWhiteSpace(dateRange)) dateVenueParts.Add(dateRange);
        if (!string.IsNullOrWhiteSpace(model.EventVenue)) dateVenueParts.Add(model.EventVenue);
        if (dateVenueParts.Count > 0) detailRows.Append(DetailRow("Date & venue", string.Join(" · ", dateVenueParts)));

        if (!string.IsNullOrWhiteSpace(model.Tier)) detailRows.Append(DetailRow("Guest tier", model.Tier));
        if (!string.IsNullOrWhiteSpace(model.Reference)) detailRows.Append(DetailRow("Reference", model.Reference, mono: true));

        var detailCard = $@"
            <div style='margin:24px 0;background:rgba(141,1,52,0.07);border:1px solid rgba(141,1,52,0.22);border-radius:14px;overflow:hidden;'>
                <div style='padding:11px 18px;background:rgba(141,1,52,0.12);border-bottom:1px solid rgba(141,1,52,0.18);font-size:10.5px;letter-spacing:0.16em;text-transform:uppercase;color:{AccentSoft};font-weight:600;'>Invitation Details</div>
                <div>{detailRows}</div>
            </div>";

        // Template body is admin-authored HTML with no colors of its own — without this
        // wrapper it inherits the client's default black ink and vanishes on the dark card.
        var inner = $@"
            <div style='{BodyText}'>{model.BodyHtml}</div>
            {detailCard}
            {(string.IsNullOrWhiteSpace(model.CtaUrl) ? "" : Cta(model.CtaUrl, "View Invitation &amp; Respond"))}
            <p style='{Small}'>Open the button above to confirm or decline your attendance.</p>";

        var headline = string.IsNullOrWhiteSpace(model.EventTitle)
            ? "You're invited"
            : $"You're invited to<br><em style='font-style:italic;color:{AccentSoft};'>{model.EventTitle}</em>";

        var body = Shell("Official invitation", headline, inner, pills.ToString());
        await SendEmailAsync(toEmail, model.Subject, body, ct);
        _logger.LogInformation("Guest invitation email sent to {Email}", toEmail);
    }

    // ── Shared branded shell ─────────────────────────────────────────────────
    // Matches the live app's dark theme + brand palette exactly (App.jsx
    // BRAND_THEME / style.css :root, html[data-theme="dark"]): maroon accent
    // (#8d0134) over a near-black maroon background, not a generic color.
    private const string Bg = "#14000a";
    private const string CardBg = "#200011";
    private const string Ink = "#ffffff";
    private const string InkDim = "rgba(255,255,255,0.88)";
    private const string InkMute = "rgba(255,255,255,0.68)";
    private const string InkFaint = "rgba(255,255,255,0.48)";
    private const string Accent = "#8d0134";
    private const string AccentDeep = "#5e0022";
    private const string AccentSoft = "#e0648a";
    private const string Serif = "'Times New Roman',Georgia,serif";
    private const string Mono = "ui-monospace,Consolas,monospace";
    private const string P = "margin:0 0 14px;font-size:14px;line-height:1.7;color:rgba(255,255,255,0.88);";
    private const string Small = "margin:0;font-size:11.5px;line-height:1.6;color:rgba(255,255,255,0.6);";
    // Applied to raw template HTML so untinted <p>/<div>/text inherit light ink.
    private const string BodyText = "font-size:14.5px;line-height:1.75;color:#ffffff;font-family:Arial,Helvetica,sans-serif;";

    private string Shell(string eyebrow, string headlineHtml, string innerHtml, string pillsHtml = null)
    {
        var pillsBlock = string.IsNullOrWhiteSpace(pillsHtml) ? "" : $"<div style='margin-top:18px;'>{pillsHtml}</div>";
        return $@"
        <div style='margin:0;padding:32px 12px;background:{Bg};font-family:Arial,Helvetica,sans-serif;'>
          <div style='max-width:600px;margin:0 auto;background:{CardBg};border-radius:16px;overflow:hidden;border:1px solid rgba(255,255,255,0.08);'>
            <div style='background:linear-gradient(135deg,#14000a 0%,{AccentDeep} 60%,#200011 100%);padding:40px 40px 32px;'>
              <table role='presentation' cellpadding='0' cellspacing='0' style='margin-bottom:24px;'><tr>
                <td style='width:32px;height:32px;border-radius:8px;background:rgba(141,1,52,0.22);border:1px solid rgba(141,1,52,0.45);text-align:center;vertical-align:middle;font-weight:700;font-size:13px;color:{AccentSoft};font-family:Arial,sans-serif;'>G</td>
                <td style='padding-left:10px;font-size:11px;font-weight:600;letter-spacing:0.16em;text-transform:uppercase;color:{InkMute};'>{_appName}</td>
              </tr></table>
              <div style='font-size:10.5px;letter-spacing:0.18em;text-transform:uppercase;color:{AccentSoft};margin-bottom:12px;font-weight:600;'>{eyebrow}</div>
              <div style='font-family:{Serif};font-size:26px;font-weight:400;line-height:1.25;color:{Ink};'>{headlineHtml}</div>
              {pillsBlock}
            </div>
            <div style='padding:32px 40px;'>
              {innerHtml}
            </div>
            <div style='padding:18px 40px 24px;border-top:1px solid rgba(255,255,255,0.06);background:rgba(0,0,0,0.15);font-size:11px;color:{InkFaint};text-align:center;line-height:1.7;'>
              {_appName} · Guest Management System
            </div>
          </div>
        </div>";
    }

    private static string Cta(string url, string label) => $@"
            <div style='margin:28px 0 20px;text-align:center;'>
                <a href='{url}' style='display:inline-block;padding:13px 30px;background: maroon;color:#f6fdff;text-decoration:none;border-radius:10px;font-weight:700;font-size:14px;font-family:Arial,sans-serif;'>{label}</a>
            </div>";

    private static string Pill(string text) => $@"
            <span style='display:inline-block;margin:0 8px 8px 0;padding:6px 13px;border-radius:999px;background:rgba(255,255,255,0.07);border:1px solid rgba(255,255,255,0.12);font-size:11.5px;color:{InkDim};'>{text}</span>";

    private static string DetailRow(string label, string value, bool mono = false)
    {
        var valueStyle = mono
            ? $"font-family:{Mono};font-size:12.5px;color:{AccentSoft};"
            : $"font-size:13.5px;font-weight:600;color:{Ink};";
        // Table, not flex — Outlook and several mobile clients drop display:flex entirely
        // and the label/value collapse onto each other.
        return $@"
            <table role='presentation' width='100%' cellpadding='0' cellspacing='0' style='border-collapse:collapse;border-bottom:1px solid rgba(255,255,255,0.09);'>
              <tr>
                <td style='padding:11px 18px;font-size:12.5px;color:{InkMute};white-space:nowrap;'>{label}</td>
                <td align='right' style='padding:11px 18px;{valueStyle}'>{value}</td>
              </tr>
            </table>";
    }


    public async Task SendNominationLetterAsync(
        string toEmail, NominationLetterEmailModel model, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(toEmail)) return;

        var dates = model.StartDate is { } s
            ? (model.EndDate is { } e && e != s ? $"{s:d MMM} – {e:d MMM yyyy}" : $"{s:d MMM yyyy}")
            : null;

        var subject = $"Nomination Letter — {model.MissionTitle}"
                    + (model.Version > 1 ? $" (v{model.Version})" : string.Empty);

        // A table, not a prose list: the host has to read names, roles and
        // passport numbers off it and reply about specific people.
        var rows = string.Join(string.Empty, model.Roster.Select((r, i) => $@"
            <tr style='background:{(i % 2 == 1 ? "rgba(255,255,255,0.03)" : "transparent")};'>
              <td style='{Cell}'>{i + 1}</td>
              <td style='{Cell}color:{Ink};font-weight:600;'>{Escape(r.FullName)}</td>
              <td style='{Cell}'>{Escape(r.JobTitle) ?? Dash}</td>
              <td style='{Cell}'>{Escape(r.MissionRole) ?? Dash}</td>
              <td style='{Cell}'>{Escape(r.Nationality) ?? Dash}</td>
              <td style='{Cell}'>{Escape(r.PassportNumber) ?? Dash}</td>
              <td style='{Cell}'>{(r.PassportExpiry is { } pe ? pe.ToString("d MMM yyyy") : Dash)}</td>
            </tr>"));

        var note = string.IsNullOrWhiteSpace(model.Note)
            ? string.Empty
            : $"<p style='{P}'>{Escape(model.Note)}</p>";

        var destination = string.IsNullOrWhiteSpace(model.Destination)
            ? string.Empty
            : $"<p style='{Small}'>Destination: {Escape(model.Destination)}</p>";

        var inner = $@"
            <p style='{P}'>Dear {Escape(model.HostName) ?? "Sir / Madam"},</p>
            <p style='{P}'>We are pleased to submit our delegation for
              <strong style='color:{Ink};'>{Escape(model.MissionTitle)}</strong>{(dates != null ? $", {dates}" : string.Empty)}.
              The {model.Roster.Count} delegate(s) named below have been verified by our HR department.</p>
            {note}
            <table role='presentation' width='100%' cellpadding='0' cellspacing='0'
                   style='border-collapse:collapse;margin:18px 0;font-size:12.5px;'>
              <tr>
                <th style='{Head}'>#</th>
                <th style='{Head}'>Name</th>
                <th style='{Head}'>Title</th>
                <th style='{Head}'>Role</th>
                <th style='{Head}'>Nationality</th>
                <th style='{Head}'>Passport</th>
                <th style='{Head}'>Expires</th>
              </tr>
              {rows}
            </table>
            {destination}
            <p style='{Small}'>Please reply to this email to confirm the delegation or request changes.</p>";

        var body = Shell(
            "Nomination Letter",
            $"Delegation for<br><em style='font-style:italic;color:{AccentSoft};'>{Escape(model.MissionTitle)}</em>",
            inner);

        await SendEmailAsync(toEmail, subject, body, ct);
        _logger.LogInformation(
            "Nomination letter v{Version} emailed to {Email} with {Count} delegate(s)",
            model.Version, toEmail, model.Roster.Count);
    }

    public async Task SendReportReminderAsync(
        string toEmail, string delegateName, string missionTitle, string note, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(toEmail)) return;

        var extra = string.IsNullOrWhiteSpace(note)
            ? string.Empty
            : $"<p style='{P}'>{Escape(note)}</p>";

        var inner = $@"
            <p style='{P}'>Dear {Escape(delegateName) ?? "colleague"},</p>
            <p style='{P}'>Your post-mission report for
              <strong style='color:{Ink};'>{Escape(missionTitle)}</strong> has not been submitted yet.
              The mission's combined report cannot be completed without it.</p>
            {extra}
            <p style='{Small}'>Open the app and add your narrative — the trip details are filled in for you.</p>";

        var body = Shell(
            "Post-Mission Report",
            $"Still outstanding for<br><em style='font-style:italic;color:{AccentSoft};'>{Escape(missionTitle)}</em>",
            inner);

        await SendEmailAsync(toEmail, $"Post-mission report — {missionTitle}", body, ct);
        _logger.LogInformation("Post-mission report reminder sent to {Email} for {Mission}", toEmail, missionTitle);
    }

    private const string Head = "padding:8px 10px;text-align:left;font-size:10.5px;text-transform:uppercase;"
                              + "letter-spacing:0.06em;color:rgba(255,255,255,0.55);font-weight:600;"
                              + "border-bottom:1px solid rgba(255,255,255,0.14);white-space:nowrap;";

    private const string Cell = "padding:8px 10px;color:rgba(255,255,255,0.82);"
                              + "border-bottom:1px solid rgba(255,255,255,0.06);";

    private const string Dash = "—";

    /// <summary>
    /// Names and passport numbers go straight into HTML. A delegate called
    /// O'Brien &amp; Co would otherwise break the table, and anything pasted in
    /// from a spreadsheet could carry markup with it.
    /// </summary>
    private static string Escape(string raw)
        => string.IsNullOrWhiteSpace(raw) ? null : System.Net.WebUtility.HtmlEncode(raw);

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
        // ponytail: WaitUntil.Started returns once ACS accepts the message (one POST)
        // instead of polling delivery status for 10-60s. Rejections (bad sender,
        // auth, malformed address) still throw here; post-acceptance bounces do not.
        // Switch back to Completed only if a caller must confirm actual delivery.
        await _emailClient.SendAsync(WaitUntil.Started, message, ct);
    }
}
