using AuthService.Data;
using AuthService.Models;
using AuthService.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace AuthService.Services;

public class EmailVerificationService : IEmailVerificationService
{
    private readonly AuthDbContext _context;
    private readonly IEmailService _emailService;
    private readonly ILogger<EmailVerificationService> _logger;

    public EmailVerificationService(
        AuthDbContext context,
        IEmailService emailService,
        ILogger<EmailVerificationService> logger)
    {
        _context = context;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<string> GenerateVerificationTokenAsync(int userId, string email)
    {
        try
        {
            // Invalidate any existing verification tokens for this user
            var existingTokens = await _context.EmailVerificationTokens
                .Where(t => t.UserId == userId && !t.IsUsed)
                .ToListAsync();

            foreach (var token in existingTokens)
            {
                token.IsUsed = true;
                token.UsedAt = DateTime.UtcNow;
            }

            // Generate new verification token
            var verificationToken = GenerateSecureToken();
            var emailVerificationToken = new EmailVerificationToken
            {
                UserId = userId,
                Token = verificationToken,
                Email = email,
                ExpiresAt = DateTime.UtcNow.AddHours(24), // 24 hours expiry
                CreatedAt = DateTime.UtcNow
            };

            _context.EmailVerificationTokens.Add(emailVerificationToken);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Email verification token generated for user {UserId} with email {Email}", userId, email);
            return verificationToken;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate verification token for user {UserId} with email {Email}", userId, email);
            throw;
        }
    }

    public async Task<bool> VerifyEmailAsync(string token)
    {
        try
        {
            var verificationToken = await _context.EmailVerificationTokens
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.Token == token);

            if (verificationToken == null)
            {
                _logger.LogWarning("Email verification attempted with invalid token: {Token}", token);
                return false;
            }

            if (!verificationToken.IsValid)
            {
                _logger.LogWarning("Email verification attempted with expired or used token: {Token}", token);
                return false;
            }

            // Mark token as used
            verificationToken.IsUsed = true;
            verificationToken.UsedAt = DateTime.UtcNow;

            // Mark user's email as verified
            verificationToken.User.IsEmailVerified = true;

            await _context.SaveChangesAsync();

            // Send welcome email
            await _emailService.SendWelcomeEmailAsync(verificationToken.Email, verificationToken.User.Email ?? "User");

            _logger.LogInformation("Email verified successfully for user {UserId} with email {Email}", 
                verificationToken.UserId, verificationToken.Email);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to verify email with token: {Token}", token);
            return false;
        }
    }

    public async Task<bool> ResendVerificationEmailAsync(string email)
    {
        try
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == email && !u.IsEmailVerified);

            if (user == null)
            {
                _logger.LogWarning("Verification email resend attempted for non-existent or already verified email: {Email}", email);
                return false;
            }

            // Check if there's a recent verification email sent (within last 5 minutes)
            var recentToken = await _context.EmailVerificationTokens
                .Where(t => t.UserId == user.Id && !t.IsUsed && t.CreatedAt > DateTime.UtcNow.AddMinutes(-5))
                .OrderByDescending(t => t.CreatedAt)
                .FirstOrDefaultAsync();

            if (recentToken != null)
            {
                _logger.LogWarning("Verification email resend attempt too soon for email: {Email}", email);
                return false;
            }

            // Generate new verification token
            var token = await GenerateVerificationTokenAsync(user.Id, email);

            // Send verification email
            var emailSent = await _emailService.SendVerificationEmailAsync(email, token, user.Email ?? "User");

            if (emailSent)
            {
                _logger.LogInformation("Verification email resent successfully to {Email}", email);
                return true;
            }

            _logger.LogWarning("Failed to send verification email to {Email}", email);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resend verification email to {Email}", email);
            return false;
        }
    }

    public async Task CleanupExpiredTokensAsync()
    {
        try
        {
            var expiredTokens = await _context.EmailVerificationTokens
                .Where(t => t.ExpiresAt < DateTime.UtcNow || t.CreatedAt < DateTime.UtcNow.AddDays(-30))
                .ToListAsync();

            if (expiredTokens.Any())
            {
                _context.EmailVerificationTokens.RemoveRange(expiredTokens);
                var deletedCount = await _context.SaveChangesAsync();
                
                _logger.LogInformation("Cleaned up {Count} expired email verification tokens", deletedCount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cleanup expired email verification tokens");
        }
    }

    private static string GenerateSecureToken()
    {
        using var rng = RandomNumberGenerator.Create();
        var tokenBytes = new byte[32];
        rng.GetBytes(tokenBytes);
        return Convert.ToBase64String(tokenBytes).Replace("+", "-").Replace("/", "_").Replace("=", "");
    }
}