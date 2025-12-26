using System.Collections.Frozen;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
#if WINDOWS
using System.Management;
#endif

namespace PocketFence_Simple.Services;

public enum NetworkMode
{
    WindowsHotspot,        // Windows computer creates hotspot
    MacOSHotspot,          // macOS computer creates Internet Sharing hotspot
    LinuxHotspot,          // Linux computer creates hostapd hotspot
    iOSCellularHotspot,    // Connected to iOS device's cellular hotspot
    AndroidCellularHotspot, // Connected to Android device's cellular hotspot
    AndroidWiFiBridge,     // Connected to Android device bridging WiFi
    SharedWiFi,           // Both devices on same existing WiFi
    Offline
}

public partial class NetworkModeService(ILogger<NetworkModeService> logger, IConfiguration configuration) : INetworkModeService
{
    private readonly ILogger<NetworkModeService> _logger = logger;
    private readonly IConfiguration _configuration = configuration;
    private NetworkMode _currentMode = NetworkMode.Offline;
    private string _networkName = string.Empty;
    private readonly PeriodicTimer _monitorTimer = new(TimeSpan.FromSeconds(10));

    // Apple OUI prefixes for detecting iOS hotspots
    private static readonly FrozenSet<string> AppleOuiPrefixes = new[]
    {
        "00:03:93", "00:0A:27", "00:0A:95", "00:0D:93", "00:11:24", "00:13:E8",
        "00:14:51", "00:16:CB", "00:17:F2", "00:19:E3", "00:1B:63", "00:1C:B3",
        "00:1E:C2", "00:21:E9", "00:23:12", "00:23:DF", "00:24:36", "00:25:00",
        "00:25:BC", "00:26:08", "00:26:4A", "00:26:B0", "00:26:BB", "10:9A:DD",
        "14:20:5E", "28:E0:2C", "40:A6:D9", "58:55:CA", "70:11:24", "8C:7A:AA",
        "90:72:40", "A4:D1:D2", "B8:09:8A", "BC:92:6B", "D0:E1:40", "DC:2B:61"
    }.ToFrozenSet();

    // Android OUI prefixes for detecting Android hotspots
    private static readonly FrozenSet<string> AndroidOuiPrefixes = new[]
    {
        // Samsung
        "00:12:FB", "00:15:B9", "00:16:6B", "00:17:C9", "00:18:AF", "00:1A:8A",
        "00:1D:25", "00:1E:7D", "00:21:19", "00:23:39", "00:26:37", "08:00:28",
        "10:1D:C0", "20:64:32", "34:23:BA", "38:AA:3C", "3C:8B:FE", "50:CC:F8",
        "5C:0A:5B", "60:21:C0", "78:1F:DB", "84:38:35", "88:30:8A", "8C:77:12",
        "90:18:7C", "98:52:3D", "A0:21:95", "AC:36:13", "C8:BA:94", "E8:50:8B",
        // Google/HTC
        "00:11:09", "00:13:E9", "00:15:70", "00:17:E2", "00:19:79", "00:1B:98",
        "00:1E:46", "00:22:58", "00:23:76", "00:25:D3", "7C:61:93", "A4:77:33",
        "B4:CE:F6", "CC:FA:00", "F0:25:B7", "F4:F5:24",
        // LG
        "00:1C:62", "00:1F:6B", "00:22:A9", "00:26:E8", "10:68:3F", "34:FC:EF",
        "E0:B7:B1", "E8:5B:5B", "F8:A9:D0",
        // OnePlus
        "AC:37:43", "A8:26:D9", "E8:92:A4"
    }.ToFrozenSet();

    [GeneratedRegex(@"iPhone.*'s?\s+iPhone", RegexOptions.IgnoreCase)]
    private static partial Regex iPhoneHotspotPattern();

    [GeneratedRegex(@"(\w+)'s?\s+(Galaxy|Pixel|OnePlus|LG|Android|Phone)", RegexOptions.IgnoreCase)]
    private static partial Regex AndroidHotspotPattern();

    [GeneratedRegex(@"^(AndroidAP|AndroidHotspot|Galaxy\w+|Pixel\w+|OnePlus\w+)", RegexOptions.IgnoreCase)]
    private static partial Regex AndroidDevicePattern();

    public NetworkMode CurrentMode => _currentMode;
    public string NetworkName => _networkName;
    public bool IsOnline => _currentMode != NetworkMode.Offline;
    public bool CanCreateHotspot => _currentMode switch
    {
        NetworkMode.SharedWiFi => true,
        NetworkMode.Offline => false,
        NetworkMode.WindowsHotspot => true,
        NetworkMode.MacOSHotspot => true,
        NetworkMode.LinuxHotspot => true,
        NetworkMode.iOSCellularHotspot => false, // Cannot create system hotspot while on iOS hotspot
        NetworkMode.AndroidCellularHotspot => false, // Cannot create system hotspot while on Android cellular
        NetworkMode.AndroidWiFiBridge => true, // Android WiFi bridge allows additional hotspots
        _ => false
    };
    public bool IsConnectedToiOSHotspot => _currentMode == NetworkMode.iOSCellularHotspot;
    public bool IsConnectedToAndroidHotspot => _currentMode is NetworkMode.AndroidCellularHotspot or NetworkMode.AndroidWiFiBridge;

