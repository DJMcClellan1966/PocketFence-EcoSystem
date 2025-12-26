#if WINDOWS
using System.Management;
#endif
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using PocketFence_Simple.Models;

namespace PocketFence_Simple.Services
{
    public class HotspotService
    {
        private readonly INetworkModeService _networkModeService;
        private bool _isHotspotEnabled = false;
        private readonly Dictionary<string, ConnectedDevice> _deviceCache = new(); // O(1) device lookup by MAC
        private Timer? _statusMonitor; // Monitor hotspot status to detect unexpected shutdowns
        
        public HotspotService(INetworkModeService networkModeService)
        {
            _networkModeService = networkModeService;
        }
        
        public event EventHandler<string>? HotspotStatusChanged;
        public event EventHandler<ConnectedDevice>? DeviceConnected;
        public event EventHandler<ConnectedDevice>? DeviceDisconnected;
        
        public bool IsActive => _isHotspotEnabled;

        public async Task<bool> EnableHotspotAsync(string ssid, string password)
        {
            try
            {
                // Check if we can create a hotspot based on current network mode
                if (!_networkModeService.CanCreateHotspot)
                {
                    var currentMode = _networkModeService.CurrentMode;
                    var networkName = _networkModeService.NetworkName;
                    
                    switch (currentMode)
                    {
                        case NetworkMode.iOSCellularHotspot:
                            throw new InvalidOperationException($"Cannot create Windows hotspot while connected to iOS cellular hotspot '{networkName}'. PocketFence is monitoring traffic through the iOS hotspot.");
                        
                        case NetworkMode.Offline:
                            throw new InvalidOperationException("Cannot create hotspot while offline. Please connect to a network first.");
                        
                        default:
                            throw new InvalidOperationException($"Cannot create hotspot in current network mode: {currentMode}");
                    }
                }

                // Create the hotspot profile
                var profileXml = CreateHotspotProfile(ssid, password);
                
                // Use netsh command to set up hosted network
                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "netsh",
                    Arguments = $"wlan set hostednetwork mode=allow ssid=\"{ssid}\" key=\"{password}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    Verb = "runas"
                };

                using var process = System.Diagnostics.Process.Start(startInfo);
                if (process != null)
                {
                    await process.WaitForExitAsync();
                    
                    if (process.ExitCode == 0)
                    {
                        // Start the hosted network
                        await StartHostedNetworkAsync();
                        _isHotspotEnabled = true;
                        
                        // Start monitoring hotspot status to detect unexpected shutdowns
                        StartStatusMonitoring();
                        
                        HotspotStatusChanged?.Invoke(this, "Hotspot enabled successfully");
                        return true;
                    }
                }
                
                HotspotStatusChanged?.Invoke(this, "Failed to enable hotspot");
                return false;
            }
            catch (Exception ex)
            {
                HotspotStatusChanged?.Invoke(this, $"Error enabling hotspot: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> DisableHotspotAsync()
        {
            try
            {
                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "netsh",
                    Arguments = "wlan stop hostednetwork",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    Verb = "runas"
                };

                using var process = System.Diagnostics.Process.Start(startInfo);
                if (process != null)
                {
                    await process.WaitForExitAsync();
                    
                    if (process.ExitCode == 0)
                    {
                        _isHotspotEnabled = false;
                        
                        // Stop status monitoring
                        StopStatusMonitoring();
                        
                        HotspotStatusChanged?.Invoke(this, "Hotspot disabled successfully");
                        return true;
                    }
                }
                
                HotspotStatusChanged?.Invoke(this, "Failed to disable hotspot");
                return false;
            }
            catch (Exception ex)
            {
                HotspotStatusChanged?.Invoke(this, $"Error disabling hotspot: {ex.Message}");
                return false;
            }
        }

        public async Task<List<ConnectedDevice>> GetConnectedDevicesAsync()
        {
            var devices = new List<ConnectedDevice>();
            
            try
            {
                // Get ARP table entries for connected devices
                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "arp",
                    Arguments = "-a",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = System.Diagnostics.Process.Start(startInfo);
                if (process != null)
                {
                    var output = await process.StandardOutput.ReadToEndAsync();
                    await process.WaitForExitAsync();
                    
                    devices = ParseArpOutput(output);
                    
                    // Update cache for O(1) lookups - merge with existing device info
                    foreach (var device in devices)
                    {
                        if (_deviceCache.TryGetValue(device.MacAddress, out var existingDevice))
                        {
                            // Preserve existing device properties and update LastSeen
                            device.FirstSeen = existingDevice.FirstSeen;
                            device.DeviceName = existingDevice.DeviceName;
                            device.IsBlocked = existingDevice.IsBlocked;
                            device.IsChildDevice = existingDevice.IsChildDevice;
                            device.IsFiltered = existingDevice.IsFiltered;
                            device.BlockedSites = existingDevice.BlockedSites;
                            device.Category = existingDevice.Category;
                        }
                        device.LastSeen = DateTime.Now;
                        _deviceCache[device.MacAddress] = device;
                    }
                }
            }
            catch (Exception ex)
            {
                // Log error instead of console output
                System.Diagnostics.Debug.WriteLine($"Error getting connected devices: {ex.Message}");
            }
            
            return devices;
        }

        private async Task<bool> StartHostedNetworkAsync()
        {
#if WINDOWS || OSX || LINUX
            System.Diagnostics.ProcessStartInfo startInfo;
            
#if WINDOWS
            startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = "wlan start hostednetwork",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                Verb = "runas"
            };
#elif OSX
            // macOS Internet Sharing via networksetup
            startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "sudo",
                Arguments = "networksetup -setinternetsharing Wi-Fi on",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
#elif LINUX
            // Linux hostapd configuration
            startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "sudo",
                Arguments = "systemctl start hostapd",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
#endif

            try
            {
                using var process = System.Diagnostics.Process.Start(startInfo);
                if (process != null)
                {
                    await process.WaitForExitAsync();
                    return process.ExitCode == 0;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error starting hotspot: {ex.Message}");
            }
            
            return false;
#else
            // Platform not supported
            return false;
#endif
        }

        private string CreateHotspotProfile(string ssid, string password)
        {
            return $@"<?xml version=""1.0""?>
<WLANProfile xmlns=""http://www.microsoft.com/networking/WLAN/profile/v1"">
    <name>{ssid}</name>
    <SSIDConfig>
        <SSID>
            <name>{ssid}</name>
        </SSID>
    </SSIDConfig>
    <connectionType>ESS</connectionType>
    <connectionMode>auto</connectionMode>
    <MSM>
        <security>
            <authEncryption>
                <authentication>WPA2PSK</authentication>
                <encryption>AES</encryption>
                <useOneX>false</useOneX>
            </authEncryption>
            <sharedKey>
                <keyType>passPhrase</keyType>
                <protected>false</protected>
                <keyMaterial>{password}</keyMaterial>
            </sharedKey>
        </security>
    </MSM>
</WLANProfile>";
        }

        private async Task<List<ConnectedDevice>> GetDevicesFromNetworkAsync()
        {
            var devices = new List<ConnectedDevice>();
            
            try
            {
#if WINDOWS
                // Windows: Use ARP table
                var arpResult = await RunCommandAsync("arp", "-a");
                return ParseArpOutput(arpResult);
#elif OSX
                // macOS: Use arp command with different flags
                var arpResult = await RunCommandAsync("arp", "-a -l");
                return ParseMacOSArpOutput(arpResult);
#elif LINUX
                // Linux: Use ARP table or nmap
                var arpResult = await RunCommandAsync("arp", "-a");
                return ParseLinuxArpOutput(arpResult);
#else
                // Cross-platform fallback: Network ping sweep
                return await PerformPingSweepAsync();
#endif
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting network devices: {ex.Message}");
                return devices;
            }
        }

        private async Task<string> RunCommandAsync(string command, string arguments)
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = System.Diagnostics.Process.Start(startInfo);
            if (process != null)
            {
                await process.WaitForExitAsync();
                return await process.StandardOutput.ReadToEndAsync();
            }
            
            return string.Empty;
        }

