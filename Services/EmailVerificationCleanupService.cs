using AuthService.Services.Interfaces;

namespace AuthService.Services;

public class EmailVerificationCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<EmailVerificationCleanupService> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromHours(6); // Run every 6 hours

    public EmailVerificationCleanupService(
        IServiceProvider serviceProvider,
        ILogger<EmailVerificationCleanupService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Email verification cleanup service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupExpiredTokensAsync();
                await Task.Delay(_interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Email verification cleanup service is stopping");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during email verification cleanup");
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken); // Wait 5 minutes before retrying
            }
        }

        _logger.LogInformation("Email verification cleanup service stopped");
    }

    private async Task CleanupExpiredTokensAsync()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var emailVerificationService = scope.ServiceProvider.GetRequiredService<IEmailVerificationService>();
            
            await emailVerificationService.CleanupExpiredTokensAsync();
            
            _logger.LogInformation("Email verification cleanup completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during email verification cleanup");
            throw;
        }
    }
}