namespace Core.Entities
{
    public class Message
    {
        public int Id { get; set; }
        public string Content { get; set; } = default!;
        public bool IsRead { get; set; } = false;
        public DateTime SentAt { get; set; } = DateTime.UtcNow;
        public int SenderId { get; set; }
        public User Sender { get; set; } = default!;
        public int ReceiverId { get; set; }
        public User Receiver { get; set; } = default!;
    }
}
