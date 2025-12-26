using PocketFence_Simple.Services.iOS;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace PocketFence_Simple.Controllers;

[ApiController]
[Route("api/[controller]")]
public class iOSController(
    iOSHotspotHelper iOSHelper,
    ILogger<iOSController> logger) : ControllerBase
{
    [HttpPost("generate-connection-info")]
    public async Task<ActionResult<object>> GenerateConnectionInfo([FromBody] iOSConnectionRequest request)
    {
        try
        {
            logger.LogInformation("🍎 Generating iOS connection info for SSID: {SSID}", request.SSID);
            
            var connectionInfo = await iOSHelper.GenerateConnectionInfoAsync(
                request.SSID, 
                request.Password, 
                HttpContext.RequestAborted);
            
            return Ok(new
            {
                success = true,
                data = connectionInfo,
                message = "iOS connection information generated successfully"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Failed to generate iOS connection info");
            return StatusCode(500, new 
            { 
                success = false, 
                message = "Failed to generate connection information",
                error = ex.Message 
            });
        }
    }
    
    [HttpGet("wifi-qr/{ssid}")]
    public async Task<ActionResult<object>> GenerateWiFiQRCode(string ssid, [FromQuery] string password)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                return BadRequest(new { success = false, message = "Password is required" });
            }
            
            var qrCodeData = await iOSHelper.GenerateWiFiQRCodeAsync(ssid, password, HttpContext.RequestAborted);
            
            return Ok(new
            {
                success = true,
                qrCode = qrCodeData,
                wifiString = $"WIFI:T:WPA;S:{ssid};P:{password};;",
                message = "WiFi QR code generated successfully"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Failed to generate WiFi QR code");
            return StatusCode(500, new 
            { 
                success = false, 
                message = "Failed to generate QR code",
                error = ex.Message 
            });
        }
    }
    
    [HttpGet("devices")]
    public async Task<ActionResult<object>> GetConnectediOSDevices()
    {
        try
        {
            var devices = await iOSHelper.GetConnectediOSDevicesAsync(HttpContext.RequestAborted);
            
            return Ok(new
            {
                success = true,
                devices = devices.Select(device => new
                {
                    device.MacAddress,
                    device.IpAddress,
                    device.DeviceModel,
                    device.DetectedAt,
                    device.RequiresPWA,
                    device.IsConnectedToPWA,
                    device.LastPWAAccess,
                    DeviceName = device.DeviceName ?? "Unknown iOS Device"
                }),
                count = devices.Count(),
                message = "iOS devices retrieved successfully"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Failed to get iOS devices");
            return StatusCode(500, new 
            { 
                success = false, 
                message = "Failed to retrieve iOS devices",
                error = ex.Message 
            });
        }
    }
    
    [HttpPost("detect-device")]
    public async Task<ActionResult<object>> DetectiOSDevice([FromBody] DeviceDetectionRequest request)
    {
        try
        {
            var isiOSDevice = await iOSHelper.DetectiOSDeviceAsync(
                request.MacAddress, 
                request.IpAddress, 
                HttpContext.RequestAborted);
            
            return Ok(new
            {
                success = true,
                isiOSDevice,
                macAddress = request.MacAddress,
                ipAddress = request.IpAddress,
                message = isiOSDevice ? "iOS device detected" : "Non-iOS device"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Failed to detect iOS device");
            return StatusCode(500, new 
            { 
                success = false, 
                message = "Failed to detect device",
                error = ex.Message 
            });
        }
    }
    
    [HttpGet("pwa-manifest")]
    public ActionResult<object> GetPWAManifest()
    {
        try
        {
            var manifest = new
            {
                name = "PocketFence",
                short_name = "PocketFence",
                description = "AI-Powered Parental Control Dashboard",
                start_url = "/",
                display = "standalone",
                background_color = "#ffffff",
                theme_color = "#2563eb",
                orientation = "portrait",
                scope = "/",
                lang = "en-US",
                categories = new[] { "productivity", "security", "parental-control" },
                icons = new[]
                {
                    new { src = "/icon-192.png", sizes = "192x192", type = "image/png", purpose = "any maskable" },
                    new { src = "/icon-512.png", sizes = "512x512", type = "image/png", purpose = "any maskable" },
                    new { src = "/icon-apple-touch.png", sizes = "180x180", type = "image/png", purpose = "any" }
                },
                screenshots = new[]
                {
                    new { src = "/screenshot1.png", sizes = "390x844", type = "image/png", form_factor = "narrow" },
                    new { src = "/screenshot2.png", sizes = "1024x768", type = "image/png", form_factor = "wide" }
                },
                ios = new
                {
                    apple_mobile_web_app_capable = "yes",
                    apple_mobile_web_app_status_bar_style = "black-translucent",
                    apple_mobile_web_app_title = "PocketFence",
                    apple_touch_icon = "/icon-apple-touch.png"
                }
            };
            
            Response.ContentType = "application/manifest+json";
            return Ok(manifest);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Failed to generate PWA manifest");
            return StatusCode(500, new 
            { 
                success = false, 
                message = "Failed to generate PWA manifest",
                error = ex.Message 
            });
        }
    }
}

public sealed record iOSConnectionRequest
{
    public required string SSID { get; init; }
    public required string Password { get; init; }
}

public sealed record DeviceDetectionRequest
{
    public required string MacAddress { get; init; }
    public required string IpAddress { get; init; }
}