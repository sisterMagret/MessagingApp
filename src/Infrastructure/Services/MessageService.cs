using Core.Contracts;
using Core.Dtos;
using Core.Entities;
using Core.Enums;
using Core.Interfaces;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services
{
    public class MessageService : IMessageService
    {
        private readonly MessagingDbContext _context;
        private readonly IEmailSender _emailSender;
        private readonly INotificationService _notifier;
        private readonly ISubscriptionService _subscriptions;

        public MessageService(
            MessagingDbContext context,
            IEmailSender emailSender,
            INotificationService notifier,
            ISubscriptionService subscriptions)
        {
            _context = context;
            _emailSender = emailSender;
            _notifier = notifier;
            _subscriptions = subscriptions;
        }

        public async Task<MessageDto> SendAsync(int senderId, MessageCreateRequest request)
        {
            // 🔒 Enforce subscriptions for file/voice
            if (!string.IsNullOrEmpty(request.FileUrl))
            {
                var hasFile = await _subscriptions.HasActiveFeatureAsync(senderId, FeatureType.FileSharing);
                if (!hasFile)
                    throw new UnauthorizedAccessException("Your subscription does not allow file uploads.");
            }

            if (!string.IsNullOrEmpty(request.VoiceUrl))
            {
                var hasVoice = await _subscriptions.HasActiveFeatureAsync(senderId, FeatureType.VoiceMessage);
                if (!hasVoice)
                    throw new UnauthorizedAccessException("Your subscription does not allow voice messages.");
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
                LastNotifiedAt = null // initialize as null
            };

            _context.Messages.Add(message);
            await _context.SaveChangesAsync();

            // Notify receiver
            await _emailSender.SendEmailAsync(
                "receiver@example.com",
                "New Message",
                "You have received a new message."
            );
            await _notifier.NotifyUserAsync(request.ReceiverId.ToString(), "You received a new message!");

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

        public async Task<PagedResult<MessageDto>> GetInboxAsync(int userId, int page, int pageSize)
        {
            var query = _context.Messages
                .Where(m => m.ReceiverId == userId)
                .OrderByDescending(m => m.SentAt);

            var totalCount = await query.CountAsync();

            var items = await query
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

            return new PagedResult<MessageDto>(items, totalCount, page, pageSize);
        }

        public async Task MarkAsReadAsync(int userId, int messageId)
        {
            var message = await _context.Messages
                .FirstOrDefaultAsync(m => m.Id == messageId && m.ReceiverId == userId);

            if (message == null) return;

            message.IsRead = true;
            message.LastNotifiedAt = null; 

            await _context.SaveChangesAsync();
        }
    }
}
