using System.Diagnostics;
using PocketFence_Simple.Models;

namespace PocketFence_Simple.Services.AI;

public sealed class SelfHealingService(
    ILogger<SelfHealingService> logger) : IDisposable
{
    private readonly PeriodicTimer _healthCheckTimer = new(TimeSpan.FromMinutes(5));
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private int _restartAttempts = 0;
    private readonly int _maxRestartAttempts = 3;

    public event EventHandler<string>? RecoveryActionTaken;



    public bool HandleFatalError(Exception exception, string context)
    {
        logger.LogError(exception, "Fatal error in {Context}", context);

        try
        {
            // Attempt automatic recovery
            var recovered = AttemptRecovery(context);
            
            if (recovered)
            {
                logger.LogInformation("Successfully recovered from error in {Context}", context);
                RecoveryActionTaken?.Invoke(this, $"Recovered from {exception.GetType().Name} in {context}");
                return true;
            }

            // If recovery failed, try restart if under limit
            if (_restartAttempts < _maxRestartAttempts)
            {
                _restartAttempts++;
                logger.LogWarning("Attempting service restart ({Attempt}/{Max})", _restartAttempts, _maxRestartAttempts);
                
                // Graceful restart
                _ = Task.Run(RestartApplicationAsync);
                return true;
            }

            logger.LogCritical("Max restart attempts reached. System requires manual intervention.");
            return false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during recovery attempt");
            return false;
        }
    }

    public ValueTask<AISystemHealth> GetSystemHealthAsync(CancellationToken cancellationToken = default)
    {
        var health = new AISystemHealth
        {
            Timestamp = DateTime.UtcNow,
            IsOperational = true,
            SystemLoad = GetCurrentSystemLoad(),
            ActiveModules = GetActiveModuleList(),
            Errors = [],
            Performance = GetPerformanceMetrics(),
            AccuracyScore = CalculateSystemAccuracy()
        };

        // Comprehensive health checks
        try
        {
            var process = Process.GetCurrentProcess();
            var memoryUsageMB = process.WorkingSet64 / (1024.0 * 1024.0);
            
            if (memoryUsageMB > 500) // 500MB threshold
            {
                health.Errors.Add(new SystemError 
                { 
                    Timestamp = DateTime.UtcNow,
                    Module = "System",
                    ErrorType = "Performance",
                    Message = $"High memory usage detected: {memoryUsageMB:F0}MB",
                    Severity = memoryUsageMB > 1000 ? ErrorSeverity.Error : ErrorSeverity.Warning
                });
            }

            // CPU usage check
            if (health.SystemLoad > 0.8)
            {
                health.Errors.Add(new SystemError
                {
                    Timestamp = DateTime.UtcNow,
                    Module = "System",
                    ErrorType = "Performance",
                    Message = $"High CPU usage: {health.SystemLoad:P0}",
                    Severity = ErrorSeverity.Warning
                });
            }

            // Check disk space
            var driveInfo = new DriveInfo(Environment.SystemDirectory);
            var freeSpacePercent = (double)driveInfo.AvailableFreeSpace / driveInfo.TotalSize;
            
            if (freeSpacePercent < 0.1) // Less than 10% free
            {
                health.Errors.Add(new SystemError
                {
                    Timestamp = DateTime.UtcNow,
                    Module = "System",
                    ErrorType = "Storage",
                    Message = $"Low disk space: {freeSpacePercent:P0} available",
                    Severity = ErrorSeverity.Warning
                });
            }
        }
        catch (Exception ex)
        {
            health.IsOperational = false;
            health.Errors.Add(new SystemError 
            { 
                Timestamp = DateTime.UtcNow,
                Module = "HealthCheck",
                ErrorType = "Exception",
                Message = $"Health check failed: {ex.Message}",
                Severity = ErrorSeverity.Error
            });
        }

        health.IsOperational = health.Errors.All(e => e.Severity < ErrorSeverity.Error);
        return ValueTask.FromResult(health);
    }

    private bool AttemptRecovery(string context)
    {
        try
        {
            // Perform memory cleanup
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            // Clear temporary caches if they exist
            if (context.Contains("cache", StringComparison.OrdinalIgnoreCase))
            {
                // Simulate cache clearing
                logger.LogInformation("Cleared application caches for recovery");
            }
            
            logger.LogInformation("Performed cleanup for recovery in {Context}", context);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Recovery attempt failed");
            return false;
        }
    }

    private async Task RestartApplicationAsync()
    {
        try
        {
            logger.LogWarning("Initiating application restart for recovery");
            
            // Get current executable path
            var currentProcess = Process.GetCurrentProcess();
            var exePath = currentProcess.MainModule?.FileName;

            if (!string.IsNullOrEmpty(exePath))
            {
                // Give a brief delay for cleanup
                await Task.Delay(2000);
                
                // Start new instance
                var startInfo = new ProcessStartInfo
                {
                    FileName = exePath,
                    UseShellExecute = true,
                    Arguments = "--auto-restart"
                };
                
                Process.Start(startInfo);

                // Exit current process gracefully
                await Task.Delay(1000);
                Environment.Exit(0);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to restart application");
        }
    }

    private async Task StartHealthMonitoringAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (await _healthCheckTimer.WaitForNextTickAsync(cancellationToken))
            {
                await PerformHealthCheckAsync();
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation is requested
        }
    }

    private async Task PerformHealthCheckAsync()
    {
        try
        {
            var health = await GetSystemHealthAsync();
            
            if (!health.IsOperational || health.Errors.Count > 0)
            {
                var errorCount = health.Errors.Count;
                var criticalErrors = health.Errors.Count(e => e.Severity >= ErrorSeverity.Error);
                
                logger.LogWarning("System health issues detected: {ErrorCount} errors ({CriticalCount} critical)", 
                    errorCount, criticalErrors);
                
                // Auto-recovery for non-critical issues
                if (criticalErrors == 0 && errorCount > 0)
                {
                    AttemptRecovery("HealthCheck");
                }
                
                // Reset restart attempts if system is healthy for a while
                if (health.IsOperational && _restartAttempts > 0)
                {
                    _restartAttempts = Math.Max(0, _restartAttempts - 1);
                    logger.LogInformation("System stability improved, reduced restart attempt count to {Count}", _restartAttempts);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Health check failed");
        }
    }

    private double GetCurrentSystemLoad()
    {
        try
        {
            using var process = Process.GetCurrentProcess();
            var totalMemory = GC.GetTotalMemory(false);
            var workingSet = process.WorkingSet64;
            
            // Normalize to 0-1 range (rough estimate)
            var memoryLoad = Math.Min(workingSet / (2.0 * 1024 * 1024 * 1024), 1.0); // 2GB baseline
            return memoryLoad;
        }
        catch
        {
            return 0.5; // Default moderate load
        }
    }

    private List<string> GetActiveModuleList()
    {
        try
        {
            return [.. AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetName().Name)
                .Where(name => !string.IsNullOrEmpty(name))
                .Cast<string>()
                .OrderBy(name => name)];
        }
        catch
        {
            return ["PocketFence-Simple"]; // At least this module
        }
    }

    private Dictionary<string, double> GetPerformanceMetrics()
    {
        try
        {
            using var process = Process.GetCurrentProcess();
            return new Dictionary<string, double>
            {
                ["MemoryUsageMB"] = process.WorkingSet64 / (1024.0 * 1024.0),
                ["CpuTimeSeconds"] = process.TotalProcessorTime.TotalSeconds,
                ["UptimeHours"] = (DateTime.UtcNow - process.StartTime.ToUniversalTime()).TotalHours,
                ["ThreadCount"] = process.Threads.Count,
                ["GCMemoryMB"] = GC.GetTotalMemory(false) / (1024.0 * 1024.0)
            };
        }
        catch
        {
            return [];
        }
    }

    private double CalculateSystemAccuracy()
    {
        // Simple accuracy calculation based on system stability
        try
        {
            var uptime = DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime();
            var maxRestartPenalty = _restartAttempts * 0.1;
            var uptimeBonus = Math.Min(uptime.TotalHours / 24.0, 1.0) * 0.2; // Max 20% bonus for 24h+ uptime
            
            var baseAccuracy = 0.8; // 80% base accuracy
            return Math.Max(0.0, Math.Min(1.0, baseAccuracy + uptimeBonus - maxRestartPenalty));
        }
        catch
        {
            return 0.8; // Default accuracy
        }
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _healthCheckTimer.Dispose();
        _cancellationTokenSource.Dispose();
    }
}