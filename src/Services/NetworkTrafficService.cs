using System.Diagnostics;
using System.Net;

namespace PocketFence_Simple.Services
{
    public class NetworkTrafficService
    {
        private readonly ContentFilterService _contentFilter;
        private bool _isMonitoring = false;
        
        public event EventHandler<string>? TrafficMonitoringStatusChanged;

        public NetworkTrafficService(ContentFilterService contentFilter)
        {
            _contentFilter = contentFilter;
        }

        public async Task<bool> StartTrafficMonitoringAsync()
        {
            if (_isMonitoring)
                return true;

            try
            {
                // Configure DNS redirection to intercept web requests
                await ConfigureDnsRedirectionAsync();
                
                // Start packet capture simulation
                await StartPacketCaptureAsync();
                
                _isMonitoring = true;
                TrafficMonitoringStatusChanged?.Invoke(this, "Traffic monitoring started");
                return true;
            }
            catch (Exception ex)
            {
                TrafficMonitoringStatusChanged?.Invoke(this, $"Failed to start traffic monitoring: {ex.Message}");
                return false;
            }
        }

        public async Task StopTrafficMonitoringAsync()
        {
            if (!_isMonitoring)
                return;

            try
            {
                // Restore original DNS settings
                await RestoreDnsSettingsAsync();
                
                _isMonitoring = false;
                TrafficMonitoringStatusChanged?.Invoke(this, "Traffic monitoring stopped");
            }
            catch (Exception ex)
            {
                TrafficMonitoringStatusChanged?.Invoke(this, $"Error stopping traffic monitoring: {ex.Message}");
            }
        }

        private async Task ConfigureDnsRedirectionAsync()
        {
            try
            {
                // Use less intrusive blocking - avoid DNS redirection that can interfere with hotspot
                // Instead, we'll rely on HTTP proxy blocking
                System.Diagnostics.Debug.WriteLine("Using HTTP-based content filtering to avoid hotspot interference");
                
                // Don't modify DNS settings as this can cause hotspot to disconnect
                // The HTTP listener will handle blocking without disrupting the network adapter
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in DNS configuration: {ex.Message}");
                throw;
            }
        }

        private async Task<string> GetHostedNetworkAdapterNameAsync()
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "netsh",
                    Arguments = "wlan show profiles",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                if (process != null)
                {
                    var output = await process.StandardOutput.ReadToEndAsync();
                    await process.WaitForExitAsync();
                    
                    // Parse output to find hosted network adapter
                    // This is simplified - in reality you'd need more robust parsing
                    if (output.Contains("Microsoft Hosted Network Virtual Adapter"))
                    {
                        return "Microsoft Hosted Network Virtual Adapter";
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting adapter name: {ex.Message}");
            }
            
            return string.Empty;
        }

