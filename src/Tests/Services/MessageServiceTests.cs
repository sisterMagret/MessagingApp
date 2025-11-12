using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Core.Entities;
using Core.Enums;
using Core.Interfaces;
using Infrastructure.Services;
using Infrastructure.Data;
using Core.Contracts;
using Core.Dtos;

namespace Tests.Services
{
    public class MessageServiceTests
    {
        private readonly MessagingDbContext _dbContext;
        private readonly MessageService _service;
        private readonly Mock<IEmailSender> _emailSenderMock;
        private readonly Mock<INotificationService> _notifierMock;
        private readonly Mock<ISubscriptionService> _subscriptionServiceMock;
        private readonly Mock<ILogger<MessageService>> _loggerMock;

        public MessageServiceTests()
        {
            var options = new DbContextOptionsBuilder<MessagingDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            _dbContext = new MessagingDbContext(options);

            _emailSenderMock = new Mock<IEmailSender>();
            _notifierMock = new Mock<INotificationService>();
            _subscriptionServiceMock = new Mock<ISubscriptionService>();
            _loggerMock = new Mock<ILogger<MessageService>>();

            _service = new MessageService(
                _dbContext,
                _emailSenderMock.Object,
                _notifierMock.Object,
                _subscriptionServiceMock.Object,
                _loggerMock.Object
            );
        }

        [Fact]
        public async Task SendAsync_ShouldCreate_TextMessage()
        {
            var sender = new User { Email = "sender@test.com", PasswordHash = "pass" };
            var receiver = new User { Email = "receiver@test.com", PasswordHash = "pass" };
            _dbContext.Users.AddRange(sender, receiver);
            await _dbContext.SaveChangesAsync();

            var request = new MessageCreateRequest
            {
                ReceiverId = receiver.Id,
                Content = "Hello World"
            };

            var result = await _service.SendAsync(sender.Id, request);

            result.Should().NotBeNull();
            result.Content.Should().Be("Hello World");
            result.FileUrl.Should().BeEmpty();
            result.VoiceUrl.Should().BeEmpty();

            var msg = await _dbContext.Messages.FirstOrDefaultAsync();
            msg.Should().NotBeNull();
            msg.SenderId.Should().Be(sender.Id);
            msg.ReceiverId.Should().Be(receiver.Id);

            _emailSenderMock.Verify(e => e.SendEmailAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);

            _notifierMock.Verify(n => n.NotifyUserAsync(
                receiver.Id.ToString(), It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task SendAsync_ShouldThrow_WhenFileSharingNotAllowed()
        {
            var sender = new User { Email = "nosub@test.com", PasswordHash = "pass" };
            var receiver = new User { Email = "receiver@test.com", PasswordHash = "pass" };
            _dbContext.Users.AddRange(sender, receiver);
            await _dbContext.SaveChangesAsync();

            _subscriptionServiceMock
                .Setup(s => s.HasActiveFeatureAsync(sender.Id, FeatureType.FileSharing))
                .ReturnsAsync(false);

            var request = new MessageCreateRequest
            {
                ReceiverId = receiver.Id,
                FileUrl = "https://example.com/file.pdf"
            };

            var act = async () => await _service.SendAsync(sender.Id, request);

            await act.Should().ThrowAsync<UnauthorizedAccessException>()
                .WithMessage("File sharing is not included in your current plan.");
        }

        [Fact]
        public async Task SendAsync_ShouldThrow_WhenVoiceMessageNotAllowed()
        {
            var sender = new User { Email = "nosub@test.com", PasswordHash = "pass" };
            var receiver = new User { Email = "receiver@test.com", PasswordHash = "pass" };
            _dbContext.Users.AddRange(sender, receiver);
            await _dbContext.SaveChangesAsync();

            _subscriptionServiceMock
                .Setup(s => s.HasActiveFeatureAsync(sender.Id, FeatureType.VoiceMessage))
                .ReturnsAsync(false);

            var request = new MessageCreateRequest
            {
                ReceiverId = receiver.Id,
                VoiceUrl = "https://example.com/voice.mp3"
            };

            var act = async () => await _service.SendAsync(sender.Id, request);

            await act.Should().ThrowAsync<UnauthorizedAccessException>()
                .WithMessage("Voice messaging is not included in your current plan.");
        }

        [Fact]
        public async Task MarkAsRead_ShouldMarkMessageAsRead()
        {
            var sender = new User { Email = "sender@test.com", PasswordHash = "pass" };
            var receiver = new User { Email = "receiver@test.com", PasswordHash = "pass" };
            _dbContext.Users.AddRange(sender, receiver);
            await _dbContext.SaveChangesAsync();

            var message = new Message
            {
                SenderId = sender.Id,
                ReceiverId = receiver.Id,
                Content = "Unread message",
                IsRead = false
            };
            _dbContext.Messages.Add(message);
            await _dbContext.SaveChangesAsync();

            await _service.MarkAsReadAsync(receiver.Id, message.Id);

            var updated = await _dbContext.Messages.FindAsync(message.Id);
            updated.Should().NotBeNull();
            updated.IsRead.Should().BeTrue();
        }

        [Fact]
        public async Task MarkAsRead_ShouldDoNothing_WhenMessageNotFound()
        {
            var receiver = new User { Email = "receiver@test.com", PasswordHash = "pass" };
            _dbContext.Users.Add(receiver);
            await _dbContext.SaveChangesAsync();

            await _service.MarkAsReadAsync(receiver.Id, 999); // nonexistent

            (await _dbContext.Messages.CountAsync()).Should().Be(0);
        }

        [Fact]
        public async Task GetInbox_ShouldReturnPagedMessages()
        {
            var sender = new User { Email = "sender@test.com", PasswordHash = "pass" };
            var receiver = new User { Email = "receiver@test.com", PasswordHash = "pass" };
            _dbContext.Users.AddRange(sender, receiver);
            await _dbContext.SaveChangesAsync();

            for (int i = 1; i <= 15; i++)
            {
                _dbContext.Messages.Add(new Message
                {
                    SenderId = sender.Id,
                    ReceiverId = receiver.Id,
                    Content = $"Message {i}",
                    SentAt = DateTime.UtcNow.AddMinutes(-i)
                });
            }
            await _dbContext.SaveChangesAsync();

            var result = await _service.GetInboxAsync(receiver.Id, page: 2, pageSize: 5);

            result.Should().NotBeNull();
            result.Items.Should().HaveCount(5);
            result.TotalCount.Should().Be(15);
            result.Page.Should().Be(2);
            result.PageSize.Should().Be(5);
            result.Items.First().Content.Should().Be("Message 10");
        }
    }
}
