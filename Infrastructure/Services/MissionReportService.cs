using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Core.Constants;
using Core.Interfaces.Repositories;
using Core.Interfaces.Services;
using Core.ViewModel.Common;
using Core.ViewModel.Reports;
using DomainPersistence.Entities;

namespace Infrastructure.Services;

/// <summary>
/// Phase 9 — Reports & Close.
///
/// Each delegate writes one post-mission report. The Head of Delegation
/// assembles those into the Combined Mission Report; Protocol approves and
/// publishes it; the mission then closes. Reviewing and approving are kept as
/// two separate sign-offs because they are two different people answering two
/// different questions.
///
/// Everything here is writable AFTER the mission has completed. The completion
/// guard elsewhere stops new delegates and new bookings — it must not stop the
/// reporting, which by definition only happens once the mission is over.
/// </summary>
public class MissionReportService(IUnitOfWork _unitOfWork, IEmailService _emailService)
    : IMissionReportService
{
    // ── Post-mission reports ────────────────────────────────────────────────

    public async Task<ApiResponse<List<PostMissionReportResponse>>> GetReportsAsync(
        Guid eventId, string status, CancellationToken ct = default)
    {
        var mission = await FindMissionAsync(eventId, ct);
        if (mission == null)
            return ApiResponse<List<PostMissionReportResponse>>.NotFoundResponse("Mission not found.");

        var rows = await BuildReportsAsync(mission, ct);

        if (!string.IsNullOrWhiteSpace(status))
        {
            // `outstanding` is the working set, not a stored status: everything
            // that still needs chasing, whatever it is called in the column.
            rows = status == "outstanding"
                ? rows.Where(r => r.Status == PostMissionReportStatuses.NotSubmitted).ToList()
                : rows.Where(r => r.Status == status).ToList();
        }

        return ApiResponse<List<PostMissionReportResponse>>.SuccessResponse(rows);
    }

    public async Task<ApiResponse<PostMissionReportSummary>> GetReportSummaryAsync(
        Guid eventId, CancellationToken ct = default)
    {
        var mission = await FindMissionAsync(eventId, ct);
        if (mission == null)
            return ApiResponse<PostMissionReportSummary>.NotFoundResponse("Mission not found.");

        var rows = await BuildReportsAsync(mission, ct);
        return ApiResponse<PostMissionReportSummary>.SuccessResponse(Summarise(rows));
    }

    public async Task<ApiResponse<PostMissionReportResponse>> SaveReportAsync(
        SaveReportRequest request, int userId, CancellationToken ct = default)
    {
        if (request == null || request.Id == Guid.Empty)
            return ApiResponse<PostMissionReportResponse>.ErrorResponse("A participation id is required.");

        var participation = await _unitOfWork.EventGuests.Query()
            .Include(eg => eg.PostMissionReport)
            .FirstOrDefaultAsync(eg => eg.PublicId == request.Id, ct);

        if (participation == null)
            return ApiResponse<PostMissionReportResponse>.NotFoundResponse("Nomination not found.");

        // Submitting with nothing written is the one thing worth refusing. A
        // blank DRAFT is fine — that is just someone clearing the box.
        if (request.Submit && string.IsNullOrWhiteSpace(request.Narrative))
            return ApiResponse<PostMissionReportResponse>.ErrorResponse(
                "Write the report before submitting it.", "REPORT_EMPTY");

        var report = participation.PostMissionReport;
        if (report == null)
        {
            report = new PostMissionReport
            {
                EventGuestId = participation.Id,
                Status = PostMissionReportStatuses.NotSubmitted,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userId == 0 ? null : userId,
            };
            await _unitOfWork.PostMissionReports.AddAsync(report, ct);
        }
        else
        {
            if (report.Status == PostMissionReportStatuses.Approved)
                return ApiResponse<PostMissionReportResponse>.ErrorResponse(
                    "This report has been approved and can no longer be edited.", "REPORT_APPROVED");

            _unitOfWork.PostMissionReports.Update(report);
        }

        report.Narrative = string.IsNullOrWhiteSpace(request.Narrative) ? null : request.Narrative.Trim();
        report.SetUpdateAudit(userId);

        if (request.Submit)
        {
            report.Status = PostMissionReportStatuses.Submitted;
            // Re-submitting an edited report re-dates it: the date answers "when
            // did we last get this?", which is what the chase list needs.
            report.SubmittedOn = DateTime.UtcNow;
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return await OneAsync(participation.PublicId, ct);
    }

    public async Task<ApiResponse<PostMissionReportResponse>> ApproveReportAsync(
        Guid participationId, int userId, CancellationToken ct = default)
    {
        var participation = await _unitOfWork.EventGuests.Query()
            .Include(eg => eg.PostMissionReport)
            .FirstOrDefaultAsync(eg => eg.PublicId == participationId, ct);

        if (participation?.PostMissionReport == null)
            return ApiResponse<PostMissionReportResponse>.NotFoundResponse("No report has been started for this delegate.");

        var report = participation.PostMissionReport;
        if (report.Status != PostMissionReportStatuses.Submitted)
            return ApiResponse<PostMissionReportResponse>.ErrorResponse(
                "Only a submitted report can be approved.", "REPORT_NOT_SUBMITTED");

        report.Status = PostMissionReportStatuses.Approved;
        report.SetUpdateAudit(userId);
        _unitOfWork.PostMissionReports.Update(report);
        await _unitOfWork.SaveChangesAsync(ct);

        return await OneAsync(participationId, ct);
    }

    public async Task<ApiResponse<NudgeResult>> NudgeAsync(
        NudgeReportsRequest request, int userId, CancellationToken ct = default)
    {
        var mission = await FindMissionAsync(request?.EventId ?? Guid.Empty, ct);
        if (mission == null)
            return ApiResponse<NudgeResult>.NotFoundResponse("Mission not found.");

        var wanted = (request.Ids ?? new List<Guid>()).Where(i => i != Guid.Empty).Distinct().ToList();

        var query = _unitOfWork.EventGuests.Query()
            .Include(eg => eg.Guest)
            .Include(eg => eg.PostMissionReport)
            .Where(eg => eg.EventId == mission.Id);

        if (wanted.Count > 0) query = query.Where(eg => wanted.Contains(eg.PublicId));

        var rows = await query.ToListAsync(ct);
        var result = new NudgeResult();

        foreach (var row in rows)
        {
            var report = row.PostMissionReport;
            if (report != null && report.Status != PostMissionReportStatuses.NotSubmitted)
            {
                result.Skipped++;
                result.Notes.Add($"{Name(row.Guest)} has already submitted.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(row.Guest?.Email))
            {
                result.Skipped++;
                result.Notes.Add($"{Name(row.Guest)} has no email address on file.");
                continue;
            }

            if (report == null)
            {
                // The nudge is the first thing that creates the row: before it,
                // "not submitted" is simply the absence of a report.
                report = new PostMissionReport
                {
                    EventGuestId = row.Id,
                    Status = PostMissionReportStatuses.NotSubmitted,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = userId == 0 ? null : userId,
                };
                await _unitOfWork.PostMissionReports.AddAsync(report, ct);
            }
            else
            {
                _unitOfWork.PostMissionReports.Update(report);
            }

            report.NudgeCount += 1;
            report.LastNudgeOn = DateTime.UtcNow;
            report.SetUpdateAudit(userId);

            try
            {
                await _emailService.SendReportReminderAsync(
                    row.Guest.Email,
                    Name(row.Guest),
                    mission.Title,
                    string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
                    ct);
                result.Sent++;
            }
            catch (Exception ex)
            {
                // The count still went up — the chase was attempted, and the
                // coordinator needs to know delivery is what failed.
                result.Skipped++;
                result.Notes.Add($"{Name(row.Guest)}: the reminder could not be sent ({ex.Message}).");
            }
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return ApiResponse<NudgeResult>.SuccessResponse(result,
            $"{result.Sent} reminder(s) sent, {result.Skipped} skipped.");
    }

    // ── Combined report ─────────────────────────────────────────────────────

    public async Task<ApiResponse<CombinedReportResponse>> GetCombinedAsync(
        Guid eventId, CancellationToken ct = default)
    {
        var mission = await FindMissionAsync(eventId, ct);
        if (mission == null)
            return ApiResponse<CombinedReportResponse>.NotFoundResponse("Mission not found.");

        return ApiResponse<CombinedReportResponse>.SuccessResponse(await BuildCombinedAsync(mission, ct));
    }

    public async Task<ApiResponse<CombinedReportResponse>> AssembleAsync(
        CombinedReportActionRequest request, int userId, CancellationToken ct = default)
    {
        var mission = await FindMissionAsync(request?.EventId ?? Guid.Empty, ct);
        if (mission == null)
            return ApiResponse<CombinedReportResponse>.NotFoundResponse("Mission not found.");

        var rows = await BuildReportsAsync(mission, ct);
        var contributing = rows
            .Where(r => r.Status != PostMissionReportStatuses.NotSubmitted)
            .ToList();

        if (contributing.Count == 0)
            return ApiResponse<CombinedReportResponse>.ErrorResponse(
                "No delegate has submitted a report yet, so there is nothing to assemble.",
                "NO_REPORTS");

        var report = await _unitOfWork.CombinedReports.Query()
            .FirstOrDefaultAsync(c => c.EventId == mission.Id, ct);

        if (report == null)
        {
            report = new CombinedReport
            {
                EventId = mission.Id,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userId == 0 ? null : userId,
            };
            await _unitOfWork.CombinedReports.AddAsync(report, ct);
        }
        else
        {
            // Re-assembling after approval would change a document somebody has
            // already signed. Publishing is final for the same reason.
            if (report.Status == CombinedReportStatuses.Approved || report.Status == CombinedReportStatuses.Published)
                return ApiResponse<CombinedReportResponse>.ErrorResponse(
                    $"This report has been {report.Status} — re-assembling would rewrite what was signed off.",
                    "REPORT_LOCKED");

            _unitOfWork.CombinedReports.Update(report);
        }

        report.Status = CombinedReportStatuses.Draft;
        report.ContentHtml = Assemble(mission, contributing, rows.Count);
        report.AssembledOn = DateTime.UtcNow;
        // A re-assembly is a new draft: any earlier review is no longer about
        // this text.
        report.ReviewedBy = null;
        report.ReviewedOn = null;
        report.SetUpdateAudit(userId);

        await _unitOfWork.SaveChangesAsync(ct);
        return ApiResponse<CombinedReportResponse>.SuccessResponse(
            await BuildCombinedAsync(mission, ct),
            $"Assembled from {contributing.Count} of {rows.Count} delegate report(s).");
    }

    public async Task<ApiResponse<CombinedReportResponse>> SaveCombinedAsync(
        SaveCombinedReportRequest request, int userId, CancellationToken ct = default)
    {
        var (mission, report, error) = await LoadForActionAsync(request?.EventId ?? Guid.Empty, ct);
        if (error != null) return error;

        if (report.Status == CombinedReportStatuses.Approved || report.Status == CombinedReportStatuses.Published)
            return ApiResponse<CombinedReportResponse>.ErrorResponse(
                "An approved report can no longer be edited.", "REPORT_LOCKED");

        report.ContentHtml = request.ContentHtml;
        report.SetUpdateAudit(userId);
        _unitOfWork.CombinedReports.Update(report);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApiResponse<CombinedReportResponse>.SuccessResponse(await BuildCombinedAsync(mission, ct));
    }

    public async Task<ApiResponse<CombinedReportResponse>> SubmitForApprovalAsync(
        CombinedReportActionRequest request, int userId, CancellationToken ct = default)
    {
        var (mission, report, error) = await LoadForActionAsync(request?.EventId ?? Guid.Empty, ct);
        if (error != null) return error;

        if (report.Status != CombinedReportStatuses.Draft)
            return ApiResponse<CombinedReportResponse>.ErrorResponse(
                "Only a draft can be sent for approval.", "NOT_DRAFT");

        if (string.IsNullOrWhiteSpace(report.ContentHtml))
            return ApiResponse<CombinedReportResponse>.ErrorResponse(
                "The report is empty. Assemble it first.", "REPORT_EMPTY");

        report.Status = CombinedReportStatuses.InReview;
        // The Head of Delegation's review IS this step — they are saying the
        // assembled text is right before Protocol is asked to sign it.
        report.ReviewedBy = userId == 0 ? null : userId;
        report.ReviewedOn = DateTime.UtcNow;
        report.SetUpdateAudit(userId);

        _unitOfWork.CombinedReports.Update(report);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApiResponse<CombinedReportResponse>.SuccessResponse(await BuildCombinedAsync(mission, ct));
    }

    public async Task<ApiResponse<CombinedReportResponse>> ApproveCombinedAsync(
        CombinedReportActionRequest request, int userId, CancellationToken ct = default)
    {
        var (mission, report, error) = await LoadForActionAsync(request?.EventId ?? Guid.Empty, ct);
        if (error != null) return error;

        if (report.Status != CombinedReportStatuses.InReview)
            return ApiResponse<CombinedReportResponse>.ErrorResponse(
                "Only a report under review can be approved.", "NOT_IN_REVIEW");

        report.Status = CombinedReportStatuses.Approved;
        report.ApprovedBy = userId == 0 ? null : userId;
        report.ApprovedOn = DateTime.UtcNow;
        report.SetUpdateAudit(userId);

        _unitOfWork.CombinedReports.Update(report);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApiResponse<CombinedReportResponse>.SuccessResponse(await BuildCombinedAsync(mission, ct));
    }

    public async Task<ApiResponse<CombinedReportResponse>> PublishAsync(
        CombinedReportActionRequest request, int userId, CancellationToken ct = default)
    {
        var (mission, report, error) = await LoadForActionAsync(request?.EventId ?? Guid.Empty, ct);
        if (error != null) return error;

        if (report.Status != CombinedReportStatuses.Approved)
            return ApiResponse<CombinedReportResponse>.ErrorResponse(
                "Approve the report before publishing it.", "NOT_APPROVED");

        report.Status = CombinedReportStatuses.Published;
        report.PublishedUrl = string.IsNullOrWhiteSpace(request.PublishedUrl) ? report.PublishedUrl : request.PublishedUrl.Trim();
        report.SetUpdateAudit(userId);

        _unitOfWork.CombinedReports.Update(report);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApiResponse<CombinedReportResponse>.SuccessResponse(await BuildCombinedAsync(mission, ct));
    }

    public async Task<ApiResponse<CombinedReportResponse>> CloseMissionAsync(
        CombinedReportActionRequest request, int userId, CancellationToken ct = default)
    {
        var mission = await _unitOfWork.Events.Query()
            .FirstOrDefaultAsync(e => e.PublicId == (request == null ? Guid.Empty : request.EventId), ct);

        if (mission == null)
            return ApiResponse<CombinedReportResponse>.NotFoundResponse("Mission not found.");

        if (mission.Status == MissionStatuses.Closed)
            return ApiResponse<CombinedReportResponse>.ErrorResponse(
                "This mission is already closed.", "ALREADY_CLOSED");

        var report = await _unitOfWork.CombinedReports.QueryNoTracking()
            .FirstOrDefaultAsync(c => c.EventId == mission.Id, ct);

        // The one gate on closing. Settlement is the other half of this rule in
        // the workflow, but finance is out of scope for DMS, so the report is
        // the whole of it here.
        if (report?.Status != CombinedReportStatuses.Published)
            return ApiResponse<CombinedReportResponse>.ErrorResponse(
                "The combined mission report has to be published before the mission can close.",
                "REPORT_NOT_PUBLISHED");

        mission.Status = MissionStatuses.Closed;
        mission.SetUpdateAudit(userId);
        _unitOfWork.Events.Update(mission);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApiResponse<CombinedReportResponse>.SuccessResponse(
            await BuildCombinedAsync(mission, ct), "Mission closed and archived.");
    }

    // ── Building ────────────────────────────────────────────────────────────

    private async Task<ApiResponse<PostMissionReportResponse>> OneAsync(Guid participationId, CancellationToken ct)
    {
        var missionId = await _unitOfWork.EventGuests.QueryNoTracking()
            .Where(eg => eg.PublicId == participationId)
            .Select(eg => eg.Event.PublicId)
            .FirstOrDefaultAsync(ct);

        var mission = await FindMissionAsync(missionId, ct);
        if (mission == null)
            return ApiResponse<PostMissionReportResponse>.NotFoundResponse("Mission not found.");

        var row = (await BuildReportsAsync(mission, ct)).FirstOrDefault(r => r.ParticipationId == participationId);
        return row == null
            ? ApiResponse<PostMissionReportResponse>.NotFoundResponse("Report not found.")
            : ApiResponse<PostMissionReportResponse>.SuccessResponse(row);
    }

    /// <summary>
    /// The roster with each delegate's report and their trip facts. Facts are
    /// read fresh every time rather than snapshotted into the report, so a
    /// correction to a booking is reflected without anyone re-saving.
    /// </summary>
    private async Task<List<PostMissionReportResponse>> BuildReportsAsync(Event mission, CancellationToken ct)
    {
        var roster = await _unitOfWork.EventGuests.QueryNoTracking()
            .Where(eg => eg.EventId == mission.Id)
            .OrderBy(eg => eg.Guest.FirstName).ThenBy(eg => eg.Guest.LastName)
            .Select(eg => new
            {
                eg.Id,
                eg.PublicId,
                PersonId = eg.Guest.PublicId,
                FullName = (eg.Guest.FirstName + " " + eg.Guest.LastName).Trim(),
                eg.Guest.Email,
                eg.Guest.JobTitle,
                DepartmentName = eg.Guest.Department != null ? eg.Guest.Department.Name : null,
                MissionRoleName = eg.MissionRole != null ? eg.MissionRole.Name : null,
                eg.Subgroup,
                Report = eg.PostMissionReport,
            })
            .ToListAsync(ct);

        if (roster.Count == 0) return new List<PostMissionReportResponse>();

        var ids = roster.Select(r => r.Id).ToList();

        var flights = await _unitOfWork.Flights.QueryNoTracking()
            .Where(f => ids.Contains(f.EventGuestId))
            .SelectMany(f => f.Legs.Select(l => new
            {
                f.EventGuestId,
                l.FlightNumber,
                From = l.FromAirport != null ? l.FromAirport.Code : null,
                To = l.ToAirport != null ? l.ToAirport.Code : null,
                l.StartTime,
            }))
            .ToListAsync(ct);

        var hotels = await _unitOfWork.Accommodations.QueryNoTracking()
            .Where(a => ids.Contains(a.EventGuestId))
            .Select(a => new
            {
                a.EventGuestId,
                Hotel = a.Contract != null && a.Contract.Hotel != null ? a.Contract.Hotel.Name : null,
                a.CheckIn,
                a.CheckOut,
            })
            .ToListAsync(ct);

        var transport = await _unitOfWork.Transports.QueryNoTracking()
            .Where(t => ids.Contains(t.EventGuestId))
            .Select(t => new
            {
                t.EventGuestId,
                From = t.PickupLocation != null ? t.PickupLocation.Address : null,
                To = t.DropoffLocation != null ? t.DropoffLocation.Address : null,
                t.PickupTime,
            })
            .ToListAsync(ct);

        var sessions = await _unitOfWork.GuestSessions.QueryNoTracking()
            .Where(gs => ids.Contains(gs.EventGuestId) && gs.Session != null)
            .Select(gs => new { gs.EventGuestId, gs.Session.Title, gs.Session.Date })
            .ToListAsync(ct);

        var destination = mission.Destination?.Address;

        return roster.Select(r =>
        {
            var hotel = hotels.FirstOrDefault(h => h.EventGuestId == r.Id);

            return new PostMissionReportResponse
            {
                ParticipationId = r.PublicId,
                PersonId = r.PersonId,
                FullName = r.FullName,
                Email = r.Email,
                JobTitle = r.JobTitle,
                DepartmentName = r.DepartmentName,
                MissionRoleName = r.MissionRoleName,
                Subgroup = r.Subgroup,

                Status = r.Report?.Status ?? PostMissionReportStatuses.NotSubmitted,
                Narrative = r.Report?.Narrative,
                SubmittedOn = r.Report?.SubmittedOn,
                NudgeCount = r.Report?.NudgeCount ?? 0,
                LastNudgeOn = r.Report?.LastNudgeOn,

                Facts = new TripFacts
                {
                    MissionTitle = mission.Title,
                    StartDate = mission.StartDate,
                    EndDate = mission.EndDate,
                    Destination = destination,
                    HostName = mission.HostName,

                    Hotel = hotel?.Hotel,
                    CheckIn = hotel?.CheckIn,
                    CheckOut = hotel?.CheckOut,

                    Flights = flights.Where(f => f.EventGuestId == r.Id)
                        .OrderBy(f => f.StartTime)
                        .Select(f => Join(f.FlightNumber, f.From, f.To, f.StartTime))
                        .Where(s => s != null).ToList(),

                    Transport = transport.Where(t => t.EventGuestId == r.Id)
                        .OrderBy(t => t.PickupTime)
                        .Select(t => Join(null, t.From, t.To, t.PickupTime))
                        .Where(s => s != null).ToList(),

                    Attended = sessions.Where(s => s.EventGuestId == r.Id)
                        .OrderBy(s => s.Date)
                        .Select(s => s.Date.HasValue ? $"{s.Title} — {s.Date:dd MMM yyyy}" : s.Title)
                        .ToList(),
                },
            };
        }).ToList();
    }

    private static PostMissionReportSummary Summarise(List<PostMissionReportResponse> rows) => new()
    {
        Total = rows.Count,
        Submitted = rows.Count(r => r.Status == PostMissionReportStatuses.Submitted),
        Approved = rows.Count(r => r.Status == PostMissionReportStatuses.Approved),
        Outstanding = rows.Count(r => r.Status == PostMissionReportStatuses.NotSubmitted),
        NudgedAndStillOutstanding = rows.Count(r =>
            r.Status == PostMissionReportStatuses.NotSubmitted && r.NudgeCount > 0),
    };

    private async Task<CombinedReportResponse> BuildCombinedAsync(Event mission, CancellationToken ct)
    {
        var report = await _unitOfWork.CombinedReports.QueryNoTracking()
            .Where(c => c.EventId == mission.Id)
            .Select(c => new
            {
                c.PublicId,
                c.Status,
                c.ContentHtml,
                c.AssembledOn,
                c.ReviewedOn,
                c.ApprovedOn,
                c.PublishedUrl,
                ReviewedByName = c.ReviewedByUser != null
                    ? (c.ReviewedByUser.FirstName + " " + c.ReviewedByUser.LastName).Trim() : null,
                ApprovedByName = c.ApprovedByUser != null
                    ? (c.ApprovedByUser.FirstName + " " + c.ApprovedByUser.LastName).Trim() : null,
            })
            .FirstOrDefaultAsync(ct);

        var rows = await BuildReportsAsync(mission, ct);

        // A report submitted after the body was assembled is not in it. Same
        // signal as the nomination letter's roster-changed banner, and for the
        // same reason: what is on screen would otherwise quietly be stale.
        var newestSubmission = rows
            .Where(r => r.SubmittedOn.HasValue)
            .Select(r => r.SubmittedOn.Value)
            .DefaultIfEmpty()
            .Max();

        return new CombinedReportResponse
        {
            Id = report?.PublicId ?? Guid.Empty,
            EventId = mission.PublicId,
            MissionTitle = mission.Title,
            Status = report?.Status ?? CombinedReportStatuses.Draft,
            ContentHtml = report?.ContentHtml,
            AssembledOn = report?.AssembledOn,
            ReviewedByName = report?.ReviewedByName,
            ReviewedOn = report?.ReviewedOn,
            ApprovedByName = report?.ApprovedByName,
            ApprovedOn = report?.ApprovedOn,
            PublishedUrl = report?.PublishedUrl,

            MissionStatus = mission.Status,
            MissionClosed = mission.Status == MissionStatuses.Closed,

            Reports = Summarise(rows),
            StaleSinceAssembled = report?.AssembledOn != null
                && newestSubmission != default
                && newestSubmission > report.AssembledOn.Value,
        };
    }

    /// <summary>
    /// The assembled body. Plain semantic HTML rather than a template engine:
    /// the Head of Delegation edits this text afterwards, so it has to be
    /// something a rich-text editor can round-trip without losing anything.
    /// </summary>
    private static string Assemble(Event mission, List<PostMissionReportResponse> contributing, int rosterSize)
    {
        var sb = new StringBuilder();
        var dates = FormatRange(mission.StartDate, mission.EndDate);

        sb.Append("<h2>").Append(E(mission.Title)).Append("</h2>");
        sb.Append("<p><strong>Combined Mission Report</strong>");
        if (!string.IsNullOrWhiteSpace(mission.HostName)) sb.Append(" · Host: ").Append(E(mission.HostName));
        if (dates != null) sb.Append(" · ").Append(E(dates));
        sb.Append("</p>");

        sb.Append("<h3>Summary</h3><p>")
          .Append($"This report consolidates {contributing.Count} of {rosterSize} delegate report(s) ")
          .Append("submitted for this mission.");
        if (contributing.Count < rosterSize)
            sb.Append($" {rosterSize - contributing.Count} delegate(s) had not submitted when the report was assembled.");
        sb.Append("</p>");

        sb.Append("<h3>Delegation</h3><ul>");
        foreach (var r in contributing)
        {
            sb.Append("<li>").Append(E(r.FullName));
            var role = r.MissionRoleName ?? r.JobTitle;
            if (!string.IsNullOrWhiteSpace(role)) sb.Append(" — ").Append(E(role));
            sb.Append("</li>");
        }
        sb.Append("</ul>");

        sb.Append("<h3>Delegate reports</h3>");
        foreach (var r in contributing)
        {
            sb.Append("<h4>").Append(E(r.FullName));
            if (!string.IsNullOrWhiteSpace(r.MissionRoleName)) sb.Append(" — ").Append(E(r.MissionRoleName));
            sb.Append("</h4>");

            if (r.Facts.Attended.Count > 0)
                sb.Append("<p><em>Attended: ").Append(E(string.Join("; ", r.Facts.Attended))).Append("</em></p>");

            sb.Append("<p>")
              .Append(string.IsNullOrWhiteSpace(r.Narrative)
                  ? "<em>No narrative was provided.</em>"
                  : E(r.Narrative).Replace("\n", "<br>"))
              .Append("</p>");
        }

        return sb.ToString();
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private async Task<(Event mission, CombinedReport report, ApiResponse<CombinedReportResponse> error)>
        LoadForActionAsync(Guid eventId, CancellationToken ct)
    {
        var mission = await FindMissionAsync(eventId, ct);
        if (mission == null)
            return (null, null, ApiResponse<CombinedReportResponse>.NotFoundResponse("Mission not found."));

        var report = await _unitOfWork.CombinedReports.Query()
            .FirstOrDefaultAsync(c => c.EventId == mission.Id, ct);

        return report == null
            ? (null, null, ApiResponse<CombinedReportResponse>.ErrorResponse(
                "The combined report has not been assembled yet.", "NOT_ASSEMBLED"))
            : (mission, report, null);
    }

    private Task<Event> FindMissionAsync(Guid eventId, CancellationToken ct)
        => eventId == Guid.Empty
            ? Task.FromResult<Event>(null)
            : _unitOfWork.Events.QueryNoTracking()
                .Include(e => e.Destination)
                .FirstOrDefaultAsync(e => e.PublicId == eventId, ct);

    private static string Name(Guest g) => g == null ? "This delegate" : $"{g.FirstName} {g.LastName}".Trim();

    private static string Join(string number, string from, string to, DateTime? when)
    {
        var route = from != null && to != null ? $"{from} → {to}" : from ?? to;
        var parts = new[]
        {
            number,
            route,
            when?.ToString("dd MMM", CultureInfo.InvariantCulture),
        }.Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();

        return parts.Length == 0 ? null : string.Join(", ", parts);
    }

    private static string FormatRange(DateOnly? start, DateOnly? end)
    {
        if (!start.HasValue && !end.HasValue) return null;
        if (start.HasValue && end.HasValue)
            return $"{start:dd MMM yyyy} – {end:dd MMM yyyy}";
        return (start ?? end)?.ToString("dd MMM yyyy");
    }

    private static string E(string s) => WebUtility.HtmlEncode(s ?? string.Empty);
}
