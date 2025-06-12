using AuthService.Services.Interfaces;

namespace AuthService.Services
{
    public class PasswordResetCleanupService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<PasswordResetCleanupService> _logger;
        private readonly TimeSpan _cleanupInterval = TimeSpan.FromHours(6); // Run cleanup every 6 hours

        public PasswordResetCleanupService(
            IServiceProvider serviceProvider, 
            ILogger<PasswordResetCleanupService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Password reset token cleanup service started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var passwordResetService = scope.ServiceProvider.GetRequiredService<IPasswordResetService>();
                    
                    await passwordResetService.CleanupExpiredTokensAsync();
                    
                    await Task.Delay(_cleanupInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // Expected when cancellation is requested
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred during password reset token cleanup");
                    // Wait a shorter interval before retrying on error
                    await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
                }
            }

            _logger.LogInformation("Password reset token cleanup service stopped");
        }
    }
}