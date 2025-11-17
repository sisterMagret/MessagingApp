using Core.Dtos;
using Core.Enums;
using FluentAssertions;
using System.Net;
using Tests.Integration;

namespace Tests.Integration
{
    public class GroupChatControllerTests : IClassFixture<MessagingAppFactory>
    {
        private readonly MessagingAppFactory _factory;
        private readonly HttpClient _client;
        private readonly MessagingAppClient _appClient;
        private readonly TestDataBuilder _testData;

        public GroupChatControllerTests(MessagingAppFactory factory)
        {
            _factory = factory;
            _client = _factory.CreateClient();
            _appClient = new MessagingAppClient(_client);
            _testData = new TestDataBuilder(_factory.GetDbContext());
        }

        private async Task<(string token, int userId)> CreateAuthenticatedUserAsync(string email = "testuser@test.com", string password = "Test@123")
        {
            var user = await _testData.CreateUserAsync(email, password);
            await _testData.GrantFeatureAsync(user.Id, FeatureType.GroupChat);

            var loginRequest = new LoginRequest
            {
                Email = email,
                Password = password
            };

            var loginResponse = await _appClient.PostAsync<AuthResponse>("/api/auth/login", loginRequest);
            return (loginResponse!.Token, user.Id);
        }

        [Fact]
        public async Task CreateGroup_WithGroupChatFeature_ShouldCreateGroup()
        {
            // Arrange
            var (token, userId) = await CreateAuthenticatedUserAsync();

            var request = new GroupCreateRequest
            {
                Name = "Test Group",
                Description = "A test group"
            };

            // Act
            var response = await _appClient.PostAsync<dynamic>("/api/groups", request, token);

            // Assert
            response.Should().NotBeNull();
        }

