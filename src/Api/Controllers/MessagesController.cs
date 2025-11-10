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
        private readonly IMessageService _messages;

        public MessagesController(IMessageService messages)
        {
            _messages = messages;
        }

        // POST api/messages
        [HttpPost]
        public async Task<IActionResult> Send([FromBody] MessageCreateRequest request)
        {
            // parse sender id from JWT claims
            var nameId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(nameId, out var senderId))
                return Unauthorized();

            var dto = await _messages.SendAsync(senderId, request);
            return Ok(dto);
        }

        // GET api/messages/inbox?page=1&pageSize=50
        [HttpGet("inbox")]
        public async Task<IActionResult> Inbox([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        {
            var nameId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(nameId, out var userId))
                return Unauthorized();

            var messages = await _messages.GetInboxAsync(userId, page, pageSize);
            return Ok(messages);
        }

        // POST api/messages/{id}/read
        [HttpPost("{id}/read")]
        public async Task<IActionResult> MarkRead([FromRoute] int id)
        {
            var nameId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(nameId, out var userId))
                return Unauthorized();

            await _messages.MarkAsReadAsync(userId, id);
            return NoContent();
        }
    }
}
