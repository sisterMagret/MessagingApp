using Api;
using Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.TestHost;

namespace Tests.Integration
{
    /// <summary>
    /// Custom WebApplicationFactory for integration testing
    /// Replaces database with InMemory version for testing
    /// </summary>
    public class MessagingAppFactory : WebApplicationFactory<Program>
    {
        private string? _dbName;

        public MessagingAppFactory() : this(null)
        {
        }

        private MessagingAppFactory(string? dbName)
        {
            _dbName = dbName ?? Guid.NewGuid().ToString();
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            // Use test environment which will pick up appsettings.Test.json
            builder.UseEnvironment("Test");
        }

        public void InitializeDatabase()
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MessagingDbContext>();
            db.Database.EnsureCreated();
        }

        public MessagingDbContext GetDbContext()
        {
            var scope = Services.CreateScope();
            return scope.ServiceProvider.GetRequiredService<MessagingDbContext>();
        }
    }

    /// <summary>
    /// Builder class for creating test data
    /// </summary>
    public class TestDataBuilder
    {
        private readonly MessagingDbContext _context;

        public TestDataBuilder(MessagingDbContext context)
        {
            _context = context;
        }

        public async Task<Core.Entities.User> CreateUserAsync(
            string email = "testuser@example.com",
            string password = "Test@123")
        {
            var hasher = new Microsoft.AspNetCore.Identity.PasswordHasher<Core.Entities.User>();
            var user = new Core.Entities.User
            {
                Email = email,
                PasswordHash = hasher.HashPassword(null!, password),
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            return user;
        }

        public async Task<Core.Entities.Subscription> GrantFeatureAsync(
            int userId,
            Core.Enums.FeatureType feature,
            int daysValid = 30)
        {
            var subscription = new Core.Entities.Subscription
            {
                UserId = userId,
                Feature = feature,
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddDays(daysValid)
            };

            _context.Subscriptions.Add(subscription);
            await _context.SaveChangesAsync();
            return subscription;
        }

        public async Task<Core.Entities.Group> CreateGroupAsync(
            int creatorId,
            string name = "Test Group")
        {
            var group = new Core.Entities.Group
            {
                Name = name,
                Description = "Test group",
                CreatedById = creatorId,
                Members = new List<Core.Entities.GroupMember>
                {
                    new Core.Entities.GroupMember
                    {
                        UserId = creatorId,
                        Role = Core.Enums.GroupRole.Owner,
                        JoinedAt = DateTime.UtcNow
                    }
                }
            };

            _context.Groups.Add(group);
            await _context.SaveChangesAsync();
            return group;
        }

        public async Task ClearDatabaseAsync()
        {
            _context.Messages.RemoveRange(_context.Messages);
            _context.GroupMembers.RemoveRange(_context.GroupMembers);
            _context.Groups.RemoveRange(_context.Groups);
            _context.Subscriptions.RemoveRange(_context.Subscriptions);
            _context.Users.RemoveRange(_context.Users);
            await _context.SaveChangesAsync();
        }
    }

    /// <summary>
    /// HTTP Client helper for E2E tests
    /// </summary>
    public class MessagingAppClient
    {
        private readonly HttpClient _httpClient;

        public MessagingAppClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<T?> GetAsync<T>(string endpoint, string? token = null)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            if (!string.IsNullOrEmpty(token))
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            
            var json = await response.Content.ReadAsStringAsync();
            return System.Text.Json.JsonSerializer.Deserialize<T>(json, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }

        public async Task<T?> PostAsync<T>(string endpoint, object payload, string? token = null)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(payload);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = content
            };

            if (!string.IsNullOrEmpty(token))
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            return System.Text.Json.JsonSerializer.Deserialize<T>(responseJson, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }

        public async Task<HttpResponseMessage> PostRawAsync(string endpoint, object payload, string? token = null)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(payload);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = content
            };

            if (!string.IsNullOrEmpty(token))
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            return await _httpClient.SendAsync(request);
        }

        public async Task<HttpResponseMessage> DeleteAsync(string endpoint, string? token = null)
        {
            var request = new HttpRequestMessage(HttpMethod.Delete, endpoint);
            if (!string.IsNullOrEmpty(token))
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            return await _httpClient.SendAsync(request);
        }
    }
}
