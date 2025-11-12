using Core.Entities;
using Core.Enums;
using Core.Interfaces;
using Infrastructure.Services;
using FluentAssertions;

namespace Tests.Services
{
    public class AuthServiceTests : ServiceTestBase
    {
        private readonly IAuthService _authService;

        public AuthServiceTests()
        {
            _authService = new AuthService(DbContext, PasswordHasher, Configuration);
        }

        [Fact]
        public async Task RegisterAsync_WithValidData_ShouldCreateUser()
        {
            // Arrange
            var request = new { Email = "newuser@example.com", Password = "Test@123" };

            // Act
            var response = await _authService.RegisterAsync(
                new Core.Dtos.RegisterRequest { Email = request.Email, Password = request.Password }
            );

            // Assert
            response.Should().NotBeNull();
            response.Email.Should().Be(request.Email);
            
            var user = DbContext.Users.FirstOrDefault(u => u.Email == request.Email);
            user.Should().NotBeNull();
        }

        [Fact]
        public async Task RegisterAsync_WithDuplicateEmail_ShouldThrowException()
        {
            // Arrange
            await CreateTestUserAsync("duplicate@example.com");
            var request = new Core.Dtos.RegisterRequest { Email = "duplicate@example.com", Password = "Test@123" };

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _authService.RegisterAsync(request)
            );
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
            response.Email.Should().Be(email);
        }