        private List<ConnectedDevice> ParseMacOSArpOutput(string arpOutput)
        {
            var devices = new List<ConnectedDevice>();
            
            if (string.IsNullOrWhiteSpace(arpOutput))
                return devices;
                
            var lines = arpOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                // macOS format: hostname (ip.address) at mac:address [ether]
                if (trimmedLine.Contains("at") && trimmedLine.Contains(":"))
                {
                    try
                    {
                        var parts = trimmedLine.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 4)
                        {
                            var ipPart = parts[1].Trim('(', ')');
                            var macPart = parts[3];
                            
                            if (IPAddress.TryParse(ipPart, out var ip) && IsValidMacAddress(macPart))
                            {
                                devices.Add(new ConnectedDevice
                                {
                                    IpAddress = ipPart,
                                    MacAddress = macPart.ToUpperInvariant(),
                                    DeviceName = parts[0],
                                    IsOnline = true,
                                    FirstSeen = DateTime.Now,
                                    LastSeen = DateTime.Now
                                });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error parsing macOS ARP line '{trimmedLine}': {ex.Message}");
                    }
                }
            }
            
            return devices;
        }

        private List<ConnectedDevice> ParseLinuxArpOutput(string arpOutput)
        {
            var devices = new List<ConnectedDevice>();
            
            if (string.IsNullOrWhiteSpace(arpOutput))
                return devices;
                
            var lines = arpOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                // Linux format: hostname (ip.address) at mac:address [ether] on interface
                if (trimmedLine.Contains("at") && trimmedLine.Contains(":"))
                {
                    try
                    {
                        var parts = trimmedLine.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 4)
                        {
                            var ipPart = parts[1].Trim('(', ')');
                            var macPart = parts[3];
                            
                            if (IPAddress.TryParse(ipPart, out var ip) && IsValidMacAddress(macPart))
                            {
                                devices.Add(new ConnectedDevice
                                {
                                    IpAddress = ipPart,
                                    MacAddress = macPart.ToUpperInvariant(),
                                    DeviceName = parts[0],
                                    IsOnline = true,
                                    FirstSeen = DateTime.Now,
                                    LastSeen = DateTime.Now
                                });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error parsing Linux ARP line '{trimmedLine}': {ex.Message}");
                    }
                }
            }
            
            return devices;
        }

