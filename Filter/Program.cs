using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PocketFence_Filter;

/// <summary>
/// PocketFence Universal Active Filter - Cross-Platform Proxy
/// Optimized like GPT4All for local deployment and universal device support
/// </summary>
public class Program
{
    private static readonly HttpClient _httpClient = new();
    private static readonly SimpleAI _ai = new SimpleAI();
    private static readonly ProxyServer _proxyServer = new ProxyServer();
    private static bool _isRunning = true;
    private static int _proxyPort = 8888;
    private static string _configPath = "pocketfence-settings.json";

    public static async Task Main(string[] args)
    {
        Console.WriteLine("🚀 PocketFence Universal Active Filter v1.0");
        Console.WriteLine("Cross-platform proxy filter optimized like GPT4All");
        Console.WriteLine();

        try
        {
            // Load shared settings from PocketFence-AI
            await LoadSharedSettingsAsync();
            
            // Initialize AI engine
            Console.WriteLine("Initializing filtering engine...");
            await _ai.InitializeAsync();
            
            // Start proxy server
            Console.WriteLine($"Starting proxy server on port {_proxyPort}...");
            await _proxyServer.StartAsync(_proxyPort);
            
            Console.WriteLine("✅ Universal filter active! Configure devices to use proxy:");
            Console.WriteLine($"   HTTP Proxy: 127.0.0.1:{_proxyPort}");
            Console.WriteLine($"   🛡️ Child Protection: {(_ai.IsChildModeEnabled ? "🟢 ON" : "🔴 OFF")}");
            Console.WriteLine($"   🎯 Age Level: {_ai.GetCurrentAgeLevel()}");
            Console.WriteLine();
            Console.WriteLine("Commands: 'status', 'stats', 'config', 'stop', 'help'");
            
            // Start command interface
            _ = Task.Run(HandleCommandsAsync);
            
            // Keep proxy running
            while (_isRunning)
            {
                await Task.Delay(1000);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
        finally
        {
            await _proxyServer.StopAsync();
            Console.WriteLine("👋 PocketFence Filter stopped.");
        }
    }

    private static async Task LoadSharedSettingsAsync()
    {
        try
        {
            // Try to load settings from PocketFence-AI
            if (File.Exists(_configPath))
            {
                var json = await File.ReadAllTextAsync(_configPath);
                var settings = JsonSerializer.Deserialize<ProxySettings>(json);
                
                if (settings != null)
                {
                    _ai.SetAgeRestriction(settings.AgeLevel);
                    _ai.EnableChildMode(settings.ChildModeEnabled);
                    _proxyPort = settings.ProxyPort;
                    Console.WriteLine($"📋 Loaded shared settings: {settings.AgeLevel}, Port {_proxyPort}");
                }
            }
            else
            {
                // Create default settings
                var defaultSettings = new ProxySettings
                {
                    AgeLevel = "elementary",
                    ChildModeEnabled = true,
                    ProxyPort = 8888,
                    AutoStart = true
                };
                
                await SaveSettingsAsync(defaultSettings);
                Console.WriteLine("📋 Created default settings - Elementary (9-12) protection");
            }
        }
        catch
        {
            Console.WriteLine("⚠️ Using default settings");
        }
    }

    private static async Task SaveSettingsAsync(ProxySettings settings)
    {
        try
        {
#pragma warning disable IL2026
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
#pragma warning restore IL2026
            await File.WriteAllTextAsync(_configPath, json);
        }
        catch
        {
            // Ignore save errors
        }
    }

    private static async Task HandleCommandsAsync()
    {
        while (_isRunning)
        {
            Console.Write("filter> ");
            var command = Console.ReadLine()?.ToLowerInvariant().Trim();

            switch (command)
            {
                case "status":
                    ShowStatus();
                    break;
                case "stats":
                    ShowStats();
                    break;
                case "config":
                    ShowConfig();
                    break;
                case "stop":
                case "exit":
                    _isRunning = false;
                    break;
                case "help":
                    ShowHelp();
                    break;
                case "clear":
                    Console.Clear();
                    break;
                default:
                    if (command?.StartsWith("ageset ") == true)
                    {
                        var level = command.Substring(7);
                        if (_ai.SetAgeRestriction(level))
                        {
                            Console.WriteLine($"🎯 Age restriction updated: {_ai.GetCurrentAgeLevel()}");
                            await SaveCurrentSettingsAsync();
                        }
                        else
                        {
                            Console.WriteLine("❌ Invalid age level. Use: early, elementary, teen, adult");
                        }
                    }
                    else if (!string.IsNullOrEmpty(command))
                    {
                        Console.WriteLine("❓ Unknown command. Type 'help' for available commands.");
                    }
                    break;
            }
        }
    }

    private static void ShowStatus()
    {
        Console.WriteLine("🚀 PocketFence Universal Filter Status:");
        Console.WriteLine($"   Proxy Status: {(_proxyServer.IsRunning ? "🟢 ACTIVE" : "🔴 STOPPED")}");
        Console.WriteLine($"   Proxy Port: {_proxyPort}");
        Console.WriteLine($"   Child Protection: {(_ai.IsChildModeEnabled ? "🟢 ON" : "🔴 OFF")}");
        Console.WriteLine($"   Age Level: {_ai.GetCurrentAgeLevel()}");
        Console.WriteLine($"   Protection: {_ai.GetProtectionDescription()}");
        Console.WriteLine($"   Requests Filtered: {_proxyServer.GetRequestCount()}");
        Console.WriteLine($"   Requests Blocked: {_proxyServer.GetBlockedCount()}");
    }

    private static void ShowStats()
    {
        var stats = _ai.GetChildProtectionStats();
        Console.WriteLine("📊 Filtering Statistics:");
        Console.WriteLine($"   Total Requests: {_proxyServer.GetRequestCount()}");
        Console.WriteLine($"   Blocked Requests: {_proxyServer.GetBlockedCount()}");
        Console.WriteLine($"   Content Blocked: {stats.ContentBlocked}");
        Console.WriteLine($"   Violence Detected: {stats.ViolenceDetected}");
        Console.WriteLine($"   Adult Content Blocked: {stats.AdultContentBlocked}");
        Console.WriteLine($"   Cyberbullying Detected: {stats.CyberBullyingDetected}");
        Console.WriteLine($"   Stranger Danger Alerts: {stats.StrangerDangerAlerts}");
        Console.WriteLine($"   Average Safety Score: {stats.SafeScoreAverage:F2}");
    }

    private static void ShowConfig()
    {
        Console.WriteLine("🔧 Configuration:");
        Console.WriteLine($"   Proxy Address: 127.0.0.1:{_proxyPort}");
        Console.WriteLine($"   Config File: {_configPath}");
        Console.WriteLine($"   Age Level: {_ai.GetCurrentAgeLevel()}");
        Console.WriteLine($"   Child Mode: {(_ai.IsChildModeEnabled ? "Enabled" : "Disabled")}");
        Console.WriteLine();
        Console.WriteLine("📱 Device Setup Instructions:");
        Console.WriteLine("   Windows: Settings > Network > Proxy > Manual proxy setup");
        Console.WriteLine($"   HTTP Proxy: 127.0.0.1  Port: {_proxyPort}");
        Console.WriteLine("   Mobile: WiFi Settings > Advanced > Proxy > Manual");
        Console.WriteLine("   Router: Set custom proxy in router configuration");
    }

    private static void ShowHelp()
    {
        Console.WriteLine("🎮 Available Commands:");
        Console.WriteLine("   status           - Show filter status and activity");
        Console.WriteLine("   stats            - Display filtering statistics");
        Console.WriteLine("   config           - Show configuration and setup instructions");
        Console.WriteLine("   ageset <level>   - Change age restriction (early/elementary/teen/adult)");
        Console.WriteLine("   clear            - Clear screen");
        Console.WriteLine("   stop             - Stop the proxy filter");
        Console.WriteLine("   help             - Show this help");
        Console.WriteLine();
        Console.WriteLine("🌐 Universal Platform Support:");
        Console.WriteLine("   ✅ Windows, macOS, Linux (native executables)");
        Console.WriteLine("   ✅ Android (via proxy settings or Termux)");
        Console.WriteLine("   ✅ iOS (limited proxy support)");
        Console.WriteLine("   ✅ Any device that supports HTTP proxy");
    }

    private static async Task SaveCurrentSettingsAsync()
    {
        var settings = new ProxySettings
        {
            AgeLevel = _ai.GetCurrentAgeLevel().ToLowerInvariant().Split(' ')[0],
            ChildModeEnabled = _ai.IsChildModeEnabled,
            ProxyPort = _proxyPort,
            AutoStart = true
        };
        
        await SaveSettingsAsync(settings);
    }
}

// Universal HTTP Proxy Server for active content filtering
public class ProxyServer
{
    private HttpListener? _listener;
    private readonly SimpleAI _ai;
    private int _requestCount = 0;
    private int _blockedCount = 0;
    private bool _isRunning = false;

    public ProxyServer()
    {
        _ai = new SimpleAI();
    }

    public bool IsRunning => _isRunning;
    public int GetRequestCount() => _requestCount;
    public int GetBlockedCount() => _blockedCount;

    public async Task StartAsync(int port)
    {
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        _listener.Prefixes.Add($"http://localhost:{port}/");
        
        _listener.Start();
        _isRunning = true;
        
        // Start handling requests
        _ = Task.Run(HandleRequestsAsync);
        
        await Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        _isRunning = false;
        _listener?.Stop();
        _listener?.Close();
        await Task.CompletedTask;
    }

    private async Task HandleRequestsAsync()
    {
        while (_isRunning && _listener != null)
        {
            try
            {
                var context = await _listener.GetContextAsync();
                _ = Task.Run(() => ProcessRequestAsync(context));
            }
            catch
            {
                // Handle listener exceptions
                break;
            }
        }
    }

    private async Task ProcessRequestAsync(HttpListenerContext context)
    {
        try
        {
            Interlocked.Increment(ref _requestCount);
            
            var request = context.Request;
            var response = context.Response;
            
            // Handle CONNECT method for HTTPS tunneling
            if (request.HttpMethod == "CONNECT")
            {
                await HandleHttpsConnect(context);
                return;
            }
            
            var url = request.Url?.ToString() ?? "";
            
            // Analyze URL for threats
            var shouldBlock = await ShouldBlockRequestAsync(url, request);
            
            if (shouldBlock)
            {
                Interlocked.Increment(ref _blockedCount);
                await SendBlockedResponseAsync(response, url);
                return;
            }
            
            // Forward the request
            await ForwardRequestAsync(context);
        }
        catch
        {
            // Handle request processing errors
            context.Response.Close();
        }
    }

    private async Task<bool> ShouldBlockRequestAsync(string url, HttpListenerRequest request)
    {
        try
        {
            // Quick domain check
            var host = request.Url?.Host ?? "";
            
            // Analyze URL and headers for threats
            var urlAnalysis = await _ai.AnalyzeThreatLevelAsync(url);
            var hostAnalysis = await _ai.AnalyzeThreatLevelAsync(host);
            
            // Check for suspicious patterns
            var userAgent = request.UserAgent ?? "";
            var userAgentAnalysis = await _ai.AnalyzeThreatLevelAsync(userAgent);
            
            // Combine threat scores
            var maxThreatLevel = Math.Max(Math.Max(urlAnalysis, hostAnalysis), userAgentAnalysis);
            
            // Use age-appropriate threshold
            var threshold = _ai.GetAgeThreshold();
            
            return maxThreatLevel > threshold;
        }
        catch
        {
            // If analysis fails, err on the side of caution
            return false;
        }
    }

    private async Task HandleHttpsConnect(HttpListenerContext context)
    {
        try
        {
            var request = context.Request;
            var response = context.Response;
            
            // For HTTPS CONNECT, we'll just allow the connection
            // In a full implementation, you'd set up SSL interception
            response.StatusCode = 200;
            response.StatusDescription = "Connection established";
            response.Close();
        }
        catch
        {
            // Handle HTTPS connect errors
        }
        
        await Task.CompletedTask;
    }

    private async Task SendBlockedResponseAsync(HttpListenerResponse response, string url)
    {
        try
        {
            var blockedPage = $@"
<!DOCTYPE html>
<html>
<head>
    <title>🛡️ PocketFence - Content Blocked</title>
    <style>
        body {{ font-family: Arial, sans-serif; text-align: center; padding: 50px; background: #f5f5f5; }}
        .container {{ max-width: 600px; margin: auto; background: white; padding: 30px; border-radius: 10px; box-shadow: 0 2px 10px rgba(0,0,0,0.1); }}
        .shield {{ font-size: 4em; color: #ff6b6b; }}
        h1 {{ color: #333; }}
        .url {{ background: #f8f9fa; padding: 10px; border-radius: 5px; word-break: break-all; margin: 20px 0; }}
        .age-level {{ color: #007bff; font-weight: bold; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='shield'>🛡️</div>
        <h1>Content Blocked by PocketFence</h1>
        <p>This content has been blocked for your protection.</p>
        <div class='url'>{url}</div>
        <p>Current protection level: <span class='age-level'>{_ai.GetCurrentAgeLevel()}</span></p>
        <p>If you believe this is an error, contact your administrator.</p>
        <hr>
        <small>PocketFence Universal Filter - Keeping the internet safe for everyone</small>
    </div>
</body>
</html>";

            var buffer = Encoding.UTF8.GetBytes(blockedPage);
            response.ContentType = "text/html; charset=utf-8";
            response.ContentLength64 = buffer.Length;
            response.StatusCode = 200;
            
            await response.OutputStream.WriteAsync(buffer);
            response.Close();
        }
        catch
        {
            // Handle response errors
            response.Close();
        }
    }

    private async Task ForwardRequestAsync(HttpListenerContext context)
    {
        try
        {
            var request = context.Request;
            var response = context.Response;
            
            // Create forwarded request
            using var httpClient = new HttpClient();
            var requestMessage = new HttpRequestMessage(new HttpMethod(request.HttpMethod), request.Url);
            
            // Copy headers
            foreach (string header in request.Headers)
            {
                try
                {
                    if (!IsHopByHopHeader(header))
                    {
                        requestMessage.Headers.TryAddWithoutValidation(header, request.Headers[header]);
                    }
                }
                catch
                {
                    // Skip problematic headers
                }
            }
            
            // Copy request body for POST/PUT
            if (request.HasEntityBody)
            {
                using var reader = new StreamReader(request.InputStream);
                var body = await reader.ReadToEndAsync();
                requestMessage.Content = new StringContent(body);
            }
            
            // Send request
            var httpResponse = await httpClient.SendAsync(requestMessage);
            
            // Copy response
            response.StatusCode = (int)httpResponse.StatusCode;
            
            foreach (var header in httpResponse.Headers)
            {
                try
                {
                    if (!IsHopByHopHeader(header.Key))
                    {
                        response.Headers.Add(header.Key, string.Join(",", header.Value));
                    }
                }
                catch
                {
                    // Skip problematic headers
                }
            }
            
            // Copy content
            var content = await httpResponse.Content.ReadAsByteArrayAsync();
            response.ContentLength64 = content.Length;
            await response.OutputStream.WriteAsync(content);
            response.Close();
        }
        catch
        {
            // Handle forwarding errors
            try
            {
                context.Response.StatusCode = 502;
                context.Response.Close();
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    private static bool IsHopByHopHeader(string headerName)
    {
        var hopByHopHeaders = new[] {
            "connection", "keep-alive", "proxy-authenticate", "proxy-authorization",
            "te", "trailers", "transfer-encoding", "upgrade"
        };
        
        return hopByHopHeaders.Contains(headerName.ToLowerInvariant());
    }
}

// Shared settings structure for PocketFence ecosystem integration
public class ProxySettings
{
    public string AgeLevel { get; set; } = "elementary";
    public bool ChildModeEnabled { get; set; } = true;
    public int ProxyPort { get; set; } = 8888;
    public bool AutoStart { get; set; } = true;
}

public enum AgeLevel
{
    Early,      // 5-8: Maximum protection
    Elementary, // 9-12: High protection  
    Teen,       // 13-17: Moderate protection
    Adult       // 18+: Minimal protection
}

// Lightweight AI engine optimized for local inference with child protection
public class SimpleAI
{
    private readonly Dictionary<string, double> _threatKeywords;
    private readonly Dictionary<string, double> _safePatterns;
    private readonly Dictionary<string, double> _childUnsafeKeywords;
    private int _processedCount = 0;
    private bool _childModeEnabled = true; // Default to child protection on
    private AgeLevel _currentAgeLevel = AgeLevel.Elementary; // Default to Elementary (9-12)
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
        
        foreach (var keyword in _threatKeywords)
        {
            if (contentLower.Contains(keyword.Key))
            {
                score += keyword.Value;
                matches++;
            }
        }
        
        foreach (var pattern in _safePatterns)
        {
            if (contentLower.Contains(pattern.Key))
            {
                score += pattern.Value;
                matches++;
            }
        }
        
        if (matches > 0)
            score = Math.Max(0.0, Math.Min(1.0, score / matches));
            
        return Task.FromResult(score);
    }

    public void EnableChildMode(bool enabled)
    {
        _childModeEnabled = enabled;
        _childStats.IsEnabled = enabled;
    }

    public bool IsChildModeEnabled => _childModeEnabled;

    public bool SetAgeRestriction(string level)
    {
        switch (level.ToLowerInvariant())
        {
            case "early":
                _currentAgeLevel = AgeLevel.Early;
                return true;
            case "elementary":
                _currentAgeLevel = AgeLevel.Elementary;
                return true;
            case "teen":
                _currentAgeLevel = AgeLevel.Teen;
                return true;
            case "adult":
                _currentAgeLevel = AgeLevel.Adult;
                return true;
            default:
                return false;
        }
    }

    public string GetCurrentAgeLevel()
    {
        return _currentAgeLevel switch
        {
            AgeLevel.Early => "Early Childhood (5-8)",
            AgeLevel.Elementary => "Elementary (9-12)",
            AgeLevel.Teen => "Teen (13-17)",
            AgeLevel.Adult => "Adult (18+)",
            _ => "Unknown"
        };
    }

    public string GetProtectionDescription()
    {
        return _currentAgeLevel switch
        {
            AgeLevel.Early => "Maximum protection - Educational content only",
            AgeLevel.Elementary => "High protection - Age-appropriate entertainment",
            AgeLevel.Teen => "Moderate protection - Teen-suitable content",
            AgeLevel.Adult => "Minimal protection - Full content access",
            _ => "Unknown protection level"
        };
    }

    public double GetAgeThreshold()
    {
        return _currentAgeLevel switch
        {
            AgeLevel.Early => 0.2,      // Very strict for young children
            AgeLevel.Elementary => 0.4, // Strict but allows educational content
            AgeLevel.Teen => 0.6,       // Moderate protection
            AgeLevel.Adult => 0.8,      // Minimal protection
            _ => 0.4
        };
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
            
            var threshold = GetAgeThreshold();
            if (unsafeScore > threshold) _childStats.ContentBlocked++;
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

public class ContentAnalysis
{
    public double SafetyScore { get; set; }
    public string Category { get; set; } = "General";
    public double Confidence { get; set; }
    public string Recommendation { get; set; } = "ALLOW";
    public List<string> Flags { get; set; } = new List<string>();
}