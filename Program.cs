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
                case "update":
                    await CheckForUpdatesAsync();
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
                case "childmode on":
                    _ai.EnableChildMode(true);
                    Console.WriteLine("🛡️ Child protection mode ENABLED");
                    break;
                case "childmode off":
                    _ai.EnableChildMode(false);
                    Console.WriteLine("🔓 Child protection mode DISABLED");
                    break;
                case "childstats":
                    ShowChildStats();
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
        Console.WriteLine("  childmode on/off - Enable/disable child protection mode");
        Console.WriteLine("  childstats       - Show child protection statistics");
        Console.WriteLine("  clear            - Clear screen");
        Console.WriteLine("  help             - Show this help");
        Console.WriteLine("  exit             - Exit program");
        Console.WriteLine();
        Console.WriteLine("🛡️ Child Protection Features:");
        Console.WriteLine("  • Blocks inappropriate content automatically");
        Console.WriteLine("  • Monitors for cyberbullying keywords");
        Console.WriteLine("  • Filters adult/violent content");
        Console.WriteLine("  • Detects stranger danger situations");
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

    private static void ShowChildStats()
    {
        var childStats = _ai.GetChildProtectionStats();
        
        Console.WriteLine("🛡️ Child Protection Statistics:");
        Console.WriteLine($"   Mode: {(childStats.IsEnabled ? "✅ ENABLED" : "❌ DISABLED")}");
        Console.WriteLine($"   Content Blocked: {childStats.ContentBlocked}");
        Console.WriteLine($"   Violence Detected: {childStats.ViolenceDetected}");
        Console.WriteLine($"   Adult Content Blocked: {childStats.AdultContentBlocked}");
        Console.WriteLine($"   Cyberbullying Detected: {childStats.CyberBullyingDetected}");
        Console.WriteLine($"   Stranger Danger Alerts: {childStats.StrangerDangerAlerts}");
        Console.WriteLine($"   Safe Score Average: {childStats.SafeScoreAverage:F2}");
    }
    
    private static async Task CheckForUpdatesAsync()
    {
        await UpdateManager.CheckForUpdatesAsync();
    }
}

// Lightweight AI engine optimized for local inference with child protection
public class SimpleAI
{
    private readonly Dictionary<string, double> _threatKeywords;
    private readonly Dictionary<string, double> _safePatterns;
    private readonly Dictionary<string, double> _childUnsafeKeywords;
    private int _processedCount = 0;
    private bool _childModeEnabled = true; // Default to child protection on
    private ChildProtectionStats _childStats = new ChildProtectionStats();
    
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
        
