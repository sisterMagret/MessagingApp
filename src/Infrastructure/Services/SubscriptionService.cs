using Core.Entities;
using Core.Enums;
using Core.Interfaces;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services
{
    public class SubscriptionService : ISubscriptionService
    {
        private readonly MessagingDbContext _context;
        private readonly ILogger<SubscriptionService> _logger;

        public SubscriptionService(MessagingDbContext context, ILogger<SubscriptionService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<bool> HasActiveFeatureAsync(int userId, FeatureType feature)
        {
            var now = DateTime.UtcNow;

            return await _context.Subscriptions
                .AnyAsync(s =>
                    s.UserId == userId &&
                    s.Feature == feature &&
                    s.EndDate >= now);
        }

        public async Task GrantAsync(int userId, FeatureType feature, TimeSpan duration)
        {
            var now = DateTime.UtcNow;
            var endDate = now.Add(duration);

            // Check for existing subscription
            var existing = await _context.Subscriptions
                .FirstOrDefaultAsync(s => s.UserId == userId && s.Feature == feature);

            Subscription subscription;

            if (existing != null)
            {
                // Extend existing subscription
                if (existing.EndDate > now)
                {
                    // Extend from current end date
                    existing.EndDate = existing.EndDate.Add(duration);
                }
                else
                {
                    // Renew expired subscription from now
                    existing.StartDate = now;
                    existing.EndDate = endDate;
                }
                subscription = existing;
            }
            else
            {
                // Create new subscription
                subscription = new Subscription
                {
                    UserId = userId,
                    Feature = feature,
                    StartDate = now,
                    EndDate = endDate
                };
                _context.Subscriptions.Add(subscription);
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Granted {Feature} to user {UserId} until {EndDate}",
                feature, userId, subscription.EndDate);
        }

        public async Task<bool> PurchaseFeatureAsync(int userId, FeatureType feature, int months)
        {
            try
            {
                var duration = TimeSpan.FromDays(months * 30); // Simplified: 30 days per month
                await GrantAsync(userId, feature, duration);
                _logger.LogInformation("User {UserId} purchased {Feature} for {Months} months",
                    userId, feature, months);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to purchase feature {Feature} for user {UserId}",
                    feature, userId);
                return false;
            }
        }

        public async Task<bool> RevokeAsync(int userId, FeatureType feature)
        {
            var subscription = await _context.Subscriptions
                .FirstOrDefaultAsync(s => s.UserId == userId && s.Feature == feature);

            if (subscription != null)
            {
                _context.Subscriptions.Remove(subscription);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Revoked {Feature} from user {UserId}", feature, userId);
                return true;
            }

            return false;
        }

        public async Task<IEnumerable<Subscription>> GetUserSubscriptionsAsync(int userId)
        {
            return await _context.Subscriptions
                .Where(s => s.UserId == userId)
                .OrderBy(s => s.Feature)
                .ToListAsync();
        }

        public async Task<Subscription?> GetUserSubscriptionAsync(int userId, FeatureType feature)
        {
            return await _context.Subscriptions
                .FirstOrDefaultAsync(s => s.UserId == userId && s.Feature == feature);
        }

        public async Task<IEnumerable<Subscription>> GetExpiringSubscriptionsAsync(TimeSpan within)
        {
            var threshold = DateTime.UtcNow.Add(within);
            return await _context.Subscriptions
                .Include(s => s.User)
                .Where(s => s.EndDate <= threshold && s.EndDate > DateTime.UtcNow)
                .OrderBy(s => s.EndDate)
                .ToListAsync();
        }

        public async Task CleanExpiredSubscriptionsAsync()
        {
            var expired = _context.Subscriptions.Where(s => s.EndDate < DateTime.UtcNow);
            int count = await expired.CountAsync();

            _context.Subscriptions.RemoveRange(expired);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Cleaned up {Count} expired subscriptions", count);
        }
    }
}