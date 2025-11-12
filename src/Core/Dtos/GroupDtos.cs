using Core.Enums;

namespace Core.Dtos
{
    public class GroupCreateRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public List<int> MemberIds { get; set; } = new();
    }

    public class GroupDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int CreatedById { get; set; }
        public string CreatedByEmail { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public int MemberCount { get; set; }
    }

    public class GroupMemberDto
    {
        public int UserId { get; set; }
        public string UserEmail { get; set; } = string.Empty;
        public GroupRole Role { get; set; }
        public DateTime JoinedAt { get; set; }
    }
}