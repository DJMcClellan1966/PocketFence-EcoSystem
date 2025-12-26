using PocketFence_Simple.Models;
using System.Collections.Frozen;
using System.Text.Json;

namespace PocketFence_Simple.Services.AI;

public sealed class AIParentalAssistantService(
    ILogger<AIParentalAssistantService> logger) : IDisposable
{
    private readonly ConcurrentDictionary<string, List<ParentalGuidance>> _guidanceHistory = [];
    private readonly ConcurrentDictionary<string, DeviceUsagePattern> _usagePatterns = [];
    private readonly PeriodicTimer _analysisTimer = new(TimeSpan.FromHours(1));
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    
    private static readonly FrozenSet<string> SafetyTopics = FrozenSet.ToFrozenSet([
        "cyberbullying", "online-predators", "inappropriate-content", "privacy-settings",
        "screen-time", "digital-wellness", "online-gaming", "social-media", "digital-citizenship"
    ]);

    public event EventHandler<ParentalInsight>? InsightGenerated;

    static AIParentalAssistantService()
    {
        // Static initialization if needed
    }

    public ValueTask<ParentalGuidance> GetNavigationHelpAsync(string feature, string? userContext = null, CancellationToken cancellationToken = default)
    {
        var guidance = new ParentalGuidance
        {
            Id = Guid.NewGuid().ToString(),
            Type = GuidanceType.NavigationHelp,
            Topic = feature,
            Content = GetFeatureHelp(feature, userContext),
            Timestamp = DateTime.UtcNow,
            Priority = DetermineGuidancePriority(feature),
            ActionItems = GenerateActionItems(feature),
            RelatedTopics = GetRelatedTopics(feature)
        };

        StoreGuidance("navigation", guidance);
        logger.LogDebug("Provided navigation help for feature: {Feature}", feature);
        
        return ValueTask.FromResult(guidance);
    }

    public async ValueTask<ParentalGuidance> GetUsageInsightsAsync(IReadOnlyList<ConnectedDevice> devices, CancellationToken cancellationToken = default)
    {
        var insights = await AnalyzeDeviceUsageAsync(devices, cancellationToken);
        var recommendations = GenerateRecommendations(insights);
        
        var guidance = new ParentalGuidance
        {
            Id = Guid.NewGuid().ToString(),
            Type = GuidanceType.UsageInsights,
            Topic = "device-usage-analysis",
            Content = FormatUsageInsights(insights),
            Timestamp = DateTime.UtcNow,
            Priority = insights.RiskLevel switch
            {
                RiskLevel.High => GuidancePriority.High,
                RiskLevel.Medium => GuidancePriority.Medium,
                _ => GuidancePriority.Low
            },
            ActionItems = recommendations,
            Metrics = insights.Metrics,
            RelatedTopics = ["screen-time", "digital-wellness", "family-safety"]
        };

        StoreGuidance("insights", guidance);
        
        if (insights.RiskLevel >= RiskLevel.Medium)
        {
            var insight = new ParentalInsight
            {
                Type = "UsageAlert",
                Message = $"Detected {insights.RiskLevel} risk usage patterns",
                Recommendations = recommendations,
                Priority = insights.RiskLevel == RiskLevel.High ? "High" : "Medium"
            };
            
            InsightGenerated?.Invoke(this, insight);
        }

        logger.LogInformation("Generated usage insights for {DeviceCount} devices with {RiskLevel} risk", 
            devices.Count, insights.RiskLevel);
            
        return guidance;
    }

    public ValueTask<IReadOnlyList<string>> GetConversationStartersAsync(ChildProfile profile, CancellationToken cancellationToken = default)
    {
        var starters = profile.AgeGroup switch
        {
            AgeGroup.YoungChild => GetYoungChildStarters(),
            AgeGroup.Preteen => GetPreteenStarters(),
            AgeGroup.Teen => GetTeenStarters(),
            _ => GetGeneralStarters()
        };

        // Personalize based on recent activity
        if (_usagePatterns.TryGetValue(profile.DeviceId, out var pattern))
        {
            starters = [.. starters, .. GetPersonalizedStarters(pattern)];
        }

        logger.LogDebug("Generated {Count} conversation starters for {AgeGroup}", starters.Count, profile.AgeGroup);
        return ValueTask.FromResult<IReadOnlyList<string>>(starters.Take(5).ToArray());
    }

    public ValueTask<SafetyGuidance> GetSafetyGuidanceAsync(string topic, AgeGroup ageGroup, CancellationToken cancellationToken = default)
    {
        if (!SafetyTopics.Contains(topic))
        {
            topic = "digital-citizenship"; // Default fallback
        }

        var guidance = new SafetyGuidance
        {
            Id = Guid.NewGuid().ToString(),
            Topic = topic,
            AgeGroup = ageGroup,
            Content = GetAgeAppropriateContent(topic, ageGroup),
            Tips = GetSafetyTips(topic, ageGroup),
            WarningSignsToWatch = GetWarningSignsForTopic(topic),
            Resources = GetExternalResources(topic),
            Timestamp = DateTime.UtcNow
        };

        logger.LogInformation("Provided safety guidance on {Topic} for {AgeGroup}", topic, ageGroup);
        return ValueTask.FromResult(guidance);
    }

    public ValueTask<IReadOnlyList<ParentalGuidance>> GetGuidanceHistoryAsync(string category, int limit = 10, CancellationToken cancellationToken = default)
    {
        if (_guidanceHistory.TryGetValue(category, out var history))
        {
            var results = history
                .OrderByDescending(g => g.Timestamp)
                .Take(limit)
                .ToArray();
                
            return ValueTask.FromResult<IReadOnlyList<ParentalGuidance>>(results);
        }
        
        return ValueTask.FromResult<IReadOnlyList<ParentalGuidance>>([]);
    }

    private async ValueTask<DeviceUsageAnalysis> AnalyzeDeviceUsageAsync(IReadOnlyList<ConnectedDevice> devices, CancellationToken cancellationToken)
    {
        await Task.Delay(100, cancellationToken); // Simulate analysis time
        
        var totalDevices = devices.Count;
        var blockedCount = devices.Count(d => d.IsBlocked);
        var activeDevices = devices.Count(d => DateTime.UtcNow.Subtract(TimeSpan.FromHours(24)) < DateTime.UtcNow);
        
        var metrics = new Dictionary<string, double>
        {
            ["TotalDevices"] = totalDevices,
            ["BlockedDevices"] = blockedCount,
            ["ActiveDevices24h"] = activeDevices,
            ["BlockedPercentage"] = totalDevices > 0 ? blockedCount / (double)totalDevices * 100 : 0,
            ["ActivityScore"] = CalculateActivityScore(devices)
        };

        var riskLevel = DetermineRiskLevel(metrics);
        
        return new DeviceUsageAnalysis
        {
            TotalDevices = totalDevices,
            BlockedDevices = blockedCount,
            ActiveDevices = activeDevices,
            RiskLevel = riskLevel,
            Metrics = metrics,
            AnalyzedAt = DateTime.UtcNow
        };
    }

    private double CalculateActivityScore(IReadOnlyList<ConnectedDevice> devices)
    {
        if (devices.Count == 0) return 0.0;
        
        var recentlyActive = devices.Count(d => DateTime.UtcNow.Subtract(TimeSpan.FromHours(6)) < DateTime.UtcNow);
        return Math.Min(recentlyActive / (double)devices.Count * 100, 100);
    }

    private RiskLevel DetermineRiskLevel(Dictionary<string, double> metrics)
    {
        var blockedPercentage = metrics["BlockedPercentage"];
        var activityScore = metrics["ActivityScore"];
        
        return (blockedPercentage, activityScore) switch
        {
            (> 50, > 80) => RiskLevel.High,
            (> 25, > 60) => RiskLevel.Medium,
            _ => RiskLevel.Low
        };
    }

    private List<string> GenerateRecommendations(DeviceUsageAnalysis analysis)
    {
        List<string> recommendations = [];

        if (analysis.RiskLevel == RiskLevel.High)
        {
            recommendations.AddRange([
                "Consider implementing stricter content filters",
                "Review and discuss internet usage rules with your children",
                "Set up scheduled device breaks during study hours"
            ]);
        }
        
        if (analysis.BlockedDevices > analysis.TotalDevices / 2)
        {
            recommendations.Add("High number of blocked devices detected - review filtering settings");
        }
        
        if (analysis.ActiveDevices > analysis.TotalDevices * 0.8)
        {
            recommendations.Add("Most devices active recently - consider implementing screen time limits");
        }

        return recommendations.Count > 0 ? recommendations : ["Current usage patterns appear normal - continue monitoring"];
    }

    private List<string> GetYoungChildStarters() => [
        "What games did you play on the computer today?",
        "Did you see any pictures or videos that made you feel uncomfortable?",
        "Who helped you use the internet today?",
        "What was your favorite website or app today?",
        "Did anyone ask you for your name or where you live online?"
    ];

    private List<string> GetPreteenStarters() => [
        "What's your favorite app or website right now?",
        "Have you made any new friends online recently?",
        "What do you do if you see something online that makes you uncomfortable?",
        "How do you decide if a website is safe to use?",
        "What privacy settings do you have on your accounts?"
    ];

    private List<string> GetTeenStarters() => [
        "How was your online experience today?",
        "Have you encountered any cyberbullying recently?",
        "What social media platforms are you most active on?",
        "How do you handle stranger requests on social media?",
        "What would you do if someone shared something inappropriate about you online?"
    ];

    private List<string> GetGeneralStarters() => [
        "How do you feel about your screen time lately?",
        "What's the most interesting thing you learned online today?",
        "Have you encountered anything online that concerned you?",
        "What digital skills would you like to learn more about?"
    ];

    private async Task StartPeriodicAnalysisAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (await _analysisTimer.WaitForNextTickAsync(cancellationToken))
            {
                await PerformPeriodicAnalysisAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation is requested
        }
    }

    private async Task PerformPeriodicAnalysisAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Analyze stored usage patterns for insights
            var insights = _usagePatterns.Values
                .Where(p => p.LastUpdated > DateTime.UtcNow.AddHours(-24))
                .Count();
                
            if (insights > 0)
            {
                logger.LogDebug("Analyzed {Count} usage patterns in periodic review", insights);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in periodic analysis");
        }
    }

    private string GetFeatureHelp(string feature, string? userContext) => feature.ToLowerInvariant() switch
    {
        "dashboard" => "The dashboard shows all connected devices and their current status. Click on any device card to view detailed information, activity logs, and adjust settings. The color coding helps you quickly identify device status: green for safe, yellow for monitored, red for blocked.",
        "filters" => "Content filters automatically block inappropriate content based on categories and custom rules. You can create custom filtering rules, whitelist trusted sites, and adjust sensitivity levels. Use the 'Quick Setup' for age-appropriate defaults.",
        "devices" => "Device management lets you control each connected device individually. Set time limits, create custom restriction schedules, view activity reports, and configure device-specific rules. Use bulk actions to apply settings to multiple devices.",
        "reports" => "Reports provide detailed analytics on internet usage, blocked content attempts, peak usage times, and trend analysis. Export reports for deeper analysis or sharing with other caregivers.",
        "settings" => "Global settings control application-wide preferences including notification settings, security options, backup configurations, and account management. Changes here affect all devices and users.",
        "ai-assistant" => "The AI Assistant provides personalized guidance, safety recommendations, and conversation starters. Ask questions about digital parenting, get real-time insights, and receive proactive alerts about potential concerns.",
        _ => $"For help with {feature}, explore the tooltips and help icons throughout the interface. Check the user guide in Settings > Help for comprehensive documentation."
    };

    private GuidancePriority DetermineGuidancePriority(string feature) => feature.ToLowerInvariant() switch
    {
        "security" or "threats" or "alerts" => GuidancePriority.High,
        "filters" or "devices" or "settings" => GuidancePriority.Medium,
        _ => GuidancePriority.Normal
    };

    private List<string> GenerateActionItems(string feature) => feature.ToLowerInvariant() switch
    {
        "dashboard" => ["Check device status", "Review recent alerts", "Update filtering rules"],
        "filters" => ["Review blocked content", "Update filter categories", "Test filter effectiveness"],
        "devices" => ["Check device activity", "Update time limits", "Review access permissions"],
        "reports" => ["Export usage data", "Review trends", "Share with family"],
        "settings" => ["Update preferences", "Review security settings", "Configure notifications"],
        _ => ["Explore the feature", "Check help documentation", "Contact support if needed"]
    };

    private List<string> GetRelatedTopics(string feature) => feature.ToLowerInvariant() switch
    {
        "dashboard" => ["device-management", "activity-monitoring", "alerts"],
        "filters" => ["content-blocking", "safe-browsing", "parental-controls"],
        "devices" => ["screen-time", "device-limits", "access-control"],
        "reports" => ["usage-analytics", "trend-analysis", "family-insights"],
        "settings" => ["account-management", "security", "preferences"],
        _ => ["digital-safety", "family-protection", "online-security"]
    };

    private string FormatUsageInsights(DeviceUsageAnalysis analysis)
    {
        var content = $"Usage Analysis Summary:\n\n";
        content += $"• Total Devices: {analysis.TotalDevices}\n";
        content += $"• Active Devices (24h): {analysis.ActiveDevices}\n";
        content += $"• Devices with Restrictions: {analysis.BlockedDevices}\n";
        content += $"• Risk Level: {analysis.RiskLevel}\n\n";

        if (analysis.Metrics.TryGetValue("ActivityScore", out var activityScore))
        {
            content += $"• Device Activity Score: {activityScore:F0}%\n";
        }

        content += $"\nLast Analysis: {analysis.AnalyzedAt:g}";
        return content;
    }

    private List<string> GetPersonalizedStarters(DeviceUsagePattern pattern)
    {
        List<string> starters = [];
        
        if (pattern.Data.TryGetValue("HighActivity", out var highActivity) && highActivity.Equals(true))
        {
            starters.Add("I noticed you've been quite active online today. How are you feeling about your screen time?");
        }
        
        if (pattern.Data.TryGetValue("LateNightUsage", out var lateNight) && lateNight.Equals(true))
        {
            starters.Add("I see some late-night device usage. How is it affecting your sleep?");
        }

        return starters;
    }

    private string GetAgeAppropriateContent(string topic, AgeGroup ageGroup) => (topic, ageGroup) switch
    {
        ("cyberbullying", AgeGroup.YoungChild) => "Sometimes people online might say mean things. If someone is not being nice to you online, tell a grown-up right away.",
        ("cyberbullying", AgeGroup.Teen) => "Cyberbullying can take many forms. Learn to recognize it, don't engage with bullies, save evidence, and report incidents.",
        ("privacy-settings", AgeGroup.YoungChild) => "Never share your real name, address, or school with strangers online. Keep your information private.",
        ("privacy-settings", AgeGroup.Teen) => "Review privacy settings regularly, understand what information you're sharing, and be cautious about location sharing.",
        _ => "Digital safety is important for everyone. Always think before you share, trust your instincts, and ask for help when needed."
    };

    private List<string> GetSafetyTips(string topic, AgeGroup ageGroup) => (topic, ageGroup) switch
    {
        ("cyberbullying", AgeGroup.YoungChild) => ["Tell an adult immediately", "Don't respond to mean messages", "Block people who are mean", "Save screenshots"],
        ("cyberbullying", AgeGroup.Teen) => ["Don't retaliate", "Document everything", "Report to platforms", "Seek support from trusted adults"],
        ("online-predators", _) => ["Never meet online friends in person", "Don't share personal information", "Be wary of excessive compliments", "Trust your instincts"],
        ("privacy-settings", _) => ["Use strong passwords", "Enable two-factor authentication", "Review app permissions", "Limit personal information sharing"],
        _ => ["Stay alert online", "Think before sharing", "Ask for help when unsure", "Report suspicious activity"]
    };

    private List<string> GetWarningSignsForTopic(string topic) => topic switch
    {
        "cyberbullying" => ["Withdrawn behavior", "Reluctance to use devices", "Mood changes after device use", "Secretiveness about online activity"],
        "online-predators" => ["Secretive online conversations", "New gifts or money", "Scheduled meetings", "Defensive about online friends"],
        "inappropriate-content" => ["Sudden behavior changes", "Use of new vocabulary", "Secretive browsing", "Clearing browser history frequently"],
        _ => ["Changes in behavior", "Secretiveness", "Mood swings", "Reluctance to discuss online activities"]
    };

    private List<string> GetExternalResources(string topic) => topic switch
    {
        "cyberbullying" => ["StopBullying.gov", "ConnectSafely.org", "Common Sense Media"],
        "online-predators" => ["National Center for Missing & Exploited Children", "FBI's IC3", "NetSmartz"],
        "digital-wellness" => ["Screen Time Action Network", "Digital Wellness Institute", "Center for Humane Technology"],
        _ => ["Family Online Safety Institute", "National Cyber Security Alliance", "ConnectSafely.org"]
    };

    private void StoreGuidance(string category, ParentalGuidance guidance)
    {
        _guidanceHistory.AddOrUpdate(
            category,
            [guidance],
            (key, existingList) =>
            {
                existingList.Add(guidance);
                
                // Keep only last 100 items per category for memory efficiency
                if (existingList.Count > 100)
                {
                    return existingList.TakeLast(100).ToList();
                }
                
                return existingList;
            });

        logger.LogDebug("Stored guidance in category {Category}: {Type}", category, guidance.Type);
    }

    // Additional helper methods would continue with modern patterns...
    
    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _analysisTimer.Dispose();
        _cancellationTokenSource.Dispose();
    }
}

