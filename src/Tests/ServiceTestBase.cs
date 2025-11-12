using Core.Contracts;
using Core.Interfaces;
using Infrastructure.Data;
using Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Moq;
using Core.Entities;
using Core.Enums;

namespace Tests
{
    /// <summary>
    /// Base class for all service unit tests providing:
    /// - InMemory database context
    /// - Mock services
    /// - Helper methods for common operations
    /// </summary>
    public abstract class ServiceTestBase : IDisposable
    {
        protected readonly MessagingDbContext DbContext;
        protected readonly Mock<IEmailSender> MockEmailSender;
        protected readonly Mock<INotificationService> MockNotificationService;
        protected readonly PasswordHasher<User> PasswordHasher;
        protected readonly IConfiguration Configuration;

        protected ServiceTestBase()
        {
            // Create in-memory database
            var options = new DbContextOptionsBuilder<MessagingDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            DbContext = new MessagingDbContext(options);
            
            // Setup mocks
            MockEmailSender = new Mock<IEmailSender>();
            MockNotificationService = new Mock<INotificationService>();
            PasswordHasher = new PasswordHasher<User>();

            // Mock configuration for JWT
            var mockConfig = new Mock<IConfiguration>();
            mockConfig.Setup(x => x["Jwt:Key"])
                .Returns("this-is-a-very-secure-secret-key-that-is-long-enough-for-hs256-algorithm");
            mockConfig.Setup(x => x["Jwt:Issuer"])
                .Returns("MessagingApp");
            mockConfig.Setup(x => x["Jwt:Audience"])
                .Returns("MessagingAppUsers");
            Configuration = mockConfig.Object;
        }

        /// <summary>
        /// Creates and saves a test user
        /// </summary>
        protected async Task<User> CreateTestUserAsync(string email = "test@example.com", string password = "Test@123")
        {
            var user = new User
            {
                Email = email,
                PasswordHash = PasswordHasher.HashPassword(null!, password),
                CreatedAt = DateTime.UtcNow
            };

            DbContext.Users.Add(user);
            await DbContext.SaveChangesAsync();
            return user;
        }

        /// <summary>
        /// Creates multiple test users
        /// </summary>
        protected async Task<List<User>> CreateMultipleUsersAsync(int count)
        {
            var users = new List<User>();
            for (int i = 0; i < count; i++)
            {
                var user = await CreateTestUserAsync($"user{i}@example.com");
                users.Add(user);
            }
            return users;
        }

        /// <summary>
        /// Grants a subscription to a user
        /// </summary>
        protected async Task<Subscription> GrantSubscriptionAsync(int userId, FeatureType feature, int daysValid = 30)
        {
            var subscription = new Subscription
            {
                UserId = userId,
                Feature = feature,
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddDays(daysValid)
            };

            DbContext.Subscriptions.Add(subscription);
            await DbContext.SaveChangesAsync();
            return subscription;
        }

        /// <summary>
        /// Creates a test group with specified members
        /// </summary>
        protected async Task<Group> CreateTestGroupAsync(int creatorId, string name = "Test Group", List<int>? memberIds = null)
        {
            var group = new Group
            {
                Name = name,
                Description = "Test group description",
                CreatedById = creatorId,
                Members = new List<GroupMember>
                {
                    new GroupMember { UserId = creatorId, Role = GroupRole.Owner, JoinedAt = DateTime.UtcNow }
                }
            };

            if (memberIds != null)
            {
                foreach (var memberId in memberIds)
                {
                    group.Members.Add(new GroupMember
                    {
                        UserId = memberId,
                        Role = GroupRole.Member,
                        JoinedAt = DateTime.UtcNow
                    });
                }
            }

            DbContext.Groups.Add(group);
            await DbContext.SaveChangesAsync();
            return group;
        }

        /// <summary>
        /// Creates a test message
        /// </summary>
        protected async Task<Message> CreateTestMessageAsync(int senderId, int? receiverId = null, int? groupId = null, string content = "Test message")
        {
            var message = new Message
            {
                SenderId = senderId,
                ReceiverId = receiverId,
                GroupId = groupId,
                Content = content,
                SentAt = DateTime.UtcNow,
                IsRead = false
            };

            DbContext.Messages.Add(message);
            await DbContext.SaveChangesAsync();
            return message;
        }

        /// <summary>
        /// Verifies email was sent (mock)
        /// </summary>
        protected void VerifyEmailSent(Times? times = null)
        {
            MockEmailSender.Verify(
                x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
                times ?? Times.AtLeastOnce());
        }

        public virtual void Dispose()
        {
            DbContext?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
