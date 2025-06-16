using System.ComponentModel.DataAnnotations;
using AuthService.Models.Enums;

namespace AuthService.Models
{
    public class User
    {
        [Key]
        public int Id { get; set; }

        [StringLength(100)]
        public string? FirstName { get; set; }

        [StringLength(100)]
        public string? LastName { get; set; }

        [EmailAddress]
        [StringLength(255)]
        public string? Email { get; set; }

        [Phone]
        [StringLength(20)]
        public string? PhoneNumber { get; set; }

        [StringLength(255)]
        public string? PasswordHash { get; set; }

        public bool IsEmailVerified { get; set; } = false;
        public bool IsPhoneVerified { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastLoginAt { get; set; }

        public bool IsActive { get; set; } = true;

        // Keycloak integration
        [StringLength(255)]
        public string? KeycloakId { get; set; }

        // Navigation properties
        public virtual ICollection<OtpToken> OtpTokens { get; set; } = new List<OtpToken>();
        public virtual ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
        public virtual ICollection<EmailVerificationToken> EmailVerificationTokens { get; set; } = new List<EmailVerificationToken>();
        public virtual ICollection<PasswordResetToken> PasswordResetTokens { get; set; } = new List<PasswordResetToken>();
        public virtual ICollection<UserRoleAssignment> RoleAssignments { get; set; } = new List<UserRoleAssignment>();

        // Helper methods for roles
        public UserRole? GetCurrentRole()
        {
            return RoleAssignments
                .Where(r => r.IsActive)
                .OrderByDescending(r => r.AssignedAt)
                .FirstOrDefault()?.Role;
        }

        public bool HasRole(UserRole role)
        {
            return RoleAssignments.Any(r => r.IsActive && r.Role == role);
        }

        public bool HasPermission(string permission)
        {
            var currentRole = GetCurrentRole();
            return currentRole?.HasPermission(permission) ?? false;
        }
    }
}