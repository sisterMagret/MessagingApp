using Core.Dtos;
using Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class MessagesController : ControllerBase
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
                if (request is null)
                    return BadRequest("Request cannot be null.");

                if (string.IsNullOrWhiteSpace(request.Content))
                    return BadRequest("Message content cannot be empty.");

                var senderIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (!int.TryParse(senderIdClaim, out var senderId))
                    return Unauthorized();

                // Validate sending to self
                if (request.ReceiverId.HasValue && request.ReceiverId.Value == senderId)
                    return BadRequest("Cannot send message to yourself");

                var message = await _messageService.SendAsync(senderId, request);
                return Ok(message);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpGet("inbox")]
        public async Task<IActionResult> GetInbox([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out var userId))
                return Unauthorized();

            var inbox = await _messageService.GetInboxAsync(userId, page, pageSize);
            return Ok(inbox);
        }

        [HttpPost("{id}/read")]
        public async Task<IActionResult> MarkAsRead([FromRoute] int id)
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out var userId))
                return Unauthorized();

            await _messageService.MarkAsReadAsync(userId, id);
            return NoContent();
        }
    }
}
