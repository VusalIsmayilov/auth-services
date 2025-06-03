using Microsoft.EntityFrameworkCore;
using AuthService.Data;
using AuthService.Models;
using AuthService.Services.Interfaces;
using AuthService.DTOs;

namespace AuthService.Services
{
    public class UserService : IUserService
    {
        private readonly AuthDbContext _context;
        private readonly IPasswordService _passwordService;
        private readonly IOtpService _otpService;
        private readonly ILogger<UserService> _logger;

        public UserService(
            AuthDbContext context,
            IPasswordService passwordService,
            IOtpService otpService,
            ILogger<UserService> logger)
        {
            _context = context;
            _passwordService = passwordService;
            _otpService = otpService;
            _logger = logger;
        }

        public async Task<User?> RegisterWithEmailAsync(string email, string password)
        {
            try
            {
                // Check if email is already in use
                if (await IsEmailInUseAsync(email))
                {
                    _logger.LogWarning("Registration attempted with existing email: {Email}", email);
                    return null;
                }

                // Hash the password
                var passwordHash = _passwordService.HashPassword(password);

                // Create new user
                var user = new User
                {
                    Email = email,
                    PasswordHash = passwordHash,
                    IsEmailVerified = true, // Auto-verify for email registration
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                // Update last login
                await UpdateLastLoginAsync(user.Id);

                _logger.LogInformation("User registered successfully with email: {Email}, ID: {UserId}", email, user.Id);
                return user;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registering user with email: {Email}", email);
                return null;
            }
        }

        public async Task<bool> RegisterWithPhoneAsync(string phoneNumber)
        {
            try
            {
                // Check if phone is already in use
                if (await IsPhoneInUseAsync(phoneNumber))
                {
                    _logger.LogWarning("Registration attempted with existing phone: {PhoneNumber}", phoneNumber);
                    return false;
                }

                // Create user (will be activated after OTP verification)
                var user = new User
                {
                    PhoneNumber = phoneNumber,
                    IsPhoneVerified = false,
                    IsActive = false, // Will be activated after OTP verification
                    CreatedAt = DateTime.UtcNow
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                // Send OTP
                var otpResult = await _otpService.SendOtpAsync(phoneNumber);
                if (!otpResult.Success)
                {
                    // Remove user if OTP sending failed
                    _context.Users.Remove(user);
                    await _context.SaveChangesAsync();
                    return false;
                }

                _logger.LogInformation("User registration initiated with phone: {PhoneNumber}, ID: {UserId}", phoneNumber, user.Id);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registering user with phone: {PhoneNumber}", phoneNumber);
                return false;
            }
        }

        public async Task<User?> AuthenticateWithEmailAsync(string email, string password)
        {
            try
            {
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email == email && u.IsActive);

                if (user == null)
                {
                    _logger.LogWarning("Authentication failed: User not found for email: {Email}", email);
                    return null;
                }

                if (string.IsNullOrEmpty(user.PasswordHash))
                {
                    _logger.LogWarning("Authentication failed: No password set for user: {Email}", email);
                    return null;
                }

                if (!_passwordService.VerifyPassword(password, user.PasswordHash))
                {
                    _logger.LogWarning("Authentication failed: Invalid password for user: {Email}", email);
                    return null;
                }

                // Update last login
                await UpdateLastLoginAsync(user.Id);

                _logger.LogInformation("User authenticated successfully with email: {Email}, ID: {UserId}", email, user.Id);
                return user;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error authenticating user with email: {Email}", email);
                return null;
            }
        }

        public async Task<bool> InitiatePhoneLoginAsync(string phoneNumber)
        {
            try
            {
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.PhoneNumber == phoneNumber && u.IsActive);

                if (user == null)
                {
                    _logger.LogWarning("Phone login failed: User not found for phone: {PhoneNumber}", phoneNumber);
                    return false;
                }

                // Send OTP
                var otpResult = await _otpService.SendOtpAsync(phoneNumber);
                if (!otpResult.Success)
                {
                    _logger.LogWarning("Phone login failed: Could not send OTP to: {PhoneNumber}", phoneNumber);
                    return false;
                }

                _logger.LogInformation("Phone login initiated for: {PhoneNumber}, ID: {UserId}", phoneNumber, user.Id);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initiating phone login: {PhoneNumber}", phoneNumber);
                return false;
            }
        }

