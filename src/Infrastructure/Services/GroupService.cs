using Core.Dtos;
using Core.Entities;
using Core.Enums;
using Core.Interfaces;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services
{
    public class GroupService : IGroupService
    {
        private readonly MessagingDbContext _context;
        private readonly ISubscriptionService _subscriptionService;
        private readonly ILogger<GroupService> _logger;

        public GroupService(MessagingDbContext context, ISubscriptionService subscriptionService, ILogger<GroupService> logger)
        {
            _context = context;
            _subscriptionService = subscriptionService;
            _logger = logger;
        }

        public async Task<GroupDto> CreateGroupAsync(int userId, CreateGroupRequest request)
        {
            // Check if user has group chat subscription
            if (!await _subscriptionService.HasActiveFeatureAsync(userId, FeatureType.GroupChat))
            {
                throw new UnauthorizedAccessException("Group chat feature is not included in your current plan.");
            }

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                throw new ArgumentException("User not found.");
            }

            var group = new Group
            {
                Name = request.Name,
                Description = request.Description,
                CreatedById = userId,
                CreatedAt = DateTime.UtcNow
            };

            _context.Groups.Add(group);
            await _context.SaveChangesAsync();

            // Add creator as owner
            var groupMember = new GroupMember
            {
                GroupId = group.Id,
                UserId = userId,
                Role = GroupRole.Owner,
                JoinedAt = DateTime.UtcNow
            };

            _context.GroupMembers.Add(groupMember);
            await _context.SaveChangesAsync();

            _logger.LogInformation("User {UserId} created group {GroupId}", userId, group.Id);

            return new GroupDto
            {
                Id = group.Id,
                Name = group.Name,
                Description = group.Description,
                CreatedById = group.CreatedById,
                CreatedByEmail = user.Email,
                CreatedAt = group.CreatedAt,
                MemberCount = 1
            };
        }

        public async Task AddMemberAsync(int groupId, int newMemberId, int currentUserId)
        {
            // Check if current user is admin/owner of the group
            var currentUserMember = await _context.GroupMembers
                .FirstOrDefaultAsync(gm => gm.GroupId == groupId && gm.UserId == currentUserId);

            if (currentUserMember == null || (currentUserMember.Role != GroupRole.Admin && currentUserMember.Role != GroupRole.Owner))
            {
                throw new UnauthorizedAccessException("You don't have permission to add members to this group.");
            }

            // Check if new member exists
            var newMember = await _context.Users.FindAsync(newMemberId);
            if (newMember == null)
            {
                throw new ArgumentException("User not found.");
            }

            // Check if new member already in group
            var existingMember = await _context.GroupMembers
                .FirstOrDefaultAsync(gm => gm.GroupId == groupId && gm.UserId == newMemberId);

            if (existingMember != null)
            {
                throw new InvalidOperationException("User is already a member of this group.");
            }

            var groupMember = new GroupMember
            {
                GroupId = groupId,
                UserId = newMemberId,
                Role = GroupRole.Member,
                JoinedAt = DateTime.UtcNow
            };

            _context.GroupMembers.Add(groupMember);
            await _context.SaveChangesAsync();

            _logger.LogInformation("User {NewMemberId} added to group {GroupId} by user {CurrentUserId}",
                newMemberId, groupId, currentUserId);
        }

        public async Task<List<MessageDto>> GetGroupMessagesAsync(int groupId, int userId)
        {
            // Check if user is member of the group
            if (!await IsUserInGroupAsync(userId, groupId))
            {
                throw new UnauthorizedAccessException("You are not a member of this group.");
            }

            var messages = await _context.Messages
                .Where(m => m.GroupId == groupId)
                .OrderBy(m => m.SentAt)
                .Select(m => new MessageDto
                {
                    Id = m.Id,
                    SenderId = m.SenderId,
                    GroupId = m.GroupId,
                    Content = m.Content,
                    FileUrl = m.FileUrl ?? string.Empty,
                    VoiceUrl = m.VoiceUrl ?? string.Empty,
                    SentAt = m.SentAt,
                    IsRead = m.IsRead
                })
                .ToListAsync();

            return messages;
        }

        public async Task<bool> IsUserInGroupAsync(int userId, int groupId)
        {
            return await _context.GroupMembers
                .AnyAsync(gm => gm.GroupId == groupId && gm.UserId == userId);
        }

        public async Task RemoveMemberAsync(int groupId, int memberId, int currentUserId)
        {
            // Check if current user is admin/owner
            var currentUserMember = await _context.GroupMembers
                .FirstOrDefaultAsync(gm => gm.GroupId == groupId && gm.UserId == currentUserId);

            if (currentUserMember == null || (currentUserMember.Role != GroupRole.Admin && currentUserMember.Role != GroupRole.Owner))
            {
                throw new UnauthorizedAccessException("You don't have permission to remove members from this group.");
            }

            var memberToRemove = await _context.GroupMembers
                .FirstOrDefaultAsync(gm => gm.GroupId == groupId && gm.UserId == memberId);

            if (memberToRemove == null)
            {
                throw new ArgumentException("Member not found in this group.");
            }

            _context.GroupMembers.Remove(memberToRemove);
            await _context.SaveChangesAsync();

            _logger.LogInformation("User {MemberId} removed from group {GroupId} by user {CurrentUserId}",
                memberId, groupId, currentUserId);
        }

        public async Task<GroupDetailDto?> GetGroupAsync(int groupId)
        {
            var group = await _context.Groups
                .Include(g => g.CreatedBy)
                .Include(g => g.Members)
                .ThenInclude(m => m.User)
                .FirstOrDefaultAsync(g => g.Id == groupId);

            if (group == null) return null;

            var messageCount = await _context.Messages.CountAsync(m => m.GroupId == groupId);

            return new GroupDetailDto
            {
                Id = group.Id,
                Name = group.Name,
                Description = group.Description,
                CreatedById = group.CreatedById,
                CreatedByEmail = group.CreatedBy.Email,
                CreatedAt = group.CreatedAt,
                Members = group.Members.Select(m => new GroupMemberDto
                {
                    UserId = m.UserId,
                    UserEmail = m.User.Email,
                    Role = m.Role,
                    JoinedAt = m.JoinedAt
                }).ToList(),
                MessageCount = messageCount
            };
        }

        public async Task<List<GroupDto>> GetUserGroupsAsync(int userId)
        {
            return await _context.Groups
                .Where(g => g.Members.Any(m => m.UserId == userId))
                .Include(g => g.CreatedBy)
                .Select(g => new GroupDto
                {
                    Id = g.Id,
                    Name = g.Name,
                    Description = g.Description,
                    CreatedById = g.CreatedById,
                    CreatedByEmail = g.CreatedBy.Email,
                    CreatedAt = g.CreatedAt,
                    MemberCount = g.Members.Count()
                })
                .ToListAsync();
        }
    }
}