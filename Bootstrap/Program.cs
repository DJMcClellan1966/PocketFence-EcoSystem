using System.Net.Http;
using System.Runtime.InteropServices;
using System.IO.Compression;

namespace PocketFence.Bootstrap;

/// <summary>
/// Lightweight bootstrap downloader for PocketFence AI
/// Downloads and launches platform-specific optimized binary
/// </summary>
public class Program
{
    private static readonly HttpClient _httpClient = new();
    private static readonly string _baseUrl = "https://github.com/DJMcClellan1966/PocketFence-EcoSystem/releases/latest/download/";
    
    public static async Task<int> Main(string[] args)
    {
        Console.WriteLine("🚀 PocketFence AI Bootstrap v1.0");
        Console.WriteLine("Downloading optimized binary for your platform...");
        
        try
        {
            // Detect platform
            var platform = GetPlatformIdentifier();
            var fileName = $"PocketFence-AI-{platform}";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                fileName += ".exe";
            
            var localPath = Path.Combine(".", fileName);
            
            // Download if not exists or outdated
            if (!File.Exists(localPath) || await IsUpdateAvailable(platform))
            {
                await DownloadBinary(platform, localPath);
            }
            
            // Make executable on Unix systems
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                await MakeExecutable(localPath);
            }
            
            // Launch the main application
            Console.WriteLine("✅ Launching PocketFence AI...");
            await LaunchApplication(localPath, args);
            
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
            return 1;
        }
    }
    
    private static string GetPlatformIdentifier()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return RuntimeInformation.ProcessArchitecture == Architecture.Arm64 
                ? "win-arm64" : "win-x64";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return RuntimeInformation.ProcessArchitecture == Architecture.Arm64 
                ? "osx-arm64" : "osx-x64";
        }
        else // Linux
        {
            return RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.Arm64 => "linux-arm64",
                Architecture.Arm => "linux-arm",
                _ => "linux-x64"
            };
        }
    }
    
    private static async Task<bool> IsUpdateAvailable(string platform)
    {
        try
        {
            var versionUrl = $"{_baseUrl}version.txt";
            var latestVersion = await _httpClient.GetStringAsync(versionUrl);
            var currentVersion = GetCurrentVersion();
            
            return !string.Equals(latestVersion.Trim(), currentVersion, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false; // Assume no update if can't check
        }
    }
    
    private static async Task DownloadBinary(string platform, string localPath)
    {
        var url = $"{_baseUrl}PocketFence-AI-{platform}";
        if (platform.StartsWith("win"))
            url += ".exe";
        
        Console.WriteLine($"📥 Downloading from {url}...");
        
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        
        var totalBytes = response.Content.Headers.ContentLength ?? 0;
        var downloadedBytes = 0L;
        
        using var fileStream = File.Create(localPath);
        using var httpStream = await response.Content.ReadAsStreamAsync();
        
        var buffer = new byte[8192];
        int bytesRead;
        
        while ((bytesRead = await httpStream.ReadAsync(buffer)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
            downloadedBytes += bytesRead;
            
            if (totalBytes > 0)
            {
                var progress = (double)downloadedBytes / totalBytes * 100;
                Console.Write($"\r📦 Progress: {progress:F1}% ({downloadedBytes / 1024 / 1024:F1}MB)");
            }
        }
        
        Console.WriteLine($"\n✅ Downloaded {downloadedBytes / 1024 / 1024:F1}MB");
    }
    
    private static async Task MakeExecutable(string path)
    {
        if (File.Exists("/bin/chmod"))
        {
            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "/bin/chmod",
                    Arguments = $"+x \"{path}\"",
                    UseShellExecute = false
                }
            };
            process.Start();
            await process.WaitForExitAsync();
        }
    }
    
    private static async Task LaunchApplication(string path, string[] args)
    {
        var process = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = path,
                Arguments = string.Join(" ", args),
                UseShellExecute = false
            }
        };
        
        process.Start();
        await process.WaitForExitAsync();
    }
    
    private static string GetCurrentVersion()
    {
        var versionFile = "version.txt";
        return File.Exists(versionFile) ? File.ReadAllText(versionFile).Trim() : "unknown";
    }
}