using System.Text.Json;

namespace PocketFence_AI;

/// <summary>
/// PocketFence AI - Lightweight Local Content Filter
/// Optimized for local inference like GPT4All
/// </summary>
public class Program
{
    private static readonly SimpleAI _ai = new SimpleAI();
    private static readonly ContentFilter _filter = new ContentFilter();
    
    public static async Task Main(string[] args)
    {
        Console.WriteLine("🤖 PocketFence AI - Local Content Filter v1.0");
        Console.WriteLine("Optimized for local inference without external dependencies");
        Console.WriteLine();
        
        try
        {
            // Initialize AI model
            Console.WriteLine("Initializing AI model...");
            await _ai.InitializeAsync();
            
            // Load content filters
            Console.WriteLine("Loading content filters...");
            await _filter.LoadFiltersAsync();
            
            Console.WriteLine("✅ Ready! Type 'help' for commands or 'exit' to quit.");
            Console.WriteLine();
            
            // Start interactive CLI
            await RunInteractiveModeAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
            return;
        }
    }
    
    private static async Task RunInteractiveModeAsync()
    {
        while (true)
        {
            Console.Write("pocketfence> ");
            var input = Console.ReadLine()?.Trim() ?? "";
            
            switch (input.ToLower())
            {
                case "help":
                    ShowHelp();
                    break;
                case "exit":
                case "quit":
                    Console.WriteLine("👋 Goodbye!");
                    return;
                case var cmd when cmd.StartsWith("check "):
                    await CheckContentAsync(input.Substring(6));
                    break;
                case var cmd when cmd.StartsWith("analyze "):
                    await AnalyzeContentAsync(input.Substring(8));
                    break;
                case "stats":
                    ShowStats();
                    break;
                case "clear":
                    Console.Clear();
                    break;
                default:
                    Console.WriteLine("❓ Unknown command. Type 'help' for available commands.");
                    break;
            }
        }
    }
    
    private static void ShowHelp()
    {
        Console.WriteLine("Available commands:");
        Console.WriteLine("  check <url>      - Check if URL should be blocked");
        Console.WriteLine("  analyze <text>   - Analyze text content for safety");
        Console.WriteLine("  stats            - Show filtering statistics");
        Console.WriteLine("  clear            - Clear screen");
        Console.WriteLine("  help             - Show this help");
        Console.WriteLine("  exit             - Exit program");
    }
    
    private static async Task CheckContentAsync(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            Console.WriteLine("❌ Please provide a URL to check");
            return;
        }
        
        var result = await _filter.CheckUrlAsync(url);
        var aiScore = await _ai.AnalyzeThreatLevelAsync(url);
        
        Console.WriteLine($"🔍 Analysis for: {url}");
        Console.WriteLine($"   Filter Result: {(result.IsBlocked ? "❌ BLOCKED" : "✅ ALLOWED")}");
        Console.WriteLine($"   AI Threat Score: {aiScore:F2}/1.0");
        Console.WriteLine($"   Reason: {result.Reason}");
        
        if (result.IsBlocked || aiScore > 0.7)
        {
            Console.WriteLine($"   ⚠️  Recommendation: {(result.IsBlocked ? "Block" : "Monitor")}");
        }
    }
    
    private static async Task AnalyzeContentAsync(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            Console.WriteLine("❌ Please provide content to analyze");
            return;
        }
        
        var analysis = await _ai.AnalyzeContentAsync(content);
        
        Console.WriteLine("🧠 AI Content Analysis:");
        Console.WriteLine($"   Safety Score: {analysis.SafetyScore:F2}/1.0");
        Console.WriteLine($"   Category: {analysis.Category}");
        Console.WriteLine($"   Confidence: {analysis.Confidence:F2}");
        Console.WriteLine($"   Recommendation: {analysis.Recommendation}");
        
        if (analysis.Flags.Any())
        {
            Console.WriteLine($"   ⚠️  Flags: {string.Join(", ", analysis.Flags)}");
        }
    }
    
    private static void ShowStats()
    {
        var stats = _filter.GetStatistics();
        
        Console.WriteLine("📊 Filtering Statistics:");
        Console.WriteLine($"   Total Requests: {stats.TotalRequests}");
        Console.WriteLine($"   Blocked: {stats.BlockedRequests} ({stats.BlockRate:P1})");
        Console.WriteLine($"   Allowed: {stats.AllowedRequests} ({stats.AllowRate:P1})");
        Console.WriteLine($"   AI Processed: {_ai.GetProcessedCount()}");
    }
}

// Lightweight AI engine optimized for local inference
public class SimpleAI
{
    private readonly Dictionary<string, double> _threatKeywords;
    private readonly Dictionary<string, double> _safePatterns;
    private int _processedCount = 0;
    
    public SimpleAI()
    {
        _threatKeywords = new Dictionary<string, double>
        {
            // High-risk keywords
            { "malware", 0.9 }, { "virus", 0.9 }, { "phishing", 0.95 },
            { "adult", 0.8 }, { "gambling", 0.7 }, { "violence", 0.85 },
            { "drugs", 0.8 }, { "weapons", 0.85 }, { "hate", 0.9 },
            
            // Medium-risk keywords
            { "download", 0.4 }, { "free", 0.3 }, { "click", 0.3 },
            { "urgent", 0.5 }, { "limited", 0.4 }, { "offer", 0.3 }
        };
        
        _safePatterns = new Dictionary<string, double>
        {
            { "education", -0.3 }, { "learning", -0.3 }, { "school", -0.3 },
            { "tutorial", -0.2 }, { "help", -0.2 }, { "support", -0.2 },
            { "documentation", -0.3 }, { "official", -0.3 }
        };
    }
    
