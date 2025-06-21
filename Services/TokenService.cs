using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using AuthService.Models;
using AuthService.Services.Interfaces;
using AuthService.Data;
using AuthService.DTOs;

namespace AuthService.Services
{
    public class TokenService : ITokenService
    {
        private readonly IConfiguration _configuration;
        private readonly AuthDbContext _context;
        private readonly ILogger<TokenService> _logger;

        private readonly string _secretKey;
        private readonly string _issuer;
        private readonly string _audience;
        private readonly TimeSpan _accessTokenExpiry = TimeSpan.FromMinutes(15); // 15 minutes
        private readonly TimeSpan _refreshTokenExpiry = TimeSpan.FromDays(7);    // 7 days

        public TokenService(IConfiguration configuration, AuthDbContext context, ILogger<TokenService> logger)
        {
            _configuration = configuration;
            _context = context;
            _logger = logger;

            _secretKey = _configuration["JWT:SecretKey"]
                ?? throw new InvalidOperationException("JWT SecretKey not configured");
            _issuer = _configuration["JWT:Issuer"]
                ?? throw new InvalidOperationException("JWT Issuer not configured");
            _audience = _configuration["JWT:Audience"]
                ?? throw new InvalidOperationException("JWT Audience not configured");
        }

        public async Task<TokenResponse> GenerateTokensAsync(User user, string? deviceInfo = null, string? ipAddress = null)
        {
            try
            {
                var accessToken = GenerateAccessToken(user);
                var refreshToken = await GenerateRefreshTokenAsync(user, deviceInfo, ipAddress);

                var accessTokenExpiry = DateTime.UtcNow.Add(_accessTokenExpiry);
                var refreshTokenExpiry = DateTime.UtcNow.Add(_refreshTokenExpiry);

                return new TokenResponse
                {
                    AccessToken = accessToken,
                    RefreshToken = refreshToken,
                    AccessTokenExpiresAt = accessTokenExpiry,
                    RefreshTokenExpiresAt = refreshTokenExpiry,
                    TokenType = "Bearer"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating tokens for user {UserId}", user.Id);
                throw;
            }
        }

        public string GenerateAccessToken(User user)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email ?? string.Empty),
                new Claim("phone", user.PhoneNumber ?? string.Empty),
                new Claim("email_verified", user.IsEmailVerified.ToString()),
                new Claim("phone_verified", user.IsPhoneVerified.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
            };

            var token = new JwtSecurityToken(
                issuer: _issuer,
                audience: _audience,
                claims: claims,
                expires: DateTime.UtcNow.Add(_accessTokenExpiry),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private async Task<string> GenerateRefreshTokenAsync(User user, string? deviceInfo = null, string? ipAddress = null)
        {
            var randomBytes = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomBytes);
            var refreshToken = Convert.ToBase64String(randomBytes);

            var refreshTokenEntity = new RefreshToken
            {
                UserId = user.Id,
                Token = refreshToken,
                ExpiresAt = DateTime.UtcNow.Add(_refreshTokenExpiry),
                DeviceInfo = deviceInfo,
                IpAddress = ipAddress
            };

            _context.RefreshTokens.Add(refreshTokenEntity);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Generated new refresh token for user {UserId}", user.Id);
            return refreshToken;
        }

        public int? ValidateToken(string token)
        {
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.UTF8.GetBytes(_secretKey);

                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidIssuer = _issuer,
                    ValidateAudience = true,
                    ValidAudience = _audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };

                var principal = tokenHandler.ValidateToken(token, validationParameters, out _);
                var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier);

                if (userIdClaim != null && int.TryParse(userIdClaim.Value, out var userId))
                {
                    return userId;
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Token validation failed");
                return null;
            }
        }

        public async Task<TokenResponse?> RefreshTokenAsync(string refreshToken, string? deviceInfo = null, string? ipAddress = null)
        {
            try
            {
                var refreshTokenEntity = await ValidateRefreshTokenAsync(refreshToken);
                if (refreshTokenEntity == null)
                {
                    _logger.LogWarning("Invalid refresh token provided");
                    return null;
                }

                var user = await _context.Users.FindAsync(refreshTokenEntity.UserId);
                if (user == null || !user.IsActive)
                {
                    _logger.LogWarning("User not found or inactive for refresh token");
                    return null;
                }

                // Revoke the old refresh token
                refreshTokenEntity.IsRevoked = true;
                refreshTokenEntity.RevokedAt = DateTime.UtcNow;

                // Generate new tokens
                var newTokenResponse = await GenerateTokensAsync(user, deviceInfo, ipAddress);

                // Set the replaced by token reference
                refreshTokenEntity.ReplacedByToken = newTokenResponse.RefreshToken;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Refresh token successfully rotated for user {UserId}", user.Id);
                return newTokenResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing token");
                return null;
            }
        }

        public async Task<RefreshToken?> ValidateRefreshTokenAsync(string refreshToken)
        {
            try
            {
                var token = await _context.RefreshTokens
                    .Include(rt => rt.User)
                    .FirstOrDefaultAsync(rt => rt.Token == refreshToken);

                return token?.IsActive == true ? token : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating refresh token");
                return null;
            }
        }

        public async Task<bool> RevokeRefreshTokenAsync(string refreshToken, string? replacedByToken = null)
        {
            try
            {
                var token = await _context.RefreshTokens
                    .FirstOrDefaultAsync(rt => rt.Token == refreshToken && rt.IsActive);

                if (token == null)
                {
                    _logger.LogWarning("Refresh token not found or already revoked");
                    return false;
                }

                token.IsRevoked = true;
                token.RevokedAt = DateTime.UtcNow;
                token.ReplacedByToken = replacedByToken;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Refresh token revoked for user {UserId}", token.UserId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revoking refresh token");
                return false;
            }
        }

        public async Task<int> RevokeAllUserRefreshTokensAsync(int userId)
        {
            try
            {
                var activeTokens = await _context.RefreshTokens
                    .Where(rt => rt.UserId == userId && rt.IsActive)
                    .ToListAsync();

                foreach (var token in activeTokens)
                {
                    token.IsRevoked = true;
                    token.RevokedAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("Revoked {Count} refresh tokens for user {UserId}", activeTokens.Count, userId);
                return activeTokens.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revoking all refresh tokens for user {UserId}", userId);
                return 0;
            }
        }

        public object GetJwks()
        {
            // For HMAC SHA256, we expose the key identifier and algorithm info
            // Note: In production, consider using RSA keys for better security
            using var sha256 = SHA256.Create();
            var keyId = Convert.ToBase64String(sha256.ComputeHash(Encoding.UTF8.GetBytes(_secretKey))).Substring(0, 8);
            
            return new
            {
                keys = new[]
                {
                    new
                    {
                        kty = "oct", // Key type: octet sequence (symmetric key)
                        use = "sig", // Usage: signature
                        alg = "HS256", // Algorithm: HMAC SHA256
                        kid = keyId, // Key ID
                        // Note: For HMAC, the actual key is not exposed in JWKS
                        // This is a simplified JWKS for documentation purposes
                    }
                }
            };
        }
    }
}