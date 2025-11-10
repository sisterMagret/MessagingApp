namespace Core.Entities

{
    public class User
    {
        public int Id { get; set; }
        public string Email { get; set; } = default!;
        public string PasswordHash { get; set; } = default!;
        public bool IsOnline { get; set; }
        public DateTime LastSeen { get; set; } = DateTime.UtcNow;
        public ICollection<Message> Messages { get; set; } = new List<Message>();
        public ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
    }
}
