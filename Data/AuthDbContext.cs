using Microsoft.EntityFrameworkCore;
using AuthService.Models;

namespace AuthService.Data
{
    public class AuthDbContext : DbContext
    {
        public AuthDbContext(DbContextOptions<AuthDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; } = null!;
        public DbSet<OtpToken> OtpTokens { get; set; } = null!;
        public DbSet<RefreshToken> RefreshTokens { get; set; } = null!;
        public DbSet<EmailVerificationToken> EmailVerificationTokens { get; set; } = null!;
        public DbSet<UserRoleAssignment> UserRoleAssignments { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // User entity configuration
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasIndex(e => e.Email)
                    .IsUnique()
                    .HasFilter("\"Email\" IS NOT NULL");

                entity.HasIndex(e => e.PhoneNumber)
                    .IsUnique()
                    .HasFilter("\"PhoneNumber\" IS NOT NULL");

                entity.Property(e => e.Email)
                    .HasMaxLength(255);

                entity.Property(e => e.PhoneNumber)
                    .HasMaxLength(20);

                entity.Property(e => e.PasswordHash)
                    .HasMaxLength(255);

                entity.Property(e => e.KeycloakId)
                    .HasMaxLength(255);

                entity.HasIndex(e => e.KeycloakId)
                    .IsUnique()
                    .HasFilter("\"KeycloakId\" IS NOT NULL");
            });

            // OtpToken entity configuration
            modelBuilder.Entity<OtpToken>(entity =>
            {
                entity.HasIndex(e => new { e.PhoneNumber, e.Token })
                    .HasDatabaseName("IX_OtpTokens_PhoneNumber_Token");

                entity.HasIndex(e => e.ExpiresAt)
                    .HasDatabaseName("IX_OtpTokens_ExpiresAt");

                entity.Property(e => e.Token)
                    .HasMaxLength(6)
                    .IsRequired();

                entity.Property(e => e.PhoneNumber)
                    .HasMaxLength(20)
                    .IsRequired();

                entity.HasOne(e => e.User)
                    .WithMany(u => u.OtpTokens)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // RefreshToken entity configuration
            modelBuilder.Entity<RefreshToken>(entity =>
            {
                entity.HasIndex(e => e.Token)
                    .IsUnique()
                    .HasDatabaseName("IX_RefreshTokens_Token");

                entity.HasIndex(e => e.UserId)
                    .HasDatabaseName("IX_RefreshTokens_UserId");

                entity.HasIndex(e => e.ExpiresAt)
                    .HasDatabaseName("IX_RefreshTokens_ExpiresAt");

                entity.HasIndex(e => new { e.UserId, e.IsRevoked, e.ExpiresAt })
                    .HasDatabaseName("IX_RefreshTokens_UserId_IsRevoked_ExpiresAt");

                entity.Property(e => e.Token)
                    .HasMaxLength(512)
                    .IsRequired();

                entity.Property(e => e.ReplacedByToken)
                    .HasMaxLength(512);

                entity.Property(e => e.DeviceInfo)
                    .HasMaxLength(255);

                entity.Property(e => e.IpAddress)
                    .HasMaxLength(45);

                entity.HasOne(e => e.User)
                    .WithMany(u => u.RefreshTokens)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // EmailVerificationToken entity configuration
            modelBuilder.Entity<EmailVerificationToken>(entity =>
            {
                entity.HasIndex(e => e.Token)
                    .IsUnique()
                    .HasDatabaseName("IX_EmailVerificationTokens_Token");

                entity.HasIndex(e => e.UserId)
                    .HasDatabaseName("IX_EmailVerificationTokens_UserId");

                entity.HasIndex(e => e.ExpiresAt)
                    .HasDatabaseName("IX_EmailVerificationTokens_ExpiresAt");

                entity.HasIndex(e => new { e.Email, e.IsUsed })
                    .HasDatabaseName("IX_EmailVerificationTokens_Email_IsUsed");

                entity.Property(e => e.Token)
                    .HasMaxLength(100)
                    .IsRequired();

                entity.Property(e => e.Email)
                    .HasMaxLength(255)
                    .IsRequired();

                entity.HasOne(e => e.User)
                    .WithMany(u => u.EmailVerificationTokens)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // UserRoleAssignment entity configuration
            modelBuilder.Entity<UserRoleAssignment>(entity =>
            {
                entity.HasIndex(e => new { e.UserId, e.Role, e.RevokedAt })
                    .HasDatabaseName("IX_UserRoleAssignments_UserId_Role_RevokedAt");

                entity.HasIndex(e => e.AssignedAt)
                    .HasDatabaseName("IX_UserRoleAssignments_AssignedAt");

                entity.Property(e => e.Role)
                    .HasConversion<int>();

                entity.Property(e => e.Notes)
                    .HasMaxLength(500);

                entity.HasOne(e => e.User)
                    .WithMany(u => u.RoleAssignments)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.AssignedByUser)
                    .WithMany()
                    .HasForeignKey(e => e.AssignedByUserId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(e => e.RevokedByUser)
                    .WithMany()
                    .HasForeignKey(e => e.RevokedByUserId)
                    .OnDelete(DeleteBehavior.SetNull);
            });
        }
    }
}