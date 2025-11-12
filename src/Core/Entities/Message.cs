using System.ComponentModel.DataAnnotations;

namespace Core.Entities
{
    public class Message
    {
        public int Id { get; set; }
        public int SenderId { get; set; }
        public int? ReceiverId { get; set; } // Nullable for group messages
        public int? GroupId { get; set; } // Nullable for direct messages

        [Required, MaxLength(2000)]
        public string Content { get; set; } = string.Empty;
        public string? FileUrl { get; set; }
        public string? VoiceUrl { get; set; }
        public DateTime SentAt { get; set; } = DateTime.UtcNow;
        public bool IsRead { get; set; } = false;
        public DateTime? LastNotifiedAt { get; set; }

        // Navigation properties
        public virtual User Sender { get; set; } = null!;
        public virtual User? Receiver { get; set; }
        public virtual Group? Group { get; set; }
    }
}