        _childUnsafeKeywords = new Dictionary<string, double>
        {
            // Violence & Weapons
            { "violence", 0.95 }, { "fighting", 0.8 }, { "weapons", 0.9 },
            { "gun", 0.9 }, { "knife", 0.8 }, { "blood", 0.85 }, { "gore", 0.9 },
            { "killing", 0.95 }, { "murder", 0.95 }, { "death", 0.7 },
            
            // Adult Content
            { "adult", 0.9 }, { "mature", 0.8 }, { "explicit", 0.95 },
            { "sexual", 0.95 }, { "dating", 0.6 }, { "romance", 0.4 },
            { "intimate", 0.8 }, { "naked", 0.9 }, { "nude", 0.9 },
            
            // Stranger Danger
            { "meet me", 0.95 }, { "personal info", 0.8 }, { "address", 0.7 },
            { "phone number", 0.8 }, { "secret", 0.6 }, { "don't tell", 0.9 },
            { "parents", 0.5 }, { "alone", 0.6 }, { "private", 0.5 },
            
            // Cyberbullying
            { "stupid", 0.7 }, { "ugly", 0.7 }, { "hate you", 0.9 },
            { "kill yourself", 0.99 }, { "worthless", 0.8 }, { "loser", 0.7 },
            { "fat", 0.6 }, { "dumb", 0.6 }, { "nobody likes", 0.8 },
            
            // Drugs & Substances
            { "alcohol", 0.8 }, { "beer", 0.7 }, { "wine", 0.6 },
            { "smoking", 0.8 }, { "vaping", 0.8 }, { "cigarettes", 0.8 },
            { "marijuana", 0.9 }, { "weed", 0.8 }, { "drugs", 0.9 },
            { "pills", 0.7 }, { "medicine", 0.3 },
            
            // Gambling
            { "gambling", 0.9 }, { "casino", 0.8 }, { "betting", 0.8 },
            { "lottery", 0.6 }, { "poker", 0.7 }, { "slots", 0.8 }
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
        var childUnsafeScore = AnalyzeChildSafety(content);
        var flags = new List<string>();
        var category = "General";
        
        // Determine category and flags based on content
        var contentLower = content.ToLowerInvariant();
        
        // Child safety checks (higher priority when child mode is enabled)
        if (_childModeEnabled && childUnsafeScore > 0.6)
        {
            if (contentLower.Contains("violence") || contentLower.Contains("fighting") || contentLower.Contains("weapons"))
            {
                category = "Violence";
                flags.Add("Child Unsafe - Violence");
            }
            else if (contentLower.Contains("adult") || contentLower.Contains("explicit") || contentLower.Contains("sexual"))
            {
                category = "Adult Content";
                flags.Add("Child Unsafe - Adult Content");
            }
            else if (contentLower.Contains("meet") || contentLower.Contains("secret") || contentLower.Contains("address"))
            {
                category = "Stranger Danger";
                flags.Add("Child Unsafe - Stranger Danger");
            }
            else if (contentLower.Contains("stupid") || contentLower.Contains("hate") || contentLower.Contains("ugly"))
            {
                category = "Cyberbullying";
                flags.Add("Child Unsafe - Cyberbullying");
            }
        }
        else if (contentLower.Contains("adult") || contentLower.Contains("explicit"))
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
        
        // Combine threat and child safety scores
        var finalThreatLevel = _childModeEnabled ? Math.Max(threatLevel, childUnsafeScore) : threatLevel;
        
        var recommendation = finalThreatLevel > 0.7 ? "BLOCK" : 
                           finalThreatLevel > 0.4 ? "MONITOR" : "ALLOW";
        
        // Add child mode indicator
        if (_childModeEnabled && childUnsafeScore > 0.3)
        {
            flags.Add("🛡️ Child Protection Active");
        }
        
        return new ContentAnalysis
        {
            SafetyScore = 1.0 - finalThreatLevel,
            Category = category,
            Confidence = Math.Min(0.95, finalThreatLevel + 0.3),
            Recommendation = recommendation,
            Flags = flags
        };
    }

    public void EnableChildMode(bool enabled)
    {
        _childModeEnabled = enabled;
        _childStats.IsEnabled = enabled;
    }

    public ChildProtectionStats GetChildProtectionStats()
    {
        return _childStats;
    }

    private double AnalyzeChildSafety(string content)
    {
        if (!_childModeEnabled) return 0.0;

        var contentLower = content.ToLowerInvariant();
        double unsafeScore = 0.0;
        int matches = 0;

        foreach (var keyword in _childUnsafeKeywords)
        {
            if (contentLower.Contains(keyword.Key))
            {
                unsafeScore += keyword.Value;
                matches++;

                // Update specific stats based on keyword type
                if (keyword.Key.Contains("violence") || keyword.Key.Contains("fighting") || keyword.Key.Contains("weapons"))
                    _childStats.ViolenceDetected++;
                else if (keyword.Key.Contains("adult") || keyword.Key.Contains("explicit") || keyword.Key.Contains("sexual"))
                    _childStats.AdultContentBlocked++;
                else if (keyword.Key.Contains("stupid") || keyword.Key.Contains("hate") || keyword.Key.Contains("kill yourself"))
                    _childStats.CyberBullyingDetected++;
                else if (keyword.Key.Contains("meet") || keyword.Key.Contains("secret") || keyword.Key.Contains("don't tell"))
                    _childStats.StrangerDangerAlerts++;
            }
        }

        if (matches > 0)
        {
            unsafeScore = Math.Max(0.0, Math.Min(1.0, unsafeScore / matches));
            if (unsafeScore > 0.6) _childStats.ContentBlocked++;
        }

        _childStats.SafeScoreAverage = (_childStats.SafeScoreAverage + (1.0 - unsafeScore)) / 2.0;
        return unsafeScore;
    }
    
    public int GetProcessedCount() => _processedCount;
}

// Child protection statistics
public class ChildProtectionStats
{
    public bool IsEnabled { get; set; } = true;
    public int ContentBlocked { get; set; } = 0;
    public int ViolenceDetected { get; set; } = 0;
    public int AdultContentBlocked { get; set; } = 0;
    public int CyberBullyingDetected { get; set; } = 0;
    public int StrangerDangerAlerts { get; set; } = 0;
    public double SafeScoreAverage { get; set; } = 1.0;
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
            // Security threats
            "malicious.com", "phishing.net", "illegal-downloads.net",
            