        private async Task StartPacketCaptureAsync()
        {
            // Start a simple HTTP proxy to intercept web traffic
            await Task.Run(() =>
            {
                try
                {
                    var listener = new HttpListener();
                    listener.Prefixes.Add("http://127.0.0.1:8080/");
                    listener.Start();
                    
                    System.Diagnostics.Debug.WriteLine("HTTP proxy started on port 8080");
                    
                    Task.Run(async () =>
                    {
                        while (_isMonitoring && listener.IsListening)
                        {
                            try
                            {
                                var context = await listener.GetContextAsync();
                                _ = Task.Run(() => ProcessHttpRequest(context));
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Error processing HTTP request: {ex.Message}");
                            }
                        }
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error starting packet capture: {ex.Message}");
                }
            });
        }

        private async Task ProcessHttpRequest(HttpListenerContext context)
        {
            try
            {
                var request = context.Request;
                var response = context.Response;
                
                var url = request.Url?.ToString() ?? "";
                var clientIp = request.RemoteEndPoint?.Address?.ToString() ?? "";
                
                System.Diagnostics.Debug.WriteLine($"Processing request: {url} from {clientIp}");
                
                // Get device MAC address from IP (simplified)
                var deviceMac = await GetMacAddressFromIp(clientIp);
                
                // Check if request should be blocked
                if (_contentFilter.ShouldBlockRequest(url, deviceMac))
                {
                    // Block the request without disrupting the hotspot connection
                    await SendBlockedPageAsync(response, url);
                    System.Diagnostics.Debug.WriteLine($"Blocked request to {url} from device {deviceMac}");
                }
                else
                {
                    // For allowed requests, just close the connection gracefully 
                    // to avoid interfering with the client's actual request
                    response.StatusCode = 204; // No Content
                    response.Close();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error processing HTTP request: {ex.Message}");
                // Don't let request processing errors affect the hotspot
                try
                {
                    context.Response.StatusCode = 500;
                    context.Response.Close();
                }
                catch { /* Ignore cleanup errors */ }
            }
        }

        private async Task<string> GetMacAddressFromIp(string ipAddress)
        {
            try
            {
                // Use ARP table to get MAC address
                var startInfo = new ProcessStartInfo
                {
                    FileName = "arp",
                    Arguments = $"-a {ipAddress}",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                if (process != null)
                {
                    var output = await process.StandardOutput.ReadToEndAsync();
                    await process.WaitForExitAsync();
                    
                    // More efficient parsing with ReadOnlySpan and StringSplitOptions
                    var lines = output.AsSpan().Trim().ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    
                    foreach (var line in lines)
                    {
                        var trimmedLine = line.Trim();
                        if (trimmedLine.Contains(ipAddress, StringComparison.OrdinalIgnoreCase) && 
                            trimmedLine.Contains("-", StringComparison.Ordinal))
                        {
                            var parts = trimmedLine.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length >= 2)
                            {
                                return parts[1];
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting MAC address for {ipAddress}: {ex.Message}");
            }
            
            return "Unknown";
        }

        private async Task SendBlockedPageAsync(HttpListenerResponse response, string blockedUrl)
        {
            try
            {
                var blockedPageHtml = $@"
                <!DOCTYPE html>
                <html>
                <head>
                    <title>PocketFence - Content Blocked</title>
                    <style>
                        body {{ font-family: Arial, sans-serif; text-align: center; margin: 50px; }}
                        .container {{ max-width: 600px; margin: 0 auto; }}
                        .warning {{ color: #d9534f; font-size: 24px; margin: 20px 0; }}
                        .blocked-url {{ background: #f5f5f5; padding: 10px; border-radius: 5px; word-break: break-all; }}
                    </style>
                </head>
                <body>
                    <div class='container'>
                        <h1>🛡️ PocketFence</h1>
                        <div class='warning'>⚠️ Content Blocked</div>
                        <p>This website has been blocked by your network administrator for containing potentially harmful or inappropriate content.</p>
                        <div class='blocked-url'>Blocked URL: {blockedUrl}</div>
                        <p>If you believe this is an error, please contact your network administrator.</p>
                        <p><small>Blocked at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}</small></p>
                        <p><small>PocketFence is protecting your network connection.</small></p>
                    </div>
                </body>
                </html>";
                
                var buffer = System.Text.Encoding.UTF8.GetBytes(blockedPageHtml);
                response.StatusCode = 200;
                response.ContentType = "text/html; charset=utf-8";
                response.ContentLength64 = buffer.Length;
                
                // Add headers to prevent caching
                response.Headers.Add("Cache-Control", "no-cache, no-store, must-revalidate");
                response.Headers.Add("Pragma", "no-cache");
                response.Headers.Add("Expires", "0");
                
                await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                response.OutputStream.Close();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error sending blocked page: {ex.Message}");
                // Ensure response is closed even on error to prevent hanging connections
                try { response.Close(); } catch { /* Ignore */ }
            }
        }

        private async Task ForwardRequestAsync(HttpListenerRequest request, HttpListenerResponse response)
        {
            try
            {
                // This is a simplified proxy implementation
                // In a real implementation, you'd need a more robust HTTP proxy
                
                using var client = new HttpClient();
                var requestUri = request.Url;
                
                if (requestUri != null)
                {
                    var httpResponse = await client.GetAsync(requestUri);
                    var content = await httpResponse.Content.ReadAsByteArrayAsync();
                    
                    response.StatusCode = (int)httpResponse.StatusCode;
                    response.ContentType = httpResponse.Content.Headers.ContentType?.ToString() ?? "text/html";
                    response.ContentLength64 = content.Length;
                    
                    await response.OutputStream.WriteAsync(content, 0, content.Length);
                }
                
                response.OutputStream.Close();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error forwarding request: {ex.Message}");
                
                // Send error response
                var errorHtml = "<html><body><h1>Proxy Error</h1><p>Unable to process request.</p></body></html>";
                var buffer = System.Text.Encoding.UTF8.GetBytes(errorHtml);
                response.StatusCode = 500;
                response.ContentLength64 = buffer.Length;
                await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                response.OutputStream.Close();
            }
        }

        private async Task RestoreDnsSettingsAsync()
        {
            try
            {
                // Since we're not modifying DNS settings anymore, 
                // there's nothing to restore to avoid hotspot interference
                System.Diagnostics.Debug.WriteLine("DNS settings not modified - no restoration needed");
            }
            catch (Exception)
            {
                // Continue without issues
            }
        }

        public bool IsMonitoring => _isMonitoring;
    }
}