    public Task InitializeAsync()
    {
        // Simulate model loading - in real implementation this would load ML models
        Thread.Sleep(100);
        return Task.CompletedTask;
    }
    
    public Task<double> AnalyzeThreatLevelAsync(string content)
    {
        _processedCount++;
        
        if (string.IsNullOrWhiteSpace(content))
            return Task.FromResult(0.0);
            
        var contentLower = content.ToLowerInvariant();
        double score = 0.0;
        int matches = 0;
        
        // Check threat keywords
        foreach (var keyword in _threatKeywords)
        {
            if (contentLower.Contains(keyword.Key))
            {
                score += keyword.Value;
                matches++;
            }
        }
        
        // Check safe patterns
        foreach (var pattern in _safePatterns)
        {
            if (contentLower.Contains(pattern.Key))
            {
                score += pattern.Value;
                matches++;
            }
        }
        
        // Normalize score
        if (matches > 0)
            score = Math.Max(0.0, Math.Min(1.0, score / matches));
            
        return Task.FromResult(score);
    }
    
    public async Task<ContentAnalysis> AnalyzeContentAsync(string content)
    {
        var threatLevel = await AnalyzeThreatLevelAsync(content);
        var flags = new List<string>();
        var category = "General";
        
        // Determine category and flags based on content
        var contentLower = content.ToLowerInvariant();
        
        if (contentLower.Contains("adult") || contentLower.Contains("explicit"))
        {
            category = "Adult Content";
            flags.Add("NSFW");
        }
        else if (contentLower.Contains("violence") || contentLower.Contains("weapon"))
        {
            category = "Violence";
            flags.Add("Violent Content");
        }
        else if (contentLower.Contains("malware") || contentLower.Contains("virus"))
        {
            category = "Security Threat";
            flags.Add("Malicious");
        }
        
        var recommendation = threatLevel > 0.7 ? "BLOCK" : 
                           threatLevel > 0.4 ? "MONITOR" : "ALLOW";
        
        return new ContentAnalysis
        {
            SafetyScore = 1.0 - threatLevel,
            Category = category,
            Confidence = Math.Min(0.95, threatLevel + 0.3),
            Recommendation = recommendation,
            Flags = flags
        };
    }
    
    public int GetProcessedCount() => _processedCount;
}

// Lightweight content filter
public class ContentFilter
{
    private readonly HashSet<string> _blockedDomains;
    private readonly List<string> _blockedKeywords;
    private int _totalRequests = 0;
    private int _blockedRequests = 0;
    
    public ContentFilter()
    {
        _blockedDomains = new HashSet<string>
        {
            "malicious.com", "phishing.net", "adult-content.com",
            "gambling.org", "illegal-downloads.net"
        };
        
        _blockedKeywords = new List<string>
        {
            "adult", "gambling", "drugs", "weapons", "violence",
            "malware", "phishing", "illegal", "torrent"
        };
    }
    
    public Task LoadFiltersAsync()
    {
        // Simulate loading filters - in real implementation load from file/database
        Thread.Sleep(50);
        return Task.CompletedTask;
    }
    
    public Task<FilterResult> CheckUrlAsync(string url)
    {
        _totalRequests++;
        
        var urlLower = url.ToLowerInvariant();
        var domain = ExtractDomain(url);
        
        // Check blocked domains
        if (_blockedDomains.Contains(domain))
        {
            _blockedRequests++;
            return Task.FromResult(new FilterResult
            {
                IsBlocked = true,
                Reason = $"Domain '{domain}' is in blocklist"
            });
        }
        
        // Check blocked keywords
        foreach (var keyword in _blockedKeywords)
        {
            if (urlLower.Contains(keyword))
            {
                _blockedRequests++;
                return Task.FromResult(new FilterResult
                {
                    IsBlocked = true,
                    Reason = $"Contains blocked keyword '{keyword}'"
                });
            }
        }
        
        return Task.FromResult(new FilterResult
        {
            IsBlocked = false,
            Reason = "No blocking rules matched"
        });
    }
    
    private string ExtractDomain(string url)
    {
        try
        {
            if (!url.StartsWith("http"))
                url = "http://" + url;
            var uri = new Uri(url);
            return uri.Host;
        }
        catch
        {
            return url.Split('/')[0];
        }
    }
    
    public FilterStatistics GetStatistics()
    {
        return new FilterStatistics
        {
            TotalRequests = _totalRequests,
            BlockedRequests = _blockedRequests,
            AllowedRequests = _totalRequests - _blockedRequests,
            BlockRate = _totalRequests > 0 ? (double)_blockedRequests / _totalRequests : 0,
            AllowRate = _totalRequests > 0 ? (double)(_totalRequests - _blockedRequests) / _totalRequests : 0
        };
    }
}

// Simple data models
public class ContentAnalysis
{
    public double SafetyScore { get; set; }
    public string Category { get; set; } = "";
    public double Confidence { get; set; }
    public string Recommendation { get; set; } = "";
    public List<string> Flags { get; set; } = new();
}

public class FilterResult
{
    public bool IsBlocked { get; set; }
    public string Reason { get; set; } = "";
}

public class FilterStatistics
{
    public int TotalRequests { get; set; }
    public int BlockedRequests { get; set; }
    public int AllowedRequests { get; set; }
    public double BlockRate { get; set; }
    public double AllowRate { get; set; }
}