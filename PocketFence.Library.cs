using System.Text.Json;
using PocketFence_AI; // Import the main namespace

namespace PocketFence.Library;

/// <summary>
/// PocketFence AI Library - Lightweight Content Filtering Engine
/// Can be integrated into any .NET application
/// </summary>
public class PocketFenceEngine
{
    private readonly SimpleAI _ai;
    private readonly ContentFilter _filter;
    
    public PocketFenceEngine(PocketFenceConfig? config = null)
    {
        config ??= new PocketFenceConfig();
        _ai = new SimpleAI();
        _filter = new ContentFilter();
        // TODO: Configure child mode and custom domains/keywords if needed
    }
    
    /// <summary>
    /// Initialize the engine (call once before using)
    /// </summary>
    public async Task InitializeAsync()
    {
        await _ai.InitializeAsync();
        await _filter.LoadFiltersAsync();
    }
    
    /// <summary>
    /// Check if a URL should be blocked
    /// </summary>
    public async Task<UrlCheckResult> CheckUrlAsync(string url)
    {
        var filterResult = await _filter.CheckUrlAsync(url);
        var threatScore = await _ai.AnalyzeThreatLevelAsync(url);
        
        return new UrlCheckResult
        {
            Url = url,
            IsBlocked = filterResult.IsBlocked,
            ThreatScore = threatScore,
            Reason = filterResult.Reason,
            Recommendation = threatScore > 0.7 ? "BLOCK" : 
                           threatScore > 0.4 ? "MONITOR" : "ALLOW"
        };
    }
    
    /// <summary>
    /// Analyze text content for safety
    /// </summary>
    public async Task<ContentAnalysisResult> AnalyzeContentAsync(string content)
    {
        var analysis = await _ai.AnalyzeContentAsync(content);
        
        return new ContentAnalysisResult
        {
            Content = content,
            SafetyScore = analysis.SafetyScore,
            Category = analysis.Category,
            Confidence = analysis.Confidence,
            Recommendation = analysis.Recommendation,
            Flags = analysis.Flags.ToList(),
            IsChildSafe = analysis.SafetyScore > 0.6
        };
    }
    
    /// <summary>
    /// Enable or disable child protection mode
    /// </summary>
    public void SetChildMode(bool enabled)
    {
        _ai.EnableChildMode(enabled);
    }
    
    /// <summary>
    /// Get filtering statistics
    /// </summary>
    public FilteringStats GetStatistics()
    {
        var filterStats = _filter.GetStatistics();
        var childStats = _ai.GetChildProtectionStats();
        
        return new FilteringStats
        {
            TotalRequests = filterStats.TotalRequests,
            BlockedRequests = filterStats.BlockedRequests,
            AllowedRequests = filterStats.AllowedRequests,
            BlockRate = filterStats.BlockRate,
            AiProcessedCount = _ai.GetProcessedCount(),
            ChildProtectionStats = new ChildProtectionStats
            {
                IsEnabled = childStats.IsEnabled,
                ContentBlocked = childStats.ContentBlocked,
                ViolenceDetected = childStats.ViolenceDetected,
                AdultContentBlocked = childStats.AdultContentBlocked,
                CyberBullyingDetected = childStats.CyberBullyingDetected,
                StrangerDangerAlerts = childStats.StrangerDangerAlerts,
                SafeScoreAverage = childStats.SafeScoreAverage
            }
        };
    }
}

/// <summary>
/// Configuration for PocketFence Engine
/// </summary>
public class PocketFenceConfig
{
    public bool ChildModeEnabled { get; set; } = true;
    public List<string> CustomBlockedDomains { get; set; } = new();
    public List<string> CustomBlockedKeywords { get; set; } = new();
    public double ThreatThreshold { get; set; } = 0.7;
    public double MonitorThreshold { get; set; } = 0.4;
}

/// <summary>
/// Result of URL checking
/// </summary>
public class UrlCheckResult
{
    public string Url { get; set; } = "";
    public bool IsBlocked { get; set; }
    public double ThreatScore { get; set; }
    public string Reason { get; set; } = "";
    public string Recommendation { get; set; } = "";
}

/// <summary>
/// Result of content analysis
/// </summary>
public class ContentAnalysisResult
{
    public string Content { get; set; } = "";
    public double SafetyScore { get; set; }
    public string Category { get; set; } = "";
    public double Confidence { get; set; }
    public string Recommendation { get; set; } = "";
    public List<string> Flags { get; set; } = new();
    public bool IsChildSafe { get; set; }
}

/// <summary>
/// Comprehensive filtering statistics
/// </summary>
public class FilteringStats
{
    public int TotalRequests { get; set; }
    public int BlockedRequests { get; set; }
    public int AllowedRequests { get; set; }
    public double BlockRate { get; set; }
    public int AiProcessedCount { get; set; }
    public ChildProtectionStats ChildProtectionStats { get; set; } = new();
}

/// <summary>
/// Child protection specific statistics
/// </summary>
public class ChildProtectionStats
{
    public bool IsEnabled { get; set; }
    public int ContentBlocked { get; set; }
    public int ViolenceDetected { get; set; }
    public int AdultContentBlocked { get; set; }
    public int CyberBullyingDetected { get; set; }
    public int StrangerDangerAlerts { get; set; }
    public double SafeScoreAverage { get; set; }
}