            // Adult content sites
            "pornhub.com", "xvideos.com", "xnxx.com", "redtube.com",
            "youporn.com", "tube8.com", "spankbang.com", "xhamster.com",
            "porn.com", "sex.com", "xxx.com", "adult.com",
            "chaturbate.com", "cam4.com", "myfreecams.com", "livejasmin.com",
            "onlyfans.com", "playboy.com", "penthouse.com",
            
            // Gambling sites
            "gambling.org", "casino.com", "bet365.com", "888casino.com",
            "pokerstars.com", "bwin.com", "betway.com",
            
            // Dating sites (child protection)
            "tinder.com", "bumble.com", "match.com", "okcupid.com",
            "pof.com", "adultfriendfinder.com", "ashley-madison.com"
        };
        
        _blockedKeywords = new List<string>
        {
            // Adult content keywords
            "porn", "xxx", "sex", "adult", "nude", "naked", "explicit",
            "erotic", "cam", "live", "chat", "dating", "hookup",
            
            // Gambling keywords  
            "gambling", "casino", "bet", "poker", "slots", "lottery",
            
            // Violence/weapons
            "weapons", "violence", "gore", "blood",
            
            // Substances
            "drugs", "weed", "marijuana", "cocaine",
            
            // Security threats
            "malware", "phishing", "virus", "hack",
            
            // Illegal content
            "illegal", "torrent", "pirate", "crack", "keygen"
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
            var host = uri.Host.ToLowerInvariant();
            
            // Remove www. prefix for consistent matching
            if (host.StartsWith("www."))
                host = host.Substring(4);
                
            return host;
        }
        catch
        {
            var domain = url.Split('/')[0].ToLowerInvariant();
            if (domain.StartsWith("www."))
                domain = domain.Substring(4);
            return domain;
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

// Update management functionality
public static class UpdateManager
{
    private static readonly HttpClient _httpClient = new();
    private const string UpdateUrl = "https://github.com/DJMcClellan1966/PocketFence-EcoSystem/releases/latest/download/";
    
    public static async Task<bool> CheckForUpdatesAsync()
    {
        try
        {
            Console.WriteLine("🔍 Checking for updates...");
            
            var currentVersion = GetCurrentVersion();
            var latestVersion = await GetLatestVersionAsync();
            
            if (string.Equals(currentVersion, latestVersion, StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("✅ You have the latest version!");
                return false;
            }
            
            Console.WriteLine($"📦 Update available: {currentVersion} → {latestVersion}");
            Console.Write("Would you like to download it? (y/n): ");
            
            var response = Console.ReadLine()?.ToLower();
            if (response == "y" || response == "yes")
            {
                await DownloadUpdateAsync();
                return true;
            }
            
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Update check failed: {ex.Message}");
            return false;
        }
    }
    
    private static async Task<string> GetLatestVersionAsync()
    {
        var versionUrl = $"{UpdateUrl}version.txt";
        return (await _httpClient.GetStringAsync(versionUrl)).Trim();
    }
    
    private static async Task DownloadUpdateAsync()
    {
        var platform = GetPlatformIdentifier();
        var fileName = $"PocketFence-AI-{platform}";
        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            fileName += ".exe";
        
        var updateUrl = $"{UpdateUrl}{fileName}";
        var tempPath = $"{fileName}.new";
        
        Console.WriteLine($"📥 Downloading from {updateUrl}...");
        
        var response = await _httpClient.GetAsync(updateUrl);
        response.EnsureSuccessStatusCode();
        
        await using var fileStream = File.Create(tempPath);
        await response.Content.CopyToAsync(fileStream);
        
        Console.WriteLine("✅ Download complete! Restart the application to use the new version.");
        Console.WriteLine($"💡 Replace the current executable with: {tempPath}");
    }
    
    private static string GetPlatformIdentifier()
    {
        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            return Environment.Is64BitProcess ? "win-x64" : "win-x86";
        else if (Environment.OSVersion.Platform == PlatformID.MacOSX)
            return "osx-x64";
        else
            return "linux-x64";
    }
    
    private static string GetCurrentVersion() => "1.0.0";
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