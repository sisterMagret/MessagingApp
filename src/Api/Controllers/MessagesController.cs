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

        // POST api/messages
        [HttpPost]
        public async Task<IActionResult> SendMessage([FromBody] MessageCreateRequest request)
        {
            if (request is null)
                return BadRequest("Request cannot be null.");

            if (string.IsNullOrWhiteSpace(request.Content))
                return BadRequest("Message content cannot be empty.");

            var senderIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(senderIdClaim, out var senderId))
                return Unauthorized();

            var message = await _messageService.SendAsync(senderId, request);
            return Ok(message);
        }

        // GET api/messages/inbox?page=1&pageSize=50
        [HttpGet("inbox")]
        public async Task<IActionResult> GetInbox([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out var userId))
                return Unauthorized();

            var inbox = await _messageService.GetInboxAsync(userId, page, pageSize);
            return Ok(inbox);
        }

        // POST api/messages/{id}/read
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
