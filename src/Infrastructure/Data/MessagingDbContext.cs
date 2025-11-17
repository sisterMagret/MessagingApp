using Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data
{
    public class MessagingDbContext : DbContext
    {
        public MessagingDbContext(DbContextOptions<MessagingDbContext> options)
            : base(options) { }

        public DbSet<User> Users => Set<User>();
        public DbSet<Message> Messages => Set<Message>();
        public DbSet<Subscription> Subscriptions => Set<Subscription>();
        public DbSet<Group> Groups => Set<Group>();
        public DbSet<GroupMember> GroupMembers => Set<GroupMember>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // User configuration
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasIndex(u => u.Email).IsUnique();
                entity.Property(u => u.Email).IsRequired().HasMaxLength(255);
                entity.Property(u => u.PasswordHash).IsRequired().HasMaxLength(255);
            });

            // Message relationships
            modelBuilder.Entity<Message>()
                .HasOne(m => m.Sender)
                .WithMany(u => u.Messages)
                .HasForeignKey(m => m.SenderId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Message>()
                .HasOne(m => m.Receiver)
                .WithMany()
                .HasForeignKey(m => m.ReceiverId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Message>()
                .HasOne(m => m.Group)
                .WithMany(g => g.Messages)
                .HasForeignKey(m => m.GroupId)
                .OnDelete(DeleteBehavior.Cascade);

            // Group relationships
            modelBuilder.Entity<Group>()
                .HasOne(g => g.CreatedBy)
                .WithMany()
                .HasForeignKey(g => g.CreatedById)
                .OnDelete(DeleteBehavior.Restrict);

            // GroupMember unique constraint
            modelBuilder.Entity<GroupMember>()
                .HasIndex(gm => new { gm.GroupId, gm.UserId })
                .IsUnique();

            // Subscription configuration
            modelBuilder.Entity<Subscription>()
                .HasIndex(s => new { s.UserId, s.Feature })
                .IsUnique();
        }
    }
}