using Microsoft.EntityFrameworkCore;
using AuthService.Data;
using AuthService.Models;
using AuthService.Services.Interfaces;
using AuthService.DTOs;

namespace AuthService.Services
{
    public class OtpService : IOtpService
    {
        private readonly AuthDbContext _context;
        private readonly ILogger<OtpService> _logger;
        private readonly TimeSpan _otpExpiryTime = TimeSpan.FromMinutes(5);
        private readonly int _maxOtpAttempts = 3;
        private readonly TimeSpan _rateLimitWindow = TimeSpan.FromHours(1);

        public OtpService(AuthDbContext context, ILogger<OtpService> logger)
        {
            _context = context;
            _logger = logger;
        }


        public async Task CleanupExpiredOtpsAsync()
        {
            // Implement the logic to clean up expired OTPs
            // For example:
            var expiredOtps = await _context.OtpTokens
                .Where(t => t.ExpiresAt < DateTime.UtcNow)
                .ToListAsync();

            _context.OtpTokens.RemoveRange(expiredOtps);
            await _context.SaveChangesAsync();
        }
        public async Task<OtpResult> SendOtpAsync(string phoneNumber)
        {
            try
            {
                // Check rate limiting
                if (!await CanSendOtpAsync(phoneNumber))
                {
                    _logger.LogWarning("OTP rate limit exceeded for phone: {PhoneNumber}", phoneNumber);
                    return new OtpResult
                    {
                        Success = false,
                        Message = "Too many OTP requests. Please try again later."
                    };
                }

                // Find user by phone number
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.PhoneNumber == phoneNumber);

                if (user == null)
                {
                    _logger.LogWarning("OTP request for non-existent phone: {PhoneNumber}", phoneNumber);
                    return new OtpResult
                    {
                        Success = false,
                        Message = "Phone number not found."
                    };
                }

                // Invalidate any existing OTP tokens for this phone
                var existingTokens = await _context.OtpTokens
                    .Where(t => t.PhoneNumber == phoneNumber && !t.IsUsed)
                    .ToListAsync();

                foreach (var token in existingTokens)
                {
                    token.IsUsed = true;
                    token.UsedAt = DateTime.UtcNow;
                }

                // Generate new OTP
                var otpCode = GenerateOtpCode();
                var expiresAt = DateTime.UtcNow.Add(_otpExpiryTime);

                var otpToken = new OtpToken
                {
                    UserId = user.Id,
                    Token = otpCode,
                    PhoneNumber = phoneNumber,
                    ExpiresAt = expiresAt,
                    CreatedAt = DateTime.UtcNow,
                    IsUsed = false,
                    AttemptCount = 0
                };

                _context.OtpTokens.Add(otpToken);
                await _context.SaveChangesAsync();

                // Send OTP (simulate sending - replace with real SMS service)
                var sent = await SendOtpSmsAsync(phoneNumber, otpCode);
                if (!sent)
                {
                    // Mark as used if sending failed
                    otpToken.IsUsed = true;
                    otpToken.UsedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    return new OtpResult
                    {
                        Success = false,
                        Message = "Failed to send OTP. Please try again."
                    };
                }

                _logger.LogInformation("OTP sent successfully to phone: {PhoneNumber}", phoneNumber);
                return new OtpResult
                {
                    Success = true,
                    Message = "OTP sent successfully",
                    ExpiresAt = expiresAt
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending OTP to phone: {PhoneNumber}", phoneNumber);
                return new OtpResult
                {
                    Success = false,
                    Message = "An error occurred while sending OTP."
                };
            }
        }

