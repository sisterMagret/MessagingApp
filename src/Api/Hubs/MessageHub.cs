using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using Core.Dtos;

namespace Api.Hubs
{
    public class MessageHub : Hub
    {
        // Called when a user sends a message via SignalR
        public async Task SendMessage(MessageDto message)
        {
            // Send to the specific receiver if connected
            var receiverConnectionId = ConnectedUsers.GetConnectionId(message.ReceiverId);
            if (!string.IsNullOrEmpty(receiverConnectionId))
            {
                await Clients.Client(receiverConnectionId).SendAsync("ReceiveMessage", message);
            }
        }

        public override async Task OnConnectedAsync()
        {
            // Get userId from claims
            var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdClaim, out var userId))
            {
                ConnectedUsers.Add(userId, Context.ConnectionId);
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            // Remove from connection map
            var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdClaim, out var userId))
            {
                ConnectedUsers.Remove(userId);
            }

            await base.OnDisconnectedAsync(exception);
        }
    }
}
