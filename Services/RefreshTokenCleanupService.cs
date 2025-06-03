using Microsoft.EntityFrameworkCore;
using AuthService.Data;

namespace AuthService.Services
{
    public class RefreshTokenCleanupService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<RefreshTokenCleanupService> _logger;
        private readonly TimeSpan _cleanupInterval = TimeSpan.FromHours(1); // Run every hour

        public RefreshTokenCleanupService(IServiceProvider serviceProvider, ILogger<RefreshTokenCleanupService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("RefreshToken cleanup service started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CleanupExpiredRefreshTokensAsync();
                    await Task.Delay(_cleanupInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("RefreshToken cleanup service is stopping");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred during RefreshToken cleanup");
                    // Continue the service even if cleanup fails
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken); // Wait 5 minutes before retry
                }
            }

            _logger.LogInformation("RefreshToken cleanup service stopped");
        }

        private async Task CleanupExpiredRefreshTokensAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AuthDbContext>();

            try
            {
                var cutoffDate = DateTime.UtcNow.AddDays(-30); // Keep for 30 days after expiration for audit purposes

                var expiredTokens = await context.RefreshTokens
                    .Where(rt => rt.ExpiresAt < cutoffDate ||
                                (rt.IsRevoked && rt.RevokedAt < cutoffDate))
                    .ToListAsync();

                if (expiredTokens.Any())
                {
                    context.RefreshTokens.RemoveRange(expiredTokens);
                    var deletedCount = await context.SaveChangesAsync();

                    _logger.LogInformation("Cleaned up {Count} expired refresh tokens", deletedCount);
                }
                else
                {
                    _logger.LogDebug("No expired refresh tokens found for cleanup");
                }

                // Also log some statistics
                await LogRefreshTokenStatisticsAsync(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cleanup expired refresh tokens");
                throw;
            }
        }

        private async Task LogRefreshTokenStatisticsAsync(AuthDbContext context)
        {
            try
            {
                var stats = await context.RefreshTokens
                    .GroupBy(rt => 1)
                    .Select(g => new
                    {
                        Total = g.Count(),
                        Active = g.Count(rt => rt.IsActive),
                        Expired = g.Count(rt => rt.IsExpired && !rt.IsRevoked),
                        Revoked = g.Count(rt => rt.IsRevoked)
                    })
                    .FirstOrDefaultAsync();

                if (stats != null)
                {
                    _logger.LogDebug("RefreshToken Statistics - Total: {Total}, Active: {Active}, Expired: {Expired}, Revoked: {Revoked}",
                        stats.Total, stats.Active, stats.Expired, stats.Revoked);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to log refresh token statistics");
            }
        }

        public override async Task StopAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("RefreshToken cleanup service is stopping...");
            await base.StopAsync(stoppingToken);
        }
    }
}