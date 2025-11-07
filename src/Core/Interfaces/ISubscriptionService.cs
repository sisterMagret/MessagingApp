public interface ISubscriptionService
{
    Task<object> CreateAsync(int userId, Core.Enums.FeatureType features, int months);
    Task<object?> GetActiveAsync(int userId);
}