        public async Task<bool> VerifyOtpAsync(string phoneNumber, string otpCode)
        {
            try
            {
                var otpToken = await _context.OtpTokens
                    .Where(t => t.PhoneNumber == phoneNumber &&
                               t.Token == otpCode &&
                               !t.IsUsed &&
                               t.ExpiresAt > DateTime.UtcNow)
                    .OrderByDescending(t => t.CreatedAt)
                    .FirstOrDefaultAsync();

                if (otpToken == null)
                {
                    _logger.LogWarning("Invalid or expired OTP for phone: {PhoneNumber}", phoneNumber);

                    // Increment attempt count for any active tokens
                    await IncrementAttemptCountAsync(phoneNumber, otpCode);
                    return false;
                }

                // Check attempt count
                if (otpToken.AttemptCount >= _maxOtpAttempts)
                {
                    _logger.LogWarning("Max OTP attempts exceeded for phone: {PhoneNumber}", phoneNumber);
                    otpToken.IsUsed = true;
                    otpToken.UsedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                    return false;
                }

                // Mark OTP as used
                otpToken.IsUsed = true;
                otpToken.UsedAt = DateTime.UtcNow;
                otpToken.AttemptCount++;

                await _context.SaveChangesAsync();

                _logger.LogInformation("OTP verified successfully for phone: {PhoneNumber}", phoneNumber);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying OTP for phone: {PhoneNumber}", phoneNumber);
                return false;
            }
        }

        public async Task<bool> CanSendOtpAsync(string phoneNumber)
        {
            try
            {
                var cutoffTime = DateTime.UtcNow.Subtract(_rateLimitWindow);
                var recentOtpCount = await _context.OtpTokens
                    .CountAsync(t => t.PhoneNumber == phoneNumber &&
                                    t.CreatedAt > cutoffTime);

                return recentOtpCount < _maxOtpAttempts;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking OTP rate limit for phone: {PhoneNumber}", phoneNumber);
                return false; // Fail safe - don't allow sending if there's an error
            }
        }

        public async Task<int> GetRemainingAttemptsAsync(string phoneNumber)
        {
            try
            {
                var cutoffTime = DateTime.UtcNow.Subtract(_rateLimitWindow);
                var recentOtpCount = await _context.OtpTokens
                    .CountAsync(t => t.PhoneNumber == phoneNumber &&
                                    t.CreatedAt > cutoffTime);

                return Math.Max(0, _maxOtpAttempts - recentOtpCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting remaining attempts for phone: {PhoneNumber}", phoneNumber);
                return 0;
            }
        }

        private async Task IncrementAttemptCountAsync(string phoneNumber, string otpCode)
        {
            try
            {
                var otpToken = await _context.OtpTokens
                    .Where(t => t.PhoneNumber == phoneNumber &&
                               t.Token == otpCode &&
                               !t.IsUsed)
                    .OrderByDescending(t => t.CreatedAt)
                    .FirstOrDefaultAsync();

                if (otpToken != null)
                {
                    otpToken.AttemptCount++;

                    // Mark as used if max attempts reached
                    if (otpToken.AttemptCount >= _maxOtpAttempts)
                    {
                        otpToken.IsUsed = true;
                        otpToken.UsedAt = DateTime.UtcNow;
                    }

                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error incrementing attempt count for phone: {PhoneNumber}", phoneNumber);
            }
        }

        private string GenerateOtpCode()
        {
            var random = new Random();
            return random.Next(100000, 999999).ToString();
        }

        private async Task<bool> SendOtpSmsAsync(string phoneNumber, string otpCode)
        {
            // Simulate SMS sending - replace with actual SMS service implementation
            // Examples: Twilio, AWS SNS, Azure Communication Services

            try
            {
                // Simulate network delay
                await Task.Delay(100);

                // Log the OTP for development/testing purposes
                _logger.LogInformation("SMS Simulation: Sending OTP {OtpCode} to {PhoneNumber}", otpCode, phoneNumber);

                // In production, replace this with actual SMS service:
                /*
                // Twilio example:
                var message = await _twilioClient.MessageResource.CreateAsync(
                    body: $"Your verification code is: {otpCode}",
                    from: new PhoneNumber(_twilioFromNumber),
                    to: new PhoneNumber(phoneNumber)
                );
                return message.Status == MessageResource.StatusEnum.Queued;
                
                // AWS SNS example:
                var request = new PublishRequest
                {
                    PhoneNumber = phoneNumber,
                    Message = $"Your verification code is: {otpCode}"
                };
                var response = await _snsClient.PublishAsync(request);
                return response.HttpStatusCode == HttpStatusCode.OK;
                */

                return true; // Simulate successful sending
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send SMS to {PhoneNumber}", phoneNumber);
                return false;
            }
        }
    }
}