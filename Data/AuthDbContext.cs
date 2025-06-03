using AuthService.Models;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Data
{
    public class AuthDbContext : DbContext
    {
        public AuthDbContext(DbContextOptions<AuthDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<OtpToken> OtpTokens { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // User configuration
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Email).IsUnique();
                entity.HasIndex(e => e.PhoneNumber).IsUnique();
                entity.Property(e => e.Email).HasMaxLength(255);
                entity.Property(e => e.PhoneNumber).HasMaxLength(20);
                entity.Property(e => e.PasswordHash).HasMaxLength(255);
            });

            // OtpToken configuration
            modelBuilder.Entity<OtpToken>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.PhoneNumber, e.Token });
                entity.Property(e => e.Token).HasMaxLength(6);
                entity.Property(e => e.PhoneNumber).HasMaxLength(20);

                entity.HasOne(e => e.User)
                    .WithMany(e => e.OtpTokens)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}