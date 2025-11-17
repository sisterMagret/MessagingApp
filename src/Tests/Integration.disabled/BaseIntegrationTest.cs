using Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Tests.Integration
{
    /// <summary>
    /// Base class for integration tests with proper database cleanup
    /// </summary>
    public abstract class BaseIntegrationTest : IClassFixture<MessagingAppFactory>, IDisposable
    {
        protected readonly MessagingAppFactory Factory;
        protected readonly HttpClient Client;
        protected readonly MessagingAppClient ApiClient;
        private readonly IServiceScope _scope;
        private readonly MessagingDbContext _dbContext;
        private readonly TestDataBuilder _dataBuilder;
        private bool _disposed = false;

        protected BaseIntegrationTest(MessagingAppFactory factory)
        {
            Factory = factory;
            Client = factory.CreateClient();
            ApiClient = new MessagingAppClient(Client);
            
            // Create a scope for database operations
            _scope = factory.Services.CreateScope();
            _dbContext = _scope.ServiceProvider.GetRequiredService<MessagingDbContext>();
            _dataBuilder = new TestDataBuilder(_dbContext);
            
            // Ensure database is created
            _dbContext.Database.EnsureCreated();
        }

        /// <summary>
        /// Gets the test data builder for creating test entities
        /// </summary>
        protected TestDataBuilder DataBuilder => _dataBuilder;

        /// <summary>
        /// Creates a user with a unique email to avoid conflicts
        /// </summary>
        protected async Task<Core.Entities.User> CreateUniqueUserAsync(string? password = null)
        {
            var uniqueEmail = $"user{Guid.NewGuid():N}@test.com";
            return await _dataBuilder.CreateUserAsync(uniqueEmail, password ?? "Test@123");
        }

        /// <summary>
        /// Creates an authenticated user and returns the JWT token
        /// </summary>
        protected async Task<(Core.Entities.User User, string Token)> CreateAuthenticatedUserAsync()
        {
            var user = await CreateUniqueUserAsync();
            
            // Login to get token
            var loginRequest = new Core.Dtos.LoginRequest
            {
                Email = user.Email,
                Password = "Test@123"
            };
            
            var loginResponse = await ApiClient.PostAsync<Core.Dtos.AuthResponse>("/api/auth/login", loginRequest);
            var token = loginResponse?.Token ?? "";
            
            return (user, token);
        }

        /// <summary>
        /// Cleans up the database after each test
        /// </summary>
        protected virtual async Task CleanupDatabaseAsync()
        {
            await _dataBuilder.ClearDatabaseAsync();
        }

        public virtual void Dispose()
        {
            if (!_disposed)
            {
                // Clean up database
                try
                {
                    CleanupDatabaseAsync().GetAwaiter().GetResult();
                }
                catch
                {
                    // Ignore cleanup errors
                }

                _scope?.Dispose();
                _disposed = true;
            }
            
            GC.SuppressFinalize(this);
        }

        ~BaseIntegrationTest()
        {
            Dispose();
        }
    }
}