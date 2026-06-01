using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

    public interface IRealTimeAlertService
    {
        /// <summary>
        /// Send a real-time message to all connected clients
        /// </summary>
        /// <param name="topic">The SignalR method/topic name to invoke on clients</param>
        /// <param name="title">The title/subject of the message</param>
        /// <param name="message">The message content to send</param>
        /// <param name="data">Optional additional data object to include</param>
        Task SendToAllAsync(string topic, string title, string message, object data = null);

        /// <summary>
        /// Send a real-time message to a specific user
        /// </summary>
        /// <param name="topic">The SignalR method/topic name to invoke on clients</param>
        /// <param name="userId">The target user ID</param>
        /// <param name="title">The title/subject of the message</param>
        /// <param name="message">The message content to send</param>
        /// <param name="data">Optional additional data object to include</param>
        Task SendToUserAsync(string topic, string userId, string title, string message, object data = null);

        /// <summary>
        /// Send a real-time message to all clients in a specific group
        /// </summary>
        /// <param name="topic">The SignalR method/topic name to invoke on clients</param>
        /// <param name="groupName">The target group name</param>
        /// <param name="title">The title/subject of the message</param>
        /// <param name="message">The message content to send</param>
        /// <param name="data">Optional additional data object to include</param>
        Task SendToGroupAsync(string topic, string groupName, string title, string message, object data = null);

        /// <summary>
        /// Send a real-time message to a specific connection
        /// </summary>
        /// <param name="topic">The SignalR method/topic name to invoke on clients</param>
        /// <param name="connectionId">The target connection ID</param>
        /// <param name="title">The title/subject of the message</param>
        /// <param name="message">The message content to send</param>
        /// <param name="data">Optional additional data object to include</param>
        Task SendToConnectionAsync(string topic, string connectionId, string title, string message, object data = null);

        /// <summary>
        /// Associate a connection with a group
        /// </summary>
        /// <param name="connectionId">The target connection ID</param>
        /// <param name="groupName">The target group name</param>
        Task JoinGroupAsync(string connectionId, string groupName);

        /// <summary>
        /// Remove a connection from a group
        /// </summary>
        /// <param name="connectionId">The target connection ID</param>
        /// <param name="groupName">The target group name</param>
        Task LeaveGroupAsync(string connectionId, string groupName);
    }