    public async Task StartMonitoringAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("🔍 Starting network mode monitoring");
        
        // Initial detection
        await DetectNetworkModeAsync();

        // Start periodic monitoring
        _ = Task.Run(async () =>
        {
            while (await _monitorTimer.WaitForNextTickAsync(cancellationToken))
            {
                await DetectNetworkModeAsync();
            }
        }, cancellationToken);
    }

    public async Task<NetworkMode> DetectNetworkModeAsync()
    {
        try
        {
            var activeInterface = GetActiveNetworkInterface();
            if (activeInterface == null)
            {
                await UpdateNetworkModeAsync(NetworkMode.Offline, "No Connection");
                return _currentMode;
            }

            var networkInfo = await GetNetworkInfoAsync(activeInterface);
            var detectedMode = await AnalyzeNetworkModeAsync(activeInterface, networkInfo);
            
            await UpdateNetworkModeAsync(detectedMode, networkInfo.Name);
            return _currentMode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to detect network mode");
            return _currentMode;
        }
    }

    private async Task<NetworkMode> AnalyzeNetworkModeAsync(NetworkInterface networkInterface, NetworkInfo networkInfo)
    {
        // Check if we're running our own Windows hotspot
        if (await IsWindowsHotspotActiveAsync())
        {
            _logger.LogDebug("Detected Windows hotspot mode");
            return NetworkMode.WindowsHotspot;
        }

        // Check if connected to iOS cellular hotspot
        if (await IsConnectedToiOSHotspotAsync(networkInterface, networkInfo))
        {
            _logger.LogDebug("Detected iOS cellular hotspot connection");
            return NetworkMode.iOSCellularHotspot;
        }

        // Check if connected to Android hotspot
        var androidMode = await DetectAndroidHotspotAsync(networkInterface, networkInfo);
        if (androidMode != null)
        {
            _logger.LogDebug("Detected Android hotspot connection: {Mode}", androidMode);
            return androidMode.Value;
        }

        // Check if on shared WiFi network
        if (networkInterface.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 && 
            networkInterface.OperationalStatus == OperationalStatus.Up)
        {
            _logger.LogDebug("Detected shared WiFi mode");
            return NetworkMode.SharedWiFi;
        }

        return NetworkMode.Offline;
    }

    private async Task<bool> IsWindowsHotspotActiveAsync()
    {
        try
        {
            using var process = new System.Diagnostics.Process
            {
                StartInfo = new()
                {
                    FileName = "netsh",
                    Arguments = "wlan show hostednetwork",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            return output.Contains("Status                 : Started", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check Windows hotspot status");
            return false;
        }
    }

    private async Task<bool> IsConnectedToiOSHotspotAsync(NetworkInterface networkInterface, NetworkInfo networkInfo)
    {
        try
        {
            // Check network name patterns
            if (iPhoneHotspotPattern().IsMatch(networkInfo.Name))
            {
                return true;
            }

            // Check for Apple MAC address patterns in network
            var gatewayMac = await GetGatewayMacAddressAsync();
            if (!string.IsNullOrEmpty(gatewayMac))
            {
                var ouiPrefix = gatewayMac[..8].ToUpperInvariant();
                if (AppleOuiPrefixes.Contains(ouiPrefix))
                {
                    _logger.LogDebug("Detected Apple device MAC: {MacAddress}", gatewayMac);
                    return true;
                }
            }

            // Check DHCP vendor information for iOS devices
            return await CheckDhcpVendorInfoAsync(networkInterface);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to detect iOS hotspot connection");
            return false;
        }
    }

    private async Task<string> GetGatewayMacAddressAsync()
    {
        try
        {
            using var process = new System.Diagnostics.Process
            {
                StartInfo = new()
                {
                    FileName = "arp",
                    Arguments = "-a",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            // Parse ARP table to find gateway MAC
            var gateway = NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.OperationalStatus == OperationalStatus.Up)
                .SelectMany(ni => ni.GetIPProperties().GatewayAddresses)
                .FirstOrDefault()?.Address?.ToString();

            if (gateway != null)
            {
                var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    if (line.Contains(gateway))
                    {
                        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 2)
                        {
                            return parts[1].Replace('-', ':');
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get gateway MAC address");
        }

        return string.Empty;
    }

    private async Task<bool> CheckDhcpVendorInfoAsync(NetworkInterface networkInterface)
    {
#if WINDOWS
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT * FROM Win32_NetworkAdapterConfiguration WHERE IPEnabled = true");
            
            foreach (ManagementObject adapter in searcher.Get())
            {
                var description = adapter["Description"]?.ToString();
                if (description != null && description.Contains("Apple", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                var dhcpServer = adapter["DHCPServer"]?.ToString();
                if (dhcpServer != null)
                {
                    // iOS hotspot DHCP servers typically use 172.20.10.1
                    if (dhcpServer.StartsWith("172.20.10."))
                    {
                        return true;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check DHCP vendor information");
        }
#else
        // Non-Windows platforms: Use basic network interface check
        await Task.Delay(10); // Async method requirement
        
        // Check if network interface name suggests iOS hotspot
        var interfaceName = networkInterface.Name.ToLowerInvariant();
        if (interfaceName.Contains("iphone") || interfaceName.Contains("ios"))
        {
            return true;
        }
#endif

        return false;
    }

    private async Task<NetworkMode?> DetectAndroidHotspotAsync(NetworkInterface networkInterface, NetworkInfo networkInfo)
    {
        try
        {
            // Check network name patterns for Android hotspots
            if (AndroidHotspotPattern().IsMatch(networkInfo.Name) || AndroidDevicePattern().IsMatch(networkInfo.Name))
            {
                // Determine if it's cellular hotspot or WiFi bridge
                return await DetermineAndroidHotspotTypeAsync(networkInfo);
            }

            // Check for Android MAC address patterns in network
            var gatewayMac = await GetGatewayMacAddressAsync();
            if (!string.IsNullOrEmpty(gatewayMac))
            {
                var ouiPrefix = gatewayMac[..8].ToUpperInvariant();
                if (AndroidOuiPrefixes.Contains(ouiPrefix))
                {
                    _logger.LogDebug("Detected Android device MAC: {MacAddress}", gatewayMac);
                    return await DetermineAndroidHotspotTypeAsync(networkInfo);
                }
            }

            // Check DHCP vendor information for Android devices
            if (await CheckAndroidDhcpVendorInfoAsync(networkInterface))
            {
                return await DetermineAndroidHotspotTypeAsync(networkInfo);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to detect Android hotspot connection");
        }

        return null;
    }

    private async Task<NetworkMode> DetermineAndroidHotspotTypeAsync(NetworkInfo networkInfo)
    {
        try
        {
            // Check network characteristics to determine if it's WiFi bridge or cellular hotspot
            // Android WiFi bridge typically maintains similar IP ranges to the original network
            // Cellular hotspots typically use different ranges (like 192.168.43.x for many Android devices)
            
            var localIp = await GetLocalIPAddressAsync();
            if (!string.IsNullOrEmpty(localIp))
            {
                // Common Android cellular hotspot IP ranges
                if (localIp.StartsWith("192.168.43.") ||   // Most common Android hotspot range
                    localIp.StartsWith("192.168.49.") ||   // Alternative Android range
                    localIp.StartsWith("172.20.10.") ||    // Some Samsung devices
                    localIp.StartsWith("10.0.0."))         // Some devices use this range
                {
                    _logger.LogDebug("Android cellular hotspot detected based on IP range: {IP}", localIp);
                    return NetworkMode.AndroidCellularHotspot;
                }
                else
                {
                    _logger.LogDebug("Android WiFi bridge detected based on IP range: {IP}", localIp);
                    return NetworkMode.AndroidWiFiBridge;
                }
            }

            // Default to cellular hotspot if we can't determine the type
            return NetworkMode.AndroidCellularHotspot;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to determine Android hotspot type, defaulting to cellular");
            return NetworkMode.AndroidCellularHotspot;
        }
    }

    private async Task<string> GetLocalIPAddressAsync()
    {
        try
        {
            var activeInterface = GetActiveNetworkInterface();
            if (activeInterface != null)
            {
                var ipProperties = activeInterface.GetIPProperties();
                var ipAddress = ipProperties.UnicastAddresses
                    .FirstOrDefault(ip => ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork &&
                                         !System.Net.IPAddress.IsLoopback(ip.Address))?.Address?.ToString();
                
                return ipAddress ?? string.Empty;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get local IP address");
        }

        return string.Empty;
    }

    private async Task<bool> CheckAndroidDhcpVendorInfoAsync(NetworkInterface networkInterface)
    {
#if WINDOWS
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT * FROM Win32_NetworkAdapterConfiguration WHERE IPEnabled = true");
            
            foreach (ManagementObject adapter in searcher.Get())
            {
                var description = adapter["Description"]?.ToString();
                if (description != null && (
                    description.Contains("Android", StringComparison.OrdinalIgnoreCase) ||
                    description.Contains("Samsung", StringComparison.OrdinalIgnoreCase) ||
                    description.Contains("Google", StringComparison.OrdinalIgnoreCase) ||
                    description.Contains("LG", StringComparison.OrdinalIgnoreCase) ||
                    description.Contains("OnePlus", StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }

                var dhcpServer = adapter["DHCPServer"]?.ToString();
                if (dhcpServer != null)
                {
                    // Common Android hotspot DHCP server IPs
                    if (dhcpServer.StartsWith("192.168.43.") ||
                        dhcpServer.StartsWith("192.168.49.") ||
                        dhcpServer.StartsWith("10.0.0."))
                    {
                        return true;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check Android DHCP vendor information");
        }
#else
        // Non-Windows platforms: Use basic network interface check
        await Task.Delay(10); // Async method requirement
        
        // Check if network interface name suggests Android hotspot
        var interfaceName = networkInterface.Name.ToLowerInvariant();
        if (interfaceName.Contains("android") || interfaceName.Contains("samsung") || 
            interfaceName.Contains("google") || interfaceName.Contains("lg"))
        {
            return true;
        }
#endif

        return false;
    }

    private NetworkInterface? GetActiveNetworkInterface()
    {
        return NetworkInterface.GetAllNetworkInterfaces()
            .Where(ni => ni.OperationalStatus == OperationalStatus.Up &&
                        ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .OrderByDescending(ni => ni.Speed)
            .FirstOrDefault();
    }

    private async Task<NetworkInfo> GetNetworkInfoAsync(NetworkInterface networkInterface)
    {
        var name = "Unknown";
        
        try
        {
            if (networkInterface.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
            {
                name = await GetWiFiNetworkNameAsync();
            }
            else
            {
                name = networkInterface.Name;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get network name");
        }

        return new NetworkInfo(name, networkInterface.NetworkInterfaceType);
    }

    private async Task<string> GetWiFiNetworkNameAsync()
    {
        try
        {
            using var process = new System.Diagnostics.Process
            {
                StartInfo = new()
                {
                    FileName = "netsh",
                    Arguments = "wlan show interfaces",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            foreach (var line in output.Split('\n'))
            {
                if (line.Trim().StartsWith("SSID", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = line.Split(':', 2, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 2)
                    {
                        return parts[1].Trim();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get WiFi network name");
        }

        return "WiFi Network";
    }

    private async Task UpdateNetworkModeAsync(NetworkMode newMode, string networkName)
    {
        if (_currentMode != newMode || _networkName != networkName)
        {
            var previousMode = _currentMode;
            _currentMode = newMode;
            _networkName = networkName;

            _logger.LogInformation("🔄 Network mode changed: {PreviousMode} → {NewMode} on {NetworkName}", 
                previousMode, newMode, networkName);

            // Notify other services of mode change
            await OnNetworkModeChangedAsync(previousMode, newMode);
        }
    }

    private async Task OnNetworkModeChangedAsync(NetworkMode previousMode, NetworkMode newMode)
    {
        try
        {
            // Different behaviors based on network mode
            switch (newMode)
            {
                case NetworkMode.WindowsHotspot:
                    _logger.LogInformation("📡 Windows hotspot mode: PocketFence can manage hotspot and filter content");
                    break;

                case NetworkMode.iOSCellularHotspot:
                    _logger.LogInformation("📱 iOS cellular hotspot mode: PocketFence will monitor traffic but cannot manage hotspot");
                    break;

                case NetworkMode.AndroidCellularHotspot:
                    _logger.LogInformation("🤖 Android cellular hotspot mode: PocketFence monitoring traffic from Android cellular connection");
                    break;

                case NetworkMode.AndroidWiFiBridge:
                    _logger.LogInformation("🌉 Android WiFi bridge mode: Android device bridging WiFi connection as hotspot");
                    break;

                case NetworkMode.SharedWiFi:
                    _logger.LogInformation("🏠 Shared WiFi mode: PocketFence can filter content for this device");
                    break;

                case NetworkMode.Offline:
                    _logger.LogWarning("🔌 Offline mode: Limited functionality available");
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle network mode change");
        }
    }

    public void Dispose()
    {
        _monitorTimer?.Dispose();
    }
}

public interface INetworkModeService : IDisposable
{
    NetworkMode CurrentMode { get; }
    string NetworkName { get; }
    bool IsOnline { get; }
    bool CanCreateHotspot { get; }
    bool IsConnectedToiOSHotspot { get; }
    bool IsConnectedToAndroidHotspot { get; }
    
    Task StartMonitoringAsync(CancellationToken cancellationToken = default);
    Task<NetworkMode> DetectNetworkModeAsync();
}

public record NetworkInfo(string Name, NetworkInterfaceType Type);