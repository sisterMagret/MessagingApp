using Core.Dtos;
using Core.Entities;

namespace Core.Interfaces
{
    public interface IGroupService
    {
        Task<GroupDto> CreateGroupAsync(int userId, CreateGroupRequest request);
        Task AddMemberAsync(int groupId, int newMemberId, int currentUserId);
        Task RemoveMemberAsync(int groupId, int memberId, int currentUserId);
        Task<GroupDetailDto?> GetGroupAsync(int groupId);
        Task<List<GroupDto>> GetUserGroupsAsync(int userId);
        Task<List<MessageDto>> GetGroupMessagesAsync(int groupId, int userId);
        Task<bool> IsUserInGroupAsync(int userId, int groupId);
    }

    public class CreateGroupRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
    }
}