        public async Task<User?> VerifyOtpAsync(string phoneNumber, string otpCode)
        {
            try
            {
                // Verify OTP first
                var isOtpValid = await _otpService.VerifyOtpAsync(phoneNumber, otpCode);
                if (!isOtpValid)
                {
                    _logger.LogWarning("OTP verification failed for phone: {PhoneNumber}", phoneNumber);
                    return null;
                }

                // Get or create user
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.PhoneNumber == phoneNumber);

                if (user == null)
                {
                    _logger.LogError("User not found after OTP verification for phone: {PhoneNumber}", phoneNumber);
                    return null;
                }

                // Activate user and verify phone if not already done
                if (!user.IsActive || !user.IsPhoneVerified)
                {
                    user.IsActive = true;
                    user.IsPhoneVerified = true;
                    await _context.SaveChangesAsync();
                }

                // Update last login
                await UpdateLastLoginAsync(user.Id);

                _logger.LogInformation("OTP verified successfully for phone: {PhoneNumber}, ID: {UserId}", phoneNumber, user.Id);
                return user;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying OTP for phone: {PhoneNumber}", phoneNumber);
                return null;
            }
        }

        public async Task<User?> GetUserByIdAsync(int userId)
        {
            try
            {
                return await _context.Users
                    .FirstOrDefaultAsync(u => u.Id == userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user by ID: {UserId}", userId);
                return null;
            }
        }

        public async Task<User?> GetUserByEmailAsync(string email)
        {
            try
            {
                return await _context.Users
                    .FirstOrDefaultAsync(u => u.Email == email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user by email: {Email}", email);
                return null;
            }
        }

        public async Task<User?> GetUserByPhoneAsync(string phoneNumber)
        {
            try
            {
                return await _context.Users
                    .FirstOrDefaultAsync(u => u.PhoneNumber == phoneNumber);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user by phone: {PhoneNumber}", phoneNumber);
                return null;
            }
        }

        public async Task<UserInfo?> GetUserInfoAsync(int userId)
        {
            try
            {
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Id == userId);

                if (user == null)
                {
                    return null;
                }

                return new UserInfo
                {
                    Id = user.Id,
                    Email = user.Email,
                    PhoneNumber = user.PhoneNumber,
                    IsEmailVerified = user.IsEmailVerified,
                    IsPhoneVerified = user.IsPhoneVerified,
                    CreatedAt = user.CreatedAt,
                    LastLoginAt = user.LastLoginAt,
                    IsActive = user.IsActive
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user info: {UserId}", userId);
                return null;
            }
        }

        public async Task<bool> UpdateLastLoginAsync(int userId)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    return false;
                }

                user.LastLoginAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating last login for user: {UserId}", userId);
                return false;
            }
        }

        public async Task<bool> UpdateEmailVerificationAsync(int userId, bool isVerified)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    return false;
                }

                user.IsEmailVerified = isVerified;
                await _context.SaveChangesAsync();

                _logger.LogInformation("Email verification updated for user {UserId}: {IsVerified}", userId, isVerified);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating email verification for user: {UserId}", userId);
                return false;
            }
        }

        public async Task<bool> UpdatePhoneVerificationAsync(int userId, bool isVerified)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    return false;
                }

                user.IsPhoneVerified = isVerified;
                await _context.SaveChangesAsync();

                _logger.LogInformation("Phone verification updated for user {UserId}: {IsVerified}", userId, isVerified);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating phone verification for user: {UserId}", userId);
                return false;
            }
        }

        public async Task<bool> DeactivateUserAsync(int userId)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    return false;
                }

                user.IsActive = false;
                await _context.SaveChangesAsync();

                _logger.LogInformation("User deactivated: {UserId}", userId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deactivating user: {UserId}", userId);
                return false;
            }
        }

        public async Task<bool> ActivateUserAsync(int userId)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    return false;
                }

                user.IsActive = true;
                await _context.SaveChangesAsync();

                _logger.LogInformation("User activated: {UserId}", userId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error activating user: {UserId}", userId);
                return false;
            }
        }

        public async Task<bool> IsEmailInUseAsync(string email)
        {
            try
            {
                return await _context.Users
                    .AnyAsync(u => u.Email == email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if email is in use: {Email}", email);
                return true; // Return true to be safe and prevent registration
            }
        }

        public async Task<bool> IsPhoneInUseAsync(string phoneNumber)
        {
            try
            {
                return await _context.Users
                    .AnyAsync(u => u.PhoneNumber == phoneNumber);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if phone is in use: {PhoneNumber}", phoneNumber);
                return true; // Return true to be safe and prevent registration
            }
        }
    }
}