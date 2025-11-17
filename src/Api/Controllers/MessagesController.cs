using Core.Dtos;
using Core.Interfaces;
using Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class MessagesController : BaseApiController
    {
        private readonly IMessageService _messageService;

        public MessagesController(IMessageService messageService)
        {
            _messageService = messageService;
        }

        [HttpPost]
        public async Task<IActionResult> SendMessage([FromBody] MessageCreateRequest request)
        {
            try
            {
                // Validate authentication
                var authError = ValidateUserAuth(out var senderId);
                if (authError != null) return authError;

                // Validate request
                var validationError = ValidateRequired(
                    (request, "request"),
                    (request?.Content, "content")
                );
                if (validationError != null) return validationError;

                // Additional validations
                if (string.IsNullOrWhiteSpace(request!.Content))
                    return Error("Message content cannot be empty.", 400, "EMPTY_CONTENT");

                if (request.Content.Length > 1000)
                    return Error("Message content cannot exceed 1000 characters.", 400, "CONTENT_TOO_LONG");

                // Validate not sending to self
                if (request.ReceiverId.HasValue && request.ReceiverId.Value == senderId)
                    return Error("You cannot send a message to yourself.", 400, "SELF_MESSAGE");

                var message = await _messageService.SendAsync(senderId, request);
                return Success(message, "Message sent successfully.");
            }
            catch (ArgumentException ex)
            {
                return Error($"Invalid message data: {ex.Message}", 400, "INVALID_MESSAGE_DATA");
            }
            catch (UnauthorizedAccessException ex)
            {
                return Error($"Access denied: {ex.Message}", 403, "ACCESS_DENIED");
            }
            catch (Exception ex)
            {
                return HandleException(ex, "Failed to send message");
            }
        }

        [HttpGet("inbox")]
        public async Task<IActionResult> GetInbox([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        {
            try
            {
                // Validate authentication
                var authError = ValidateUserAuth(out var userId);
                if (authError != null) return authError;

                // Validate pagination parameters
                if (page < 1)
                    return Error("Page number must be greater than 0.", 400, "INVALID_PAGE");

                if (pageSize < 1 || pageSize > 100)
                    return Error("Page size must be between 1 and 100.", 400, "INVALID_PAGE_SIZE");

                var inbox = await _messageService.GetInboxAsync(userId, page, pageSize);
                return Success(inbox, $"Retrieved {inbox.Items.Count} messages from your inbox.");
            }
            catch (Exception ex)
            {
                return HandleException(ex, "Failed to retrieve inbox messages");
            }
        }

        [HttpPost("{id}/read")]
        public async Task<IActionResult> MarkAsRead([FromRoute] int id)
        {
            try
            {
                // Validate authentication
                var authError = ValidateUserAuth(out var userId);
                if (authError != null) return authError;

                // Validate message ID
                if (id <= 0)
                    return Error("Invalid message ID.", 400, "INVALID_MESSAGE_ID");

                await _messageService.MarkAsReadAsync(userId, id);
                return Success("Message marked as read.");
            }
            catch (ArgumentException ex) when (ex.Message.Contains("not found"))
            {
                return Error("Message not found or you don't have permission to access it.", 404, "MESSAGE_NOT_FOUND");
            }
            catch (Exception ex)
            {
                return HandleException(ex, "Failed to mark message as read");
            }
        }
    }
}
