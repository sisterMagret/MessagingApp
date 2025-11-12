using Core.Dtos;
using Core.Enums;
using FluentAssertions;
using System.Net;

namespace Tests.Integration
{
    public class EndToEndTests : IAsyncLifetime
    {
        private MessagingAppFactory _factory = null!;
        private MessagingAppClient _client = null!;
        private TestDataBuilder _dataBuilder = null!;

        public async Task InitializeAsync()
        {
            _factory = new MessagingAppFactory();
            var httpClient = _factory.CreateClient();
            _client = new MessagingAppClient(httpClient);
            _dataBuilder = new TestDataBuilder(_factory.GetDbContext());
            await _dataBuilder.ClearDatabaseAsync();
        }

        public async Task DisposeAsync()
        {
            _factory?.Dispose();
            await Task.CompletedTask;
        }

        #region Authentication Tests

        [Fact]
        public async Task E2E_UserCanRegisterAndLogin()
        {
            // Arrange
            var registerRequest = new RegisterRequest
            {
                Email = "alice@example.com",
                Password = "Alice@12345"
            };

            // Act - Register
            var registerResponse = await _client.PostAsync<AuthResponse>("/api/auth/register", registerRequest);

            // Assert - Registration successful
            registerResponse.Should().NotBeNull();
            registerResponse!.Email.Should().Be("alice@example.com");
            var registerToken = registerResponse.Token;
            registerToken.Should().NotBeNullOrEmpty();

            // Act - Login
            var loginRequest = new LoginRequest
            {
                Email = "alice@example.com",
                Password = "Alice@12345"
            };
            var loginResponse = await _client.PostAsync<AuthResponse>("/api/auth/register", loginRequest);

            // Assert - Login successful
            loginResponse.Should().NotBeNull();
            loginResponse!.Token.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task E2E_UserCannotLoginWithWrongPassword()
        {
            // Arrange
            await _client.PostAsync<AuthResponse>("/api/auth/register", new RegisterRequest
            {
                Email = "bob@example.com",
                Password = "Bob@12345"
            });

            var wrongLoginRequest = new LoginRequest
            {
                Email = "bob@example.com",
                Password = "WrongPassword@123"
            };

            // Act
            var response = await _client.PostRawAsync("/api/auth/login", wrongLoginRequest);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        #endregion

        #region Direct Messaging Tests

        [Fact]
        public async Task E2E_UserCanSendAndReceiveDirectMessages()
        {
            // Arrange
            var alice = await _dataBuilder.CreateUserAsync("alice@example.com");
            var bob = await _dataBuilder.CreateUserAsync("bob@example.com");

            var loginAlice = new LoginRequest { Email = "alice@example.com", Password = "Test@123" };
            var aliceAuth = await _client.PostAsync<AuthResponse>("/api/auth/login", loginAlice);
            var aliceToken = aliceAuth!.Token;

            var loginBob = new LoginRequest { Email = "bob@example.com", Password = "Test@123" };
            var bobAuth = await _client.PostAsync<AuthResponse>("/api/auth/login", loginBob);
            var bobToken = bobAuth!.Token;

            // Act - Alice sends message to Bob
            var messageRequest = new MessageCreateRequest
            {
                Content = "Hi Bob, how are you?",
                ReceiverId = bob.Id,
                GroupId = null
            };
            var sentMessage = await _client.PostAsync<MessageDto>(
                "/api/messages",
                messageRequest,
                aliceToken
            );

            // Assert - Message sent
            sentMessage.Should().NotBeNull();
            sentMessage!.Content.Should().Be("Hi Bob, how are you?");
            sentMessage.SenderId.Should().Be(alice.Id);

            // Act - Bob checks inbox
            var inbox = await _client.GetAsync<PagedResult<MessageDto>>(
                "/api/messages/inbox",
                bobToken
            );

            // Assert - Message received
            inbox.Should().NotBeNull();
            inbox!.Data.Should().Contain(m => m.Content == "Hi Bob, how are you?");
        }

        [Fact]
        public async Task E2E_UserCanMarkMessageAsRead()
        {
            // Arrange
            var alice = await _dataBuilder.CreateUserAsync("alice@example.com");
            var bob = await _dataBuilder.CreateUserAsync("bob@example.com");

            var loginAlice = new LoginRequest { Email = "alice@example.com", Password = "Test@123" };
            var aliceAuth = await _client.PostAsync<AuthResponse>("/api/auth/login", loginAlice);

            var messageRequest = new MessageCreateRequest
            {
                Content = "Test message",
                ReceiverId = bob.Id,
                GroupId = null
            };
            var sentMessage = await _client.PostAsync<MessageDto>(
                "/api/messages",
                messageRequest,
                aliceAuth!.Token
            );

            // Act - Mark as read
            var readResponse = await _client.PostAsync<object>(
                $"/api/messages/{sentMessage!.Id}/read",
                new { },
                aliceAuth.Token
            );

            // Assert
            readResponse.Should().NotBeNull();
        }

        #endregion

        #region Feature Gating Tests

        [Fact]
        public async Task E2E_UserCannotSendVoiceMessageWithoutFeature()
        {
            // Arrange
            var alice = await _dataBuilder.CreateUserAsync("alice@example.com");
            var bob = await _dataBuilder.CreateUserAsync("bob@example.com");

            var loginAlice = new LoginRequest { Email = "alice@example.com", Password = "Test@123" };
            var aliceAuth = await _client.PostAsync<AuthResponse>("/api/auth/login", loginAlice);

            var voiceMessageRequest = new MessageCreateRequest
            {
                Content = "Voice message",
                ReceiverId = bob.Id,
                VoiceUrl = "https://example.com/voice.mp3"
            };

            // Act
            var response = await _client.PostRawAsync(
                "/api/messages",
                voiceMessageRequest,
                aliceAuth!.Token
            );

            // Assert - Should be unauthorized
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task E2E_UserCanSendVoiceMessageWithFeature()
        {
            // Arrange
            var alice = await _dataBuilder.CreateUserAsync("alice@example.com");
            var bob = await _dataBuilder.CreateUserAsync("bob@example.com");
            await _dataBuilder.GrantFeatureAsync(alice.Id, FeatureType.VoiceMessage);

            var loginAlice = new LoginRequest { Email = "alice@example.com", Password = "Test@123" };
            var aliceAuth = await _client.PostAsync<AuthResponse>("/api/auth/login", loginAlice);

            var voiceMessageRequest = new MessageCreateRequest
            {
                Content = "Voice message",
                ReceiverId = bob.Id,
                VoiceUrl = "https://example.com/voice.mp3"
            };

            // Act
            var sentMessage = await _client.PostAsync<MessageDto>(
                "/api/messages",
                voiceMessageRequest,
                aliceAuth!.Token
            );

            // Assert - Should succeed
            sentMessage.Should().NotBeNull();
            sentMessage!.VoiceUrl.Should().Be("https://example.com/voice.mp3");
        }

        #endregion

        #region Subscription Tests

        [Fact]
        public async Task E2E_UserCanCheckIfHasFeature()
        {
            // Arrange
            var alice = await _dataBuilder.CreateUserAsync("alice@example.com");
            await _dataBuilder.GrantFeatureAsync(alice.Id, FeatureType.FileSharing);

            var loginAlice = new LoginRequest { Email = "alice@example.com", Password = "Test@123" };
            var aliceAuth = await _client.PostAsync<AuthResponse>("/api/auth/login", loginAlice);

            // Act - Check for feature they have
            var hasFileSharing = await _client.GetAsync<bool>(
                $"/api/subscriptions/has-feature/{(int)FeatureType.FileSharing}",
                aliceAuth!.Token
            );

            // Assert
            hasFileSharing.Should().BeTrue();

            // Act - Check for feature they don't have
            var hasVoice = await _client.GetAsync<bool>(
                $"/api/subscriptions/has-feature/{(int)FeatureType.VoiceMessage}",
                aliceAuth.Token
            );

            // Assert
            hasVoice.Should().BeFalse();
        }

        [Fact]
        public async Task E2E_UserCanGetTheirSubscriptions()
        {
            // Arrange
            var alice = await _dataBuilder.CreateUserAsync("alice@example.com");
            await _dataBuilder.GrantFeatureAsync(alice.Id, FeatureType.VoiceMessage);
            await _dataBuilder.GrantFeatureAsync(alice.Id, FeatureType.FileSharing);

            var loginAlice = new LoginRequest { Email = "alice@example.com", Password = "Test@123" };
            var aliceAuth = await _client.PostAsync<AuthResponse>("/api/auth/login", loginAlice);

            // Act
            var subscriptions = await _client.GetAsync<List<SubscriptionDto>>(
                "/api/subscriptions/my-subscriptions",
                aliceAuth!.Token
            );

            // Assert
            subscriptions.Should().NotBeNull();
            subscriptions!.Should().HaveCount(2);
        }

        #endregion

        #region Payment & Purchase Tests

        [Fact]
        public async Task E2E_UserCanPurchaseFeatureAndSendVoiceMessage()
        {
            // Arrange - Register and login users
            var alice = await _dataBuilder.CreateUserAsync("alice@example.com");
            var bob = await _dataBuilder.CreateUserAsync("bob@example.com");

            var loginAlice = new LoginRequest { Email = "alice@example.com", Password = "Test@123" };
            var aliceAuth = await _client.PostAsync<AuthResponse>("/api/auth/login", loginAlice);
            var aliceToken = aliceAuth!.Token;

            // Act 1 - Try to send voice message without feature (should fail)
            var voiceBeforePurchase = new MessageCreateRequest
            {
                Content = "Voice before purchase",
                ReceiverId = bob.Id,
                VoiceUrl = "https://example.com/voice1.mp3"
            };
            var failedResponse = await _client.PostRawAsync(
                "/api/messages",
                voiceBeforePurchase,
                aliceToken
            );
            failedResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

            // Act 2 - Purchase VoiceMessage feature
            var purchaseRequest = new PaymentRequest
            {
                Feature = FeatureType.VoiceMessage,
                Months = 1,
                PaymentToken = "tok_visa",
                Amount = 2.99m
            };
            var purchaseResponse = await _client.PostAsync<PaymentResponse>(
                "/api/payments/purchase",
                purchaseRequest,
                aliceToken
            );

            // Assert - Purchase successful
            purchaseResponse.Should().NotBeNull();
            purchaseResponse!.Success.Should().BeTrue();

            // Act 3 - Now send voice message (should succeed)
            var voiceAfterPurchase = new MessageCreateRequest
            {
                Content = "Voice after purchase",
                ReceiverId = bob.Id,
                VoiceUrl = "https://example.com/voice2.mp3"
            };
            var successMessage = await _client.PostAsync<MessageDto>(
                "/api/messages",
                voiceAfterPurchase,
                aliceToken
            );

            // Assert - Message sent successfully
            successMessage.Should().NotBeNull();
            successMessage!.VoiceUrl.Should().Be("https://example.com/voice2.mp3");
        }

        [Fact]
        public async Task E2E_UserCanPurchaseMultipleFeaturesAndCreateGroup()
        {
            // Arrange
            var alice = await _dataBuilder.CreateUserAsync("alice@example.com");
            var bob = await _dataBuilder.CreateUserAsync("bob@example.com");

            var loginAlice = new LoginRequest { Email = "alice@example.com", Password = "Test@123" };
            var aliceAuth = await _client.PostAsync<AuthResponse>("/api/auth/login", loginAlice);
            var aliceToken = aliceAuth!.Token;

            // Act - Purchase GroupChat feature
            var groupChatPurchase = new PaymentRequest
            {
                Feature = FeatureType.GroupChat,
                Months = 2,
                PaymentToken = "tok_visa",
                Amount = 9.99m * 2
            };
            var purchaseResponse = await _client.PostAsync<PaymentResponse>(
                "/api/payments/purchase",
                groupChatPurchase,
                aliceToken
            );

            // Assert - Purchase successful
            purchaseResponse!.Success.Should().BeTrue();

            // Act - Create group
            var groupRequest = new GroupCreateRequest
            {
                Name = "Dev Team",
                Description = "Our development team"
            };
            var createdGroup = await _client.PostAsync<GroupDto>(
                "/api/groups",
                groupRequest,
                aliceToken
            );

            // Assert - Group created
            createdGroup.Should().NotBeNull();
            createdGroup!.Name.Should().Be("Dev Team");

            // Act - Add Bob to group
            var addMemberRequest = new { userId = bob.Id };
            var addResponse = await _client.PostAsync<object>(
                $"/api/groups/{createdGroup.Id}/members",
                addMemberRequest,
                aliceToken
            );

            // Assert
            addResponse.Should().NotBeNull();

            // Act - Send group message
            var groupMessageRequest = new MessageCreateRequest
            {
                Content = "Welcome to the team!",
                ReceiverId = null,
                GroupId = createdGroup.Id
            };
            var groupMessage = await _client.PostAsync<MessageDto>(
                "/api/messages",
                groupMessageRequest,
                aliceToken
            );

            // Assert
            groupMessage.Should().NotBeNull();
            groupMessage!.GroupId.Should().Be(createdGroup.Id);
        }

        #endregion

        #region Group Chat Tests

        [Fact]
        public async Task E2E_UserCannotCreateGroupWithoutFeature()
        {
            // Arrange
            var alice = await _dataBuilder.CreateUserAsync("alice@example.com");

            var loginAlice = new LoginRequest { Email = "alice@example.com", Password = "Test@123" };
            var aliceAuth = await _client.PostAsync<AuthResponse>("/api/auth/login", loginAlice);

            var groupRequest = new GroupCreateRequest
            {
                Name = "Dev Team",
                Description = "Our team"
            };

            // Act
            var response = await _client.PostRawAsync(
                "/api/groups",
                groupRequest,
                aliceAuth!.Token
            );

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task E2E_UserCanViewGroupMessages()
        {
            // Arrange
            var alice = await _dataBuilder.CreateUserAsync("alice@example.com");
            var bob = await _dataBuilder.CreateUserAsync("bob@example.com");

            await _dataBuilder.GrantFeatureAsync(alice.Id, FeatureType.GroupChat);
            
            var group = await _dataBuilder.CreateGroupAsync(alice.Id, "Team Channel");
            // Add Bob to group
            group.Members.Add(new Core.Entities.GroupMember
            {
                UserId = bob.Id,
                Role = GroupRole.Member,
                JoinedAt = DateTime.UtcNow
            });
            
            var db = _factory.GetDbContext();
            db.Groups.Update(group);
            await db.SaveChangesAsync();

            var loginAlice = new LoginRequest { Email = "alice@example.com", Password = "Test@123" };
            var aliceAuth = await _client.PostAsync<AuthResponse>("/api/auth/login", loginAlice);

            // Act - Send group message
            var messageRequest = new MessageCreateRequest
            {
                Content = "Hello team!",
                ReceiverId = null,
                GroupId = group.Id
            };
            await _client.PostAsync<MessageDto>(
                "/api/messages",
                messageRequest,
                aliceAuth!.Token
            );

            // Act - Get group messages
            var messages = await _client.GetAsync<List<MessageDto>>(
                $"/api/groups/{group.Id}/messages",
                aliceAuth.Token
            );

            // Assert
            messages.Should().NotBeNull();
            messages!.Should().Contain(m => m.Content == "Hello team!");
        }

        #endregion
    }
}
