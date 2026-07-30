using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Core.Interfaces.Repositories;
using Core.Interfaces.Services;
using Core.ViewModel.Noification;
using DomainPersistence.Entities;
using FirebaseNotification = FirebaseAdmin.Messaging.Notification;

// Matches the existing convention of this folder — no namespace declared.
//
// Real device push (FCM), for when the recipient's app isn't open — see
// ManualNotificationProvider for the live in-app SignalR channel, which this
// runs alongside (not instead of): both are registered as IPushNotificationProvider
// in ServiceExtensions and NotificationManagerService fans out to every one.
//
// Every recipient — staff, driver, or guest — is a User now, so this fans out
// over DomainPersistence.Entities.Device rows filtered by UserId (one guest/
// staff member can be logged into several devices; every active, opt-in one
// gets its own independent send attempt).
//
// Configuration (see appsettings.json "Firebase" section): a service-account
// key, either as a file path (Firebase:ServiceAccountKeyPath — preferred, the
// path itself comes from a secret store in production) or inline JSON
// (Firebase:ServiceAccountJson, for environments that inject secrets as env
// vars). Neither set => this provider silently no-ops (logged once), so an
// environment without Firebase configured still runs fine — SignalR delivery
// alone still works.
public class FirebaseNotificationProvider(
    IUnitOfWork _unitOfWork,
    IConfiguration _configuration,
    ILogger<FirebaseNotificationProvider> _logger) : IPushNotificationProvider
{
    private static readonly object InitLock = new();
    private static FirebaseApp _app;
    private static bool _initAttempted;

    public async Task SendAsync(PushNotificationPayload payload, CancellationToken ct = default)
    {
        var app = GetOrInitializeApp();
        if (app is null) return;

        var devices = await _unitOfWork.Devices.Query()
            .Where(d => d.UserId == payload.UserId && d.IsActive && d.NotificationsEnabled)
            .ToListAsync(ct).ConfigureAwait(false);

        if (devices.Count == 0) return;

        var messaging = FirebaseMessaging.GetMessaging(app);
        foreach (var device in devices)
        {
            try
            {
                await SendToDeviceAsync(messaging, device, payload, ct).ConfigureAwait(false);
            }
            catch (FirebaseMessagingException fcmEx) when (IsDeadToken(fcmEx))
            {
                // Token is permanently invalid — deactivate rather than delete, so
                // the row's history (which platform/version last worked) survives.
                device.IsActive = false;
                _unitOfWork.Devices.Update(device);
                await _unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);
                _logger.LogWarning(fcmEx, "Deactivated dead FCM token for user {UserId} (device {DeviceId})", payload.UserId, device.Id);
            }
            catch (Exception ex)
            {
                // One dead/invalid token must not stop the user's other devices.
                _logger.LogError(ex, "Error pushing to device {DeviceId} (user {UserId})", device.Id, payload.UserId);
            }
        }
    }

    private async Task SendToDeviceAsync(FirebaseMessaging messaging, Device device, PushNotificationPayload payload, CancellationToken ct)
    {
        var message = new Message
        {
            Token = device.Token,
            Notification = new FirebaseNotification { Title = payload.Title, Body = payload.Body },
            // FCM data payloads are string-only.
            Data = payload.Data?.ToDictionary(kv => kv.Key, kv => kv.Value ?? string.Empty)
                ?? new Dictionary<string, string>(),
        };

        await messaging.SendAsync(message, ct).ConfigureAwait(false);

        device.LastActiveAt = DateTime.UtcNow;
        _unitOfWork.Devices.Update(device);
        await _unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    private static bool IsDeadToken(FirebaseMessagingException ex)
        => ex.MessagingErrorCode is MessagingErrorCode.Unregistered or MessagingErrorCode.SenderIdMismatch;

    // Lazy, once-per-process init — cheap to call on every SendAsync once warm,
    // and a missing/broken config degrades to "no Firebase push" instead of
    // crashing the app (SignalR delivery via ManualNotificationProvider is
    // unaffected either way).
    private FirebaseApp GetOrInitializeApp()
    {
        if (_app != null) return _app;
        lock (InitLock)
        {
            if (_app != null || _initAttempted) return _app;
            _initAttempted = true;

            try
            {
                var keyPath = _configuration["Firebase:ServiceAccountKeyPath"];
                var inlineJson = _configuration["Firebase:ServiceAccountJson"];
                var projectId = _configuration["Firebase:ProjectId"];

                GoogleCredential credential;
                if (!string.IsNullOrWhiteSpace(keyPath) && File.Exists(keyPath))
                    credential = GoogleCredential.FromFile(keyPath);
                else if (!string.IsNullOrWhiteSpace(inlineJson))
                    credential = GoogleCredential.FromJson(inlineJson);
                else
                {
                    _logger.LogWarning(
                        "Firebase is not configured (Firebase:ServiceAccountKeyPath / Firebase:ServiceAccountJson) — device push notifications are disabled; SignalR delivery still works.");
                    return null;
                }

                _app = FirebaseApp.Create(new AppOptions
                {
                    Credential = credential,
                    ProjectId = string.IsNullOrWhiteSpace(projectId) ? null : projectId,
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Firebase — device push notifications are disabled.");
            }

            return _app;
        }
    }
}
