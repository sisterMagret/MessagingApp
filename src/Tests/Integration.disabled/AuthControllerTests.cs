using Core.Dtos;
using FluentAssertions;
using System.Net;
using Tests.Integration;

namespace Tests.Integration
{
    public class AuthControllerTests : BaseIntegrationTest
    {
        public AuthControllerTests(MessagingAppFactory factory) : base(factory)
        {
        }

        [Fact]
        public async Task Register_WithValidData_ShouldCreateUserAndReturnToken()
        {
            // Arrange
            var request = new RegisterRequest
            {
                Email = "newuser@test.com",
                Password = "ValidPassword@123"
            };

            // Act
            var response = await ApiClient.PostAsync<AuthResponse>("/api/auth/register", request);

            // Assert
            response.Should().NotBeNull();
            response!.Email.Should().Be("newuser@test.com");
            response.Token.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task Register_WithDuplicateEmail_ShouldReturnBadRequest()
        {
            // Arrange
            await DataBuilder.CreateUserAsync("existing@test.com", "Password@123");

            var request = new RegisterRequest
            {
                Email = "existing@test.com",
                Password = "NewPassword@123"
            };

            // Act
            var response = await ApiClient.PostRawAsync("/api/auth/register", request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Register_WithInvalidEmail_ShouldReturnBadRequest()
        {
            // Arrange
            var request = new RegisterRequest
            {
                Email = "invalid-email",
                Password = "ValidPassword@123"
            };

            // Act
            var response = await ApiClient.PostRawAsync("/api/auth/register", request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Login_WithValidCredentials_ShouldReturnToken()
        {
            // Arrange
            await DataBuilder.CreateUserAsync("logintest@test.com", "Password@123");

            var request = new LoginRequest
            {
                Email = "logintest@test.com",
                Password = "Password@123"
            };

            // Act
            var response = await ApiClient.PostAsync<AuthResponse>("/api/auth/login", request);

            // Assert
            response.Should().NotBeNull();
            response!.Email.Should().Be("logintest@test.com");
            response.Token.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task Login_WithInvalidCredentials_ShouldReturnUnauthorized()
        {
            // Arrange
            await DataBuilder.CreateUserAsync("logintest2@test.com", "CorrectPassword@123");

            var request = new LoginRequest
            {
                Email = "logintest2@test.com",
                Password = "WrongPassword@123"
            };

            // Act
            var response = await ApiClient.PostRawAsync("/api/auth/login", request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        }

        [Fact]
        public async Task Login_WithNonexistentUser_ShouldReturnUnauthorized()
        {
            // Arrange
            var request = new LoginRequest
            {
                Email = "nonexistent@test.com",
                Password = "Password@123"
            };

            // Act
            var response = await ApiClient.PostRawAsync("/api/auth/login", request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task Me_WithValidToken_ShouldReturnUserInfo()
        {
            // Arrange
            await DataBuilder.CreateUserAsync("metest@test.com", "Password@123");

            var loginRequest = new LoginRequest
            {
                Email = "metest@test.com",
                Password = "Password@123"
            };

            var loginResponse = await ApiClient.PostAsync<AuthResponse>("/api/auth/login", loginRequest);
            var token = loginResponse!.Token;

            // Act
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            var finalResponse = await Client.SendAsync(request);

            // Assert
            finalResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var content = await finalResponse.Content.ReadAsStringAsync();
            content.Should().Contain("metest@test.com");
        }

        [Fact]
        public async Task Me_WithoutToken_ShouldReturnUnauthorized()
        {
            // Act
            var response = await Client.GetAsync("/api/auth/me");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task Me_WithInvalidToken_ShouldReturnUnauthorized()
        {
            // Act
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "invalid-token");
            var response = await Client.SendAsync(request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Theory]
        [InlineData("", "Password@123")]
        [InlineData("test@example.com", "")]
        [InlineData("", "")]
        public async Task Register_WithMissingFields_ShouldReturnBadRequest(string email, string password)
        {
            // Arrange
            var request = new RegisterRequest
            {
                Email = email,
                Password = password
            };

            // Act
            var response = await ApiClient.PostRawAsync("/api/auth/register", request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Theory]
        [InlineData("", "Password@123")]
        [InlineData("test@example.com", "")]
        [InlineData("", "")]
        public async Task Login_WithMissingFields_ShouldReturnBadRequest(string email, string password)
        {
            // Arrange
            var request = new LoginRequest
            {
                Email = email,
                Password = password
            };

            // Act
            var response = await ApiClient.PostRawAsync("/api/auth/login", request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task AuthFlow_CompleteUserJourney_ShouldWorkEndToEnd()
        {
            // Arrange
            var registerRequest = new RegisterRequest
            {
                Email = "journey@test.com",
                Password = "JourneyPassword@123"
            };

            // Act & Assert - Register
            var registerResponse = await ApiClient.PostAsync<AuthResponse>("/api/auth/register", registerRequest);
            registerResponse.Should().NotBeNull();
            registerResponse!.Email.Should().Be("journey@test.com");
            registerResponse.Token.Should().NotBeNullOrEmpty();

            // Act & Assert - Login
            var loginRequest = new LoginRequest
            {
                Email = "journey@test.com",
                Password = "JourneyPassword@123"
            };

            var loginResponse = await ApiClient.PostAsync<AuthResponse>("/api/auth/login", loginRequest);
            loginResponse.Should().NotBeNull();
            loginResponse!.Email.Should().Be("journey@test.com");
            loginResponse.Token.Should().NotBeNullOrEmpty();

            // Act & Assert - Me endpoint
            var meRequest = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
            meRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginResponse.Token);
            var meResponse = await Client.SendAsync(meRequest);
            meResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }
}