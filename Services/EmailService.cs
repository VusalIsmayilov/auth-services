using AuthService.Services.Interfaces;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;
using System.Text;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace AuthService.Services;

public class EmailSettings
{
    public string Provider { get; set; } = "SMTP"; // "SMTP" or "SendGrid"
    public string SendGridApiKey { get; set; } = string.Empty;
    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; }
    public string SmtpUsername { get; set; } = string.Empty;
    public string SmtpPassword { get; set; } = string.Empty;
    public string FromEmail { get; set; } = string.Empty;
    public string FromName { get; set; } = string.Empty;
    public bool EnableSsl { get; set; } = true;
    public string BaseUrl { get; set; } = string.Empty;
}

public class EmailService : IEmailService
{
    private readonly EmailSettings _emailSettings;
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IOptions<EmailSettings> emailSettings, IConfiguration configuration, ILogger<EmailService> logger)
    {
        _emailSettings = emailSettings.Value;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<bool> SendVerificationEmailAsync(string email, string token, string userName = "")
    {
        try
        {
            var subject = "Verify Your Email Address";
            var frontendBaseUrl = _configuration["Frontend:BaseUrl"] ?? _emailSettings.BaseUrl;
            var verificationUrl = $"{frontendBaseUrl}/email-verification?token={token}";
            
            var body = GetVerificationEmailTemplate(userName.IsNullOrEmpty() ? email : userName, verificationUrl);
            
            await SendEmailAsync(email, subject, body);
            
            _logger.LogInformation("Verification email sent successfully to {Email}", email);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send verification email to {Email}", email);
            return false;
        }
    }

    public async Task<bool> SendPasswordResetEmailAsync(string email, string token, string userName = "")
    {
        try
        {
            var subject = "Reset Your Password";
            var frontendBaseUrl = _configuration["Frontend:BaseUrl"] ?? _emailSettings.BaseUrl;
            var resetUrl = $"{frontendBaseUrl}/reset-password?token={token}";
            
            var body = GetPasswordResetEmailTemplate(userName.IsNullOrEmpty() ? email : userName, resetUrl);
            
            await SendEmailAsync(email, subject, body);
            
            _logger.LogInformation("Password reset email sent successfully to {Email}", email);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send password reset email to {Email}", email);
            return false;
        }
    }

    public async Task<bool> SendWelcomeEmailAsync(string email, string userName = "")
    {
        try
        {
            var subject = "Welcome to Our Service!";
            var body = GetWelcomeEmailTemplate(userName.IsNullOrEmpty() ? email : userName);
            
            await SendEmailAsync(email, subject, body);
            
            _logger.LogInformation("Welcome email sent successfully to {Email}", email);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send welcome email to {Email}", email);
            return false;
        }
    }

    private async Task SendEmailAsync(string toEmail, string subject, string body)
    {
        switch (_emailSettings.Provider.ToUpper())
        {
            case "SENDGRID":
                await SendEmailViaSendGridAsync(toEmail, subject, body);
                break;
            case "SMTP":
                await SendEmailViaSmtpAsync(toEmail, subject, body);
                break;
            default:
                // If no provider is configured, simulate email sending (for development)
                _logger.LogInformation("EMAIL SIMULATION: To: {Email}, Subject: {Subject}", toEmail, subject);
                _logger.LogInformation("EMAIL CONTENT: {Body}", body);
                break;
        }
    }

    private async Task SendEmailViaSendGridAsync(string toEmail, string subject, string body)
    {
        if (string.IsNullOrEmpty(_emailSettings.SendGridApiKey))
        {
            _logger.LogWarning("SendGrid API key is not configured. Falling back to email simulation.");
            _logger.LogInformation("EMAIL SIMULATION (SendGrid): To: {Email}, Subject: {Subject}", toEmail, subject);
            _logger.LogInformation("EMAIL CONTENT: {Body}", body);
            return;
        }

        try
        {
            var client = new SendGridClient(_emailSettings.SendGridApiKey);
            var from = new EmailAddress(_emailSettings.FromEmail, _emailSettings.FromName);
            var to = new EmailAddress(toEmail);
            
            var msg = MailHelper.CreateSingleEmail(from, to, subject, null, body);
            
            var response = await client.SendEmailAsync(msg);
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Email sent successfully via SendGrid to {Email}", toEmail);
            }
            else
            {
                var responseBody = await response.Body.ReadAsStringAsync();
                _logger.LogError("Failed to send email via SendGrid. Status: {StatusCode}, Response: {Response}", 
                    response.StatusCode, responseBody);
                throw new Exception($"SendGrid API returned status code: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending email via SendGrid to {Email}", toEmail);
            throw;
        }
    }

    private async Task SendEmailViaSmtpAsync(string toEmail, string subject, string body)
    {
        // If SMTP is not configured, simulate email sending (for development)
        if (string.IsNullOrEmpty(_emailSettings.SmtpHost))
        {
            _logger.LogInformation("EMAIL SIMULATION (SMTP): To: {Email}, Subject: {Subject}", toEmail, subject);
            _logger.LogInformation("EMAIL CONTENT: {Body}", body);
            return;
        }

        try
        {
            using var client = new SmtpClient(_emailSettings.SmtpHost, _emailSettings.SmtpPort);
            client.EnableSsl = _emailSettings.EnableSsl;
            client.UseDefaultCredentials = false;
            client.Credentials = new NetworkCredential(_emailSettings.SmtpUsername, _emailSettings.SmtpPassword);

            using var message = new MailMessage();
            message.From = new MailAddress(_emailSettings.FromEmail, _emailSettings.FromName);
            message.To.Add(toEmail);
            message.Subject = subject;
            message.Body = body;
            message.IsBodyHtml = true;

            await client.SendMailAsync(message);
            _logger.LogInformation("Email sent successfully via SMTP to {Email}", toEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending email via SMTP to {Email}", toEmail);
            throw;
        }
    }

    private static string GetVerificationEmailTemplate(string userName, string verificationUrl)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>Email Verification</title>
</head>
<body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
    <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
        <h2 style='color: #4CAF50;'>Email Verification Required</h2>
        
        <p>Hello {userName},</p>
        
        <p>Thank you for registering with our service! To complete your registration, please verify your email address by clicking the button below:</p>
        
        <div style='text-align: center; margin: 30px 0;'>
            <a href='{verificationUrl}' 
               style='background-color: #4CAF50; color: white; padding: 12px 30px; text-decoration: none; border-radius: 5px; display: inline-block;'>
                Verify Email Address
            </a>
        </div>
        
        <p>If the button doesn't work, you can also copy and paste the following link into your browser:</p>
        <p style='word-break: break-all; color: #666;'>{verificationUrl}</p>
        
        <p><strong>This verification link will expire in 24 hours.</strong></p>
        
        <p>If you didn't create an account with us, please ignore this email.</p>
        
        <hr style='border: none; border-top: 1px solid #eee; margin: 30px 0;'>
        <p style='font-size: 12px; color: #666;'>
            This is an automated message, please do not reply to this email.
        </p>
    </div>
</body>
</html>";
    }

    private static string GetPasswordResetEmailTemplate(string userName, string resetUrl)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>Password Reset</title>
</head>
<body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
    <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
        <h2 style='color: #FF6B6B;'>Password Reset Request</h2>
        
        <p>Hello {userName},</p>
        
        <p>We received a request to reset your password. Click the button below to create a new password:</p>
        
        <div style='text-align: center; margin: 30px 0;'>
            <a href='{resetUrl}' 
               style='background-color: #FF6B6B; color: white; padding: 12px 30px; text-decoration: none; border-radius: 5px; display: inline-block;'>
                Reset Password
            </a>
        </div>
        
        <p>If the button doesn't work, you can also copy and paste the following link into your browser:</p>
        <p style='word-break: break-all; color: #666;'>{resetUrl}</p>
        
        <p><strong>This reset link will expire in 24 hours.</strong></p>
        
        <p>If you didn't request a password reset, please ignore this email. Your password will remain unchanged.</p>
        
        <hr style='border: none; border-top: 1px solid #eee; margin: 30px 0;'>
        <p style='font-size: 12px; color: #666;'>
            This is an automated message, please do not reply to this email.
        </p>
    </div>
</body>
</html>";
    }

    private static string GetWelcomeEmailTemplate(string userName)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>Welcome!</title>
</head>
<body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
    <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
        <h2 style='color: #4CAF50;'>Welcome to Our Service!</h2>
        
        <p>Hello {userName},</p>
        
        <p>Welcome! Your email has been successfully verified and your account is now active.</p>
        
        <p>You can now enjoy all the features of our service. If you have any questions or need assistance, feel free to contact our support team.</p>
        
        <p>Thank you for choosing our service!</p>
        
        <hr style='border: none; border-top: 1px solid #eee; margin: 30px 0;'>
        <p style='font-size: 12px; color: #666;'>
            This is an automated message, please do not reply to this email.
        </p>
    </div>
</body>
</html>";
    }
}

public static class StringExtensions
{
    public static bool IsNullOrEmpty(this string? value)
    {
        return string.IsNullOrEmpty(value);
    }
}