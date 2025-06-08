using AuthService.Models;
using AuthService.DTOs;

namespace AuthService.Services.Interfaces
{
    public interface IUserService
    {
        /// <summary>
        /// Registers a new user with email and password
        /// </summary>
        Task<User?> RegisterWithEmailAsync(string email, string password);

        /// <summary>
        /// Registers a new user with phone number and sends OTP
        /// </summary>
        Task<bool> RegisterWithPhoneAsync(string phoneNumber);

        /// <summary>
        /// Authenticates user with email and password
        /// </summary>
        Task<User?> AuthenticateWithEmailAsync(string email, string password);

        /// <summary>
        /// Initiates phone login by sending OTP
        /// </summary>
        Task<bool> InitiatePhoneLoginAsync(string phoneNumber);

        /// <summary>
        /// Verifies OTP and returns user if valid
        /// </summary>
        Task<User?> VerifyOtpAsync(string phoneNumber, string otpCode);

        /// <summary>
        /// Gets user by ID
        /// </summary>
        Task<User?> GetUserByIdAsync(int userId);

        /// <summary>
        /// Gets user by email
        /// </summary>
        Task<User?> GetUserByEmailAsync(string email);

        /// <summary>
        /// Gets user by phone number
        /// </summary>
        Task<User?> GetUserByPhoneAsync(string phoneNumber);

        /// <summary>
        /// Gets user information by ID
        /// </summary>
        Task<UserInfo?> GetUserInfoAsync(int userId);

        /// <summary>
        /// Updates user's last login time
        /// </summary>
        Task<bool> UpdateLastLoginAsync(int userId);

        /// <summary>
        /// Updates user's email verification status
        /// </summary>
        Task<bool> UpdateEmailVerificationAsync(int userId, bool isVerified);

        /// <summary>
        /// Updates user's phone verification status
        /// </summary>
        Task<bool> UpdatePhoneVerificationAsync(int userId, bool isVerified);

        /// <summary>
        /// Deactivates a user account
        /// </summary>
        Task<bool> DeactivateUserAsync(int userId);

        /// <summary>
        /// Activates a user account
        /// </summary>
        Task<bool> ActivateUserAsync(int userId);

        /// <summary>
        /// Checks if email is already in use
        /// </summary>
        Task<bool> IsEmailInUseAsync(string email);

        /// <summary>
        /// Checks if phone number is already in use
        /// </summary>
        Task<bool> IsPhoneInUseAsync(string phoneNumber);

        /// <summary>
        /// Gets all users
        /// </summary>
        Task<List<User>> GetAllUsersAsync();

        /// <summary>
        /// Gets count of active users
        /// </summary>
        Task<int> GetActiveUserCountAsync();
    }
}