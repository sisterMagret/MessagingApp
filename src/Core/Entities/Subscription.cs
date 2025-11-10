using Core.Enums;

namespace Core.Entities
{
    public class Subscription
    {
        public int Id { get; set; }
        public FeatureType Feature { get; set; }
        public DateTime StartDate { get; set; } = DateTime.UtcNow;
        public DateTime EndDate { get; set; }
        public bool IsActive => DateTime.UtcNow <= EndDate;

        public int UserId { get; set; }
        public User User { get; set; } = default!;
    }
}
