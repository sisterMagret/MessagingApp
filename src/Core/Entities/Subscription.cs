using Core.Enums;

namespace Core.Entities;

public class Subscription
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public FeatureType Features { get; set; }
    public DateTime StartDate { get; set; } = DateTime.UtcNow;
    public DateTime EndDate { get; set; }

    public bool IsActive => DateTime.UtcNow < EndDate;
}
