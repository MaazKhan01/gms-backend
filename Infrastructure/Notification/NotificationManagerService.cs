using Microsoft.Extensions.Logging;
using Core.Interfaces;
using Core.Interfaces.Repositories;
using Core.Interfaces.Services;
using Core.ViewModel.Noification;
using DomainPersistence.Entities;

public class NotificationManagerService(
    IUnitOfWork _unitOfWork,
    IRealTimeAlertService _realTimeAlertService,
    ILogger<NotificationManagerService> _logger
) : INotificationManagerService
{
    public async Task SendAlert(SendAlertRequest input)
    {
        try
        {
            if (!string.IsNullOrEmpty(input.Topic))
            {
                await _realTimeAlertService.SendToAllAsync(
                    topic: input.Topic,
                    title: input.Title,
                    message: input.Message,
                    data: input.Metadata
                ).ConfigureAwait(false);
            }

            if (input.UserId.HasValue)
            {
                // input.UserId is the public Guid; resolve it to the internal int FK.
                var user = await _unitOfWork.Users.GetByPublicIdAsync(input.UserId.Value).ConfigureAwait(false);
                if (user != null)
                {
                    var notification = new Notification
                    {
                        UserId = user.Id,
                        Title = input.Title ?? string.Empty,
                        Message = input.Message,
                        Type = input.NotificationTemplateCode,
                        RedirectUrl = input.Url,
                        Read = false,
                        CreatedAt = DateTime.UtcNow
                    };

                    await _unitOfWork.Notifications.AddAsync(notification).ConfigureAwait(false);
                    await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending alert");
        }
    }
}
