using Engine.Config;
using Engine.Services.UserCache;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Engine.BackgroundServices;

/// <summary>
/// Periodically warms up the user cache and Copilot stats so that the first
/// interactive request after start-up (or after a long idle) doesn't have to
/// pay the cold-path cost. Both <see cref="IUserCacheManager.SyncUsersAsync"/>
/// and <see cref="IUserCacheManager.UpdateCopilotStatsAsync"/> already self-
/// throttle against their configured TTLs, so calling them on a fixed cadence
/// is cheap when the cache is fresh and effective when it is not.
/// </summary>
public class CacheWarmupHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly UserCacheConfig _config;
    private readonly ILogger<CacheWarmupHostedService> _logger;

    /// <summary>
    /// Delay before the very first warm-up attempt, so we don't compete with the
    /// rest of the app's start-up work.
    /// </summary>
    private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(30);

    /// <summary>
    /// How often the warm-up loop runs. The actual refresh work is gated by
    /// the per-operation TTLs in <see cref="UserCacheConfig"/>.
    /// </summary>
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(15);

    public CacheWarmupHostedService(
        IServiceScopeFactory scopeFactory,
        UserCacheConfig config,
        ILogger<CacheWarmupHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Cache warm-up hosted service is starting");

        try
        {
            await Task.Delay(InitialDelay, stoppingToken);
        }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during cache warm-up cycle");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException) { break; }
        }

        _logger.LogInformation("Cache warm-up hosted service is stopping");
    }

    private async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var cacheManager = scope.ServiceProvider.GetRequiredService<IUserCacheManager>();

        var metadata = await cacheManager.GetSyncMetadataAsync();
        cancellationToken.ThrowIfCancellationRequested();

        // User directory: warm up if we have never synced or the last delta is older than CacheExpiration.
        var userCacheStale = metadata.LastDeltaSyncDate == null ||
                             DateTime.UtcNow - metadata.LastDeltaSyncDate.Value > _config.CacheExpiration;

        if (userCacheStale)
        {
            _logger.LogInformation("Warm-up: refreshing user directory cache (last delta {LastDeltaSync})", metadata.LastDeltaSyncDate);
            try
            {
                await cacheManager.SyncUsersAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Warm-up: user directory sync failed; will retry on next cycle");
            }
        }

        cancellationToken.ThrowIfCancellationRequested();

        // Copilot stats: warm up if we have never updated or are past CopilotStatsRefreshInterval.
        var statsStale = metadata.LastCopilotStatsUpdate == null ||
                         DateTime.UtcNow - metadata.LastCopilotStatsUpdate.Value > _config.CopilotStatsRefreshInterval;

        if (statsStale)
        {
            _logger.LogInformation("Warm-up: refreshing Copilot stats (last update {LastStatsUpdate})", metadata.LastCopilotStatsUpdate);
            try
            {
                await cacheManager.UpdateCopilotStatsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Warm-up: Copilot stats refresh failed; will retry on next cycle");
            }
        }
    }
}
