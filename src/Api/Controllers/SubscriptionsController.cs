using Core.Dtos;
using Core.Enums;
using Core.Interfaces;
using Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class SubscriptionsController : BaseApiController
    {
        private readonly ISubscriptionService _subscriptionService;
        private readonly IPaymentService _paymentService;
        private readonly ILogger<SubscriptionsController> _logger;

        public SubscriptionsController(
            ISubscriptionService subscriptionService,
            IPaymentService paymentService,
            ILogger<SubscriptionsController> logger)
        {
            _subscriptionService = subscriptionService;
            _paymentService = paymentService;
            _logger = logger;
        }

        /// <summary>
        /// Check if the logged-in user has an active feature subscription.
        /// </summary>
        [HttpGet("has-feature/{feature}")]
        public async Task<IActionResult> HasFeature([FromRoute] FeatureType feature)
        {
            try
            {
                // Validate authentication
                var authError = ValidateUserAuth(out var userId);
                if (authError != null) return authError;

                // Validate feature
                if (!Enum.IsDefined(typeof(FeatureType), feature))
                    return Error("Invalid feature type.", 400, "INVALID_FEATURE");

                var hasFeature = await _subscriptionService.HasActiveFeatureAsync(userId, feature);
                var responseData = new { UserId = userId, Feature = feature.ToString(), Active = hasFeature };
                
                return Success(responseData, $"Feature {feature} status retrieved successfully.");
            }
            catch (Exception ex)
            {
                return HandleException(ex, "Failed to check feature status");
            }
        }

        /// <summary>
        /// Get all subscriptions for the current user
        /// </summary>
        [HttpGet("my-subscriptions")]
        public async Task<IActionResult> GetMySubscriptions()
        {
            var nameId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(nameId, out var userId))
                return Unauthorized();

            var subscriptions = await _subscriptionService.GetUserSubscriptionsAsync(userId);
            var subscriptionDtos = subscriptions.Select(s => new SubscriptionDto
            {
                Id = s.Id,
                Feature = s.Feature,
                StartDate = s.StartDate,
                EndDate = s.EndDate,
                IsActive = s.IsActive
            });

            return Ok(new { userId, subscriptions = subscriptionDtos });
        }

        /// <summary>
        /// Get a specific subscription for the current user
        /// </summary>
        [HttpGet("my-subscriptions/{feature}")]
        public async Task<IActionResult> GetMySubscription([FromRoute] FeatureType feature)
        {
            var nameId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(nameId, out var userId))
                return Unauthorized();

            var subscription = await _subscriptionService.GetUserSubscriptionAsync(userId, feature);
            if (subscription == null)
                return NotFound($"No subscription found for {feature}");

            var subscriptionDto = new SubscriptionDto
            {
                Id = subscription.Id,
                Feature = subscription.Feature,
                StartDate = subscription.StartDate,
                EndDate = subscription.EndDate,
                IsActive = subscription.IsActive
            };

            return Ok(subscriptionDto);
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

            await _subscriptionService.GrantAsync(
                request.UserId,
                request.Feature,
                TimeSpan.FromDays(request.DurationDays)
            );

            var subscription = await _subscriptionService.GetUserSubscriptionAsync(request.UserId, request.Feature);

            return Ok(new
            {
                message = $"Granted {request.Feature} to user {request.UserId} for {request.DurationDays} days.",
                subscription = subscription != null ? new SubscriptionDto
                {
                    Id = subscription.Id,
                    Feature = subscription.Feature,
                    StartDate = subscription.StartDate,
                    EndDate = subscription.EndDate,
                    IsActive = subscription.IsActive
                } : null
            });
        }

        /// <summary>
        /// Revoke a user's subscription (admin-only)
        /// </summary>
        [HttpDelete("revoke/{userId}/{feature}")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> Revoke([FromRoute] int userId, [FromRoute] FeatureType feature)
        {
            var revoked = await _subscriptionService.RevokeAsync(userId, feature);
            if (!revoked)
                return NotFound($"No active subscription found for user {userId} and feature {feature}");

            return Ok(new { message = $"Revoked {feature} from user {userId}" });
        }

        /// <summary>
        /// Get available subscription plans
        /// </summary>
        [HttpGet("plans")]
        public async Task<IActionResult> GetPlans()
        {
            var plans = await _paymentService.GetAvailablePlansAsync();
            return Ok(plans);
        }

        /// <summary>
        /// Get subscription summary for current user
        /// </summary>
        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary()
        {
            var nameId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(nameId, out var userId))
                return Unauthorized();

            var subscriptions = await _subscriptionService.GetUserSubscriptionsAsync(userId);
            var plans = await _paymentService.GetAvailablePlansAsync();

            var activeSubscriptions = subscriptions.Where(s => s.IsActive)
                .Select(s => new SubscriptionDto
                {
                    Id = s.Id,
                    Feature = s.Feature,
                    StartDate = s.StartDate,
                    EndDate = s.EndDate,
                    IsActive = s.IsActive
                }).ToList();

            var expiredSubscriptions = subscriptions.Where(s => !s.IsActive)
                .Select(s => new SubscriptionDto
                {
                    Id = s.Id,
                    Feature = s.Feature,
                    StartDate = s.StartDate,
                    EndDate = s.EndDate,
                    IsActive = s.IsActive
                }).ToList();

            // Calculate monthly cost
            decimal monthlyCost = activeSubscriptions.Sum(s =>
            {
                var plan = plans.FirstOrDefault(p => p.Feature == s.Feature);
                return plan?.MonthlyPrice ?? 0;
            });

            var summary = new UserSubscriptionSummary
            {
                UserId = userId,
                ActiveSubscriptions = activeSubscriptions,
                ExpiredSubscriptions = expiredSubscriptions,
                MonthlyCost = monthlyCost
            };

            return Ok(summary);
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