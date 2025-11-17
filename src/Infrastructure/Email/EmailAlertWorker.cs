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
                    var subscriptionService = scope.ServiceProvider.GetRequiredService<ISubscriptionService>();

                    var cutoff = DateTime.UtcNow - _unreadThreshold;

                    var unreadMessages = await db.Messages
                        .Include(m => m.Sender)
                        .Include(m => m.Receiver)
                        .Where(m => !m.IsRead &&
                                    m.SentAt <= cutoff &&
                                    (m.LastNotifiedAt == null || m.LastNotifiedAt < cutoff) &&
                                    m.ReceiverId != null) // Only direct messages
                        .ToListAsync(stoppingToken);

                    foreach (var message in unreadMessages)
                    {
                        try
                        {
                            // Only notify if user has EmailAlerts subscription
                            if (!await subscriptionService.HasActiveFeatureAsync(message.ReceiverId!.Value, FeatureType.EmailAlerts))
                                continue;

                            await emailSender.SendEmailAsync(
                                message.Receiver.Email,
                                "Unread Message Reminder",
                                $"You have an unread message from {message.Sender.Email}: {message.Content.Truncate(100)}"
                            );

                            message.LastNotifiedAt = DateTime.UtcNow;
                            _logger.LogInformation("Sent email alert for message {MessageId} to user {UserId}", 
                                message.Id, message.ReceiverId);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to notify user {UserId} for message {MessageId}",
                                message.ReceiverId, message.Id);
                        }
                    }

                    if (unreadMessages.Any())
                        await db.SaveChangesAsync(stoppingToken);

                    _logger.LogInformation("Processed {Count} unread messages for email alerts", unreadMessages.Count);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in EmailAlertWorker loop");
                }

                await Task.Delay(_interval, stoppingToken);
            }
        }
    }

    public static class StringExtensions
    {
        public static string Truncate(this string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return value.Length <= maxLength ? value : value[..maxLength] + "...";
        }
    }
}