using System;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using Core.Entities;
using Infrastructure.Data;
using Infrastructure.Email;
using Microsoft.EntityFrameworkCore;
using Xunit;
using FluentAssertions;

namespace Tests.Workers
{
    public class EmailAlertWorkerTests
    {
        [Fact]
        public async Task ShouldDetectUnreadMessagesOlderThan30Minutes()
        {
            var options = new DbContextOptionsBuilder<MessagingDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            using var db = new MessagingDbContext(options);
            db.Messages.Add(new Message { Id = 1, Content = "Hey", SentAt = DateTime.UtcNow.AddMinutes(-45), IsRead = false });
            await db.SaveChangesAsync();

            var worker = new EmailAlertWorker(db, new ConsoleEmailSender());
            await worker.CheckAndNotifyAsync();

            var msg = await db.Messages.FirstAsync();
            msg.LastNotifiedAt.Should().NotBeNull();
        }
    }
}
