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
        private readonly IGroupService _groupService;
        private readonly IFileService _fileService;
        private readonly ILogger<MessageService> _logger;

        public MessageService(
            MessagingDbContext context,
            IEmailSender emailSender,
            INotificationService notifier,
            ISubscriptionService subscriptions,
            IGroupService groupService,
            IFileService fileService,
            ILogger<MessageService> logger)
        {
            _context = context;
            _emailSender = emailSender;
            _notifier = notifier;
            _subscriptions = subscriptions;
            _groupService = groupService;
            _fileService = fileService;
            _logger = logger;
        }

        public async Task<MessageDto> SendAsync(int senderId, MessageCreateRequest request)
        {
            // Validate: either receiver OR group must be specified
            if (request.ReceiverId == null && request.GroupId == null)
                throw new ArgumentException("Either ReceiverId or GroupId must be specified");

            if (request.ReceiverId != null && request.GroupId != null)
                throw new ArgumentException("Cannot specify both ReceiverId and GroupId");

            // Group message validation
            if (request.GroupId.HasValue)
            {
                var hasGroupFeature = await _subscriptions.HasActiveFeatureAsync(senderId, FeatureType.GroupChat);
                if (!hasGroupFeature)
                    throw new UnauthorizedAccessException("Group chat is not included in your current plan.");

                // Verify user is in the group
                if (!await _groupService.IsUserInGroupAsync(senderId, request.GroupId.Value))
                    throw new UnauthorizedAccessException("You are not a member of this group");
            }
            else
            {
                // Direct message - verify receiver exists
                var receiverExists = await _context.Users.AnyAsync(u => u.Id == request.ReceiverId);
                if (!receiverExists)
                    throw new ArgumentException($"Receiver with ID {request.ReceiverId} not found");
            }

            // Feature gating for file sharing
            if (!string.IsNullOrWhiteSpace(request.FileUrl))
            {
                var hasFileFeature = await _subscriptions.HasActiveFeatureAsync(senderId, FeatureType.FileSharing);
                if (!hasFileFeature)
                    throw new UnauthorizedAccessException("File sharing is not included in your current plan.");
            }

            // Feature gating for voice messages
            if (!string.IsNullOrWhiteSpace(request.VoiceUrl))
            {
                var hasVoiceFeature = await _subscriptions.HasActiveFeatureAsync(senderId, FeatureType.VoiceMessage);
                if (!hasVoiceFeature)
                    throw new UnauthorizedAccessException("Voice messaging is not included in your current plan.");
            }

            var message = new Message
            {
                SenderId = senderId,
                ReceiverId = request.ReceiverId,
                GroupId = request.GroupId,
                Content = request.Content,
                FileUrl = request.FileUrl,
                VoiceUrl = request.VoiceUrl,
                SentAt = DateTime.UtcNow,
                IsRead = false,
                LastNotifiedAt = null
            };

            _context.Messages.Add(message);
            await _context.SaveChangesAsync();

            // Load sender info for response
            await _context.Entry(message)
                .Reference(m => m.Sender)
                .LoadAsync();

            var messageDto = new MessageDto
            {
                Id = message.Id,
                SenderId = message.SenderId,
                ReceiverId = message.ReceiverId,
                GroupId = message.GroupId,
                SenderEmail = message.Sender.Email,
                Content = message.Content,
                FileUrl = message.FileUrl ?? string.Empty,
                VoiceUrl = message.VoiceUrl ?? string.Empty,
                SentAt = message.SentAt,
                IsRead = message.IsRead
            };

            // Send notifications
            await SendNotificationsAsync(message, messageDto);

            _logger.LogInformation("User {SenderId} sent message {MessageId}", senderId, message.Id);

            return messageDto;
        }

        private async Task SendNotificationsAsync(Message message, MessageDto messageDto)
        {
            try
            {
                if (message.ReceiverId.HasValue)
                {
                    // Direct message notification
                    var receiver = await _context.Users.FindAsync(message.ReceiverId.Value);
                    if (receiver != null && !string.IsNullOrEmpty(receiver.Email))
                    {
                        await _emailSender.SendEmailAsync(
                            receiver.Email,
                            "New Message Received",
                            $"You have a new message from {message.Sender.Email}: {message.Content}"
                        );
                    }

                    await _notifier.NotifyUserAsync(
                        message.ReceiverId.Value.ToString(),
                        $"You received a new message from {message.Sender.Email}!"
                    );
                }
                else if (message.GroupId.HasValue)
                {
                    // Group message notification (notify all members except sender)
                    var groupMembers = await _context.GroupMembers
                        .Where(gm => gm.GroupId == message.GroupId && gm.UserId != message.SenderId)
                        .Include(gm => gm.User)
                        .ToListAsync();

                    foreach (var member in groupMembers)
                    {
                        if (!string.IsNullOrEmpty(member.User.Email))
                        {
                            await _emailSender.SendEmailAsync(
                                member.User.Email,
                                $"New Message in {message.Group?.Name ?? "Group"}",
                                $"New message from {message.Sender.Email}: {message.Content}"
                            );
                        }

                        await _notifier.NotifyUserAsync(
                            member.UserId.ToString(),
                            $"New message in {message.Group?.Name ?? "group"} from {message.Sender.Email}"
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send notifications for message {MessageId}", message.Id);
            }
        }

        public async Task<PagedResult<MessageDto>> GetInboxAsync(int userId, int page, int pageSize)
        {
            var query = _context.Messages
                .Include(m => m.Sender)
                .Include(m => m.Group)
                .Where(m => m.ReceiverId == userId ||
                           (m.GroupId != null && m.Group!.Members.Any(gm => gm.UserId == userId)))
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
                    GroupId = m.GroupId,
                    SenderEmail = m.Sender.Email, // Now safe because we included it
                    Content = m.Content,
                    FileUrl = m.FileUrl ?? string.Empty,
                    VoiceUrl = m.VoiceUrl ?? string.Empty,
                    SentAt = m.SentAt,
                    IsRead = m.IsRead
                })
                .ToListAsync();

            return new PagedResult<MessageDto>(messages, totalCount, page, pageSize);
        }

        public async Task MarkAsReadAsync(int userId, int messageId)
        {
            var message = await _context.Messages
                .FirstOrDefaultAsync(m => m.Id == messageId &&
                                         (m.ReceiverId == userId ||
                                          (m.GroupId != null && m.Group!.Members.Any(gm => gm.UserId == userId))));

            if (message == null)
            {
                _logger.LogWarning("User {UserId} attempted to mark non-existent message {MessageId} as read",
                    userId, messageId);
                return;
            }

            message.IsRead = true;
            message.LastNotifiedAt = null;

            await _context.SaveChangesAsync();
            _logger.LogInformation("User {UserId} marked message {MessageId} as read", userId, messageId);
        }

        public async Task<List<MessageDto>> GetGroupMessagesAsync(int groupId, int userId)
        {
            // Verify user is in group
            var isMember = await _context.GroupMembers
                .AnyAsync(gm => gm.GroupId == groupId && gm.UserId == userId);

            if (!isMember)
                throw new UnauthorizedAccessException("You are not a member of this group");

            var messages = await _context.Messages
                .Where(m => m.GroupId == groupId)
                .Include(m => m.Sender)
                .OrderByDescending(m => m.SentAt)
                .Take(50) // Limit to last 50 messages
                .Select(m => new MessageDto
                {
                    Id = m.Id,
                    SenderId = m.SenderId,
                    ReceiverId = m.ReceiverId,
                    GroupId = m.GroupId,
                    SenderEmail = m.Sender.Email,
                    Content = m.Content,
                    FileUrl = m.FileUrl ?? string.Empty,
                    VoiceUrl = m.VoiceUrl ?? string.Empty,
                    SentAt = m.SentAt,
                    IsRead = m.IsRead
                })
                .ToListAsync();

            return messages;
        }
    }
}