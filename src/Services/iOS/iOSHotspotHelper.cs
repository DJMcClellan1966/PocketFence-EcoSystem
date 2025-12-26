using System.Net;
using System.Net.NetworkInformation;
using System.Text.Json;
using PocketFence_Simple.Models;

namespace PocketFence_Simple.Services.iOS;

public sealed class iOSHotspotHelper(ILogger<iOSHotspotHelper> logger)
{
    private readonly Dictionary<string, iOSDeviceInfo> _iOSDevices = [];
    
    public async ValueTask<iOSConnectionInfo> GenerateConnectionInfoAsync(string ssid, string password, CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("🍎 Generating iOS WiFi connection info for SSID: {SSID}", ssid);
            
            // Get the local network IP address
            var hostIpAddress = await GetHostIpAddressAsync();
            
            // Generate iOS-specific configuration
            var connectionInfo = new iOSConnectionInfo
            {
                SSID = ssid,
                Password = password,
                HostIpAddress = hostIpAddress,
                DashboardUrl = $"http://{hostIpAddress}:5000",
                SecureDashboardUrl = $"https://{hostIpAddress}:5001",
                QRCodeData = GenerateQRCodeData(ssid, password),
                iOSWiFiProfile = GenerateiOSWiFiProfile(ssid, password),
                ConnectionInstructions = GenerateConnectionInstructions(ssid, hostIpAddress)
            };
            
            logger.LogInformation("✅ iOS connection info generated successfully");
            return connectionInfo;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Failed to generate iOS connection info");
            throw;
        }
    }
    
    public ValueTask<bool> DetectiOSDeviceAsync(string macAddress, string ipAddress, CancellationToken cancellationToken = default)
    {
        try
        {
            // Detect iOS devices by MAC address vendor prefix or User-Agent patterns
            var isiOSDevice = IsAppleDevice(macAddress) || HasiOSCharacteristics(ipAddress);
            
            if (isiOSDevice)
            {
                var deviceInfo = new iOSDeviceInfo
                {
                    MacAddress = macAddress,
                    IpAddress = ipAddress,
                    DetectedAt = DateTime.UtcNow,
                    DeviceModel = DetectDeviceModel(macAddress),
                    RequiresPWA = true
                };
                
                _iOSDevices[macAddress] = deviceInfo;
                logger.LogInformation("🍎 iOS device detected: {MAC} at {IP}", macAddress, ipAddress);
            }
            
            return ValueTask.FromResult(isiOSDevice);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Failed to detect iOS device");
            return ValueTask.FromResult(false);
        }
    }
    
    public async ValueTask<string> GenerateWiFiQRCodeAsync(string ssid, string password, CancellationToken cancellationToken = default)
    {
        try
        {
            // Generate WiFi QR code in standard format: WIFI:T:WPA;S:ssid;P:password;;
            var wifiString = $"WIFI:T:WPA;S:{ssid};P:{password};;";
            
            logger.LogInformation("📱 Generated WiFi QR code for iOS devices");
            return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(wifiString));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Failed to generate WiFi QR code");
            throw;
        }
    }
    
    public ValueTask<IEnumerable<iOSDeviceInfo>> GetConnectediOSDevicesAsync(CancellationToken cancellationToken = default)
    {
        var activeiOSDevices = _iOSDevices.Values
            .Where(device => device.DetectedAt > DateTime.UtcNow.AddMinutes(-5)) // Active in last 5 minutes
            .ToArray();
            
        logger.LogDebug("📱 Found {Count} active iOS devices", activeiOSDevices.Length);
        return ValueTask.FromResult<IEnumerable<iOSDeviceInfo>>(activeiOSDevices);
    }
    
    private static async Task<string> GetHostIpAddressAsync()
    {
        var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces()
            .Where(ni => ni.OperationalStatus == OperationalStatus.Up && 
                        ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .SelectMany(ni => ni.GetIPProperties().UnicastAddresses)
            .Where(addr => addr.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork &&
                          !IPAddress.IsLoopback(addr.Address))
            .Select(addr => addr.Address.ToString())
            .FirstOrDefault();
            
        return networkInterfaces ?? "localhost";
    }
    
    private static bool IsAppleDevice(string macAddress)
    {
        if (string.IsNullOrWhiteSpace(macAddress)) return false;
        
        // Apple MAC address prefixes (OUI - Organizationally Unique Identifier)
        string[] applePrefixes = [
            "00:03:93", "00:05:02", "00:0A:27", "00:0A:95", "00:0D:93", "00:11:24",
            "00:12:FB", "00:13:72", "00:14:51", "00:15:93", "00:16:CB", "00:17:F2",
            "00:19:E3", "00:1B:63", "00:1C:B3", "00:1D:4F", "00:1E:52", "00:1E:C2",
            "00:1F:5B", "00:1F:F3", "00:21:E9", "00:22:41", "00:23:12", "00:23:DF",
            "00:24:36", "00:25:00", "00:25:4B", "00:25:BC", "00:26:08", "00:26:4A",
            "00:26:B0", "00:26:BB", "04:0C:CE", "04:15:52", "04:1E:64", "04:48:9A",
            "04:4E:5C", "04:54:53", "04:69:F8", "04:DB:56", "04:E5:36", "08:74:02",
            "0C:30:21", "0C:4D:E9", "0C:74:C2", "10:9A:DD", "10:DD:B1", "14:10:9F",
            "14:20:5E", "14:7D:DA", "14:8F:C6", "18:34:51", "18:65:90", "18:AF:8F",
            "18:EE:69", "1C:1A:C0", "1C:AB:A7", "20:AB:37", "20:C9:D0", "24:A0:74",
            "24:AB:81", "24:F0:94", "24:F6:77", "28:39:5E", "28:5A:EB", "28:6A:B8",
            "28:6A:BA", "28:87:BA", "28:CF:DA", "28:CF:E9", "28:E0:2C", "28:E7:CF",
            "2C:1F:23", "2C:2D:94", "2C:B4:3A", "30:7C:5E", "30:90:AB", "34:2D:0D",
            "34:36:3B", "34:A3:95", "34:AB:37", "34:C0:59", "34:E2:FD", "38:0A:94",
            "38:2D:E8", "38:89:DC", "3C:15:C2", "3C:2E:FF", "40:30:04", "40:33:1A",
            "40:4D:7F", "40:6C:8F", "40:A6:D9", "40:B3:95", "40:CB:C0", "44:00:10",
            "44:2A:60", "44:4C:0C", "44:D8:84", "48:43:7C", "48:60:BC", "48:74:12",
            "48:A1:95", "4C:32:75", "4C:7C:5F", "4C:8D:79", "4C:B1:99", "50:EA:D6",
            "54:72:4F", "54:8A:A8", "58:55:CA", "5C:59:48", "5C:95:AE", "5C:F9:38",
            "60:03:08", "60:33:4B", "60:5B:B4", "60:A3:7D", "60:C5:47", "60:F4:45",
            "64:20:9F", "64:76:BA", "64:A3:CB", "68:05:CA", "68:26:CD", "68:5B:35",
            "68:7F:74", "68:96:7B", "68:AB:1E", "68:D9:3C", "6C:19:C0", "6C:40:08",
            "6C:4D:73", "6C:94:66", "6C:AD:F8", "70:11:24", "70:48:0F", "70:56:81",
            "70:73:CB", "70:CD:60", "70:DE:E2", "74:E2:E6", "74:E7:C6", "78:31:C1",
            "78:4F:43", "78:52:1A", "78:67:D0", "78:7B:8A", "78:A3:E4", "78:CA:39",
            "7C:6D:62", "7C:D1:C3", "7C:FA:DF", "80:06:E0", "80:92:9F", "80:B0:3D",
            "80:E6:50", "84:38:35", "84:78:AC", "84:B1:53", "84:FC:AC", "88:1F:A1",
            "88:53:2E", "88:63:DF", "88:E8:7F", "8C:58:77", "8C:7C:92", "8C:8E:F2",
            "90:72:40", "94:E9:6A", "94:F6:A3", "98:03:D8", "98:B8:E3", "9C:04:EB",
            "9C:20:7B", "9C:29:3F", "9C:84:BF", "9C:F3:87", "A0:99:9B", "A4:5E:60",
            "A4:B1:97", "A4:D1:8C", "A8:20:66", "A8:5C:2C", "A8:96:8A", "A8:FA:D8",
            "AC:1F:74", "AC:29:3A", "AC:87:A3", "AC:BC:32", "AC:CF:5C", "B0:65:BD",
            "B0:CA:68", "B4:18:D1", "B4:BF:E4", "B4:F0:AB", "B4:F6:1C", "B8:09:8A",
            "B8:17:C2", "B8:53:AC", "B8:78:2E", "B8:C7:5D", "B8:E8:56", "B8:F6:53",
            "BC:52:B7", "BC:67:78", "BC:6C:21", "BC:92:6B", "BC:A9:20", "BC:EC:5D",
            "C0:9A:D0", "C0:CE:CD", "C4:2C:03", "C8:2A:14", "C8:33:4B", "C8:B5:B7",
            "C8:BC:C8", "C8:E0:EB", "CC:08:E0", "CC:25:EF", "CC:29:F5", "D0:03:4B",
            "D0:23:DB", "D0:81:7A", "D4:20:B0", "D4:61:9D", "D4:90:9C", "D4:A3:3D",
            "D8:1D:72", "D8:30:62", "D8:A2:5E", "D8:BB:2C", "DC:0C:5C", "DC:2B:2A",
            "DC:37:45", "DC:3F:27", "DC:A9:04", "E0:33:8E", "E0:AC:CB", "E0:B9:BA",
            "E0:C9:7A", "E4:25:E7", "E4:CE:8F", "E8:04:0B", "E8:80:2E", "E8:B2:AC",
            "EC:35:86", "EC:8A:4C", "F0:18:98", "F0:1F:AF", "F0:B4:79", "F0:C1:F1",
            "F0:DB:E2", "F4:0F:24", "F4:1B:A1", "F4:37:B7", "F4:F1:5A", "F4:F9:51",
            "F8:0C:F3", "F8:1E:DF", "F8:2F:A8", "F8:F1:B6", "FC:25:3F", "FC:FC:48"
        ];
        
        var normalizedMac = macAddress.Replace(":", "").Replace("-", "").ToUpperInvariant();
        return applePrefixes.Any(prefix => normalizedMac.StartsWith(prefix.Replace(":", "")));
    }
    
    private static bool HasiOSCharacteristics(string ipAddress)
    {
        // Additional iOS detection logic could be implemented here
        // For now, we rely primarily on MAC address detection
        return false;
    }
    
    private static string DetectDeviceModel(string macAddress)
    {
        // Simple device model detection based on MAC address patterns
        // This is a simplified version - more sophisticated detection could be implemented
        if (string.IsNullOrWhiteSpace(macAddress)) return "Unknown iOS Device";
        
        var normalizedMac = macAddress.Replace(":", "").Replace("-", "").ToUpperInvariant();
        
        // Map MAC prefixes to device types (simplified)
        return normalizedMac switch
        {
            var mac when mac.StartsWith("F0C1F1") || mac.StartsWith("F4F15A") => "iPhone",
            var mac when mac.StartsWith("3C15C2") || mac.StartsWith("40B395") => "iPad", 
            var mac when mac.StartsWith("A45E60") || mac.StartsWith("8863DF") => "MacBook",
            _ => "iOS Device"
        };
    }
    
    private static string GenerateQRCodeData(string ssid, string password)
    {
        return $"WIFI:T:WPA;S:{ssid};P:{password};;";
    }
    
    private static string GenerateiOSWiFiProfile(string ssid, string password)
    {
        // Generate a simplified iOS WiFi configuration
        return JsonSerializer.Serialize(new
        {
            ssid,
            password,
            security = "WPA2",
            autoConnect = true
        });
    }
    
    private static IEnumerable<string> GenerateConnectionInstructions(string ssid, string hostIpAddress)
    {
        return [
            "1. Open Settings app on your iOS device",
            $"2. Tap Wi-Fi and select '{ssid}' network",
            "3. Enter the password when prompted",
            "4. Once connected, open Safari browser",
            $"5. Navigate to: http://{hostIpAddress}:5000",
            "6. Tap Share button and select 'Add to Home Screen'",
            "7. Your PocketFence app will now be available on your home screen!"
        ];
    }
}

public sealed record iOSConnectionInfo
{
    public required string SSID { get; init; }
    public required string Password { get; init; }
    public required string HostIpAddress { get; init; }
    public required string DashboardUrl { get; init; }
    public required string SecureDashboardUrl { get; init; }
    public required string QRCodeData { get; init; }
    public required string iOSWiFiProfile { get; init; }
    public required IEnumerable<string> ConnectionInstructions { get; init; }
}

public sealed record iOSDeviceInfo
{
    public required string MacAddress { get; init; }
    public required string IpAddress { get; init; }
    public required DateTime DetectedAt { get; init; }
    public required string DeviceModel { get; init; }
    public required bool RequiresPWA { get; init; }
    public string? DeviceName { get; init; }
    public bool IsConnectedToPWA { get; set; }
    public DateTime? LastPWAAccess { get; set; }
}