// Modern record types for better performance and immutability
public record ParentalGuidance
{
    public required string Id { get; init; }
    public GuidanceType Type { get; init; }
    public required string Topic { get; init; }
    public required string Content { get; init; }
    public DateTime Timestamp { get; init; }
    public GuidancePriority Priority { get; init; } = GuidancePriority.Normal;
    public List<string> ActionItems { get; init; } = [];
    public Dictionary<string, double> Metrics { get; init; } = [];
    public List<string> RelatedTopics { get; init; } = [];
}

public enum GuidanceType { NavigationHelp, UsageInsights, SafetyGuidance, ProactiveTip }
public enum GuidancePriority { Low, Normal, Medium, High }
public enum AgeGroup { YoungChild, Preteen, Teen, YoungAdult }
public enum RiskLevel { Low, Medium, High }

public record ChildProfile(string DeviceId, AgeGroup AgeGroup, string Name = "");
public record DeviceUsagePattern(string DeviceId, DateTime LastUpdated, Dictionary<string, object> Data);
public record DeviceUsageAnalysis
{
    public int TotalDevices { get; init; }
    public int BlockedDevices { get; init; }
    public int ActiveDevices { get; init; }
    public RiskLevel RiskLevel { get; init; }
    public Dictionary<string, double> Metrics { get; init; } = [];
    public DateTime AnalyzedAt { get; init; }
}

public record ParentalInsight
{
    public required string Type { get; init; }
    public required string Message { get; init; }
    public List<string> Recommendations { get; init; } = [];
    public required string Priority { get; init; }
}

public record SafetyGuidance
{
    public required string Id { get; init; }
    public required string Topic { get; init; }
    public AgeGroup AgeGroup { get; init; }
    public required string Content { get; init; }
    public List<string> Tips { get; init; } = [];
    public List<string> WarningSignsToWatch { get; init; } = [];
    public List<string> Resources { get; init; } = [];
    public DateTime Timestamp { get; init; }
}