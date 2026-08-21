using FeedMe.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FeedMe.Data
{
    public class ApplicationDbContext : IdentityDbContext<AppUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Category> Categories { get; set; }
        public DbSet<MenuItem> MenuItems { get; set; }
        public DbSet<TimeSlot> TimeSlots { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<TokenTransaction> TokenTransactions { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // 1. Foreign Key Relationships
            builder.Entity<Order>()
                .HasOne(o => o.Student)
                .WithMany()
                .HasForeignKey(o => o.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<OrderItem>()
                .HasOne(oi => oi.Order)
                .WithMany(o => o.OrderItems)
                .HasForeignKey(oi => oi.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<OrderItem>()
                .HasOne(oi => oi.MenuItem)
                .WithMany()
                .HasForeignKey(oi => oi.MenuItemId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Notification>()
                .HasOne(n => n.User)
                .WithMany()
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // 2. Indexes
            builder.Entity<AppUser>()
                .HasIndex(u => u.StudentId)
                .IsUnique()
                .HasFilter("[StudentId] IS NOT NULL");

            builder.Entity<AppUser>()
                .HasIndex(u => u.StaffId)
                .IsUnique()
                .HasFilter("[StaffId] IS NOT NULL");

            builder.Entity<Category>()
                .HasIndex(c => c.Name)
                .IsUnique();

            builder.Entity<TimeSlot>()
                .HasIndex(t => t.StartTime);

            // 3. Property Lengths & Requirements
            builder.Entity<AppUser>(entity =>
            {
                entity.Property(e => e.FullName).HasMaxLength(100);
                entity.Property(e => e.StudentId).HasMaxLength(20);
                entity.Property(e => e.StaffId).HasMaxLength(20);
                entity.Property(e => e.ContactNumber).HasMaxLength(15);
                entity.Property(e => e.DietaryPreference).HasMaxLength(50);
            });

            builder.Entity<Category>(entity =>
            {
                entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            });

            builder.Entity<MenuItem>(entity =>
            {
                entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
                entity.Property(e => e.Description).HasMaxLength(500);
                entity.Property(e => e.ImageUrl).HasMaxLength(255);
            });

            builder.Entity<TimeSlot>(entity =>
            {
                entity.Property(e => e.StartTime).IsRequired();
                entity.Property(e => e.EndTime).IsRequired();
            });

            builder.Entity<Order>(entity =>
            {
                entity.Property(e => e.StaffNotes).HasMaxLength(500);
            });

            builder.Entity<Notification>(entity =>
            {
                entity.Property(e => e.Message).HasMaxLength(1000);
                entity.Property(e => e.Type).HasMaxLength(50);
            });

            builder.Entity<TokenTransaction>(entity =>
            {
                entity.Property(e => e.SenderName).HasMaxLength(100);
                entity.Property(e => e.ReceiverName).HasMaxLength(100);
                entity.Property(e => e.Description).HasMaxLength(500);
            });

            // 4. Decimal Precisions (Prevents EF Core Truncation Warnings)
            builder.Entity<MenuItem>()
                .Property(m => m.Price)
                .HasPrecision(18, 2);

            builder.Entity<Order>()
                .Property(o => o.TotalAmount)
                .HasPrecision(18, 2);

            builder.Entity<OrderItem>()
                .Property(oi => oi.UnitPrice)
                .HasPrecision(18, 2);

            builder.Entity<AppUser>()
                .Property(u => u.TokenBalance)
                .HasPrecision(18, 2);

            builder.Entity<TokenTransaction>()
                .Property(t => t.Amount)
                .HasPrecision(18, 2);
        }
    }
}