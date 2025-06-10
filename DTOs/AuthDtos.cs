using System.ComponentModel.DataAnnotations;
using AuthService.Models.Enums;

namespace AuthService.DTOs
{
    // User Information DTO
    public class UserInfo
    {
        public int Id { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public bool IsEmailVerified { get; set; }
        public bool IsPhoneVerified { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public bool IsActive { get; set; }
    }

    // Request DTOs
    public class RegisterEmailRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MinLength(6)]
        public string Password { get; set; } = string.Empty;
    }

    public class RegisterPhoneRequest
    {
        [Required]
        [Phone]
        public string PhoneNumber { get; set; } = string.Empty;
    }

    public class LoginEmailRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;
    }

    public class LoginPhoneRequest
    {
        [Required]
        [Phone]
        public string PhoneNumber { get; set; } = string.Empty;
    }

    public class VerifyOtpRequest
    {
        [Required]
        [Phone]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required]
        [StringLength(6, MinimumLength = 6)]
        public string OtpCode { get; set; } = string.Empty;
    }

    public class SendOtpRequest
    {
        [Required]
        [Phone]
        public string PhoneNumber { get; set; } = string.Empty;
    }

    public class RefreshTokenRequest
    {
        [Required]
        public string RefreshToken { get; set; } = string.Empty;
    }

    public class RevokeTokenRequest
    {
        [Required]
        public string RefreshToken { get; set; } = string.Empty;
    }

    public class VerifyEmailRequest
    {
        [Required]
        public string Token { get; set; } = string.Empty;
    }

    public class ResendVerificationRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
    }

    public class ForgotPasswordRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
    }

    public class ResetPasswordRequest
    {
        [Required]
        public string Token { get; set; } = string.Empty;

        [Required]
        [MinLength(6)]
        public string NewPassword { get; set; } = string.Empty;
    }

    public class ValidateResetTokenRequest
    {
        [Required]
        public string Token { get; set; } = string.Empty;
    }

    // Response DTOs
    public class TokenResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public DateTime AccessTokenExpiresAt { get; set; }
        public DateTime RefreshTokenExpiresAt { get; set; }
        public string TokenType { get; set; } = "Bearer";
    }

    public class AuthResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public TokenResponse? Tokens { get; set; }
        public UserResponse? User { get; set; }
    }

    public class UserResponse
    {
        public int Id { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public bool IsEmailVerified { get; set; }
        public bool IsPhoneVerified { get; set; }
        public UserRole? CurrentRole { get; set; }
        public string? CurrentRoleDisplayName { get; set; }
    }

    public class ApiResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class OtpResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public DateTime? ExpiresAt { get; set; }
    }

    /// <summary>
    /// Result of OTP sending operation (service layer)
    /// </summary>
    public class OtpResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public DateTime? ExpiresAt { get; set; }
    }

    // Legacy response for backwards compatibility
    public class LegacyAuthResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? Token { get; set; }
        public UserResponse? User { get; set; }
    }

    // Role Management DTOs
    public class AssignRoleRequest
    {
        [Required]
        public int UserId { get; set; }

        [Required]
        public UserRole Role { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }
    }

    public class RevokeRoleRequest
    {
        [Required]
        public int UserId { get; set; }

        [Required]
        public UserRole Role { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }
    }

    public class RoleAssignmentResponse
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public UserRole Role { get; set; }
        public string RoleDisplayName { get; set; } = string.Empty;
        public DateTime AssignedAt { get; set; }
        public int? AssignedByUserId { get; set; }
        public string? AssignedByEmail { get; set; }
        public DateTime? RevokedAt { get; set; }
        public int? RevokedByUserId { get; set; }
        public string? RevokedByEmail { get; set; }
        public bool IsActive { get; set; }
        public string? Notes { get; set; }
    }

    public class UserWithRoleResponse
    {
        public int Id { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public bool IsEmailVerified { get; set; }
        public bool IsPhoneVerified { get; set; }
        public UserRole? CurrentRole { get; set; }
        public string? CurrentRoleDisplayName { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public bool IsActive { get; set; }
    }

    public class RoleStatisticsResponse
    {
        public Dictionary<string, int> RoleCounts { get; set; } = new();
        public int TotalActiveUsers { get; set; }
        public int UsersWithoutRole { get; set; }
    }
}