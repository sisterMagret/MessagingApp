using Core.Enums;
using System.Threading.Tasks;

namespace Core.Interfaces
{
    public interface ISubscriptionService
    {
        /// <summary>
        /// Checks if a user has an active subscription for a specific feature.
        /// </summary>
        Task<bool> HasActiveFeatureAsync(int userId, FeatureType feature);


        /// <summary>
        /// Grants (creates or extends) a subscription for a given feature.
        /// </summary>
        Task GrantAsync(int userId, FeatureType feature, TimeSpan duration);
    }
}
