using System.Net.Http;
using System.Text.Json;

namespace PocketFence_Simple.Services.AI;

public sealed class AutoUpdateService(
    ILogger<AutoUpdateService> logger,
    IHttpClientFactory httpClientFactory) : IDisposable
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient("UpdateService");
    private readonly PeriodicTimer _updateCheckTimer = new(TimeSpan.FromHours(6)); // Check every 6 hours
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private Version _currentVersion = new("2.0.0");

    public event EventHandler<AutoUpdateInfo>? UpdateAvailable;
    public event EventHandler<string>? UpdateCompleted;
    public event EventHandler<Exception>? UpdateFailed;

    static AutoUpdateService()
    {
        // Static initialization if needed
    }

    private void ConfigureHttpClient()
    {
        _httpClient.DefaultRequestHeaders.Add("User-Agent", $"PocketFence-Simple/{_currentVersion}");
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    public async ValueTask<bool> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("Checking for updates...");
            
            // Simulate checking GitHub releases API
            var updateInfo = await SimulateUpdateCheckAsync(cancellationToken);
            
            if (updateInfo?.AvailableVersion > _currentVersion)
            {
                logger.LogInformation("Update available: {Version} (Current: {Current})", 
                    updateInfo.AvailableVersion, _currentVersion);
                    
                UpdateAvailable?.Invoke(this, updateInfo);
                return true;
            }

            logger.LogDebug("No updates available. Current version {Version} is up to date", _currentVersion);
            return false;
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Network error checking for updates");
            return false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error checking for updates");
            UpdateFailed?.Invoke(this, ex);
            return false;
        }
    }

    private async ValueTask<AutoUpdateInfo?> SimulateUpdateCheckAsync(CancellationToken cancellationToken)
    {
        // In real implementation, this would call GitHub API or update server
        await Task.Delay(1000, cancellationToken); // Simulate network call
        
        var latestVersion = new Version("2.1.0");
        
        if (latestVersion <= _currentVersion)
            return null;

        return new AutoUpdateInfo
        {
            AvailableVersion = latestVersion,
            CurrentVersion = _currentVersion,
            IsCritical = latestVersion.Major > _currentVersion.Major,
            IsSecurityUpdate = Random.Shared.NextDouble() > 0.5, // Random for demo
            DownloadUrl = $"https://github.com/DJMcClellan1966/PocketFence-Simple/releases/tag/v{latestVersion}",
            ReleaseNotes = GenerateReleaseNotes(latestVersion),
            DownloadSizeBytes = Random.Shared.NextInt64(10_000_000, 50_000_000),
            ReleaseDate = DateTime.UtcNow.AddDays(-Random.Shared.Next(1, 30))
        };
    }

    private static string GenerateReleaseNotes(Version version) => version.Major switch
    {
        > 2 => "Major update with new features:\n• Enhanced AI threat detection\n• Improved performance\n• New parental controls",
        2 when version.Minor > 0 => "Feature update:\n• Bug fixes and stability improvements\n• Enhanced security features\n• Performance optimizations",
        _ => "Patch update:\n• Critical security fixes\n• Bug fixes\n• Minor improvements"
    };

    public async ValueTask<bool> InstallUpdateAsync(AutoUpdateInfo updateInfo, IProgress<UpdateProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("Starting update installation for version {Version}", updateInfo.AvailableVersion);
            
            // Simulate download and installation
            var totalSteps = 5;
            var stepNames = new[] { "Downloading", "Verifying", "Backing up", "Installing", "Finalizing" };
            
            for (int i = 0; i < totalSteps; i++)
            {
                await Task.Delay(Random.Shared.Next(500, 2000), cancellationToken);
                
                var progressValue = (double)(i + 1) / totalSteps;
                progress?.Report(new UpdateProgress
                {
                    Percentage = progressValue * 100,
                    Status = stepNames[i],
                    CurrentStep = i + 1,
                    TotalSteps = totalSteps
                });
                
                logger.LogDebug("Update progress: {Step} ({Percentage:F0}%)", stepNames[i], progressValue * 100);
            }
            
            _currentVersion = updateInfo.AvailableVersion;
            logger.LogInformation("Update installed successfully to version {Version}", _currentVersion);
            
            UpdateCompleted?.Invoke(this, $"Successfully updated to version {_currentVersion}");
            return true;
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Update installation was cancelled");
            return false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error installing update to version {Version}", updateInfo.AvailableVersion);
            UpdateFailed?.Invoke(this, ex);
            return false;
        }
    }

    public async ValueTask<UpdateHistory[]> GetUpdateHistoryAsync(int limit = 10, CancellationToken cancellationToken = default)
    {
        try
        {
            // Simulate getting update history
            await Task.Delay(100, cancellationToken);
            
            return Enumerable.Range(1, limit)
                .Select(i => new UpdateHistory
                {
                    Version = new Version(2, 0, i),
                    InstallDate = DateTime.UtcNow.AddDays(-i * 7),
                    Success = i % 10 != 0, // Simulate occasional failures
                    Notes = $"Update {i} - Various improvements and fixes"
                })
                .OrderByDescending(h => h.InstallDate)
                .ToArray();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving update history");
            return [];
        }
    }

    private async Task StartPeriodicUpdateCheckAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Initial delay before first check
            await Task.Delay(TimeSpan.FromMinutes(5), cancellationToken);
            
            while (await _updateCheckTimer.WaitForNextTickAsync(cancellationToken))
            {
                await CheckForUpdatesAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation is requested
        }
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _updateCheckTimer.Dispose();
        _httpClient.Dispose();
        _cancellationTokenSource.Dispose();
    }
}

public record AutoUpdateInfo
{
    public required Version AvailableVersion { get; init; }
    public required Version CurrentVersion { get; init; }
    public bool IsCritical { get; init; }
    public bool IsSecurityUpdate { get; init; }
    public required string DownloadUrl { get; init; }
    public required string ReleaseNotes { get; init; }
    public long DownloadSizeBytes { get; init; }
    public DateTime ReleaseDate { get; init; }
}

public record UpdateProgress
{
    public double Percentage { get; init; }
    public required string Status { get; init; }
    public int CurrentStep { get; init; }
    public int TotalSteps { get; init; }
}

public record UpdateHistory
{
    public required Version Version { get; init; }
    public DateTime InstallDate { get; init; }
    public bool Success { get; init; }
    public required string Notes { get; init; }
}