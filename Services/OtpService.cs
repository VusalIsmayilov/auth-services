using AuthService.Data;
using AuthService.DTOs;
using AuthService.Models;
using AuthService.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Services
{
    public class OtpService : IOtpService
    {
        private readonly AuthDbContext _context;
        private readonly ILogger<OtpService> _logger;
        private readonly int _otpExpiryMinutes = 5;
        private readonly int _maxAttempts = 3;

        public OtpService(AuthDbContext context, ILogger<OtpService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<OtpResponse> GenerateOtpAsync(string phoneNumber)
        {
            try
            {
                // Clean up old OTPs for this phone number
                await CleanupOldOtpsAsync(phoneNumber);

                // Check rate limiting (max 3 OTPs per hour)
                var recentOtps = await _context.OtpTokens
                    .Where(o => o.PhoneNumber == phoneNumber &&
                               o.CreatedAt > DateTime.UtcNow.AddHours(-1))
                    .CountAsync();

                if (recentOtps >= 3)
                {
                    return new OtpResponse
                    {
                        Success = false,
                        Message = "Too many OTP requests. Please try again later."
                    };
                }

                // Generate 6-digit OTP
                var otpCode = GenerateOtpCode();
                var expiresAt = DateTime.UtcNow.AddMinutes(_otpExpiryMinutes);

                // Find or create user
                var user = await _context.Users.FirstOrDefaultAsync(u => u.PhoneNumber == phoneNumber);
                if (user == null)
                {
                    user = new User
                    {
                        PhoneNumber = phoneNumber,
                        IsPhoneVerified = false
                    };
                    _context.Users.Add(user);
                    await _context.SaveChangesAsync();
                }

                // Save OTP to database
                var otpToken = new OtpToken
                {
                    UserId = user.Id,
                    Token = otpCode,
                    PhoneNumber = phoneNumber,
                    ExpiresAt = expiresAt
                };

                _context.OtpTokens.Add(otpToken);
                await _context.SaveChangesAsync();

                // Send OTP (simulated)
                await SendOtpAsync(phoneNumber, otpCode);

                return new OtpResponse
                {
                    Success = true,
                    Message = "OTP sent successfully",
                    ExpiresAt = expiresAt
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating OTP for phone number: {PhoneNumber}", phoneNumber);
                return new OtpResponse
                {
                    Success = false,
                    Message = "Failed to generate OTP. Please try again."
                };
            }
        }

        public async Task<bool> ValidateOtpAsync(string phoneNumber, string otpCode)
        {
            try
            {
                var otpToken = await _context.OtpTokens
                    .Where(o => o.PhoneNumber == phoneNumber &&
                               o.Token == otpCode &&
                               !o.IsUsed &&
                               o.ExpiresAt > DateTime.UtcNow)
                    .FirstOrDefaultAsync();

                if (otpToken == null)
                {
                    // Increment attempt count for existing tokens
                    var existingTokens = await _context.OtpTokens
                        .Where(o => o.PhoneNumber == phoneNumber && !o.IsUsed)
                        .ToListAsync();

                    foreach (var token in existingTokens)
                    {
                        token.AttemptCount++;
                        if (token.AttemptCount >= _maxAttempts)
                        {
                            token.IsUsed = true;
                            token.UsedAt = DateTime.UtcNow;
                        }
                    }

                    await _context.SaveChangesAsync();
                    return false;
                }

                // Mark OTP as used
                otpToken.IsUsed = true;
                otpToken.UsedAt = DateTime.UtcNow;

                // Mark user phone as verified
                var user = await _context.Users.FindAsync(otpToken.UserId);
                if (user != null)
                {
                    user.IsPhoneVerified = true;
                    user.LastLoginAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating OTP for phone number: {PhoneNumber}", phoneNumber);
                return false;
            }
        }

        public async Task<bool> SendOtpAsync(string phoneNumber, string otpCode)
        {
            // Simulate SMS sending
            // In real implementation, integrate with SMS provider like Twilio, AWS SNS, etc.
            _logger.LogInformation("Sending OTP {OtpCode} to phone number: {PhoneNumber}", otpCode, phoneNumber);

            // Simulate network delay
            await Task.Delay(100);

            return true;
        }

        public async Task CleanupExpiredOtpsAsync()
        {
            try
            {
                var expiredOtps = await _context.OtpTokens
                    .Where(o => o.ExpiresAt < DateTime.UtcNow || o.AttemptCount >= _maxAttempts)
                    .ToListAsync();

                _context.OtpTokens.RemoveRange(expiredOtps);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Cleaned up {Count} expired OTP tokens", expiredOtps.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up expired OTPs");
            }
        }

        private async Task CleanupOldOtpsAsync(string phoneNumber)
        {
            var oldOtps = await _context.OtpTokens
                .Where(o => o.PhoneNumber == phoneNumber &&
                           (o.ExpiresAt < DateTime.UtcNow || o.IsUsed))
                .ToListAsync();

            _context.OtpTokens.RemoveRange(oldOtps);
            await _context.SaveChangesAsync();
        }

        private string GenerateOtpCode()
        {
            var random = new Random();
            return random.Next(100000, 999999).ToString();
        }
    }
}