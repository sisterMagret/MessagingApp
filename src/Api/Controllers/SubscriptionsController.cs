using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Core.Interfaces;
using Api.DTOs;
using Core.Enums;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SubscriptionsController : ControllerBase
{
    private readonly ISubscriptionService _subs;

    public SubscriptionsController(ISubscriptionService subs)
    {
        _subs = subs;
    }

    [HttpPost]
    public async Task<IActionResult> Create(SubscriptionCreateDto dto)
    {
        var userId = int.Parse(User.FindFirst("nameid")!.Value);
        var result = await _subs.CreateAsync(userId, dto.Features, dto.Months);
        return Ok(result);
    }

    [HttpGet("active")]
    public async Task<IActionResult> Active()
    {
        var userId = int.Parse(User.FindFirst("nameid")!.Value);
        var result = await _subs.GetActiveAsync(userId);
        return Ok(result);
    }
}
