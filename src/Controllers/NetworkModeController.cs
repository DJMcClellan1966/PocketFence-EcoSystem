using Microsoft.AspNetCore.Mvc;
using PocketFence_Simple.Services;

namespace PocketFence_Simple.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NetworkModeController(INetworkModeService networkModeService, ILogger<NetworkModeController> logger) : ControllerBase
{
    private readonly INetworkModeService _networkModeService = networkModeService;
    private readonly ILogger<NetworkModeController> _logger = logger;

    [HttpGet("status")]
    public ActionResult GetNetworkModeStatus()
    {
        try
        {
            var currentMode = _networkModeService.CurrentMode;
            var networkName = _networkModeService.NetworkName;
            var canCreateHotspot = _networkModeService.CanCreateHotspot;
            var isConnectedToiOSHotspot = _networkModeService.IsConnectedToiOSHotspot;
            var isConnectedToAndroidHotspot = _networkModeService.IsConnectedToAndroidHotspot;

            return Ok(new
            {
                mode = currentMode.ToString(),
                networkName,
                isOnline = _networkModeService.IsOnline,
                canCreateHotspot,
                isConnectedToiOSHotspot,
                isConnectedToAndroidHotspot,
                capabilities = GetModeCapabilities(currentMode),
                recommendations = GetModeRecommendations(currentMode, networkName)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get network mode status");
            return StatusCode(500, new { error = "Failed to get network mode status" });
        }
    }

    [HttpPost("refresh")]
    public async Task<ActionResult> RefreshNetworkMode()
    {
        try
        {
            var detectedMode = await _networkModeService.DetectNetworkModeAsync();
            
            _logger.LogInformation("Network mode refreshed: {Mode}", detectedMode);
            
            return Ok(new
            {
                mode = detectedMode.ToString(),
                networkName = _networkModeService.NetworkName,
                isOnline = _networkModeService.IsOnline,
                message = "Network mode refreshed successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh network mode");
            return StatusCode(500, new { error = "Failed to refresh network mode" });
        }
    }

    [HttpGet("recommendations")]
    public ActionResult GetNetworkRecommendations()
    {
        try
        {
            var currentMode = _networkModeService.CurrentMode;
            var networkName = _networkModeService.NetworkName;
            
            var recommendations = GetDetailedRecommendations(currentMode, networkName);
            
            return Ok(new
            {
                currentMode = currentMode.ToString(),
                networkName,
                recommendations
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get network recommendations");
            return StatusCode(500, new { error = "Failed to get network recommendations" });
        }
    }

    private static object GetModeCapabilities(NetworkMode mode)
    {
        return mode switch
        {
            NetworkMode.WindowsHotspot => new
            {
                canCreateHotspot = true,
                canFilterTraffic = true,
                canManageDevices = true,
                canMonitorUsage = true,
                hasFullControl = true
            },
            NetworkMode.iOSCellularHotspot => new
            {
                canCreateHotspot = false,
                canFilterTraffic = true,
                canManageDevices = false,
                canMonitorUsage = true,
                hasFullControl = false
            },
            NetworkMode.AndroidCellularHotspot => new
            {
                canCreateHotspot = false,
                canFilterTraffic = true,
                canManageDevices = false,
                canMonitorUsage = true,
                hasFullControl = false,
                supportsWiFiBridge = true
            },
            NetworkMode.AndroidWiFiBridge => new
            {
                canCreateHotspot = true,
                canFilterTraffic = true,
                canManageDevices = true,
                canMonitorUsage = true,
                hasFullControl = true,
                isWiFiBridge = true
            },
            NetworkMode.SharedWiFi => new
            {
                canCreateHotspot = true,
                canFilterTraffic = true,
                canManageDevices = false,
                canMonitorUsage = true,
                hasFullControl = false
            },
            NetworkMode.Offline => new
            {
                canCreateHotspot = false,
                canFilterTraffic = false,
                canManageDevices = false,
                canMonitorUsage = false,
                hasFullControl = false
            },
            _ => new { }
        };
    }

    private static string[] GetModeRecommendations(NetworkMode mode, string networkName)
    {
        return mode switch
        {
            NetworkMode.WindowsHotspot => [
                "✅ Optimal setup! PocketFence has full control",
                "📱 iOS devices can connect and be fully managed",
                "🔒 All traffic is filtered and monitored",
                "⚡ Best performance for parental controls"
            ],
            
            NetworkMode.iOSCellularHotspot => [
                $"📱 Connected to '{networkName}' iOS cellular hotspot",
                "🔍 PocketFence can monitor but not manage the hotspot",
                "⚠️ Limited device management capabilities",
                "💡 For full control, enable Windows hotspot instead"
            ],
            
            NetworkMode.AndroidCellularHotspot => [
                $"🤖 Connected to '{networkName}' Android cellular hotspot",
                "📊 PocketFence can monitor traffic from cellular connection",
                "⚠️ Limited device management on cellular hotspot",
                "💡 Android can also bridge WiFi if available"
            ],
            
            NetworkMode.AndroidWiFiBridge => [
                $"🌉 Connected to '{networkName}' Android WiFi bridge",
                "✅ Android device is sharing its WiFi connection",
                "🎯 Full PocketFence functionality available",
                "⚡ Excellent setup for parental control!"
            ],
            
            NetworkMode.SharedWiFi => [
                $"🏠 Connected to shared WiFi '{networkName}'",
                "✅ PocketFence can filter content for this device",
                "⚡ You can create a Windows hotspot for child devices",
                "🔧 Enable hotspot to manage multiple devices"
            ],
            
            NetworkMode.Offline => [
                "🔌 No network connection detected",
                "📶 Connect to WiFi or cellular to use PocketFence",
                "🔗 Check your network settings",
                "⚠️ Most features require internet connection"
            ],
            
            _ => ["❓ Unknown network mode"]
        };
    }

    private static object GetDetailedRecommendations(NetworkMode mode, string networkName)
    {
        return mode switch
        {
            NetworkMode.WindowsHotspot => new
            {
                status = "optimal",
                title = "Perfect Setup! 🎯",
                description = "PocketFence is running in optimal mode with full parental control capabilities.",
                actions = new[]
                {
                    new { text = "Manage Connected Devices", icon = "📱", action = "view-devices" },
                    new { text = "Configure Content Filters", icon = "🔒", action = "configure-filters" },
                    new { text = "View Usage Reports", icon = "📊", action = "view-reports" }
                }
            },

            NetworkMode.iOSCellularHotspot => new
            {
                status = "limited",
                title = "iOS Hotspot Mode 📱",
                description = $"Connected to '{networkName}' iOS cellular hotspot. Some features are limited.",
                actions = new[]
                {
                    new { text = "Monitor Traffic Only", icon = "🔍", action = "monitor-traffic" },
                    new { text = "View Connection Info", icon = "📋", action = "connection-info" },
                    new { text = "Switch to Windows Hotspot", icon = "🔄", action = "enable-windows-hotspot" }
                }
            },

            NetworkMode.AndroidCellularHotspot => new
            {
                status = "limited",
                title = "Android Cellular Hotspot 🤖",
                description = $"Connected to '{networkName}' Android cellular hotspot. Traffic monitoring available.",
                actions = new[]
                {
                    new { text = "Monitor Traffic", icon = "📊", action = "monitor-traffic" },
                    new { text = "Check WiFi Bridge Support", icon = "🌉", action = "check-wifi-bridge" },
                    new { text = "Connection Details", icon = "📋", action = "connection-info" }
                }
            },

            NetworkMode.AndroidWiFiBridge => new
            {
                status = "excellent",
                title = "Android WiFi Bridge 🌉",
                description = $"Connected to '{networkName}' via Android WiFi bridge. Full functionality available!",
                actions = new[]
                {
                    new { text = "Manage Connected Devices", icon = "📱", action = "view-devices" },
                    new { text = "Configure Filters", icon = "🔒", action = "configure-filters" },
                    new { text = "View Usage Reports", icon = "📊", action = "view-reports" }
                }
            },

            NetworkMode.SharedWiFi => new
            {
                status = "good",
                title = "Shared WiFi Mode 🏠",
                description = $"Connected to '{networkName}'. You can create a hotspot for child devices.",
                actions = new[]
                {
                    new { text = "Enable Windows Hotspot", icon = "📡", action = "enable-hotspot" },
                    new { text = "Filter This Device", icon = "🔒", action = "configure-local-filter" },
                    new { text = "Network Information", icon = "ℹ️", action = "network-info" }
                }
            },

            NetworkMode.Offline => new
            {
                status = "offline",
                title = "No Connection 🔌",
                description = "PocketFence requires a network connection to function.",
                actions = new[]
                {
                    new { text = "Check Network Settings", icon = "⚙️", action = "check-network" },
                    new { text = "Troubleshoot Connection", icon = "🔧", action = "troubleshoot" },
                    new { text = "Refresh Status", icon = "🔄", action = "refresh" }
                }
            },

            _ => new
            {
                status = "unknown",
                title = "Unknown Mode ❓",
                description = "Unable to determine network mode",
                actions = new[]
                {
                    new { text = "Refresh Network Status", icon = "🔄", action = "refresh" }
                }
            }
        };
    }
}