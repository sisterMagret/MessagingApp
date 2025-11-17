using Core.Entities;
using Core.Enums;
using Core.Interfaces;
using Core.Contracts;
using Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace Tests.Services
{
    public class SubscriptionServiceTests : ServiceTestBase
    {
        private readonly ISubscriptionService _subscriptionService;
        private readonly Mock<ILogger<SubscriptionService>> _mockLogger;

        public SubscriptionServiceTests()
        {
            _mockLogger = new Mock<ILogger<SubscriptionService>>();
            _subscriptionService = new SubscriptionService(DbContext, _mockLogger.Object);
        }

        [Fact]
        public async Task HasActiveFeatureAsync_WithActiveFeature_ShouldReturnTrue()
        {
            // Arrange
            var user = await CreateTestUserAsync();
            await GrantSubscriptionAsync(user.Id, FeatureType.VoiceMessage);

            // Act
            var hasFeature = await _subscriptionService.HasActiveFeatureAsync(user.Id, FeatureType.VoiceMessage);

            // Assert
            hasFeature.Should().BeTrue();
        }

        [Fact]
        public async Task HasActiveFeatureAsync_WithoutFeature_ShouldReturnFalse()
        {
            // Arrange
            var user = await CreateTestUserAsync();

            // Act
            var hasFeature = await _subscriptionService.HasActiveFeatureAsync(user.Id, FeatureType.VoiceMessage);

            // Assert
            hasFeature.Should().BeFalse();
        }

        [Fact]
        public async Task HasActiveFeatureAsync_WithExpiredFeature_ShouldReturnFalse()
        {
            // Arrange
            var user = await CreateTestUserAsync();
            await GrantSubscriptionAsync(user.Id, FeatureType.FileSharing, daysValid: -10);

            // Act
            var hasFeature = await _subscriptionService.HasActiveFeatureAsync(user.Id, FeatureType.FileSharing);

            // Assert
            hasFeature.Should().BeFalse();
        }

        [Fact]
        public async Task GrantAsync_ShouldCreateSubscription()
        {
            // Arrange
            var user = await CreateTestUserAsync();
            var duration = TimeSpan.FromDays(30);

            // Act
            await _subscriptionService.GrantAsync(user.Id, FeatureType.GroupChat, duration);

            // Assert
            var subscription = DbContext.Subscriptions.FirstOrDefault(
                s => s.UserId == user.Id && s.Feature == FeatureType.GroupChat
            );
            subscription.Should().NotBeNull();
        }

        [Fact]
        public async Task RevokeAsync_ShouldRemoveSubscription()
        {
            // Arrange
            var user = await CreateTestUserAsync();
            await GrantSubscriptionAsync(user.Id, FeatureType.EmailAlerts);

            // Act
            await _subscriptionService.RevokeAsync(user.Id, FeatureType.EmailAlerts);

            // Assert
            var hasFeature = await _subscriptionService.HasActiveFeatureAsync(user.Id, FeatureType.EmailAlerts);
            hasFeature.Should().BeFalse();
        }

        [Fact]
        public async Task GetUserSubscriptionsAsync_ShouldReturnAllSubscriptions()
        {
            // Arrange
            var user = await CreateTestUserAsync();
            await GrantSubscriptionAsync(user.Id, FeatureType.VoiceMessage);
            await GrantSubscriptionAsync(user.Id, FeatureType.FileSharing);

            // Act
            var subscriptions = await _subscriptionService.GetUserSubscriptionsAsync(user.Id);

            // Assert
            subscriptions.Should().HaveCount(2);
        }
    }

    public class AuthServiceTests : ServiceTestBase
    {
        private readonly IAuthService _authService;

        public AuthServiceTests()
        {
            _authService = new AuthService(DbContext, Configuration);
        }

        [Fact]
        public async Task RegisterAsync_WithValidData_ShouldCreateUser()
        {
            // Arrange
            var request = new Core.Dtos.RegisterRequest
            {
                Email = "newuser@example.com",
                Password = "Test@123"
            };

            // Act
            var response = await _authService.RegisterAsync(request);

            // Assert
            response.Should().NotBeNull();
            response.Email.Should().Be("newuser@example.com");
        }

        [Fact]
        public async Task LoginAsync_WithValidCredentials_ShouldReturnToken()
        {
            // Arrange
            const string email = "testuser@example.com";
            const string password = "Test@123";
            await CreateTestUserAsync(email, password);

            var request = new Core.Dtos.LoginRequest { Email = email, Password = password };

            // Act
            var response = await _authService.LoginAsync(request);

            // Assert
            response.Should().NotBeNull();
            response.Token.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task LoginAsync_WithWrongPassword_ShouldThrowException()
        {
            // Arrange
            const string email = "testuser@example.com";
            await CreateTestUserAsync(email, "CorrectPassword@123");

            var request = new Core.Dtos.LoginRequest { Email = email, Password = "WrongPassword@123" };

            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _authService.LoginAsync(request));
        }
    }

    public class GroupServiceTests : ServiceTestBase
    {
        private readonly IGroupService _groupService;
        private readonly ISubscriptionService _subscriptionService;
        private readonly Mock<ILogger<GroupService>> _mockLogger;

        public GroupServiceTests()
        {
            _mockLogger = new Mock<ILogger<GroupService>>();
            _subscriptionService = new SubscriptionService(DbContext, new Mock<ILogger<SubscriptionService>>().Object);
            _groupService = new GroupService(DbContext, _subscriptionService, _mockLogger.Object);
        }

        [Fact]
        public async Task CreateGroupAsync_WithGroupChatFeature_ShouldCreateGroup()
        {
            // Arrange
            var creator = await CreateTestUserAsync();
            await GrantSubscriptionAsync(creator.Id, FeatureType.GroupChat);

            var request = new CreateGroupRequest { Name = "Dev Team", Description = "Our team" };

            // Act
            var group = await _groupService.CreateGroupAsync(creator.Id, request);

            // Assert
            group.Should().NotBeNull();
            group.Name.Should().Be("Dev Team");
        }

        [Fact]
        public async Task AddMemberAsync_ShouldAddMember()
        {
            // Arrange
            var owner = await CreateTestUserAsync("owner@example.com");
            var member = await CreateTestUserAsync("member@example.com");
            await GrantSubscriptionAsync(owner.Id, FeatureType.GroupChat);
            var group = await CreateTestGroupAsync(owner.Id);

            // Act
            await _groupService.AddMemberAsync(group.Id, member.Id, owner.Id);

            // Assert
            var isMember = await _groupService.IsUserInGroupAsync(member.Id, group.Id);
            isMember.Should().BeTrue();
        }

        [Fact]
        public async Task GetUserGroupsAsync_ShouldReturnAllUserGroups()
        {
            // Arrange
            var user = await CreateTestUserAsync();
            await GrantSubscriptionAsync(user.Id, FeatureType.GroupChat);

            var group1 = await CreateTestGroupAsync(user.Id, "Group 1");
            var group2 = await CreateTestGroupAsync(user.Id, "Group 2");

            // Act
            var groups = await _groupService.GetUserGroupsAsync(user.Id);

            // Assert
            groups.Should().HaveCount(2);
        }
    }

    public class MessageServiceTests : ServiceTestBase
    {
        private readonly IMessageService _messageService;
        private readonly ISubscriptionService _subscriptionService;
        private readonly IGroupService _groupService;
        private readonly IFileService _fileService;
        private readonly Mock<ILogger<MessageService>> _mockMessageLogger;
        private readonly Mock<ILogger<SubscriptionService>> _mockSubLogger;
        private readonly Mock<ILogger<GroupService>> _mockGroupLogger;

        public MessageServiceTests()
        {
            _mockMessageLogger = new Mock<ILogger<MessageService>>();
            _mockSubLogger = new Mock<ILogger<SubscriptionService>>();
            _mockGroupLogger = new Mock<ILogger<GroupService>>();

            _fileService = new Mock<IFileService>().Object;
            _subscriptionService = new SubscriptionService(DbContext, _mockSubLogger.Object);
            _groupService = new GroupService(DbContext, _subscriptionService, _mockGroupLogger.Object);
            _messageService = new MessageService(
                DbContext,
                MockEmailSender.Object,
                MockNotificationService.Object,
                _subscriptionService,
                _groupService,
                _fileService,
                _mockMessageLogger.Object
            );
        }

        [Fact]
        public async Task SendAsync_DirectMessage_ShouldCreateMessage()
        {
            // Arrange
            var sender = await CreateTestUserAsync("sender@example.com");
            var receiver = await CreateTestUserAsync("receiver@example.com");

            var request = new Core.Dtos.MessageCreateRequest
            {
                Content = "Hello!",
                ReceiverId = receiver.Id,
                GroupId = null
            };

            // Act
            var message = await _messageService.SendAsync(sender.Id, request);

            // Assert
            message.Should().NotBeNull();
            message.Content.Should().Be("Hello!");
        }

        [Fact]
        public async Task GetInboxAsync_ShouldReturnMessages()
        {
            // Arrange
            var user1 = await CreateTestUserAsync("user1@example.com");
            var user2 = await CreateTestUserAsync("user2@example.com");

            await CreateTestMessageAsync(user2.Id, user1.Id, null, "Message 1");
            await CreateTestMessageAsync(user2.Id, user1.Id, null, "Message 2");

            // Act
            var inbox = await _messageService.GetInboxAsync(user1.Id, 1, 50);

            // Assert
            inbox.Items.Should().HaveCount(2);
        }
    }
}