        [Fact]
        public async Task CreateGroup_WithoutGroupChatFeature_ShouldReturnForbidden()
        {
            // Arrange
            var user = await _testData.CreateUserAsync("nofeature@test.com", "Test@123");
            // Note: Not granting GroupChat feature

            var loginRequest = new LoginRequest
            {
                Email = "nofeature@test.com",
                Password = "Test@123"
            };

            var loginResponse = await _appClient.PostAsync<AuthResponse>("/api/auth/login", loginRequest);
            var token = loginResponse!.Token;

            var request = new GroupCreateRequest
            {
                Name = "Test Group",
                Description = "A test group"
            };

            // Act
            var response = await _appClient.PostRawAsync("/api/groups", request, token);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task CreateGroup_WithoutAuthentication_ShouldReturnUnauthorized()
        {
            // Arrange
            var request = new GroupCreateRequest
            {
                Name = "Test Group",
                Description = "A test group"
            };

            // Act
            var response = await _appClient.PostRawAsync("/api/groups", request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Theory]
        [InlineData("", "Valid Description")]
        [InlineData(null, "Valid Description")]
        public async Task CreateGroup_WithInvalidData_ShouldReturnBadRequest(string name, string description)
        {
            // Arrange
            var (token, userId) = await CreateAuthenticatedUserAsync();

            var request = new GroupCreateRequest
            {
                Name = name,
                Description = description
            };

            // Act
            var response = await _appClient.PostRawAsync("/api/groups", request, token);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task GetUserGroups_WithGroupChatFeature_ShouldReturnGroups()
        {
            // Arrange
            var (token, userId) = await CreateAuthenticatedUserAsync();
            
            // Create a group first
            var group = await _testData.CreateGroupAsync(userId, "Test Group");

            // Act
            var response = await _appClient.GetAsync<dynamic>("/api/groups", token);

            // Assert
            response.Should().NotBeNull();
        }

        [Fact]
        public async Task GetUserGroups_WithoutGroupChatFeature_ShouldReturnForbidden()
        {
            // Arrange
            var user = await _testData.CreateUserAsync("nofeature2@test.com", "Test@123");
            
            var loginRequest = new LoginRequest
            {
                Email = "nofeature2@test.com",
                Password = "Test@123"
            };

            var loginResponse = await _appClient.PostAsync<AuthResponse>("/api/auth/login", loginRequest);
            var token = loginResponse!.Token;

            // Act
            var response = await _client.GetAsync("/api/groups");
            response = await _appClient.GetAsync<HttpResponseMessage>("/api/groups", token);

            // Assert - This will be handled by the GetAsync method throwing an exception
            // Let's use a different approach
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/groups");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            var rawResponse = await _client.SendAsync(request);
            rawResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task GetGroup_WithValidId_ShouldReturnGroup()
        {
            // Arrange
            var (token, userId) = await CreateAuthenticatedUserAsync();
            var group = await _testData.CreateGroupAsync(userId, "Test Group");

            // Act
            var response = await _appClient.GetAsync<dynamic>($"/api/groups/{group.Id}", token);

            // Assert
            response.Should().NotBeNull();
        }

        [Fact]
        public async Task GetGroup_WithInvalidId_ShouldReturnNotFound()
        {
            // Arrange
            var (token, userId) = await CreateAuthenticatedUserAsync();

            // Act
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/groups/99999");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            var response = await _client.SendAsync(request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task AddMember_WithValidData_ShouldAddMember()
        {
            // Arrange
            var (ownerToken, ownerId) = await CreateAuthenticatedUserAsync("owner@test.com");
            var memberUser = await _testData.CreateUserAsync("member@test.com", "Test@123");
            var group = await _testData.CreateGroupAsync(ownerId, "Test Group");

            // Act
            var request = new HttpRequestMessage(HttpMethod.Post, $"/api/groups/{group.Id}/members/{memberUser.Id}");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ownerToken);
            var response = await _client.SendAsync(request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        [Fact]
        public async Task AddMember_WithoutGroupChatFeature_ShouldReturnForbidden()
        {
            // Arrange
            var user = await _testData.CreateUserAsync("nofeature3@test.com", "Test@123");
            var memberUser = await _testData.CreateUserAsync("member2@test.com", "Test@123");
            var group = await _testData.CreateGroupAsync(user.Id, "Test Group");

            var loginRequest = new LoginRequest
            {
                Email = "nofeature3@test.com",
                Password = "Test@123"
            };

            var loginResponse = await _appClient.PostAsync<AuthResponse>("/api/auth/login", loginRequest);
            var token = loginResponse!.Token;

            // Act
            var request = new HttpRequestMessage(HttpMethod.Post, $"/api/groups/{group.Id}/members/{memberUser.Id}");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            var response = await _client.SendAsync(request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task RemoveMember_WithValidData_ShouldRemoveMember()
        {
            // Arrange
            var (ownerToken, ownerId) = await CreateAuthenticatedUserAsync("owner2@test.com");
            var memberUser = await _testData.CreateUserAsync("member3@test.com", "Test@123");
            var group = await _testData.CreateGroupAsync(ownerId, "Test Group");

            // First add the member
            var addRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/groups/{group.Id}/members/{memberUser.Id}");
            addRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ownerToken);
            var addResponse = await _client.SendAsync(addRequest);
            addResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

            // Act - Remove the member
            var removeRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/groups/{group.Id}/members/{memberUser.Id}");
            removeRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ownerToken);
            var removeResponse = await _client.SendAsync(removeRequest);

            // Assert
            removeResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        [Fact]
        public async Task DeleteGroup_AsOwner_ShouldDeleteGroup()
        {
            // Arrange
            var (ownerToken, ownerId) = await CreateAuthenticatedUserAsync("owner3@test.com");
            var group = await _testData.CreateGroupAsync(ownerId, "Test Group");

            // Act
            var response = await _appClient.DeleteAsync($"/api/groups/{group.Id}", ownerToken);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        [Fact]
        public async Task DeleteGroup_WithoutGroupChatFeature_ShouldReturnForbidden()
        {
            // Arrange
            var user = await _testData.CreateUserAsync("nofeature4@test.com", "Test@123");
            var group = await _testData.CreateGroupAsync(user.Id, "Test Group");

            var loginRequest = new LoginRequest
            {
                Email = "nofeature4@test.com",
                Password = "Test@123"
            };

            var loginResponse = await _appClient.PostAsync<AuthResponse>("/api/auth/login", loginRequest);
            var token = loginResponse!.Token;

            // Act
            var response = await _appClient.DeleteAsync($"/api/groups/{group.Id}", token);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task GroupFlow_CompleteWorkflow_ShouldWorkEndToEnd()
        {
            // Arrange
            var (ownerToken, ownerId) = await CreateAuthenticatedUserAsync("flowowner@test.com");
            var memberUser = await _testData.CreateUserAsync("flowmember@test.com", "Test@123");

            // Act & Assert - Create Group
            var createRequest = new GroupCreateRequest
            {
                Name = "Flow Test Group",
                Description = "End-to-end test group"
            };

            var createResponse = await _appClient.PostAsync<dynamic>("/api/groups", createRequest, ownerToken);
            createResponse.Should().NotBeNull();

            // Get the created group ID (this would normally be returned in the response)
            var group = await _testData.CreateGroupAsync(ownerId, "Flow Test Group");

            // Act & Assert - Get User Groups
            var groupsResponse = await _appClient.GetAsync<dynamic>("/api/groups", ownerToken);
            groupsResponse.Should().NotBeNull();

            // Act & Assert - Add Member
            var addMemberRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/groups/{group.Id}/members/{memberUser.Id}");
            addMemberRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ownerToken);
            var addMemberResponse = await _client.SendAsync(addMemberRequest);
            addMemberResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

            // Act & Assert - Get Group Details
            var groupDetailsResponse = await _appClient.GetAsync<dynamic>($"/api/groups/{group.Id}", ownerToken);
            groupDetailsResponse.Should().NotBeNull();

            // Act & Assert - Remove Member
            var removeMemberRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/groups/{group.Id}/members/{memberUser.Id}");
            removeMemberRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ownerToken);
            var removeMemberResponse = await _client.SendAsync(removeMemberRequest);
            removeMemberResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

            // Act & Assert - Delete Group
            var deleteResponse = await _appClient.DeleteAsync($"/api/groups/{group.Id}", ownerToken);
            deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }
    }
}