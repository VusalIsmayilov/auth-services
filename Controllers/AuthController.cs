using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using AuthService.DTOs;
using AuthService.Services.Interfaces;

namespace AuthService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly ITokenService _tokenService;
        private readonly IOtpService _otpService;
        private readonly IEmailVerificationService _emailVerificationService;
        private readonly IPasswordResetService _passwordResetService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(
            IUserService userService,
            ITokenService tokenService,
            IOtpService otpService,
            IEmailVerificationService emailVerificationService,
            IPasswordResetService passwordResetService,
            ILogger<AuthController> logger)
        {
            _userService = userService;
            _tokenService = tokenService;
            _otpService = otpService;
            _emailVerificationService = emailVerificationService;
            _passwordResetService = passwordResetService;
            _logger = logger;
        }

        /// <summary>
        /// Register a new user with email and password
        /// </summary>
        [HttpPost("register/email")]
        public async Task<ActionResult<AuthResponse>> RegisterWithEmail([FromBody] RegisterEmailRequest request)
        {
            try
            {
                var user = await _userService.RegisterWithEmailAsync(request.Email, request.Password);
                if (user == null)
                {
                    return BadRequest(new AuthResponse
                    {
                        Success = false,
                        Message = "Registration failed. Email may already be in use."
                    });
                }

                var deviceInfo = GetDeviceInfo();
                var ipAddress = GetClientIpAddress();
                var tokens = await _tokenService.GenerateTokensAsync(user, deviceInfo, ipAddress);

                var response = new AuthResponse
                {
                    Success = true,
                    Message = "Registration successful",
                    Tokens = tokens,
                    User = new UserResponse
                    {
                        Id = user.Id,
                        Email = user.Email,
                        PhoneNumber = user.PhoneNumber,
                        IsEmailVerified = user.IsEmailVerified,
                        IsPhoneVerified = user.IsPhoneVerified
                    }
                };

                _logger.LogInformation("User registered successfully with email: {Email}", request.Email);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during email registration");
                return StatusCode(500, new AuthResponse
                {
                    Success = false,
                    Message = "An error occurred during registration"
                });
            }
        }

        /// <summary>
        /// Register a new user with phone number (sends OTP)
        /// </summary>
        [HttpPost("register/phone")]
        public async Task<ActionResult<ApiResponse>> RegisterWithPhone([FromBody] RegisterPhoneRequest request)
        {
            try
            {
                var result = await _userService.RegisterWithPhoneAsync(request.PhoneNumber);
                if (!result)
                {
                    return BadRequest(new ApiResponse
                    {
                        Success = false,
                        Message = "Registration failed. Phone number may already be in use."
                    });
                }

                _logger.LogInformation("Phone registration initiated for: {PhoneNumber}", request.PhoneNumber);
                return Ok(new ApiResponse
                {
                    Success = true,
                    Message = "OTP sent to your phone number. Please verify to complete registration."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during phone registration");
                return StatusCode(500, new ApiResponse
                {
                    Success = false,
                    Message = "An error occurred during registration"
                });
            }
        }

        /// <summary>
        /// Login with email and password
        /// </summary>
        [HttpPost("login/email")]
        public async Task<ActionResult<AuthResponse>> LoginWithEmail([FromBody] LoginEmailRequest request)
        {
            try
            {
                var user = await _userService.AuthenticateWithEmailAsync(request.Email, request.Password);
                if (user == null)
                {
                    return Unauthorized(new AuthResponse
                    {
                        Success = false,
                        Message = "Invalid email or password"
                    });
                }

                var deviceInfo = GetDeviceInfo();
                var ipAddress = GetClientIpAddress();
                var tokens = await _tokenService.GenerateTokensAsync(user, deviceInfo, ipAddress);

                var response = new AuthResponse
                {
                    Success = true,
                    Message = "Login successful",
                    Tokens = tokens,
                    User = new UserResponse
                    {
                        Id = user.Id,
                        Email = user.Email,
                        PhoneNumber = user.PhoneNumber,
                        IsEmailVerified = user.IsEmailVerified,
                        IsPhoneVerified = user.IsPhoneVerified
                    }
                };

                _logger.LogInformation("User logged in successfully with email: {Email}", request.Email);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during email login");
                return StatusCode(500, new AuthResponse
                {
                    Success = false,
                    Message = "An error occurred during login"
                });
            }
        }

        /// <summary>
        /// Login with phone number (sends OTP)
        /// </summary>
        [HttpPost("login/phone")]
        public async Task<ActionResult<ApiResponse>> LoginWithPhone([FromBody] LoginPhoneRequest request)
        {
            try
            {
                var result = await _userService.InitiatePhoneLoginAsync(request.PhoneNumber);
                if (!result)
                {
                    return BadRequest(new ApiResponse
                    {
                        Success = false,
                        Message = "Phone number not found or login failed"
                    });
                }

                _logger.LogInformation("Phone login initiated for: {PhoneNumber}", request.PhoneNumber);
                return Ok(new ApiResponse
                {
                    Success = true,
                    Message = "OTP sent to your phone number. Please verify to complete login."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during phone login");
                return StatusCode(500, new ApiResponse
                {
                    Success = false,
                    Message = "An error occurred during login"
                });
            }
        }

        /// <summary>
        /// Verify OTP and complete phone authentication
        /// </summary>
        [HttpPost("verify-otp")]
        public async Task<ActionResult<AuthResponse>> VerifyOtp([FromBody] VerifyOtpRequest request)
        {
            try
            {
                var user = await _userService.VerifyOtpAsync(request.PhoneNumber, request.OtpCode);
                if (user == null)
                {
                    return BadRequest(new AuthResponse
                    {
                        Success = false,
                        Message = "Invalid OTP or phone number"
                    });
                }

                var deviceInfo = GetDeviceInfo();
                var ipAddress = GetClientIpAddress();
                var tokens = await _tokenService.GenerateTokensAsync(user, deviceInfo, ipAddress);

                var response = new AuthResponse
                {
                    Success = true,
                    Message = "Login successful",
                    Tokens = tokens,
                    User = new UserResponse
                    {
                        Id = user.Id,
                        Email = user.Email,
                        PhoneNumber = user.PhoneNumber,
                        IsEmailVerified = user.IsEmailVerified,
                        IsPhoneVerified = user.IsPhoneVerified
                    }
                };

                _logger.LogInformation("OTP verified successfully for: {PhoneNumber}", request.PhoneNumber);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during OTP verification");
                return StatusCode(500, new AuthResponse
                {
                    Success = false,
                    Message = "An error occurred during OTP verification"
                });
            }
        }

        /// <summary>
        /// Send OTP to phone number
        /// </summary>
        [HttpPost("send-otp")]
        public async Task<ActionResult<OtpResponse>> SendOtp([FromBody] SendOtpRequest request)
        {
            try
            {
                var result = await _otpService.SendOtpAsync(request.PhoneNumber);
                if (!result.Success)
                {
                    return BadRequest(new OtpResponse
                    {
                        Success = false,
                        Message = result.Message
                    });
                }

                _logger.LogInformation("OTP sent to: {PhoneNumber}", request.PhoneNumber);
                return Ok(new OtpResponse
                {
                    Success = true,
                    Message = "OTP sent successfully",
                    ExpiresAt = result.ExpiresAt
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending OTP");
                return StatusCode(500, new OtpResponse
                {
                    Success = false,
                    Message = "An error occurred while sending OTP"
                });
            }
        }

        /// <summary>
        /// Refresh access token using refresh token
        /// </summary>
        [HttpPost("refresh")]
        public async Task<ActionResult<AuthResponse>> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            try
            {
                var deviceInfo = GetDeviceInfo();
                var ipAddress = GetClientIpAddress();
                var tokens = await _tokenService.RefreshTokenAsync(request.RefreshToken, deviceInfo, ipAddress);

                if (tokens == null)
                {
                    return Unauthorized(new AuthResponse
                    {
                        Success = false,
                        Message = "Invalid or expired refresh token"
                    });
                }

                var response = new AuthResponse
                {
                    Success = true,
                    Message = "Token refreshed successfully",
                    Tokens = tokens
                };

                _logger.LogInformation("Token refreshed successfully");
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing token");
                return StatusCode(500, new AuthResponse
                {
                    Success = false,
                    Message = "An error occurred while refreshing token"
                });
            }
        }

        /// <summary>
        /// Revoke a refresh token
        /// </summary>
        [HttpPost("revoke")]
        public async Task<ActionResult<ApiResponse>> RevokeToken([FromBody] RevokeTokenRequest request)
        {
            try
            {
                var result = await _tokenService.RevokeRefreshTokenAsync(request.RefreshToken);
                if (!result)
                {
                    return BadRequest(new ApiResponse
                    {
                        Success = false,
                        Message = "Token not found or already revoked"
                    });
                }

                _logger.LogInformation("Refresh token revoked successfully");
                return Ok(new ApiResponse
                {
                    Success = true,
                    Message = "Token revoked successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revoking token");
                return StatusCode(500, new ApiResponse
                {
                    Success = false,
                    Message = "An error occurred while revoking token"
                });
            }
        }

        /// <summary>
        /// Revoke all refresh tokens for the current user
        /// </summary>
        [HttpPost("revoke-all")]
        [Authorize]
        public async Task<ActionResult<ApiResponse>> RevokeAllTokens()
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
                {
                    return Unauthorized(new ApiResponse
                    {
                        Success = false,
                        Message = "Invalid token"
                    });
                }

                var revokedCount = await _tokenService.RevokeAllUserRefreshTokensAsync(userId);

                _logger.LogInformation("Revoked {Count} tokens for user {UserId}", revokedCount, userId);
                return Ok(new ApiResponse
                {
                    Success = true,
                    Message = $"Successfully revoked {revokedCount} refresh tokens"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revoking all tokens");
                return StatusCode(500, new ApiResponse
                {
                    Success = false,
                    Message = "An error occurred while revoking tokens"
                });
            }
        }

        /// <summary>
        /// Get current authenticated user information
        /// </summary>
        [HttpGet("me")]
        [Authorize]
        public async Task<ActionResult<UserResponse>> GetCurrentUser()
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
                {
                    return Unauthorized();
                }

                var user = await _userService.GetUserByIdAsync(userId);
                if (user == null)
                {
                    return NotFound();
                }

                return Ok(new UserResponse
                {
                    Id = user.Id,
                    Email = user.Email,
                    PhoneNumber = user.PhoneNumber,
                    IsEmailVerified = user.IsEmailVerified,
                    IsPhoneVerified = user.IsPhoneVerified
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting current user");
                return StatusCode(500, new ApiResponse
                {
                    Success = false,
                    Message = "An error occurred while retrieving user information"
                });
            }
        }

        /// <summary>
        /// Verify email address using verification token
        /// </summary>
        [HttpGet("verify-email")]
        public async Task<ActionResult<ApiResponse>> VerifyEmail([FromQuery] string token)
        {
            try
            {
                if (string.IsNullOrEmpty(token))
                {
                    return BadRequest(new ApiResponse
                    {
                        Success = false,
                        Message = "Verification token is required"
                    });
                }

                var isVerified = await _emailVerificationService.VerifyEmailAsync(token);
                if (isVerified)
                {
                    _logger.LogInformation("Email verification successful for token: {Token}", token);
                    return Ok(new ApiResponse
                    {
                        Success = true,
                        Message = "Email verified successfully"
                    });
                }

                return BadRequest(new ApiResponse
                {
                    Success = false,
                    Message = "Invalid or expired verification token"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during email verification");
                return StatusCode(500, new ApiResponse
                {
                    Success = false,
                    Message = "An error occurred during email verification"
                });
            }
        }

        /// <summary>
        /// Verify email address using verification token (POST method)
        /// </summary>
        [HttpPost("verify-email")]
        public async Task<ActionResult<ApiResponse>> VerifyEmailPost([FromBody] VerifyEmailRequest request)
        {
            try
            {
                var isVerified = await _emailVerificationService.VerifyEmailAsync(request.Token);
                if (isVerified)
                {
                    _logger.LogInformation("Email verification successful for token: {Token}", request.Token);
                    return Ok(new ApiResponse
                    {
                        Success = true,
                        Message = "Email verified successfully"
                    });
                }

                return BadRequest(new ApiResponse
                {
                    Success = false,
                    Message = "Invalid or expired verification token"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during email verification");
                return StatusCode(500, new ApiResponse
                {
                    Success = false,
                    Message = "An error occurred during email verification"
                });
            }
        }

        /// <summary>
        /// Resend email verification for unverified email addresses
        /// </summary>
        [HttpPost("resend-verification")]
        public async Task<ActionResult<ApiResponse>> ResendVerificationEmail([FromBody] ResendVerificationRequest request)
        {
            try
            {
                var result = await _emailVerificationService.ResendVerificationEmailAsync(request.Email);
                if (result)
                {
                    _logger.LogInformation("Verification email resent successfully to: {Email}", request.Email);
                    return Ok(new ApiResponse
                    {
                        Success = true,
                        Message = "Verification email sent successfully"
                    });
                }

                return BadRequest(new ApiResponse
                {
                    Success = false,
                    Message = "Unable to send verification email. Email may already be verified or not found."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resending verification email");
                return StatusCode(500, new ApiResponse
                {
                    Success = false,
                    Message = "An error occurred while sending verification email"
                });
            }
        }

        /// <summary>
        /// Request password reset email
        /// </summary>
        [HttpPost("forgot-password")]
        public async Task<ActionResult<ApiResponse>> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            try
            {
                var ipAddress = GetClientIpAddress();
                var userAgent = GetDeviceInfo();
                var result = await _passwordResetService.SendPasswordResetEmailAsync(request.Email, ipAddress, userAgent);

                if (result)
                {
                    _logger.LogInformation("Password reset email sent successfully to: {Email}", request.Email);
                    return Ok(new ApiResponse
                    {
                        Success = true,
                        Message = "If an account with that email exists, a password reset link has been sent."
                    });
                }

                return BadRequest(new ApiResponse
                {
                    Success = false,
                    Message = "Unable to send password reset email. Please try again later."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending password reset email");
                return StatusCode(500, new ApiResponse
                {
                    Success = false,
                    Message = "An error occurred while processing your request"
                });
            }
        }

        /// <summary>
        /// Validate password reset token
        /// </summary>
        [HttpPost("validate-reset-token")]
        public async Task<ActionResult<ApiResponse>> ValidateResetToken([FromBody] ValidateResetTokenRequest request)
        {
            try
            {
                var isValid = await _passwordResetService.ValidateResetTokenAsync(request.Token);

                if (isValid)
                {
                    return Ok(new ApiResponse
                    {
                        Success = true,
                        Message = "Token is valid"
                    });
                }

                return BadRequest(new ApiResponse
                {
                    Success = false,
                    Message = "Invalid or expired reset token"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating reset token");
                return StatusCode(500, new ApiResponse
                {
                    Success = false,
                    Message = "An error occurred while validating the token"
                });
            }
        }

        /// <summary>
        /// Reset password using token
        /// </summary>
        [HttpPost("reset-password")]
        public async Task<ActionResult<ApiResponse>> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            try
            {
                var result = await _passwordResetService.ResetPasswordAsync(request.Token, request.NewPassword);

                if (result)
                {
                    _logger.LogInformation("Password reset successfully for token: {Token}", request.Token);
                    return Ok(new ApiResponse
                    {
                        Success = true,
                        Message = "Password has been reset successfully"
                    });
                }

                return BadRequest(new ApiResponse
                {
                    Success = false,
                    Message = "Invalid or expired reset token"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting password");
                return StatusCode(500, new ApiResponse
                {
                    Success = false,
                    Message = "An error occurred while resetting your password"
                });
            }
        }

        // Legacy endpoints for backwards compatibility (return single token)

        /// <summary>
        /// Legacy: Register with email (returns single token for backwards compatibility)
        /// </summary>
        [HttpPost("legacy/register/email")]
        public async Task<ActionResult<LegacyAuthResponse>> LegacyRegisterWithEmail([FromBody] RegisterEmailRequest request)
        {
            var result = await RegisterWithEmail(request);
            if (result.Result is OkObjectResult okResult && okResult.Value is AuthResponse authResponse)
            {
                return Ok(new LegacyAuthResponse
                {
                    Success = authResponse.Success,
                    Message = authResponse.Message,
                    Token = authResponse.Tokens?.AccessToken,
                    User = authResponse.User
                });
            }

            return BadRequest(new LegacyAuthResponse { Success = false, Message = "Registration failed" });
        }

        /// <summary>
        /// Legacy: Login with email (returns single token for backwards compatibility)
        /// </summary>
        [HttpPost("legacy/login/email")]
        public async Task<ActionResult<LegacyAuthResponse>> LegacyLoginWithEmail([FromBody] LoginEmailRequest request)
        {
            var result = await LoginWithEmail(request);
            if (result.Result is OkObjectResult okResult && okResult.Value is AuthResponse authResponse)
            {
                return Ok(new LegacyAuthResponse
                {
                    Success = authResponse.Success,
                    Message = authResponse.Message,
                    Token = authResponse.Tokens?.AccessToken,
                    User = authResponse.User
                });
            }

            return Unauthorized(new LegacyAuthResponse { Success = false, Message = "Invalid credentials" });
        }

        private string? GetDeviceInfo()
        {
            return Request.Headers["User-Agent"].ToString();
        }

        private string? GetClientIpAddress()
        {
            return Request.HttpContext.Connection.RemoteIpAddress?.ToString();
        }
    }
}