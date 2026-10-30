using System.Security.Cryptography;
using System.Text;
using LiveSwitch.API.Models;
using Microsoft.EntityFrameworkCore;

namespace LiveSwitch.API.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Call> Calls => Set<Call>();
    public DbSet<CallParticipant> CallParticipants => Set<CallParticipant>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);
            entity.HasIndex(u => u.Username).IsUnique();
            entity.HasIndex(u => u.Email).IsUnique();
        });

        modelBuilder.Entity<Call>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.HasIndex(c => c.ChannelId).IsUnique();
            entity.HasOne(c => c.InitiatorUser)
                  .WithMany()
                  .HasForeignKey(c => c.InitiatorUserId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CallParticipant>(entity =>
        {
            entity.HasKey(cp => cp.Id);
            entity.HasOne(cp => cp.Call)
                  .WithMany(c => c.Participants)
                  .HasForeignKey(cp => cp.CallId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(cp => cp.User)
                  .WithMany(u => u.CallParticipants)
                  .HasForeignKey(cp => cp.UserId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Seed initial users
        var aliceId = "user-alice-0001";
        var bobId = "user-bob-0002";
        var charlieId = "user-charlie-0003";

        modelBuilder.Entity<User>().HasData(
            new User
            {
                Id = aliceId,
                Username = "alice",
                Email = "alice@example.com",
                DisplayName = "Alice Johnson",
                AvatarUrl = "https://images.unsplash.com/photo-1494790108377-be9c29b29330?w=150",
                PasswordHash = HashPassword("Password123!"),
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new User
            {
                Id = bobId,
                Username = "bob",
                Email = "bob@example.com",
                DisplayName = "Bob Smith",
                AvatarUrl = "https://images.unsplash.com/photo-1535713875002-d1d0cf377fde?w=150",
                PasswordHash = HashPassword("Password123!"),
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new User
            {
                Id = charlieId,
                Username = "charlie",
                Email = "charlie@example.com",
                DisplayName = "Charlie Davis",
                AvatarUrl = "https://images.unsplash.com/photo-1570295999919-56ceb5ecca61?w=150",
                PasswordHash = HashPassword("Password123!"),
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            }
        );
    }

    public static string HashPassword(string password)
    {
        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(password + "LiveSwitchSalt_2026");
        return Convert.ToBase64String(sha.ComputeHash(bytes));
    }

    public static bool VerifyPassword(string password, string hash)
    {
        return HashPassword(password) == hash;
    }
}
