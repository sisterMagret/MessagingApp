using System;
using System.Threading.Tasks;
using Core.Entities;
using Core.Enums;
using Infrastructure.Data;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;
using FluentAssertions;

namespace Tests.Services
{
    public class SubscriptionServiceTests
    {
        private readonly MessagingDbContext _db;
        private readonly SubscriptionService _service;

        public SubscriptionServiceTests()
        {
            var options = new DbContextOptionsBuilder<MessagingDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _db = new MessagingDbContext(options);
            var loggerMock = new Mock<ILogger<SubscriptionService>>();
            _service = new SubscriptionService(_db, loggerMock.Object);
        }
        [Fact]
        public async Task CreateSubscription_ShouldAddNewSubscription()
        {
            var user = new User { Id = 1, Email = "sistermagret@gmail.com" };
            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            await _service.GrantAsync(user.Id, FeatureType.FileSharing, TimeSpan.FromDays(7));

            var subscription = await _db.Subscriptions.FirstOrDefaultAsync();
            subscription.Should().NotBeNull();
            subscription!.Feature.Should().Be(FeatureType.FileSharing);
            subscription.IsActive.Should().BeTrue();
        }

        [Fact]
        public async Task HasActiveFeature_ShouldReturnFalse_WhenExpired()
        {
            // Arrange
            var user = new User { Id = 2, Email = "ExpiredUser@gmail.com", PasswordHash = "password" };
            _db.Users.Add(user);

            _db.Subscriptions.Add(new Subscription
            {
                UserId = user.Id,
                Feature = FeatureType.FileSharing,
                StartDate = DateTime.UtcNow.AddDays(-10),
                EndDate = DateTime.UtcNow.AddDays(-1)
            });

            await _db.SaveChangesAsync();

            // Act
            var result = await _service.HasActiveFeatureAsync(user.Id, FeatureType.FileSharing);

            // Assert
            result.Should().BeFalse();
        }
    }
}