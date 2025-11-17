using Core.Dtos;
using Core.Enums;
using FluentAssertions;
using System.Net;
using Tests.Integration;

namespace Tests.Integration
{
    public class MessagesControllerTests : BaseIntegrationTest
    {
        public MessagesControllerTests(MessagingAppFactory factory) : base(factory)
        {
        }

        private async Task<(string token, int userId)> CreateAuthenticatedUserWithEmailAsync(string email = "testuser@test.com", string password = "Test@123")
        {
            var user = await DataBuilder.CreateUserAsync(email, password);

            var loginRequest = new LoginRequest
            {
                Email = email,
                Password = password
            };

            var loginResponse = await ApiClient.PostAsync<AuthResponse>("/api/auth/login", loginRequest);
            return (loginResponse!.Token, user.Id);
        }

        [Fact]
        public async Task SendMessage_WithValidData_ShouldCreateMessage()
        {
            // Arrange
            var (senderToken, senderId) = await CreateAuthenticatedUserAsync("sender@test.com");
            var (receiverToken, receiverId) = await CreateAuthenticatedUserAsync("receiver@test.com");

            var request = new MessageCreateDto
            {
                ReceiverId = receiverId,
                Content = "Hello, this is a test message!"
            };

            // Act
            var response = await _appClient.PostAsync<dynamic>("/api/messages", request, senderToken);

            // Assert
            response.Should().NotBeNull();
        }

