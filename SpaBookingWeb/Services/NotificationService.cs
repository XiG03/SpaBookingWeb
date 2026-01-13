using Microsoft.AspNetCore.SignalR;
using SpaBookingWeb.Hubs;
using System.Threading.Tasks;
using System;

namespace SpaBookingWeb.Services
{
    public interface INotificationService
    {
        Task NotifyAsync(string title, string content, string icon, string link = "#");
    }

    public class NotificationService : INotificationService
    {
        private readonly IHubContext<NotificationHub> _hubContext;

        // Just need HubContext, no Database needed
        public NotificationService(IHubContext<NotificationHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public async Task NotifyAsync(string title, string content, string icon, string link = "#")
        {
            // Send signal directly to client
            await _hubContext.Clients.All.SendAsync("ReceiveNotification", new 
            {
                title = title,
                content = content,
                icon = icon,
                link = link,
                createdAt = DateTime.Now.ToString("HH:mm") // Only display current hour and minute
            });
        }
    }
}