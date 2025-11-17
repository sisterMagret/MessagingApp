using System.ComponentModel.DataAnnotations;

namespace Core.Entities
{
    public class User
    {
        public int Id { get; set; }
        
        [Required, EmailAddress, MaxLength(255)]
        public string Email { get; set; } = string.Empty;
        
        [Required, MaxLength(255)]
        public string PasswordHash { get; set; } = string.Empty;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastLoginAt { get; set; }
        
        public virtual ICollection<Message> Messages { get; set; } = new List<Message>();
        public virtual ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
        public virtual ICollection<GroupMember> GroupMemberships { get; set; } = new List<GroupMember>();
    }
}