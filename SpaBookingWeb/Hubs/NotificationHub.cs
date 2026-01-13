using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace SpaBookingWeb.Hubs
{
    public class NotificationHub : Hub
    {
        // Hàm này để Client gọi lên Server (nếu cần)
        public async Task SendNotification(string message)
        {
            await Clients.All.SendAsync("ReceiveNotification", message);
        }
    }
}