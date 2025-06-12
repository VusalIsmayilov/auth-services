# Gmail SMTP Email Service Setup

This document explains how to configure Gmail SMTP for email delivery in the AuthService.

## Gmail SMTP Configuration

The AuthService is now configured to use Gmail SMTP for sending emails. This provides a reliable email delivery service using your Gmail account.

## Prerequisites

### 1. Gmail Account Setup
1. You need a Gmail account to use as the sender
2. Enable 2-Factor Authentication on your Gmail account
3. Generate an App Password (not your regular Gmail password)

### 2. Generate Gmail App Password
1. Go to your [Google Account settings](https://myaccount.google.com/)
2. Navigate to Security → 2-Step Verification
3. Scroll down to "App passwords"
4. Select "Mail" and your device
5. Copy the generated 16-character password (this is your SMTP password)

## Configuration

### Development Environment (appsettings.Development.json)
```json
{
  "Email": {
    "Provider": "Simulation",
    "SmtpHost": "",
    "SmtpPort": 587,
    "SmtpUsername": "",
    "SmtpPassword": "",
    "FromEmail": "noreply@localhost.dev",
    "FromName": "AuthService Development",
    "EnableSsl": true
  }
}
```

### Production Environment (appsettings.json)
```json
{
  "Email": {
    "Provider": "SMTP",
    "SmtpHost": "smtp.gmail.com",
    "SmtpPort": 587,
    "SmtpUsername": "your-email@gmail.com",
    "SmtpPassword": "your-gmail-app-password",
    "FromEmail": "your-email@gmail.com",
    "FromName": "AuthService",
    "EnableSsl": true,
    "BaseUrl": "http://localhost:5000"
  }
}
```

### Environment Variables (Recommended for Production)
```bash
EMAIL_PROVIDER=SMTP
SMTP_HOST=smtp.gmail.com
SMTP_PORT=587
SMTP_USERNAME=your-email@gmail.com
SMTP_PASSWORD=your-gmail-app-password
EMAIL_FROM=your-email@gmail.com
EMAIL_FROM_NAME="Your App Name"
ENABLE_SSL=true
```

## Gmail SMTP Settings

| Setting | Value |
|---------|-------|
| **SMTP Server** | smtp.gmail.com |
| **Port** | 587 (TLS) or 465 (SSL) |
| **Security** | TLS/SSL enabled |
| **Authentication** | Required |
| **Username** | Your full Gmail address |
| **Password** | Gmail App Password (16 characters) |

## Email Types Sent

The service sends the following types of emails:

### 1. Email Verification
- **Trigger**: User registers with email (`/api/auth/register/email`)
- **Content**: Verification link with 24-hour expiration
- **Action**: Click link to verify email address

### 2. Password Reset
- **Trigger**: User requests password reset (`/api/auth/forgot-password`)
- **Content**: Password reset link pointing to frontend
- **Action**: Click link to reset password (24-hour expiration)

### 3. Welcome Email
- **Trigger**: Email verification completed successfully
- **Content**: Welcome message confirming account activation

## Testing the Configuration

### 1. Development Mode (Simulation)
In development, emails are logged to console instead of being sent:
```
EMAIL SIMULATION (SMTP): To: user@example.com, Subject: Verify Your Email Address
EMAIL CONTENT: [HTML content]
```

### 2. Production Testing
1. Configure Gmail SMTP with real credentials
2. Test with password reset endpoint:
```bash
curl -X POST http://localhost:5000/api/auth/forgot-password \
  -H "Content-Type: application/json" \
  -d '{"email": "test@example.com"}'
```
3. Check Gmail "Sent" folder for outgoing emails
4. Monitor application logs for success/error messages

## Security Best Practices

### App Password Security
- **Never use your main Gmail password** in applications
- Store App Passwords securely (environment variables, Azure Key Vault)
- Regenerate App Passwords periodically
- Revoke unused App Passwords immediately

### Email Content Security
- All emails use HTML templates with proper encoding
- No sensitive data is included in email content
- Links expire after 24 hours
- Reset tokens are cryptographically secure

### Gmail Account Security
- Enable 2-Factor Authentication (required for App Passwords)
- Monitor "Recent security activity" in Google Account
- Use a dedicated Gmail account for application emails
- Review and audit App Passwords regularly

## Troubleshooting

### Common Issues

1. **"Authentication failed" Error**
   ```
   Error: The SMTP server requires a secure connection or the client was not authenticated
   ```
   **Solution**: 
   - Ensure 2FA is enabled on Gmail
   - Use App Password (not regular password)
   - Verify SMTP settings are correct

2. **"Less secure app access" Error**
   ```
   Error: Please log in via your web browser
   ```
   **Solution**: 
   - This error occurs when using regular password
   - Switch to App Password authentication
   - Ensure EnableSsl is set to true

3. **"Daily sending quota exceeded"**
   ```
   Error: 550 Daily sending quota exceeded
   ```
   **Solution**: 
   - Gmail has daily sending limits (500 emails/day for consumer accounts)
   - Consider upgrading to Google Workspace for higher limits
   - Implement rate limiting in your application

4. **SSL/TLS Connection Issues**
   ```
   Error: Unable to connect to the remote server
   ```
   **Solution**: 
   - Verify EnableSsl is set to true
   - Check firewall settings (port 587 or 465)
   - Ensure server has internet connectivity

### Debug Mode
Enable detailed SMTP logging:
```json
{
  "Logging": {
    "LogLevel": {
      "AuthService.Services.EmailService": "Debug",
      "System.Net.Mail": "Debug"
    }
  }
}
```

### Gmail Sending Limits

| Account Type | Daily Limit | Per Minute |
|--------------|-------------|------------|
| **Gmail (Free)** | 500 emails | 10 emails |
| **Google Workspace** | 2,000 emails | 20 emails |

## Alternative SMTP Providers

If Gmail limits are insufficient, consider these alternatives:

### Outlook/Hotmail SMTP
```json
{
  "SmtpHost": "smtp-mail.outlook.com",
  "SmtpPort": 587,
  "EnableSsl": true
}
```

### Yahoo SMTP
```json
{
  "SmtpHost": "smtp.mail.yahoo.com",
  "SmtpPort": 587,
  "EnableSsl": true
}
```

### Custom SMTP Server
```json
{
  "SmtpHost": "mail.yourdomain.com",
  "SmtpPort": 587,
  "EnableSsl": true
}
```

## Monitoring and Maintenance

### Application Logs
Monitor these log entries:
- `Email sent successfully via SMTP to {Email}` - Success
- `Error sending email via SMTP to {Email}` - Failure
- `EMAIL SIMULATION (SMTP)` - Development mode

### Gmail Account Monitoring
- Check "Sent" folder for outgoing emails
- Monitor "Recent security activity"
- Review App Password usage
- Watch for bounce/error emails

### Performance Considerations
- Gmail SMTP has rate limits - implement queuing for high volume
- Use connection pooling for better performance
- Consider async sending for better user experience
- Monitor response times and failure rates

## Support

For Gmail SMTP issues:
- [Gmail SMTP Documentation](https://support.google.com/mail/answer/7126229)
- [Google Account Help](https://support.google.com/accounts)
- [App Passwords Help](https://support.google.com/accounts/answer/185833)

For AuthService email integration:
- Check application logs
- Review this documentation
- Test with simulation mode first
- Verify Gmail account setup