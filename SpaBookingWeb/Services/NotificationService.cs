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

        // Chỉ cần HubContext, không cần Database
        public NotificationService(IHubContext<NotificationHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public async Task NotifyAsync(string title, string content, string icon, string link = "#")
        {
            // Bắn tín hiệu trực tiếp cho client
            await _hubContext.Clients.All.SendAsync("ReceiveNotification", new 
            {
                title = title,
                content = content,
                icon = icon,
                link = link,
                createdAt = DateTime.Now.ToString("HH:mm") // Chỉ hiển thị giờ phút hiện tại
            });
        }
    }
}