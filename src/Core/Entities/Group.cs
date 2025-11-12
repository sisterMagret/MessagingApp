using System.ComponentModel.DataAnnotations;
using Core.Enums;

namespace Core.Entities
{
    public class Group
    {
        public int Id { get; set; }

        [Required, MaxLength(100)]
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int CreatedById { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual User CreatedBy { get; set; } = null!;
        public virtual ICollection<GroupMember> Members { get; set; } = new List<GroupMember>();
        public virtual ICollection<Message> Messages { get; set; } = new List<Message>();
    }

    public class GroupMember
    {
        public int Id { get; set; }
        public int GroupId { get; set; }
        public int UserId { get; set; }
        public GroupRole Role { get; set; } = GroupRole.Member;
        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

        public virtual Group Group { get; set; } = null!;
        public virtual User User { get; set; } = null!;
    }

}