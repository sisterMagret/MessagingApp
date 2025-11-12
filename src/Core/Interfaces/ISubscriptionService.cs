using Core.Entities;
using Core.Enums;

namespace Core.Interfaces
{
    public interface ISubscriptionService
    {
        Task<bool> HasActiveFeatureAsync(int userId, FeatureType feature);
        Task GrantAsync(int userId, FeatureType feature, TimeSpan duration);
        Task<bool> PurchaseFeatureAsync(int userId, FeatureType feature, int months);
        Task<bool> RevokeAsync(int userId, FeatureType feature);
        Task<IEnumerable<Subscription>> GetUserSubscriptionsAsync(int userId);
        Task<Subscription?> GetUserSubscriptionAsync(int userId, FeatureType feature);
        Task<IEnumerable<Subscription>> GetExpiringSubscriptionsAsync(TimeSpan within);
        Task CleanExpiredSubscriptionsAsync();
    }
}