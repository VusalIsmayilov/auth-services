using AuthService.DTOs;

namespace AuthService.Services.Interfaces
{
    public interface IOtpService
    {
        Task<OtpResponse> GenerateOtpAsync(string phoneNumber);
        Task<bool> ValidateOtpAsync(string phoneNumber, string otpCode);
        Task<bool> SendOtpAsync(string phoneNumber, string otpCode);
        Task CleanupExpiredOtpsAsync();
    }
}