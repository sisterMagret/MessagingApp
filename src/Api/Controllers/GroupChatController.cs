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
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out var userId))
                return Unauthorized();

            // Check if user has Group Chat subscription
            var hasGroupChat = await _subscriptionService.HasActiveFeatureAsync(userId, FeatureType.GroupChat);
            if (!hasGroupChat)
            {
                return StatusCode(403, "Group chat feature requires a subscription. Please subscribe to the Group Chat plan.");
            }

            var createReq = new CreateGroupRequest { Name = request.Name, Description = request.Description };
            var group = await _groupService.CreateGroupAsync(userId, createReq);
            return Ok(group);
        }

        [HttpGet]
        public async Task<IActionResult> GetUserGroups()
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out var userId))
                return Unauthorized();

            // Check if user has Group Chat subscription
            var hasGroupChat = await _subscriptionService.HasActiveFeatureAsync(userId, FeatureType.GroupChat);
            if (!hasGroupChat)
            {
                return StatusCode(403, "Group chat feature requires a subscription. Please subscribe to the Group Chat plan.");
            }

            var groups = await _groupService.GetUserGroupsAsync(userId);
            return Ok(groups);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetGroup([FromRoute] int id)
        {
            var group = await _groupService.GetGroupAsync(id);
            if (group == null)
                return NotFound();
            return Ok(group);
        }

        [HttpPost("{groupId}/members/{userId}")]
        public async Task<IActionResult> AddMember([FromRoute] int groupId, [FromRoute] int userId)
        {
            var currentUserIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(currentUserIdClaim, out var currentUserId))
                return Unauthorized();

            // Check if user has Group Chat subscription
            var hasGroupChat = await _subscriptionService.HasActiveFeatureAsync(currentUserId, FeatureType.GroupChat);
            if (!hasGroupChat)
            {
                return StatusCode(403, "Group chat feature requires a subscription. Please subscribe to the Group Chat plan.");
            }

            await _groupService.AddMemberAsync(groupId, userId, currentUserId);
            return NoContent();
        }

        [HttpDelete("{groupId}/members/{userId}")]
        public async Task<IActionResult> RemoveMember([FromRoute] int groupId, [FromRoute] int userId)
        {
            var currentUserIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(currentUserIdClaim, out var currentUserId))
                return Unauthorized();

            // Check if user has Group Chat subscription
            var hasGroupChat = await _subscriptionService.HasActiveFeatureAsync(currentUserId, FeatureType.GroupChat);
            if (!hasGroupChat)
            {
                return StatusCode(403, "Group chat feature requires a subscription. Please subscribe to the Group Chat plan.");
            }

            await _groupService.RemoveMemberAsync(groupId, userId, currentUserId);
            return NoContent();
        }
    }
}