        private async Task<List<ConnectedDevice>> PerformPingSweepAsync()
        {
            var devices = new List<ConnectedDevice>();
            var network = GetLocalNetworkRange();
            
            if (string.IsNullOrEmpty(network))
                return devices;

            // Ping sweep for devices (simplified for cross-platform)
            var tasks = new List<Task>();
            for (int i = 1; i <= 254; i++)
            {
                var ip = $"{network}.{i}";
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        using var ping = new Ping();
                        var reply = await ping.SendPingAsync(ip, 1000);
                        if (reply.Status == IPStatus.Success)
                        {
                            lock (devices)
                            {
                                devices.Add(new ConnectedDevice
                                {
                                    IpAddress = ip,
                                    MacAddress = "00:00:00:00:00:00", // Unknown MAC
                                    DeviceName = "Unknown Device",
                                    IsOnline = true,
                                    FirstSeen = DateTime.Now,
                                    LastSeen = DateTime.Now
                                });
                            }
                        }
                    }
                    catch { /* Ignore ping failures */ }
                }));
            }

            await Task.WhenAll(tasks);
            return devices;
        }

        private string GetLocalNetworkRange()
        {
            try
            {
                var localIP = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(ni => ni.OperationalStatus == OperationalStatus.Up && ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    .SelectMany(ni => ni.GetIPProperties().UnicastAddresses)
                    .FirstOrDefault(addr => addr.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && 
                                           !IPAddress.IsLoopback(addr.Address))?.Address.ToString();

                if (!string.IsNullOrEmpty(localIP))
                {
                    var parts = localIP.Split('.');
                    if (parts.Length == 4)
                        return $"{parts[0]}.{parts[1]}.{parts[2]}";
                }
            }
            catch { /* Ignore errors */ }
            
            return string.Empty;
        }

        private bool IsValidMacAddress(string mac)
        {
            return !string.IsNullOrWhiteSpace(mac) && 
                   mac.Length >= 12 && 
                   mac.Contains(':') && 
                   !mac.Equals("00:00:00:00:00:00", StringComparison.OrdinalIgnoreCase);
        }

        private List<ConnectedDevice> ParseArpOutput(string arpOutput)
        {
            var devices = new List<ConnectedDevice>();
            
            if (string.IsNullOrWhiteSpace(arpOutput))
                return devices;
                
            var lines = arpOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                if (string.IsNullOrEmpty(trimmedLine) || !trimmedLine.Contains("dynamic", StringComparison.OrdinalIgnoreCase))
                    continue;
                    
                var parts = trimmedLine.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3)
                {
                    var ipAddress = parts[0];
                    var macAddress = parts[1];
                    
                    var device = new ConnectedDevice
                    {
                        IpAddress = ipAddress,
                        MacAddress = macAddress,
                        FirstSeen = DateTime.Now,
                        LastSeen = DateTime.Now,
                        DeviceName = $"Device_{macAddress.Replace("-", "").AsSpan(0, Math.Min(6, macAddress.Length))}",
                        Category = DeviceCategory.Unknown
                    };
                    
                    devices.Add(device);
                }
            }
            
            return devices;
        }

        /// <summary>
        /// Get device by MAC address with O(1) lookup performance
        /// </summary>
        /// <param name="macAddress">MAC address of the device</param>
        /// <returns>ConnectedDevice if found, null otherwise</returns>
        public ConnectedDevice? GetDeviceByMacAddress(string macAddress)
        {
            return _deviceCache.TryGetValue(macAddress, out var device) ? device : null;
        }

        /// <summary>
        /// Check if device is connected with O(1) lookup performance
        /// </summary>
        /// <param name="macAddress">MAC address of the device</param>
        /// <returns>True if device is connected, false otherwise</returns>
        public bool IsDeviceConnected(string macAddress)
        {
            return _deviceCache.ContainsKey(macAddress);
        }

        public bool IsHotspotEnabled => _isHotspotEnabled;

        public void StartDeviceMonitoring()
        {
            // Start monitoring for device connections/disconnections
            Task.Run(async () =>
            {
                var previousDevices = new Dictionary<string, ConnectedDevice>();
                
                while (_isHotspotEnabled)
                {
                    try
                    {
                        var currentDevices = await GetConnectedDevicesAsync();
                        var currentDeviceMap = currentDevices.ToDictionary(d => d.MacAddress, d => d);
                        
                        // Check for new devices - O(1) lookup instead of O(n²)
                        foreach (var device in currentDevices)
                        {
                            if (!previousDevices.ContainsKey(device.MacAddress))
                            {
                                DeviceConnected?.Invoke(this, device);
                            }
                        }
                        
                        // Check for disconnected devices - O(1) lookup
                        foreach (var kvp in previousDevices)
                        {
                            if (!currentDeviceMap.ContainsKey(kvp.Key))
                            {
                                DeviceDisconnected?.Invoke(this, kvp.Value);
                            }
                        }
                        
                        previousDevices = currentDeviceMap;
                        await Task.Delay(5000); // Check every 5 seconds
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error in device monitoring: {ex.Message}");
                        await Task.Delay(10000); // Wait longer on error
                    }
                }
            });
        }

        private void StartStatusMonitoring()
        {
            // Monitor hotspot status every 10 seconds to detect unexpected shutdowns
            _statusMonitor = new Timer(async _ =>
            {
                if (_isHotspotEnabled)
                {
                    var actualStatus = await CheckActualHotspotStatus();
                    if (!actualStatus && _isHotspotEnabled)
                    {
                        // Hotspot was shut down externally (possibly by content filtering)
                        System.Diagnostics.Debug.WriteLine("Hotspot unexpectedly shut down - attempting to restart");
                        HotspotStatusChanged?.Invoke(this, "Hotspot was unexpectedly disabled - attempting restart");
                        
                        // Try to restart the hotspot
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await Task.Delay(2000); // Wait a bit before restart
                                await StartHostedNetworkAsync();
                                HotspotStatusChanged?.Invoke(this, "Hotspot automatically restarted");
                            }
                            catch (Exception ex)
                            {
                                HotspotStatusChanged?.Invoke(this, $"Failed to restart hotspot: {ex.Message}");
                                _isHotspotEnabled = false; // Update status if restart fails
                            }
                        });
                    }
                }
            }, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
        }

        private void StopStatusMonitoring()
        {
            _statusMonitor?.Dispose();
            _statusMonitor = null;
        }

        private async Task<bool> CheckActualHotspotStatus()
        {
            try
            {
                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "netsh",
                    Arguments = "wlan show hostednetwork",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = System.Diagnostics.Process.Start(startInfo);
                if (process != null)
                {
                    var output = await process.StandardOutput.ReadToEndAsync();
                    await process.WaitForExitAsync();
                    
                    // Check if the hosted network is actually running
                    return output.Contains("Status                 : Started", StringComparison.OrdinalIgnoreCase);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error checking hotspot status: {ex.Message}");
            }
            return false;
        }
    }
}