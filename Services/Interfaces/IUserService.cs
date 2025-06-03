using AuthService.DTOs;

namespace AuthService.Services.Interfaces
{
    public interface IUserService
    {
        Task<AuthResponse> RegisterWithEmailAsync(RegisterEmailRequest request);
        Task<AuthResponse> RegisterWithPhoneAsync(RegisterPhoneRequest request);
        Task<AuthResponse> LoginWithEmailAsync(LoginEmailRequest request);
        Task<AuthResponse> LoginWithPhoneAsync(LoginPhoneRequest request);
        Task<AuthResponse> VerifyOtpAndLoginAsync(VerifyOtpRequest request);
        Task<UserInfo?> GetUserByIdAsync(int userId);
    }
}