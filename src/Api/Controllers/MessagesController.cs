using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Core.Interfaces;
using Api.DTOs;

namespace Api.Controllers;

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

    [HttpPost]
    public async Task<IActionResult> Send(MessageCreateDto dto)
    {
        var senderId = int.Parse(User.FindFirst("nameid")!.Value);
        var result = await _messages.SendAsync(senderId, dto.ReceiverId, dto.Content);
        return Ok(result);
    }

    [HttpGet("inbox")]
    public async Task<IActionResult> Inbox()
    {
        var userId = int.Parse(User.FindFirst("nameid")!.Value);
        var msgs = await _messages.GetInboxAsync(userId);
        return Ok(msgs);
    }
}