        [Fact]
        public async Task SendMessage_WithoutAuthentication_ShouldReturnUnauthorized()
        {
            // Arrange
            var request = new MessageCreateDto
            {
                ReceiverId = 1,
                Content = "Hello, this is a test message!"
            };

            // Act
            var response = await _appClient.PostRawAsync("/api/messages", request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task SendMessage_WithEmptyContent_ShouldReturnBadRequest()
        {
            // Arrange
            var (senderToken, senderId) = await CreateAuthenticatedUserAsync("sender2@test.com");
            var (receiverToken, receiverId) = await CreateAuthenticatedUserAsync("receiver2@test.com");

            var request = new MessageCreateDto
            {
                ReceiverId = receiverId,
                Content = ""
            };

            // Act
            var response = await _appClient.PostRawAsync("/api/messages", request, senderToken);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task SendMessage_WithNullContent_ShouldReturnBadRequest()
        {
            // Arrange
            var (senderToken, senderId) = await CreateAuthenticatedUserAsync("sender3@test.com");
            var (receiverToken, receiverId) = await CreateAuthenticatedUserAsync("receiver3@test.com");

            var request = new MessageCreateDto
            {
                ReceiverId = receiverId,
                Content = null!
            };

            // Act
            var response = await _appClient.PostRawAsync("/api/messages", request, senderToken);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task SendMessage_WithInvalidReceiverId_ShouldReturnBadRequest()
        {
            // Arrange
            var (senderToken, senderId) = await CreateAuthenticatedUserAsync("sender4@test.com");

            var request = new MessageCreateDto
            {
                ReceiverId = 99999, // Non-existent user
                Content = "Hello, this is a test message!"
            };

            // Act
            var response = await _appClient.PostRawAsync("/api/messages", request, senderToken);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task SendMessage_ToSelf_ShouldReturnBadRequest()
        {
            // Arrange
            var (senderToken, senderId) = await CreateAuthenticatedUserAsync("selfsender@test.com");

            var request = new MessageCreateDto
            {
                ReceiverId = senderId, // Sending to self
                Content = "Hello, myself!"
            };

            // Act
            var response = await _appClient.PostRawAsync("/api/messages", request, senderToken);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task GetInbox_WithAuthentication_ShouldReturnMessages()
        {
            // Arrange
            var (receiverToken, receiverId) = await CreateAuthenticatedUserAsync("inboxuser@test.com");
            var (senderToken, senderId) = await CreateAuthenticatedUserAsync("inboxsender@test.com");

            // Send a message first
            var sendRequest = new MessageCreateDto
            {
                ReceiverId = receiverId,
                Content = "Test inbox message"
            };
            await _appClient.PostAsync<dynamic>("/api/messages", sendRequest, senderToken);

            // Act
            var response = await _appClient.GetAsync<dynamic>("/api/messages/inbox", receiverToken);

            // Assert
            response.Should().NotBeNull();
        }

        [Fact]
        public async Task GetInbox_WithoutAuthentication_ShouldReturnUnauthorized()
        {
            // Act
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/messages/inbox");
            var response = await _client.SendAsync(request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task GetInbox_WithInvalidToken_ShouldReturnUnauthorized()
        {
            // Act
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/messages/inbox");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "invalid-token");
            var response = await _client.SendAsync(request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task MessageFlow_CompleteConversation_ShouldWorkEndToEnd()
        {
            // Arrange
            var (aliceToken, aliceId) = await CreateAuthenticatedUserAsync("alice@test.com");
            var (bobToken, bobId) = await CreateAuthenticatedUserAsync("bob@test.com");

            // Act & Assert - Alice sends message to Bob
            var aliceToBobRequest = new MessageCreateDto
            {
                ReceiverId = bobId,
                Content = "Hello Bob, how are you?"
            };

            var aliceToBobResponse = await _appClient.PostAsync<dynamic>("/api/messages", aliceToBobRequest, aliceToken);
            aliceToBobResponse.Should().NotBeNull();

            // Act & Assert - Bob checks his inbox
            var bobInboxResponse = await _appClient.GetAsync<dynamic>("/api/messages/inbox", bobToken);
            bobInboxResponse.Should().NotBeNull();

            // Act & Assert - Bob replies to Alice
            var bobToAliceRequest = new MessageCreateDto
            {
                ReceiverId = aliceId,
                Content = "Hi Alice! I'm doing great, thanks for asking."
            };

            var bobToAliceResponse = await _appClient.PostAsync<dynamic>("/api/messages", bobToAliceRequest, bobToken);
            bobToAliceResponse.Should().NotBeNull();

            // Act & Assert - Alice checks her inbox
            var aliceInboxResponse = await _appClient.GetAsync<dynamic>("/api/messages/inbox", aliceToken);
            aliceInboxResponse.Should().NotBeNull();

            // Act & Assert - Alice sends follow-up message
            var aliceFollowUpRequest = new MessageCreateDto
            {
                ReceiverId = bobId,
                Content = "That's wonderful to hear!"
            };

            var aliceFollowUpResponse = await _appClient.PostAsync<dynamic>("/api/messages", aliceFollowUpRequest, aliceToken);
            aliceFollowUpResponse.Should().NotBeNull();

            // Act & Assert - Bob checks updated inbox
            var bobUpdatedInboxResponse = await _appClient.GetAsync<dynamic>("/api/messages/inbox", bobToken);
            bobUpdatedInboxResponse.Should().NotBeNull();
        }

        [Theory]
        [InlineData("Short")]
        [InlineData("This is a medium length message that should work perfectly fine.")]
        [InlineData("This is a very long message that contains a lot of text to test how the system handles longer messages. It includes multiple sentences and should still work correctly as long as it's within reasonable limits. The system should be able to handle messages of varying lengths without any issues.")]
        public async Task SendMessage_WithVariousMessageLengths_ShouldWork(string content)
        {
            // Arrange
            var (senderToken, senderId) = await CreateAuthenticatedUserAsync($"sender{content.Length}@test.com");
            var (receiverToken, receiverId) = await CreateAuthenticatedUserAsync($"receiver{content.Length}@test.com");

            var request = new MessageCreateDto
            {
                ReceiverId = receiverId,
                Content = content
            };

            // Act
            var response = await _appClient.PostAsync<dynamic>("/api/messages", request, senderToken);

            // Assert
            response.Should().NotBeNull();
        }

        [Fact]
        public async Task SendMessage_WithSpecialCharacters_ShouldWork()
        {
            // Arrange
            var (senderToken, senderId) = await CreateAuthenticatedUserAsync("special1@test.com");
            var (receiverToken, receiverId) = await CreateAuthenticatedUserAsync("special2@test.com");

            var request = new MessageCreateDto
            {
                ReceiverId = receiverId,
                Content = "Hello! 🎉 This message contains émojis, açcénts, and spëcial ¢haractërs! 测试"
            };

            // Act
            var response = await _appClient.PostAsync<dynamic>("/api/messages", request, senderToken);

            // Assert
            response.Should().NotBeNull();
        }

        [Fact]
        public async Task MultipleUsers_ConcurrentMessaging_ShouldWork()
        {
            // Arrange
            var (user1Token, user1Id) = await CreateAuthenticatedUserAsync("concurrent1@test.com");
            var (user2Token, user2Id) = await CreateAuthenticatedUserAsync("concurrent2@test.com");
            var (user3Token, user3Id) = await CreateAuthenticatedUserAsync("concurrent3@test.com");

            // Act - Send messages concurrently
            var message1Task = _appClient.PostAsync<dynamic>("/api/messages", new MessageCreateDto
            {
                ReceiverId = user2Id,
                Content = "Message from User 1 to User 2"
            }, user1Token);

            var message2Task = _appClient.PostAsync<dynamic>("/api/messages", new MessageCreateDto
            {
                ReceiverId = user3Id,
                Content = "Message from User 2 to User 3"
            }, user2Token);

            var message3Task = _appClient.PostAsync<dynamic>("/api/messages", new MessageCreateDto
            {
                ReceiverId = user1Id,
                Content = "Message from User 3 to User 1"
            }, user3Token);

            var results = await Task.WhenAll(message1Task, message2Task, message3Task);

            // Assert
            results.Should().AllSatisfy(result => result.Should().NotBeNull());

            // Verify all users can access their inboxes
            var inbox1Task = _appClient.GetAsync<dynamic>("/api/messages/inbox", user1Token);
            var inbox2Task = _appClient.GetAsync<dynamic>("/api/messages/inbox", user2Token);
            var inbox3Task = _appClient.GetAsync<dynamic>("/api/messages/inbox", user3Token);

            var inboxResults = await Task.WhenAll(inbox1Task, inbox2Task, inbox3Task);
            inboxResults.Should().AllSatisfy(result => result.Should().NotBeNull());
        }
    }

    // DTO class for message creation
    public class MessageCreateDto
    {
        public int ReceiverId { get; set; }
        public string Content { get; set; } = default!;
    }
}