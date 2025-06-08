using AuthService.Models;

namespace AuthService.Services.Interfaces;

public interface IEmailVerificationService
{
    Task<string> GenerateVerificationTokenAsync(int userId, string email);
    Task<bool> VerifyEmailAsync(string token);
    Task<bool> ResendVerificationEmailAsync(string email);
    Task CleanupExpiredTokensAsync();
}