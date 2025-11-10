namespace Core.Entities
{
    public class Message
    {
        public int Id { get; set; }
        public string Content { get; set; } = default!;
        public string? VoiceUrl { get; set; }
        public string? FileUrl { get; set; }
        public bool IsRead { get; set; } = false;
        public DateTime SentAt { get; set; } = DateTime.UtcNow;
        public int SenderId { get; set; }
        public User Sender { get; set; } = default!;
        public int ReceiverId { get; set; }
        public User Receiver { get; set; } = default!;
        public DateTime? LastNotifiedAt { get; set; }
    }
}

