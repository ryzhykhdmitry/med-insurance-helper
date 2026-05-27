using MedInsuranceHelper.Api.Services;

namespace MedInsuranceHelper.Api.Workers;

/// <summary>
/// Background service that periodically removes expired conversation sessions.
/// Runs every 10 minutes to clean up sessions where ExpiresAt &lt; UtcNow.
/// </summary>
public class SessionCleanupWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SessionCleanupWorker> _logger;
    
    private const int CleanupIntervalMinutes = 10;

    public SessionCleanupWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<SessionCleanupWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SessionCleanupWorker started. Running every {Interval} minutes.", CleanupIntervalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Wait for the interval before running cleanup
                await Task.Delay(TimeSpan.FromMinutes(CleanupIntervalMinutes), stoppingToken);

                // Create a scope to get the session service
                using var scope = _scopeFactory.CreateScope();
                var sessionService = scope.ServiceProvider.GetRequiredService<ISessionService>();

                _logger.LogInformation("Running session cleanup...");
                sessionService.RemoveExpiredSessions();
            }
            catch (OperationCanceledException)
            {
                // Expected when stopping
                _logger.LogInformation("SessionCleanupWorker stopping.");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during session cleanup.");
            }
        }
    }
}
