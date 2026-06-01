using Microsoft.AspNetCore.SignalR;
using Core.Helpers;
using Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

    public class RealTimeAlertService : IRealTimeAlertService
    {
        private readonly IHubContext<RealTimeHubService> _hubService;
        public RealTimeAlertService(IHubContext<RealTimeHubService> hubService)
        {
            _hubService = hubService;
        }
        public async Task SendToAllAsync(string topic, string title, string message, object data = null)
        {
            await _hubService.Clients.All.SendAsync(topic, title, message, data);
        }

        public async Task SendToUserAsync(string topic, string userId, string title, string message, object data = null)
        {
            await _hubService.Clients.User(userId).SendAsync(topic, title, message, data);
        }

        public async Task SendToGroupAsync(string topic, string groupName, string title, string message, object data = null)
        {
            await _hubService.Clients.Group(groupName).SendAsync(topic, title, message, data);
        }

        public async Task SendToConnectionAsync(string topic, string connectionId, string title, string message, object data = null)
        {
            await _hubService.Clients.Client(connectionId).SendAsync(topic, title, message, data);
        }

        public async Task JoinGroupAsync(string connectionId, string groupName)
        {
            await _hubService.Groups.AddToGroupAsync(connectionId, groupName);
        }

        public async Task LeaveGroupAsync(string connectionId, string groupName)
        {
            await _hubService.Groups.RemoveFromGroupAsync(connectionId, groupName);
        }
    }


