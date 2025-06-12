# SendGrid Email Service Integration

This document explains how to configure and use SendGrid for email delivery in the AuthService.

## Features

The EmailService now supports multiple email providers:
- **SendGrid** - Cloud-based email delivery service
- **SMTP** - Traditional SMTP server (Gmail, Outlook, etc.)
- **Simulation** - Development mode with console logging

## SendGrid Setup

### 1. Create SendGrid Account
1. Go to [SendGrid.com](https://sendgrid.com)
2. Sign up for an account
3. Verify your account and complete the setup

### 2. Generate API Key
1. In SendGrid dashboard, go to Settings > API Keys
2. Click "Create API Key"
3. Choose "Restricted Access" and give it a name
4. Grant the following permissions:
   - Mail Send: Full Access
   - Template Engine: Read Access (if using templates)
5. Copy the generated API key (save it securely!)

### 3. Verify Sender Identity
1. Go to Settings > Sender Authentication
2. Choose "Single Sender Verification" (easiest) or "Domain Authentication" (recommended for production)
3. Add and verify your sender email address
4. This email will be used as the "From" address

## Configuration

### Development (appsettings.Development.json)
```json
{
  "Email": {
    "Provider": "Simulation",
    "FromEmail": "noreply@localhost.dev",
    "FromName": "AuthService Development"
  }
}
```

### Production (appsettings.json)
```json
{
  "Email": {
    "Provider": "SendGrid",
    "SendGridApiKey": "SG.your-api-key-here",
    "FromEmail": "noreply@yourdomain.com",
    "FromName": "Your App Name",
    "BaseUrl": "https://yourdomain.com"
  }
}
```

### Environment Variables (Recommended for Production)
```bash
EMAIL_PROVIDER=SendGrid
SENDGRID_API_KEY=SG.your-api-key-here
EMAIL_FROM=noreply@yourdomain.com
EMAIL_FROM_NAME="Your App Name"
```

## Email Types Supported

The service automatically sends emails for:

1. **Email Verification** (`/api/auth/register/email`)
   - Sent when users register with email
   - Contains verification link
   - 24-hour expiration

2. **Password Reset** (`/api/auth/forgot-password`)
   - Sent when users request password reset
   - Contains reset link pointing to frontend
   - 24-hour expiration

3. **Welcome Email** (when email verification completes)
   - Sent after successful email verification
   - Welcome message

## Testing

### 1. Development Mode
In development, emails are simulated and logged to console:
```
EMAIL SIMULATION: To: user@example.com, Subject: Verify Your Email Address
EMAIL CONTENT: [HTML content]
```

### 2. SendGrid Testing
1. Set up SendGrid with a test API key
2. Use a verified sender email
3. Test with the `/api/auth/forgot-password` endpoint
4. Check SendGrid dashboard for delivery statistics

### 3. SMTP Fallback
If SendGrid fails, you can configure SMTP as fallback:
```json
{
  "Email": {
    "Provider": "SMTP",
    "SmtpHost": "smtp.gmail.com",
    "SmtpPort": 587,
    "SmtpUsername": "your-email@gmail.com",
    "SmtpPassword": "your-app-password",
    "FromEmail": "your-email@gmail.com",
    "FromName": "Your App Name",
    "EnableSsl": true
  }
}
```

## Error Handling

The service includes comprehensive error handling:
- Logs all email attempts
- Falls back to simulation if SendGrid is misconfigured
- Provides detailed error messages for debugging
- Graceful failure (won't crash the application)

## Best Practices

### Security
- Store SendGrid API key in environment variables or Azure Key Vault
- Use restricted API keys with minimal permissions
- Regularly rotate API keys

### Deliverability
- Verify your domain with SendGrid for better deliverability
- Set up SPF, DKIM, and DMARC records
- Monitor your sender reputation
- Handle bounces and unsubscribes properly

### Monitoring
- Check SendGrid dashboard for delivery metrics
- Monitor application logs for email failures
- Set up alerts for high bounce rates

## Troubleshooting

### Common Issues

1. **"Unauthorized" Error**
   - Check if API key is correct
   - Ensure API key has mail send permissions

2. **"From address not verified"**
   - Verify sender email in SendGrid dashboard
   - Use the exact email address that was verified

3. **Emails not being sent**
   - Check application logs for errors
   - Verify SendGrid dashboard for send attempts
   - Ensure Provider is set to "SendGrid"

4. **Template issues**
   - HTML templates are embedded in the service
   - Check console logs for template rendering errors

### Debug Mode
Enable detailed logging by setting log level to Debug:
```json
{
  "Logging": {
    "LogLevel": {
      "AuthService.Services.EmailService": "Debug"
    }
  }
}
```

## Support

For SendGrid-related issues:
- [SendGrid Documentation](https://docs.sendgrid.com/)
- [SendGrid Support](https://support.sendgrid.com/)

For AuthService email integration issues:
- Check application logs
- Review this documentation
- Test with simulation mode first