using AuthService.Data;
using AuthService.DTOs;
using AuthService.Models;
using AuthService.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Services
{
    public class UserService : IUserService
    {
        private readonly AuthDbContext _context;
        private readonly IPasswordService _passwordService;
        private readonly ITokenService _tokenService;
        private readonly IOtpService _otpService;
        private readonly ILogger<UserService> _logger;

        public UserService(
            AuthDbContext context,
            IPasswordService passwordService,
            ITokenService tokenService,
            IOtpService otpService,
            ILogger<UserService> logger)
        {
            _context = context;
            _passwordService = passwordService;
            _tokenService = tokenService;
            _otpService = otpService;
            _logger = logger;
        }

        public async Task<AuthResponse> RegisterWithEmailAsync(RegisterEmailRequest request)
        {
            try
            {
                // Check if user already exists
                var existingUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email == request.Email);

                if (existingUser != null)
                {
                    return new AuthResponse
                    {
                        Success = false,
                        Message = "User with this email already exists"
                    };
                }

                // Create new user
                var user = new User
                {
                    Email = request.Email,
                    PasswordHash = _passwordService.HashPassword(request.Password),
                    IsEmailVerified = true, // Auto-verify for simplicity
                    CreatedAt = DateTime.UtcNow
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                // Generate token
                var token = _tokenService.GenerateJwtToken(user.Id, user.Email);

                return new AuthResponse
                {
                    Success = true,
                    Message = "Registration successful",
                    Token = token,
                    User = MapToUserInfo(user)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registering user with email: {Email}", request.Email);
                return new AuthResponse
                {
                    Success = false,
                    Message = "Registration failed. Please try again."
                };
            }
        }

        public async Task<AuthResponse> RegisterWithPhoneAsync(RegisterPhoneRequest request)
        {
            try
            {
                // Check if user already exists
                var existingUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.PhoneNumber == request.PhoneNumber);

                if (existingUser != null)
                {
                    return new AuthResponse
                    {
                        Success = false,
                        Message = "User with this phone number already exists"
                    };
                }

                // Generate OTP for registration
                var otpResult = await _otpService.GenerateOtpAsync(request.PhoneNumber);

                if (!otpResult.Success)
                {
                    return new AuthResponse
                    {
                        Success = false,
                        Message = otpResult.Message
                    };
                }

                return new AuthResponse
                {
                    Success = true,
                    Message = "OTP sent to your phone number. Please verify to complete registration."
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registering user with phone: {PhoneNumber}", request.PhoneNumber);
                return new AuthResponse
                {
                    Success = false,
                    Message = "Registration failed. Please try again."
                };
            }
        }

        public async Task<AuthResponse> LoginWithEmailAsync(LoginEmailRequest request)
        {
            try
            {
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email == request.Email && u.IsActive);

                if (user == null || string.IsNullOrEmpty(user.PasswordHash))
                {
                    return new AuthResponse
                    {
                        Success = false,
                        Message = "Invalid email or password"
                    };
                }

                if (!_passwordService.VerifyPassword(request.Password, user.PasswordHash))
                {
                    return new AuthResponse
                    {
                        Success = false,
                        Message = "Invalid email or password"
                    };
                }

                // Update last login
                user.LastLoginAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                // Generate token
                var token = _tokenService.GenerateJwtToken(user.Id, user.Email!);

                return new AuthResponse
                {
                    Success = true,
                    Message = "Login successful",
                    Token = token,
                    User = MapToUserInfo(user)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging in user with email: {Email}", request.Email);
                return new AuthResponse
                {
                    Success = false,
                    Message = "Login failed. Please try again."
                };
            }
        }

        public async Task<AuthResponse> LoginWithPhoneAsync(LoginPhoneRequest request)
        {
            try
            {
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.PhoneNumber == request.PhoneNumber && u.IsActive);

                if (user == null)
                {
                    return new AuthResponse
                    {
                        Success = false,
                        Message = "No account found with this phone number"
                    };
                }

                // Generate OTP for login
                var otpResult = await _otpService.GenerateOtpAsync(request.PhoneNumber);

                if (!otpResult.Success)
                {
                    return new AuthResponse
                    {
                        Success = false,
                        Message = otpResult.Message
                    };
                }

                return new AuthResponse
                {
                    Success = true,
                    Message = "OTP sent to your phone number. Please verify to complete login."
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging in user with phone: {PhoneNumber}", request.PhoneNumber);
                return new AuthResponse
                {
                    Success = false,
                    Message = "Login failed. Please try again."
                };
            }
        }

        public async Task<AuthResponse> VerifyOtpAndLoginAsync(VerifyOtpRequest request)
        {
            try
            {
                var isValidOtp = await _otpService.ValidateOtpAsync(request.PhoneNumber, request.OtpCode);

                if (!isValidOtp)
                {
                    return new AuthResponse
                    {
                        Success = false,
                        Message = "Invalid or expired OTP"
                    };
                }

                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.PhoneNumber == request.PhoneNumber && u.IsActive);

                if (user == null)
                {
                    return new AuthResponse
                    {
                        Success = false,
                        Message = "User not found"
                    };
                }

                // Generate token
                var token = _tokenService.GenerateJwtToken(user.Id, user.PhoneNumber);

                return new AuthResponse
                {
                    Success = true,
                    Message = "Login successful",
                    Token = token,
                    User = MapToUserInfo(user)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying OTP for phone: {PhoneNumber}", request.PhoneNumber);
                return new AuthResponse
                {
                    Success = false,
                    Message = "OTP verification failed. Please try again."
                };
            }
        }

        public async Task<UserInfo?> GetUserByIdAsync(int userId)
        {
            try
            {
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);

                return user != null ? MapToUserInfo(user) : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user by ID: {UserId}", userId);
                return null;
            }
        }

        private UserInfo MapToUserInfo(User user)
        {
            return new UserInfo
            {
                Id = user.Id,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                IsEmailVerified = user.IsEmailVerified,
                IsPhoneVerified = user.IsPhoneVerified
            };
        }
    }
}