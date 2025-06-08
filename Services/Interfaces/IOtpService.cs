using AuthService.DTOs;
using System.Threading.Tasks;


namespace AuthService.Services.Interfaces
{
    public interface IOtpService
    {
        /// <summary>
        /// Sends OTP to phone number (generates OTP internally)
        /// </summary>
        /// <param name="phoneNumber">Phone number to send OTP to</param>
        /// <returns>OTP result with success status and expiry time</returns>
        Task<OtpResult> SendOtpAsync(string phoneNumber);

        /// <summary>
        /// Verifies OTP for a phone number
        /// </summary>
        /// <param name="phoneNumber">Phone number</param>
        /// <param name="otpCode">OTP code to verify</param>
        /// <returns>True if OTP is valid, false otherwise</returns>
        Task<bool> VerifyOtpAsync(string phoneNumber, string otpCode);

        /// <summary>
        /// Checks if phone number can receive OTP (rate limiting)
        /// </summary>
        /// <param name="phoneNumber">Phone number to check</param>
        /// <returns>True if OTP can be sent, false if rate limited</returns>
        Task<bool> CanSendOtpAsync(string phoneNumber);

        /// <summary>
        /// Gets remaining OTP attempts for phone number
        /// </summary>
        /// <param name="phoneNumber">Phone number</param>
        /// <returns>Number of remaining attempts</returns>
        Task<int> GetRemainingAttemptsAsync(string phoneNumber);


        Task CleanupExpiredOtpsAsync();
    }
}