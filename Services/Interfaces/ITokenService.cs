using AuthService.Models;
using AuthService.DTOs;

namespace AuthService.Services.Interfaces
{
    public interface ITokenService
    {
        /// <summary>
        /// Generates both access and refresh tokens for a user
        /// </summary>
        /// <param name="user">The user to generate tokens for</param>
        /// <param name="deviceInfo">Optional device information</param>
        /// <param name="ipAddress">Optional IP address</param>
        /// <returns>Token response containing both access and refresh tokens</returns>
        Task<TokenResponse> GenerateTokensAsync(User user, string? deviceInfo = null, string? ipAddress = null);

        /// <summary>
        /// Generates a new access token (legacy method for backwards compatibility)
        /// </summary>
        /// <param name="user">The user to generate token for</param>
        /// <returns>JWT access token</returns>
        string GenerateAccessToken(User user);

        /// <summary>
        /// Validates a JWT token and returns user ID if valid
        /// </summary>
        /// <param name="token">JWT token to validate</param>
        /// <returns>User ID if token is valid, null otherwise</returns>
        int? ValidateToken(string token);

        /// <summary>
        /// Refreshes access token using refresh token
        /// </summary>
        /// <param name="refreshToken">The refresh token</param>
        /// <param name="deviceInfo">Optional device information</param>
        /// <param name="ipAddress">Optional IP address</param>
        /// <returns>New token response or null if refresh token is invalid</returns>
        Task<TokenResponse?> RefreshTokenAsync(string refreshToken, string? deviceInfo = null, string? ipAddress = null);

        /// <summary>
        /// Revokes a refresh token
        /// </summary>
        /// <param name="refreshToken">The refresh token to revoke</param>
        /// <param name="replacedByToken">Optional token that replaces this one</param>
        /// <returns>True if token was revoked successfully</returns>
        Task<bool> RevokeRefreshTokenAsync(string refreshToken, string? replacedByToken = null);

        /// <summary>
        /// Revokes all refresh tokens for a user
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <returns>Number of tokens revoked</returns>
        Task<int> RevokeAllUserRefreshTokensAsync(int userId);

        /// <summary>
        /// Validates if refresh token exists and is active
        /// </summary>
        /// <param name="refreshToken">The refresh token to validate</param>
        /// <returns>RefreshToken entity if valid, null otherwise</returns>
        Task<RefreshToken?> ValidateRefreshTokenAsync(string refreshToken);
    }
}