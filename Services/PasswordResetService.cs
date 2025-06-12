using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using AuthService.Data;
using AuthService.Models;
using AuthService.Services.Interfaces;

namespace AuthService.Services
{
    public class PasswordResetService : IPasswordResetService
    {
        private readonly AuthDbContext _context;
        private readonly IPasswordService _passwordService;
        private readonly IEmailService _emailService;
        private readonly ILogger<PasswordResetService> _logger;
        private readonly IConfiguration _configuration;

        public PasswordResetService(
            AuthDbContext context,
            IPasswordService passwordService,
            IEmailService emailService,
            ILogger<PasswordResetService> logger,
            IConfiguration configuration)
        {
            _context = context;
            _passwordService = passwordService;
            _emailService = emailService;
            _logger = logger;
            _configuration = configuration;
        }

        public async Task<bool> SendPasswordResetEmailAsync(string email, string? ipAddress = null, string? userAgent = null)
        {
            try
            {
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email == email && u.IsActive);

                if (user == null)
                {
                    _logger.LogWarning("Password reset requested for non-existent or inactive email: {Email}", email);
                    return true; // Return true to prevent email enumeration
                }

                // Check for recent password reset requests (rate limiting)
                var recentTokens = await _context.PasswordResetTokens
                    .Where(t => t.UserId == user.Id && 
                               t.CreatedAt > DateTime.UtcNow.AddMinutes(-5) && 
                               !t.IsUsed)
                    .CountAsync();

                if (recentTokens > 0)
                {
                    _logger.LogWarning("Rate limit exceeded for password reset requests for user: {UserId}", user.Id);
                    return true; // Return true to prevent email enumeration
                }

                // Invalidate existing unused tokens for this user
                var existingTokens = await _context.PasswordResetTokens
                    .Where(t => t.UserId == user.Id && !t.IsUsed && t.ExpiresAt > DateTime.UtcNow)
                    .ToListAsync();

                foreach (var token in existingTokens)
                {
                    token.IsUsed = true;
                    token.UsedAt = DateTime.UtcNow;
                }

                // Generate new token
                var resetToken = GenerateSecureToken();
                var expiresAt = DateTime.UtcNow.AddHours(24); // Token expires in 24 hours

                var passwordResetToken = new PasswordResetToken
                {
                    UserId = user.Id,
                    Token = resetToken,
                    ExpiresAt = expiresAt,
                    CreatedAt = DateTime.UtcNow,
                    IpAddress = ipAddress,
                    UserAgent = userAgent
                };

                _context.PasswordResetTokens.Add(passwordResetToken);
                await _context.SaveChangesAsync();

                // Send password reset email
                await _emailService.SendPasswordResetEmailAsync(email, resetToken, user.Email ?? "");

                _logger.LogInformation("Password reset email sent successfully to: {Email}", email);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending password reset email to: {Email}", email);
                return false;
            }
        }

        public async Task<bool> ResetPasswordAsync(string token, string newPassword)
        {
            try
            {
                var resetToken = await _context.PasswordResetTokens
                    .Include(t => t.User)
                    .FirstOrDefaultAsync(t => t.Token == token && 
                                             !t.IsUsed && 
                                             t.ExpiresAt > DateTime.UtcNow);

                if (resetToken == null)
                {
                    _logger.LogWarning("Invalid or expired password reset token: {Token}", token);
                    return false;
                }

                // Hash the new password
                var hashedPassword = _passwordService.HashPassword(newPassword);

                // Update user password
                resetToken.User.PasswordHash = hashedPassword;

                // Mark token as used
                resetToken.IsUsed = true;
                resetToken.UsedAt = DateTime.UtcNow;

                // Invalidate all other password reset tokens for this user
                var otherTokens = await _context.PasswordResetTokens
                    .Where(t => t.UserId == resetToken.UserId && t.Id != resetToken.Id && !t.IsUsed)
                    .ToListAsync();

                foreach (var otherToken in otherTokens)
                {
                    otherToken.IsUsed = true;
                    otherToken.UsedAt = DateTime.UtcNow;
                }

                // Revoke all refresh tokens for security
                var refreshTokens = await _context.RefreshTokens
                    .Where(t => t.UserId == resetToken.UserId && !t.IsRevoked && t.ExpiresAt > DateTime.UtcNow)
                    .ToListAsync();

                foreach (var refreshToken in refreshTokens)
                {
                    refreshToken.IsRevoked = true;
                    refreshToken.RevokedAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("Password reset successful for user: {UserId}", resetToken.UserId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting password for token: {Token}", token);
                return false;
            }
        }

        public async Task<bool> ValidateResetTokenAsync(string token)
        {
            try
            {
                var resetToken = await _context.PasswordResetTokens
                    .FirstOrDefaultAsync(t => t.Token == token && 
                                             !t.IsUsed && 
                                             t.ExpiresAt > DateTime.UtcNow);

                return resetToken != null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating password reset token: {Token}", token);
                return false;
            }
        }

        public async Task CleanupExpiredTokensAsync()
        {
            try
            {
                var expiredTokens = await _context.PasswordResetTokens
                    .Where(t => t.ExpiresAt <= DateTime.UtcNow || t.IsUsed)
                    .Where(t => t.CreatedAt <= DateTime.UtcNow.AddDays(-7)) // Keep for 7 days for audit
                    .ToListAsync();

                if (expiredTokens.Any())
                {
                    _context.PasswordResetTokens.RemoveRange(expiredTokens);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Cleaned up {Count} expired password reset tokens", expiredTokens.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up expired password reset tokens");
            }
        }

        private string GenerateSecureToken()
        {
            using var rng = RandomNumberGenerator.Create();
            var bytes = new byte[32];
            rng.GetBytes(bytes);
            return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").Replace("=", "");
        }
    }
}