using Core.Contracts;
using Core.Dtos;
using Core.Entities;
using Core.Enums;
using Core.Interfaces;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services
{
    public class MessageService : IMessageService
    {
        private readonly MessagingDbContext _context;
        private readonly IEmailSender _emailSender;
        private readonly INotificationService _notifier;
        private readonly ISubscriptionService _subscriptions;
        private readonly ILogger<MessageService> _logger;

        public MessageService(
            MessagingDbContext context,
            IEmailSender emailSender,
            INotificationService notifier,
            ISubscriptionService subscriptions,
            ILogger<MessageService> logger)
        {
            _context = context;
            _emailSender = emailSender;
            _notifier = notifier;
            _subscriptions = subscriptions;
            _logger = logger;
        }

        /// <summary>
        /// Sends a new message with optional file/voice attachments. Enforces feature gating.
        /// </summary>
        public async Task<MessageDto> SendAsync(int senderId, MessageCreateRequest request)
        {
            // 🔒 Enforce subscription gating
            if (!string.IsNullOrWhiteSpace(request.FileUrl))
            {
                var hasFileFeature = await _subscriptions.HasActiveFeatureAsync(senderId, FeatureType.FileSharing);
                if (!hasFileFeature)
                {
                    _logger.LogWarning("User {SenderId} attempted to send a file message without subscription.", senderId);
                    throw new UnauthorizedAccessException("File sharing is not included in your current plan.");
                }
            }

            if (!string.IsNullOrWhiteSpace(request.VoiceUrl))
            {
                var hasVoiceFeature = await _subscriptions.HasActiveFeatureAsync(senderId, FeatureType.VoiceMessage);
                if (!hasVoiceFeature)
                {
                    _logger.LogWarning("User {SenderId} attempted to send a voice message without subscription.", senderId);
                    throw new UnauthorizedAccessException("Voice messaging is not included in your current plan.");
                }
            }

            var message = new Message
            {
                SenderId = senderId,
                ReceiverId = request.ReceiverId,
                Content = request.Content,
                FileUrl = request.FileUrl,
                VoiceUrl = request.VoiceUrl,
                SentAt = DateTime.UtcNow,
                IsRead = false,
                LastNotifiedAt = null
            };

            _context.Messages.Add(message);
            await _context.SaveChangesAsync();

            // ✅ Notify the receiver
            await _emailSender.SendEmailAsync(
                "receiver@example.com",
                "New Message Received",
                $"You have a new message from User {senderId}."
            );

            await _notifier.NotifyUserAsync(request.ReceiverId.ToString(), "You received a new message!");

            _logger.LogInformation("User {SenderId} sent message {MessageId} to {ReceiverId}",
                senderId, message.Id, message.ReceiverId);

            return new MessageDto
            {
                Id = message.Id,
                SenderId = message.SenderId,
                ReceiverId = message.ReceiverId,
                Content = message.Content,
                FileUrl = message.FileUrl ?? string.Empty,
                VoiceUrl = message.VoiceUrl ?? string.Empty,
                SentAt = message.SentAt,
                IsRead = message.IsRead
            };
        }

        /// <summary>
        /// Fetches paged inbox messages for a user.
        /// </summary>
        public async Task<PagedResult<MessageDto>> GetInboxAsync(int userId, int page, int pageSize)
        {
            var query = _context.Messages
                .Where(m => m.ReceiverId == userId)
                .OrderByDescending(m => m.SentAt);

            var totalCount = await query.CountAsync();

            var messages = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(m => new MessageDto
                {
                    Id = m.Id,
                    SenderId = m.SenderId,
                    ReceiverId = m.ReceiverId,
                    Content = m.Content,
                    FileUrl = m.FileUrl ?? string.Empty,
                    VoiceUrl = m.VoiceUrl ?? string.Empty,
                    SentAt = m.SentAt,
                    IsRead = m.IsRead
                })
                .ToListAsync();

            return new PagedResult<MessageDto>(messages, totalCount, page, pageSize);
        }

        /// <summary>
        /// Marks a message as read by its receiver.
        /// </summary>
        public async Task MarkAsReadAsync(int userId, int messageId)
        {
            var message = await _context.Messages
                .FirstOrDefaultAsync(m => m.Id == messageId && m.ReceiverId == userId);

            if (message is null)
            {
                _logger.LogWarning("User {UserId} attempted to mark non-existent message {MessageId} as read.", userId, messageId);
                return;
            }

            message.IsRead = true;
            message.LastNotifiedAt = null;

            await _context.SaveChangesAsync();
            _logger.LogInformation("User {UserId} marked message {MessageId} as read.", userId, messageId);
        }
    }
}
