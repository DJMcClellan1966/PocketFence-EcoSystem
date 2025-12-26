using System.Collections.Frozen;
using System.Text.RegularExpressions;
using PocketFence_Simple.Models;

namespace PocketFence_Simple.Services.AI;

public sealed partial class AIThreatDetectionService(
    ILogger<AIThreatDetectionService> logger) : IDisposable
{
    private readonly ConcurrentDictionary<string, double> _threatScores = [];
    private readonly PeriodicTimer _cleanupTimer = new(TimeSpan.FromHours(1));
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    
    // Compiled regex patterns for better performance
    [GeneratedRegex(@"(hack|crack|malware|virus|trojan|phishing|scam)", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex ThreatContentPattern();
    
    [GeneratedRegex(@"\b(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\b", RegexOptions.Compiled)]
    private static partial Regex IpAddressPattern();

    private static readonly FrozenSet<string> SuspiciousDomains = FrozenSet.ToFrozenSet([
        "malicious.com", "phishing.net", "scam.org", "virus.info",
        "hack.site", "malware.download", "suspicious.link", "badware.evil"
    ]);

    private static readonly FrozenSet<string> SafeDomains = FrozenSet.ToFrozenSet([
        "google.com", "microsoft.com", "github.com", "stackoverflow.com",
        "wikipedia.org", "mozilla.org", "amazon.com"
    ]);

    public event EventHandler<AIThreatAlert>? ThreatDetected;

    static AIThreatDetectionService()
    {
        _ = Task.Run(async () =>
        {
            // Initialize background cleanup
            await Task.CompletedTask;
        });
    }

    public ValueTask<AIThreatAssessment> AnalyzeThreatAsync(string url, string content, ConnectedDevice device, CancellationToken cancellationToken = default)
    {
        var urlSpan = url.AsSpan();
        var threatScore = CalculateThreatScore(urlSpan, content.AsSpan());
        var threatLevel = GetThreatLevel(threatScore);
        
        var assessment = new AIThreatAssessment
        {
            Id = Guid.NewGuid().ToString(),
            Timestamp = DateTime.UtcNow,
            Url = url,
            DeviceId = device.Id,
            DeviceName = device.Name,
            ThreatLevel = threatLevel,
            ThreatScore = threatScore,
            Indicators = CreateThreatIndicators(urlSpan, content.AsSpan(), threatScore)
        };

        // Store for learning (using interned string to reduce memory)
        _threatScores[string.Intern(url)] = threatScore;

        // Trigger alert for significant threats
        if (threatLevel >= ThreatLevel.High)
        {
            TriggerThreatAlert(assessment, device);
        }

        logger.LogDebug("Threat analysis for {Url}: {Level} ({Score:F2})", url, threatLevel, threatScore);
        return ValueTask.FromResult(assessment);
    }

    private List<ThreatIndicator> CreateThreatIndicators(ReadOnlySpan<char> url, ReadOnlySpan<char> content, double threatScore)
    {
        List<ThreatIndicator> indicators = [];

        var urlString = url.ToString();
        
        // Check domains
        if (SuspiciousDomains.Any(domain => urlString.Contains(domain, StringComparison.OrdinalIgnoreCase)))
        {
            indicators.Add(new ThreatIndicator
            {
                Type = "SuspiciousDomain",
                Score = 0.8,
                Description = "Domain matches known threat database",
                Details = ["Domain flagged in security database"]
            });
        }

        // Content analysis
        if (ThreatContentPattern().IsMatch(content))
        {
            indicators.Add(new ThreatIndicator
            {
                Type = "SuspiciousContent",
                Score = 0.6,
                Description = "Content contains threat-related keywords",
                Details = ["Detected malware-related terminology"]
            });
        }

        // Protocol check
        if (url.StartsWith("http:".AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            indicators.Add(new ThreatIndicator
            {
                Type = "InsecureProtocol",
                Score = 0.2,
                Description = "Using insecure HTTP protocol",
                Details = ["HTTPS recommended for security"]
            });
        }

        return indicators;
    }

    private double CalculateThreatScore(ReadOnlySpan<char> url, ReadOnlySpan<char> content)
    {
        var score = 0.0;
        var urlString = url.ToString();

        // Check against known safe domains first (early exit)
        if (SafeDomains.Any(domain => urlString.Contains(domain, StringComparison.OrdinalIgnoreCase)))
            return 0.0;

        // Check against known suspicious domains using optimized lookup
        if (SuspiciousDomains.Any(domain => urlString.Contains(domain, StringComparison.OrdinalIgnoreCase)))
            score += 0.8;

        // Content analysis using compiled regex for better performance
        if (ThreatContentPattern().IsMatch(content))
            score += 0.6;

        // IP address detection (direct IP access can be suspicious)
        if (IpAddressPattern().IsMatch(url))
            score += 0.4;

        // Protocol analysis
        if (url.StartsWith("http:".AsSpan(), StringComparison.OrdinalIgnoreCase))
            score += 0.2;

        // Length-based heuristics
        if (url.Length > 150) // Very long URLs can be suspicious
            score += 0.2;
            
        if (content.Length > 50000 && content.Contains("download".AsSpan(), StringComparison.OrdinalIgnoreCase))
            score += 0.3;

        // Check for URL encoding obfuscation
        if (urlString.Count(c => c == '%') > 5)
            score += 0.2;

        return Math.Min(score, 1.0);
    }

    private static ThreatLevel GetThreatLevel(double score) => score switch
    {
        >= 0.8 => ThreatLevel.Critical,
        >= 0.6 => ThreatLevel.High,
        >= 0.4 => ThreatLevel.Medium,
        >= 0.2 => ThreatLevel.Low,
        _ => ThreatLevel.Minimal
    };

    private void TriggerThreatAlert(AIThreatAssessment assessment, ConnectedDevice device)
    {
        var alert = new AIThreatAlert
        {
            Id = assessment.Id,
            Timestamp = assessment.Timestamp,
            DeviceId = device.Id,
            DeviceName = device.Name,
            Url = assessment.Url,
            ThreatLevel = assessment.ThreatLevel,
            AutonomousAction = assessment.ThreatLevel >= ThreatLevel.Medium ? "Blocked automatically" : "Monitoring",
            ParentalReviewRequired = assessment.ThreatLevel == ThreatLevel.Critical,
            Description = $"AI detected {assessment.ThreatLevel} threat with confidence {assessment.ThreatScore:P0}"
        };

        ThreatDetected?.Invoke(this, alert);
        logger.LogWarning("Threat alert triggered for device {DeviceName}: {ThreatLevel} - {Url}", 
            device.Name, assessment.ThreatLevel, assessment.Url);
    }

    private async Task StartCleanupAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (await _cleanupTimer.WaitForNextTickAsync(cancellationToken))
            {
                CleanupOldScores();
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation is requested
        }
    }

    private void CleanupOldScores()
    {
        var keysToRemove = _threatScores
            .Where(kvp => kvp.Value < 0.1) // Remove low-confidence entries
            .Take(100) // Limit cleanup batch size for performance
            .Select(kvp => kvp.Key)
            .ToArray();
        
        var removedCount = 0;
        foreach (var key in keysToRemove)
        {
            if (_threatScores.TryRemove(key, out _))
                removedCount++;
        }

        if (removedCount > 0)
        {
            logger.LogDebug("Cleaned up {Count} old threat scores", removedCount);
        }
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _cleanupTimer.Dispose();
        _cancellationTokenSource.Dispose();
    }
}