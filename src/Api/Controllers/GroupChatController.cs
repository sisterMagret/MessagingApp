using Core.Dtos;
using Core.Interfaces;
using Core.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    [ApiController]
    [Route("api/groups")]
    [Authorize]
    public class GroupChatController : ControllerBase
    {
        private readonly IGroupService _groupService;
        private readonly ISubscriptionService _subscriptionService;

        public GroupChatController(IGroupService groupService, ISubscriptionService subscriptionService)
        {
            _groupService = groupService;
            _subscriptionService = subscriptionService;
        }

        [HttpPost]
        public async Task<IActionResult> CreateGroup([FromBody] GroupCreateRequest request)
        {
            try
            {
                var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (!int.TryParse(userIdClaim, out var userId))
                    return Unauthorized(new { error = "Invalid user authentication." });

                // Validate request
                if (request == null || string.IsNullOrWhiteSpace(request.Name))
                {
                    return BadRequest(new { error = "Group name is required." });
                }

                // Check if user has Group Chat subscription
                var hasGroupChat = await _subscriptionService.HasActiveFeatureAsync(userId, FeatureType.GroupChat);
                if (!hasGroupChat)
                {
                    return StatusCode(403, new
                    {
                        error = "Group chat feature requires a subscription.",
                        message = "Please subscribe to the Group Chat plan to create groups.",
                        feature = "GroupChat"
                    });
                }

                var createReq = new CreateGroupRequest { Name = request.Name, Description = request.Description };
                var group = await _groupService.CreateGroupAsync(userId, createReq);
                return Ok(new
                {
                    success = true,
                    message = "Group created successfully.",
                    data = group
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    error = "An error occurred while creating the group.",
                    details = ex.Message
                });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetUserGroups()
        {
            try
            {
                var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (!int.TryParse(userIdClaim, out var userId))
                    return Unauthorized(new { error = "Invalid user authentication." });

                // Check if user has Group Chat subscription
                var hasGroupChat = await _subscriptionService.HasActiveFeatureAsync(userId, FeatureType.GroupChat);
                if (!hasGroupChat)
                {
                    return StatusCode(403, new
                    {
                        error = "Group chat feature requires a subscription.",
                        message = "Please subscribe to the Group Chat plan to access your groups.",
                        feature = "GroupChat"
                    });
                }

                var groups = await _groupService.GetUserGroupsAsync(userId);
                return Ok(new
                {
                    success = true,
                    message = $"Found {groups.Count} groups.",
                    data = groups
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    error = "An error occurred while retrieving groups.",
                    details = ex.Message
                });
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetGroup([FromRoute] int id)
        {
            try
            {
                var group = await _groupService.GetGroupAsync(id);
                if (group == null)
                    return NotFound(new { error = "Group not found." });

                return Ok(new
                {
                    success = true,
                    message = "Group retrieved successfully.",
                    data = group
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    error = "An error occurred while retrieving the group.",
                    details = ex.Message
                });
            }
        }

        [HttpPost("{groupId}/members/{userId}")]
        public async Task<IActionResult> AddMember([FromRoute] int groupId, [FromRoute] int userId)
        {
            try
            {
                var currentUserIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (!int.TryParse(currentUserIdClaim, out var currentUserId))
                    return Unauthorized(new { error = "Invalid user authentication." });

                var hasGroupChat = await _subscriptionService.HasActiveFeatureAsync(currentUserId, FeatureType.GroupChat);
                if (!hasGroupChat)
                {
                    return StatusCode(403, new
                    {
                        error = "Group chat feature requires a subscription.",
                        message = "Please subscribe to the Group Chat plan to manage group members.",
                        feature = "GroupChat"
                    });
                }

                await _groupService.AddMemberAsync(groupId, userId, currentUserId);
                return Ok(new
                {
                    success = true,
                    message = "Member added successfully to the group."
                });
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("already a member"))
            {
                return BadRequest(new { error = "User is already a member of this group." });
            }
            catch (ArgumentException ex) when (ex.Message.Contains("not found"))
            {
                return NotFound(new { error = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    error = "An error occurred while adding member to group.",
                    details = ex.Message
                });
            }
        }

        [HttpDelete("{groupId}/members/{userId}")]
        public async Task<IActionResult> RemoveMember([FromRoute] int groupId, [FromRoute] int userId)
        {
            try
            {
                var currentUserIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (!int.TryParse(currentUserIdClaim, out var currentUserId))
                    return Unauthorized(new { error = "Invalid user authentication." });

                var hasGroupChat = await _subscriptionService.HasActiveFeatureAsync(currentUserId, FeatureType.GroupChat);
                if (!hasGroupChat)
                {
                    return StatusCode(403, new
                    {
                        error = "Group chat feature requires a subscription.",
                        message = "Please subscribe to the Group Chat plan to manage group members.",
                        feature = "GroupChat"
                    });
                }

                await _groupService.RemoveMemberAsync(groupId, userId, currentUserId);
                return Ok(new
                {
                    success = true,
                    message = "Member removed successfully from the group."
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    error = "An error occurred while removing member from group.",
                    details = ex.Message
                });
            }
        }

        [HttpGet("subscription-status")]
        public async Task<IActionResult> GetSubscriptionStatus()
        {
            try
            {
                var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (!int.TryParse(userIdClaim, out var userId))
                    return Unauthorized(new { error = "Invalid user authentication." });

                var hasGroupChat = await _subscriptionService.HasActiveFeatureAsync(userId, FeatureType.GroupChat);
                var subscription = await _subscriptionService.GetUserSubscriptionAsync(userId, FeatureType.GroupChat);
                var allSubscriptions = await _subscriptionService.GetUserSubscriptionsAsync(userId);

                return Ok(new
                {
                    success = true,
                    userId = userId,
                    hasGroupChatFeature = hasGroupChat,
                    groupChatSubscription = subscription,
                    allSubscriptions = allSubscriptions.Select(s => new
                    {
                        feature = s.Feature.ToString(),
                        startDate = s.StartDate,
                        endDate = s.EndDate,
                        isActive = s.EndDate >= DateTime.UtcNow
                    }),
                    currentTime = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    error = "An error occurred while checking subscription status.",
                    details = ex.Message
                });
            }
        }

        [HttpDelete("{groupId}")]
        public async Task<IActionResult> DeleteGroup([FromRoute] int groupId)
        {
            try
            {
                var currentUserIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (!int.TryParse(currentUserIdClaim, out var currentUserId))
                    return Unauthorized(new { error = "Invalid user authentication." });

                var hasGroupChat = await _subscriptionService.HasActiveFeatureAsync(currentUserId, FeatureType.GroupChat);
                if (!hasGroupChat)
                {
                    return StatusCode(403, new
                    {
                        error = "Group chat feature requires a subscription.",
                        message = "Please subscribe to the Group Chat plan to delete groups.",
                        feature = "GroupChat"
                    });
                }

                await _groupService.DeleteGroupAsync(groupId, currentUserId);
                return Ok(new
                {
                    success = true,
                    message = "Group deleted successfully."
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    error = "An error occurred while deleting the group.",
                    details = ex.Message
                });
            }
        }
    }
}