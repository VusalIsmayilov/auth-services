namespace AuthService.Services.Interfaces;

public interface IEmailService
{
    Task<bool> SendVerificationEmailAsync(string email, string token, string userName = "");
    Task<bool> SendPasswordResetEmailAsync(string email, string token, string userName = "");
    Task<bool> SendWelcomeEmailAsync(string email, string userName = "");
}