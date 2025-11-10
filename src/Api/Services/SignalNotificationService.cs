using Microsoft.AspNetCore.SignalR;
using Core.Interfaces;
using Api.Hubs;

namespace Api.Services
{
    public class SignalRNotificationService : INotificationService
    {
        private readonly IHubContext<MessageHub> _hubContext;

        public SignalRNotificationService(IHubContext<MessageHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public async Task NotifyUserAsync(string userId, string message)
        {
            await _hubContext.Clients.User(userId).SendAsync("ReceiveMessage", message);
        }
    }
}
