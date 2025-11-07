namespace Core.Entities;

public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public bool IsOnline { get; set; } = false;
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;

    public ICollection<Message> SentMessages { get; set; } = new List<Message>();
    public ICollection<Message> ReceivedMessages { get; set; } = new List<Message>();
    public ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
}
