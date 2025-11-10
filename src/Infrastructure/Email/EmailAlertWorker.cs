using Core.Contracts;
using Core.Enums;
using Core.Interfaces;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Workers
{
    public class EmailAlertWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<EmailAlertWorker> _logger;
        private readonly TimeSpan _interval = TimeSpan.FromMinutes(5);
        private readonly TimeSpan _unreadThreshold = TimeSpan.FromMinutes(30);

        public EmailAlertWorker(IServiceScopeFactory scopeFactory, ILogger<EmailAlertWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("EmailAlertWorker started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<MessagingDbContext>();
                    var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
                    var notifier = scope.ServiceProvider.GetRequiredService<INotificationService>();
                    var subscriptionService = scope.ServiceProvider.GetRequiredService<ISubscriptionService>();

                    var cutoff = DateTime.UtcNow - _unreadThreshold;

                    var unreadMessages = await db.Messages
                        .Include(m => m.Receiver)
                        .Where(m => !m.IsRead &&
                                    m.SentAt <= cutoff &&
                                    (m.LastNotifiedAt == null || m.LastNotifiedAt < cutoff))
                        .ToListAsync(stoppingToken);

                    foreach (var message in unreadMessages)
                    {
                        try
                        {
                            // Only notify if user has EmailAlerts subscription
                            if (!await subscriptionService.HasActiveFeatureAsync(message.ReceiverId, FeatureType.EmailAlerts))
                                continue;

                            await emailSender.SendEmailAsync(
                                message.Receiver.Email,
                                "Unread Message Reminder",
                                $"You have an unread message from user {message.SenderId}"
                            );

                            await notifier.NotifyUserAsync(
                                message.ReceiverId.ToString(),
                                $"You have an unread message from user {message.SenderId}"
                            );

                            message.LastNotifiedAt = DateTime.UtcNow;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to notify user {UserId} for message {MessageId}",
                                message.ReceiverId, message.Id);
                        }
                    }

                    if (unreadMessages.Any())
                        await db.SaveChangesAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in EmailAlertWorker loop");
                }

                await Task.Delay(_interval, stoppingToken);
            }
        }
    }
}
