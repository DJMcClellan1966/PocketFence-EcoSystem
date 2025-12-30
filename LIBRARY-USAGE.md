# PocketFence AI Library - Integration Examples

## 🚀 Quick Start

### 1. **Basic Usage**
```csharp
using PocketFence.Library;

// Initialize the engine
var engine = new PocketFenceEngine();
await engine.InitializeAsync();

// Check a URL
var urlResult = await engine.CheckUrlAsync("https://example.com");
if (urlResult.IsBlocked)
{
    Console.WriteLine($"Blocked: {urlResult.Reason}");
}

// Analyze content
var contentResult = await engine.AnalyzeContentAsync("Hello world!");
Console.WriteLine($"Safety Score: {contentResult.SafetyScore:F2}");
```

### 2. **Custom Configuration**
```csharp
var config = new PocketFenceConfig
{
    ChildModeEnabled = true,
    ThreatThreshold = 0.8,
    CustomBlockedDomains = { "my-blocked-site.com" },
    CustomBlockedKeywords = { "custom-keyword" }
};

var engine = new PocketFenceEngine(config);
await engine.InitializeAsync();
```

## 📱 Integration Examples

### **ASP.NET Core Web Application**
```csharp
// Startup.cs
public void ConfigureServices(IServiceCollection services)
{
    services.AddSingleton<PocketFenceEngine>();
}

// Controller
[ApiController]
public class ContentController : ControllerBase
{
    private readonly PocketFenceEngine _pocketFence;
    
    public ContentController(PocketFenceEngine pocketFence)
    {
        _pocketFence = pocketFence;
    }
    
    [HttpPost("check-url")]
    public async Task<ActionResult<UrlCheckResult>> CheckUrl([FromBody] string url)
    {
        var result = await _pocketFence.CheckUrlAsync(url);
        return Ok(result);
    }
    
    [HttpPost("analyze-content")]
    public async Task<ActionResult<ContentAnalysisResult>> AnalyzeContent([FromBody] string content)
    {
        var result = await _pocketFence.AnalyzeContentAsync(content);
        return Ok(result);
    }
}
```

### **Windows Forms Desktop App**
```csharp
public partial class MainForm : Form
{
    private PocketFenceEngine _engine;
    
    protected override async void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        
        // Initialize PocketFence
        _engine = new PocketFenceEngine();
        await _engine.InitializeAsync();
        
        lblStatus.Text = "✅ PocketFence Ready";
    }
    
    private async void btnCheckUrl_Click(object sender, EventArgs e)
    {
        var result = await _engine.CheckUrlAsync(txtUrl.Text);
        
        if (result.IsBlocked)
        {
            lblResult.Text = $"❌ BLOCKED: {result.Reason}";
            lblResult.ForeColor = Color.Red;
        }
        else
        {
            lblResult.Text = "✅ ALLOWED";
            lblResult.ForeColor = Color.Green;
        }
    }
}
```

### **WPF Application**
```csharp
public partial class MainWindow : Window
{
    private PocketFenceEngine _engine;
    
    public MainWindow()
    {
        InitializeComponent();
        InitializePocketFence();
    }
    
    private async void InitializePocketFence()
    {
        _engine = new PocketFenceEngine();
        await _engine.InitializeAsync();
        StatusLabel.Content = "🛡️ Child Protection Active";
    }
    
    private async void CheckButton_Click(object sender, RoutedEventArgs e)
    {
        var result = await _engine.CheckUrlAsync(UrlTextBox.Text);
        
        ResultPanel.Background = result.IsBlocked ? 
            new SolidColorBrush(Colors.LightCoral) : 
            new SolidColorBrush(Colors.LightGreen);
            
        ResultLabel.Content = result.IsBlocked ? 
            $"BLOCKED: {result.Reason}" : 
            "ALLOWED";
    }
}
```

