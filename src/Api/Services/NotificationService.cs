using Core.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Api.Hubs;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services
{
    public class SignalRNotificationService : INotificationService
    {
        private readonly IHubContext<MessageHub> _hubContext;
        private readonly ILogger<SignalRNotificationService> _logger;

        public SignalRNotificationService(IHubContext<MessageHub> hubContext, ILogger<SignalRNotificationService> logger)
        {
            _hubContext = hubContext;
            _logger = logger;
        }

        public async Task NotifyUserAsync(string userId, string message)
        {
            if (int.TryParse(userId, out var userIdInt))
            {
                var connectionId = ConnectedUsers.GetConnectionId(userIdInt);
                if (!string.IsNullOrEmpty(connectionId))
                {
                    await _hubContext.Clients.Client(connectionId).SendAsync("ReceiveNotification", new
                    {
                        Message = message,
                        Timestamp = DateTime.UtcNow
                    });
                    _logger.LogInformation("SignalR notification sent to user {UserId}", userId);
                }
                else
                {
                    _logger.LogWarning("User {UserId} is not connected for SignalR notification", userId);
                }
            }
        }

        public async Task NotifyGroupAsync(int groupId, string message, int excludedUserId = 0)
        {
            await _hubContext.Clients.Group($"group_{groupId}")
                .SendAsync("ReceiveGroupNotification", new
                {
                    Message = message,
                    GroupId = groupId,
                    Timestamp = DateTime.UtcNow
                });
            
            _logger.LogInformation("SignalR group notification sent to group {GroupId}", groupId);
        }
    }
}