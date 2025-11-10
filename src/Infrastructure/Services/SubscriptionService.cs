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
            var existing = await _context.Subscriptions
                .FirstOrDefaultAsync(s => s.UserId == userId && s.Feature == feature);

            if (existing != null)
            {
                // Extend or renew
                existing.EndDate = now.Add(duration);
                existing.StartDate = now;
            }
            else
            {
                var sub = new Subscription
                {
                    UserId = userId,
                    Feature = feature,
                    StartDate = now,
                    EndDate = now.Add(duration)
                };

                _context.Subscriptions.Add(sub);
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Granted {Feature} to user {UserId} until {EndDate}", feature, userId, now.Add(duration));
        }
    }
}
