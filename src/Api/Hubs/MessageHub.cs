using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using Core.Dtos;

namespace Api.Hubs
{
    public class MessageHub : Hub
    {
        private readonly ILogger<MessageHub> _logger;

        public MessageHub(ILogger<MessageHub> logger)
        {
            _logger = logger;
        }

        // Called when a user sends a message via SignalR
        public async Task SendMessage(MessageDto message)
        {
            _logger.LogInformation("SignalR: User {SenderId} sending message to {ReceiverId}", 
                message.SenderId, message.ReceiverId);

            // Send to the specific receiver if connected
            var receiverConnectionId = ConnectedUsers.GetConnectionId(message.ReceiverId ?? 0);
            if (!string.IsNullOrEmpty(receiverConnectionId))
            {
                await Clients.Client(receiverConnectionId).SendAsync("ReceiveMessage", message);
                _logger.LogInformation("SignalR: Message delivered to user {ReceiverId}", message.ReceiverId);
            }
            else
            {
                _logger.LogInformation("SignalR: User {ReceiverId} is not connected", message.ReceiverId);
            }

            // If it's a group message, send to group
            if (message.GroupId.HasValue)
            {
                await Clients.Group($"group_{message.GroupId}").SendAsync("ReceiveGroupMessage", message);
                _logger.LogInformation("SignalR: Group message sent to group {GroupId}", message.GroupId);
            }
        }

        // Join a group
        public async Task JoinGroup(int groupId)
        {
            var userId = GetUserIdFromContext();
            if (userId.HasValue)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"group_{groupId}");
                _logger.LogInformation("SignalR: User {UserId} joined group {GroupId}", userId, groupId);
            }
        }

        // Leave a group
        public async Task LeaveGroup(int groupId)
        {
            var userId = GetUserIdFromContext();
            if (userId.HasValue)
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"group_{groupId}");
                _logger.LogInformation("SignalR: User {UserId} left group {GroupId}", userId, groupId);
            }
        }

        public override async Task OnConnectedAsync()
        {
            var userId = GetUserIdFromContext();
            if (userId.HasValue)
            {
                ConnectedUsers.Add(userId.Value, Context.ConnectionId);
                _logger.LogInformation("SignalR: User {UserId} connected with connection {ConnectionId}", 
                    userId, Context.ConnectionId);
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = GetUserIdFromContext();
            if (userId.HasValue)
            {
                ConnectedUsers.Remove(userId.Value);
                _logger.LogInformation("SignalR: User {UserId} disconnected", userId);
            }
            else
            {
                ConnectedUsers.RemoveByConnectionId(Context.ConnectionId);
            }

            await base.OnDisconnectedAsync(exception);
        }

        private int? GetUserIdFromContext()
        {
            var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdClaim, out var userId))
            {
                return userId;
            }
            return null;
        }
    }
}