### **Blazor Web App**
```csharp
@page "/content-filter"
@inject PocketFenceEngine PocketFence

<h3>Content Filter</h3>

<div class="mb-3">
    <label>URL to Check:</label>
    <input @bind="urlToCheck" class="form-control" />
    <button @onclick="CheckUrl" class="btn btn-primary">Check URL</button>
</div>

<div class="mb-3">
    <label>Content to Analyze:</label>
    <textarea @bind="contentToAnalyze" class="form-control" rows="3"></textarea>
    <button @onclick="AnalyzeContent" class="btn btn-secondary">Analyze Content</button>
</div>

@if (!string.IsNullOrEmpty(result))
{
    <div class="alert @(isBlocked ? "alert-danger" : "alert-success")">
        @result
    </div>
}

@code {
    private string urlToCheck = "";
    private string contentToAnalyze = "";
    private string result = "";
    private bool isBlocked = false;
    
    private async Task CheckUrl()
    {
        var checkResult = await PocketFence.CheckUrlAsync(urlToCheck);
        isBlocked = checkResult.IsBlocked;
        result = isBlocked ? $"BLOCKED: {checkResult.Reason}" : "ALLOWED";
    }
    
    private async Task AnalyzeContent()
    {
        var analysis = await PocketFence.AnalyzeContentAsync(contentToAnalyze);
        isBlocked = !analysis.IsChildSafe;
        result = $"Safety Score: {analysis.SafetyScore:F2} | Category: {analysis.Category}";
    }
}
```

### **Console Application**
```csharp
class Program
{
    static async Task Main(string[] args)
    {
        // Initialize PocketFence
        var engine = new PocketFenceEngine();
        await engine.InitializeAsync();
        
        Console.WriteLine("🤖 PocketFence AI Library Demo");
        
        while (true)
        {
            Console.Write("Enter URL to check (or 'quit'): ");
            var input = Console.ReadLine();
            
            if (input?.ToLower() == "quit") break;
            
            var result = await engine.CheckUrlAsync(input ?? "");
            
            Console.WriteLine($"Result: {(result.IsBlocked ? "❌ BLOCKED" : "✅ ALLOWED")}");
            Console.WriteLine($"Reason: {result.Reason}");
            Console.WriteLine($"Threat Score: {result.ThreatScore:F2}");
            Console.WriteLine();
        }
    }
}
```

### **Unity Game Engine**
```csharp
using UnityEngine;
using PocketFence.Library;

public class ChatFilter : MonoBehaviour
{
    private PocketFenceEngine _engine;
    
    async void Start()
    {
        // Initialize child-safe filtering for game chat
        var config = new PocketFenceConfig 
        { 
            ChildModeEnabled = true,
            ThreatThreshold = 0.6 // Lower threshold for games
        };
        
        _engine = new PocketFenceEngine(config);
        await _engine.InitializeAsync();
        
        Debug.Log("🛡️ Chat filter initialized");
    }
    
    public async Task<bool> IsMessageSafe(string message)
    {
        var result = await _engine.AnalyzeContentAsync(message);
        return result.IsChildSafe;
    }
    
    // Usage in chat system
    public async void OnPlayerMessage(string playerMessage)
    {
        if (await IsMessageSafe(playerMessage))
        {
            DisplayMessage(playerMessage);
        }
        else
        {
            DisplayMessage("[Message blocked - inappropriate content]");
            Debug.Log($"Blocked message: {playerMessage}");
        }
    }
}
```

## 📦 **NuGet Package Distribution**

Create a `.csproj` for library distribution:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <PackageId>PocketFence.AI</PackageId>
    <Version>1.0.0</Version>
    <Authors>Your Name</Authors>
    <Description>Lightweight local AI content filtering engine</Description>
    <PackageTags>ai;content-filter;child-protection;safety</PackageTags>
  </PropertyGroup>
</Project>
```

## 🎯 **Key Benefits as a Library**

- **Zero External Dependencies**: Works offline
- **Lightweight**: ~10MB including all models
- **Cross-Platform**: Works on Windows, macOS, Linux
- **High Performance**: <1ms analysis time
- **Child-Safe**: Built-in child protection features
- **Easy Integration**: Simple async API
- **Privacy-First**: No data sent to external servers