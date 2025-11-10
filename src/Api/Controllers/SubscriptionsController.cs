using Core.Enums;
using Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class SubscriptionsController : ControllerBase
    {
        private readonly ISubscriptionService _subscriptionService;

        public SubscriptionsController(ISubscriptionService subscriptionService)
        {
            _subscriptionService = subscriptionService;
        }

        /// <summary>
        /// Check if the logged-in user has an active feature subscription.
        /// </summary>
        [HttpGet("has-feature/{feature}")]
        public async Task<IActionResult> HasFeature([FromRoute] FeatureType feature)
        {
            var nameId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(nameId, out var userId))
                return Unauthorized();

            var hasFeature = await _subscriptionService.HasActiveFeatureAsync(userId, feature);
            return Ok(new { userId, feature, active = hasFeature });
        }

        /// <summary>
        /// Grant or extend a subscription for a user (admin-only).
        /// </summary>
        [HttpPost("grant")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> Grant([FromBody] GrantSubscriptionRequest request)
        {
            if (request.DurationDays <= 0)
                return BadRequest("Duration must be greater than 0 days.");

            await _subscriptionService.GrantAsync(request.UserId, request.Feature, TimeSpan.FromDays(request.DurationDays));

            return Ok(new
            {
                message = $"Granted {request.Feature} to user {request.UserId} for {request.DurationDays} days."
            });
        }
    }

    /// <summary>
    /// Request model for granting a subscription.
    /// </summary>
    public class GrantSubscriptionRequest
    {
        public int UserId { get; set; }
        public FeatureType Feature { get; set; }
        public int DurationDays { get; set; }
    }
}
