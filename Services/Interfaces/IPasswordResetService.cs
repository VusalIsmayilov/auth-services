namespace AuthService.Services.Interfaces
{
    public interface IPasswordResetService
    {
        Task<bool> SendPasswordResetEmailAsync(string email, string? ipAddress = null, string? userAgent = null);
        Task<bool> ResetPasswordAsync(string token, string newPassword);
        Task<bool> ValidateResetTokenAsync(string token);
        Task CleanupExpiredTokensAsync();
    }
}