        [Fact]
        public async Task LoginAsync_WithInvalidPassword_ShouldThrowException()
        {
            // Arrange
            const string email = "testuser@example.com";
            await CreateTestUserAsync(email, "CorrectPassword@123");

            var request = new Core.Dtos.LoginRequest { Email = email, Password = "WrongPassword@123" };

            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => _authService.LoginAsync(request)
            );
        }

        [Fact]
        public async Task LoginAsync_WithNonexistentUser_ShouldThrowException()
        {
            // Arrange
            var request = new Core.Dtos.LoginRequest { Email = "nonexistent@example.com", Password = "Test@123" };

            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => _authService.LoginAsync(request)
            );
        }
    }

    public class SubscriptionServiceTests : ServiceTestBase
    {
        private readonly ISubscriptionService _subscriptionService;

        public SubscriptionServiceTests()
        {
            _subscriptionService = new SubscriptionService(DbContext);
        }

        [Fact]
        public async Task GrantAsync_WithNewFeature_ShouldCreateSubscription()
        {
            // Arrange
            var user = await CreateTestUserAsync();
            var duration = TimeSpan.FromDays(30);

            // Act
            await _subscriptionService.GrantAsync(user.Id, FeatureType.VoiceMessage, duration);

            // Assert
            var subscription = DbContext.Subscriptions.FirstOrDefault(
                s => s.UserId == user.Id && s.Feature == FeatureType.VoiceMessage
            );
            subscription.Should().NotBeNull();
            subscription!.EndDate.Should().BeOnOrAfter(DateTime.UtcNow.AddDays(29));
        }

        [Fact]
        public async Task GrantAsync_WithExistingExpiredSubscription_ShouldCreateNew()
        {
            // Arrange
            var user = await CreateTestUserAsync();
            await GrantSubscriptionAsync(user.Id, FeatureType.FileSharing, daysValid: -5); // Expired

            // Act
            await _subscriptionService.GrantAsync(user.Id, FeatureType.FileSharing, TimeSpan.FromDays(30));

            // Assert
            var subscriptions = DbContext.Subscriptions.Where(
                s => s.UserId == user.Id && s.Feature == FeatureType.FileSharing
            ).ToList();
            
            subscriptions.Should().HaveCount(2);
            subscriptions.Last().IsActive.Should().BeTrue();
        }

        [Fact]
        public async Task HasActiveFeatureAsync_WithActiveFeature_ShouldReturnTrue()
        {
            // Arrange
            var user = await CreateTestUserAsync();
            await GrantSubscriptionAsync(user.Id, FeatureType.GroupChat);

            // Act
            var hasFeature = await _subscriptionService.HasActiveFeatureAsync(user.Id, FeatureType.GroupChat);

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
            await GrantSubscriptionAsync(user.Id, FeatureType.EmailAlerts, daysValid: -10);

            // Act
            var hasFeature = await _subscriptionService.HasActiveFeatureAsync(user.Id, FeatureType.EmailAlerts);

            // Assert
            hasFeature.Should().BeFalse();
        }

        [Fact]
        public async Task RevokeAsync_ShouldRemoveSubscription()
        {
            // Arrange
            var user = await CreateTestUserAsync();
            var subscription = await GrantSubscriptionAsync(user.Id, FeatureType.VoiceMessage);

            // Act
            await _subscriptionService.RevokeAsync(subscription.Id);

            // Assert
            var remainingSubscription = DbContext.Subscriptions.FirstOrDefault(s => s.Id == subscription.Id);
            remainingSubscription.Should().BeNull();
        }

        [Fact]
        public async Task GetUserSubscriptionsAsync_ShouldReturnAllUserFeatures()
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

        [Fact]
        public async Task GetExpiringSubscriptionsAsync_ShouldReturnSubscriptionsExpiringInNext7Days()
        {
            // Arrange
            var user1 = await CreateTestUserAsync("user1@example.com");
            var user2 = await CreateTestUserAsync("user2@example.com");
            
            // Expiring in 5 days - should be included
            await GrantSubscriptionAsync(user1.Id, FeatureType.VoiceMessage, daysValid: 5);
            
            // Expiring in 10 days - should NOT be included
            await GrantSubscriptionAsync(user2.Id, FeatureType.FileSharing, daysValid: 10);

            // Act
            var expiring = await _subscriptionService.GetExpiringSubscriptionsAsync(daysThreshold: 7);

            // Assert
            expiring.Should().HaveCount(1);
            expiring.First().UserId.Should().Be(user1.Id);
        }
    }

    public class GroupServiceTests : ServiceTestBase
    {
        private readonly IGroupService _groupService;
        private readonly ISubscriptionService _subscriptionService;

        public GroupServiceTests()
        {
            _subscriptionService = new SubscriptionService(DbContext);
            _groupService = new GroupService(DbContext, _subscriptionService);
        }

        [Fact]
        public async Task CreateGroupAsync_WithGroupChatFeature_ShouldCreateGroup()
        {
            // Arrange
            var creator = await CreateTestUserAsync();
            await GrantSubscriptionAsync(creator.Id, FeatureType.GroupChat);

            var request = new Core.Dtos.GroupCreateRequest { Name = "Dev Team", Description = "Our team" };

            // Act
            var group = await _groupService.CreateGroupAsync(creator.Id, request);

            // Assert
            group.Should().NotBeNull();
            group.Name.Should().Be("Dev Team");
            group.CreatedById.Should().Be(creator.Id);
        }

        [Fact]
        public async Task CreateGroupAsync_WithoutGroupChatFeature_ShouldThrowException()
        {
            // Arrange
            var user = await CreateTestUserAsync();
            var request = new Core.Dtos.GroupCreateRequest { Name = "Dev Team", Description = "Our team" };

            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => _groupService.CreateGroupAsync(user.Id, request)
            );
        }

        [Fact]
        public async Task AddMemberAsync_AsOwner_ShouldAddMember()
        {
            // Arrange
            var owner = await CreateTestUserAsync("owner@example.com");
            var member = await CreateTestUserAsync("member@example.com");
            await GrantSubscriptionAsync(owner.Id, FeatureType.GroupChat);
            var group = await CreateTestGroupAsync(owner.Id);

            // Act
            await _groupService.AddMemberAsync(group.Id, owner.Id, member.Id);

            // Assert
            var groupMember = DbContext.GroupMembers.FirstOrDefault(
                gm => gm.GroupId == group.Id && gm.UserId == member.Id
            );
            groupMember.Should().NotBeNull();
        }

        [Fact]
        public async Task RemoveMemberAsync_AsOwner_ShouldRemoveMember()
        {
            // Arrange
            var owner = await CreateTestUserAsync("owner@example.com");
            var member = await CreateTestUserAsync("member@example.com");
            await GrantSubscriptionAsync(owner.Id, FeatureType.GroupChat);
            var group = await CreateTestGroupAsync(owner.Id, memberIds: new List<int> { member.Id });

            // Act
            await _groupService.RemoveMemberAsync(group.Id, owner.Id, member.Id);

            // Assert
            var groupMember = DbContext.GroupMembers.FirstOrDefault(
                gm => gm.GroupId == group.Id && gm.UserId == member.Id
            );
            groupMember.Should().BeNull();
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

        public MessageServiceTests()
        {
            _subscriptionService = new SubscriptionService(DbContext);
            _groupService = new GroupService(DbContext, _subscriptionService);
            _messageService = new MessageService(
                DbContext,
                _subscriptionService,
                _groupService,
                MockEmailSender.Object,
                MockNotificationService.Object
            );
        }

        [Fact]
        public async Task SendAsync_DirectMessageToUser_ShouldCreateMessage()
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
            message.SenderId.Should().Be(sender.Id);
            message.ReceiverId.Should().Be(receiver.Id);
        }

        [Fact]
        public async Task SendAsync_WithVoiceMessageWithoutFeature_ShouldThrowException()
        {
            // Arrange
            var sender = await CreateTestUserAsync("sender@example.com");
            var receiver = await CreateTestUserAsync("receiver@example.com");

            var request = new Core.Dtos.MessageCreateRequest
            {
                Content = "Voice message",
                ReceiverId = receiver.Id,
                VoiceUrl = "https://example.com/voice.mp3"
            };

            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => _messageService.SendAsync(sender.Id, request)
            );
        }

        [Fact]
        public async Task SendAsync_WithVoiceMessageWithFeature_ShouldSucceed()
        {
            // Arrange
            var sender = await CreateTestUserAsync("sender@example.com");
            var receiver = await CreateTestUserAsync("receiver@example.com");
            await GrantSubscriptionAsync(sender.Id, FeatureType.VoiceMessage);

            var request = new Core.Dtos.MessageCreateRequest
            {
                Content = "Voice message",
                ReceiverId = receiver.Id,
                VoiceUrl = "https://example.com/voice.mp3"
            };

            // Act
            var message = await _messageService.SendAsync(sender.Id, request);

            // Assert
            message.Should().NotBeNull();
            message.VoiceUrl.Should().Be("https://example.com/voice.mp3");
        }

        [Fact]
        public async Task GetInboxAsync_ShouldReturnUserMessages()
        {
            // Arrange
            var user1 = await CreateTestUserAsync("user1@example.com");
            var user2 = await CreateTestUserAsync("user2@example.com");

            await CreateTestMessageAsync(user2.Id, user1.Id, null, "Message 1");
            await CreateTestMessageAsync(user2.Id, user1.Id, null, "Message 2");

            // Act
            var inbox = await _messageService.GetInboxAsync(user1.Id, 1, 50);

            // Assert
            inbox.Data.Should().HaveCount(2);
            inbox.TotalCount.Should().Be(2);
        }

        [Fact]
        public async Task MarkAsReadAsync_ShouldUpdateReadStatus()
        {
            // Arrange
            var sender = await CreateTestUserAsync("sender@example.com");
            var receiver = await CreateTestUserAsync("receiver@example.com");
            var message = await CreateTestMessageAsync(sender.Id, receiver.Id, null, "Test");

            // Act
            await _messageService.MarkAsReadAsync(message.Id);

            // Assert
            var updatedMessage = DbContext.Messages.Find(message.Id);
            updatedMessage!.IsRead.Should().BeTrue();
        }
    }

    public class PaymentServiceTests : ServiceTestBase
    {
        private readonly IPaymentService _paymentService;
        private readonly ISubscriptionService _subscriptionService;

        public PaymentServiceTests()
        {
            _subscriptionService = new SubscriptionService(DbContext);
            _paymentService = new PaymentService(_subscriptionService);
        }

        [Fact]
        public async Task ProcessPaymentAsync_WithValidToken_ShouldGrantFeature()
        {
            // Arrange
            var user = await CreateTestUserAsync();
            var request = new Core.Dtos.PaymentRequest
            {
                UserId = user.Id,
                Feature = FeatureType.VoiceMessage,
                Months = 1,
                PaymentToken = "tok_visa",
                Amount = 2.99m
            };

            // Act
            var response = await _paymentService.ProcessPaymentAsync(request);

            // Assert
            response.Success.Should().BeTrue();
            
            var hasFeature = await _subscriptionService.HasActiveFeatureAsync(user.Id, FeatureType.VoiceMessage);
            hasFeature.Should().BeTrue();
        }

        [Fact]
        public async Task ProcessPaymentAsync_WithFailedToken_ShouldFail()
        {
            // Arrange
            var user = await CreateTestUserAsync();
            var request = new Core.Dtos.PaymentRequest
            {
                UserId = user.Id,
                Feature = FeatureType.FileSharing,
                Months = 1,
                PaymentToken = "payment_failed",
                Amount = 4.99m
            };

            // Act
            var response = await _paymentService.ProcessPaymentAsync(request);

            // Assert
            response.Success.Should().BeFalse();
        }

        [Fact]
        public void CalculateAmount_ShouldReturnCorrectPrice()
        {
            // Arrange
            var feature = FeatureType.GroupChat;
            var months = 3;

            // Act
            var amount = _paymentService.CalculateAmount(feature, months);

            // Assert
            amount.Should().Be(9.99m * 3);
        }
    }
}
