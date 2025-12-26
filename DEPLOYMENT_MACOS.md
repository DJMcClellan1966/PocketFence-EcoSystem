# 🍎 PocketFence-Simple: macOS Deployment Guide

Complete guide for running PocketFence-Simple on macOS (MacBook, iMac, Mac Mini, etc.)

## ✅ **macOS System Requirements**

- **macOS**: 10.14 (Mojave) or newer
- **Architecture**: Intel x64 or Apple Silicon (M1/M2/M3/M4)  
- **.NET Runtime**: Will be installed automatically
- **Network**: WiFi capability for hotspot functionality
- **Permissions**: Administrator access for network configuration

## 🚀 **Quick Installation**

### **Option 1: Download Pre-built Binary (Recommended)**
```bash
# Download macOS release
curl -L https://github.com/your-repo/PocketFence-Simple/releases/latest/download/PocketFence-Simple-osx-x64.tar.gz -o PocketFence-Simple-osx.tar.gz

# Extract
tar -xzf PocketFence-Simple-osx.tar.gz

# Make executable and run
cd PocketFence-Simple-osx-x64
chmod +x PocketFence-Simple
./PocketFence-Simple
```

### **Option 2: Build from Source**
```bash
# Prerequisites: Install .NET 10 Preview
curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --version preview

# Clone repository
git clone https://github.com/your-repo/PocketFence-Simple.git
cd PocketFence-Simple

# Build for macOS
dotnet build -c Release
dotnet publish -c Release -r osx-x64 --self-contained true

# Run
dotnet run
```

## 🔧 **macOS-Specific Configuration**

### **1. Network Permissions Setup**
```bash
# Grant network access permissions
sudo chmod +x PocketFence-Simple

# Allow hotspot functionality (requires admin password)
sudo dseditgroup -o edit -a $(whoami) -t user admin
```

### **2. Firewall Configuration**
```bash
# Allow PocketFence through macOS firewall
sudo /usr/libexec/ApplicationFirewall/socketfilterfw --add ./PocketFence-Simple
sudo /usr/libexec/ApplicationFirewall/socketfilterfw --unblock ./PocketFence-Simple

# Or disable firewall temporarily
sudo /usr/libexec/ApplicationFirewall/socketfilterfw --setglobalstate off
```

### **3. Internet Sharing Setup**
```bash
# Enable Internet Sharing (hotspot) via Terminal
sudo networksetup -setinternetsharing Wi-Fi on

# Check sharing status
networksetup -getinternetsharing

# Configure hotspot name and password
sudo networksetup -createnetworkservice "PocketFence-Hotspot" Wi-Fi
```

## 📱 **iOS/Android Device Access**

### **1. Find Your Mac's IP Address**
```bash
# Get your Mac's IP address
ifconfig | grep "inet " | grep -v 127.0.0.1 | awk '{print $2}' | head -1
```

### **2. Access PocketFence Dashboard**
1. **Connect devices to same WiFi network as your Mac**
2. **Open browser on iOS/Android device**  
3. **Navigate to**: `http://[YOUR_MAC_IP]:5000`
4. **Add to Home Screen** for app-like experience

Example: `http://192.168.1.100:5000`

## 🍎 **macOS-Specific Features**

### **Internet Sharing (Hotspot)**
- **Enable**: PocketFence can create WiFi hotspot via macOS Internet Sharing
- **Superior to iOS**: Can share WiFi connection (not cellular-only like iOS)
- **Automatic**: Configures sharing settings automatically
- **Secure**: WPA2 encryption with custom password

### **Network Monitoring**
- **ARP Table**: Uses `arp -a -l` command for device discovery
- **Real-time**: Monitors connected devices every 5 seconds  
- **Cross-device**: Tracks iOS, Android, Windows, and other macOS devices
- **Intelligent**: Identifies device types by MAC address patterns

### **Performance Benefits**
- **Native Performance**: .NET 10 optimized for Apple Silicon
- **Memory Efficient**: Lower resource usage than Windows version
- **Battery Friendly**: Optimized for MacBook battery life
- **Silent Operation**: No UI popup notifications

## 🔒 **Security Considerations**

### **Administrator Privileges**
```bash
# Run with elevated permissions for network control
sudo ./PocketFence-Simple

# Or configure sudoers for passwordless network commands
echo "$(whoami) ALL=(ALL) NOPASSWD: /usr/sbin/networksetup" | sudo tee -a /etc/sudoers
```

### **Gatekeeper Bypass**
```bash
# If macOS blocks execution (unsigned binary)
sudo xattr -dr com.apple.quarantine ./PocketFence-Simple

# Or allow in System Preferences > Security & Privacy
```

## 🖥️ **Running as System Service (Optional)**

### **Create Launch Daemon**
```bash
# Create service file
sudo tee /Library/LaunchDaemons/com.pocketfence.simple.plist << 'EOF'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>Label</key>
    <string>com.pocketfence.simple</string>
    <key>ProgramArguments</key>
    <array>
        <string>/path/to/PocketFence-Simple</string>
    </array>
    <key>RunAtLoad</key>
    <true/>
    <key>KeepAlive</key>
    <true/>
    <key>UserName</key>
    <string>root</string>
</dict>
</plist>
EOF

# Load and start service
sudo launchctl load /Library/LaunchDaemons/com.pocketfence.simple.plist
sudo launchctl start com.pocketfence.simple
```

## 🔍 **Troubleshooting**

### **Common Issues**

**Port 5000 Already in Use:**
```bash
# Find process using port 5000
lsof -i :5000

# Kill conflicting process
sudo kill -9 [PID]

# Or use different port
export ASPNETCORE_URLS="http://0.0.0.0:5001"
```

**Permission Denied Errors:**
```bash
# Fix permissions
chmod +x PocketFence-Simple
sudo chown $(whoami) PocketFence-Simple

# Run with sudo if needed
sudo ./PocketFence-Simple
```

**Network Interface Not Found:**
```bash
# List available interfaces
networksetup -listallhardwareports

# Check WiFi status
networksetup -getairportpower Wi-Fi
```

### **Log Files**
```bash
# View application logs
tail -f ~/Library/Logs/PocketFence-Simple/app.log

# System logs
log stream --predicate 'eventMessage contains "PocketFence"'
```

## 🎯 **Performance Optimization**

### **Apple Silicon (M1/M2/M3/M4) Optimization**
```bash
# Build native ARM64 binary for best performance
dotnet publish -c Release -r osx-arm64 --self-contained true

# Enable ready-to-run images
dotnet publish -c Release -r osx-arm64 --self-contained true -p:PublishReadyToRun=true
```

### **Intel Mac Optimization**
```bash
# Build x64 binary for Intel Macs
dotnet publish -c Release -r osx-x64 --self-contained true -p:PublishReadyToRun=true
```

## 🌐 **Network Configuration Examples**

### **Home Network Setup**
```bash
# Share Ethernet connection via WiFi
sudo networksetup -setinternetsharing Ethernet on

# Share WiFi connection via WiFi (bridge mode)  
sudo networksetup -setinternetsharing Wi-Fi on
```

### **Corporate Network**
```bash
# Use different SSID to avoid conflicts
export POCKETFENCE_HOTSPOT_SSID="PocketFence-Corp"
export POCKETFENCE_HOTSPOT_PASSWORD="SecurePassword123"
```

## 📊 **Monitoring & Analytics**

### **Network Statistics**
- **Real-time device count**: Dashboard shows connected devices
- **Bandwidth usage**: Monitors data consumption per device
- **Connection history**: Tracks device connection patterns
- **Security alerts**: Identifies suspicious network activity

### **System Integration**
- **Notification Center**: macOS notifications for important events
- **Menu Bar Icon**: Optional system tray integration  
- **Shortcuts**: Create keyboard shortcuts for common functions
- **Automator**: Integrate with macOS automation workflows

## 🔗 **Integration with iOS Ecosystem**

### **iCloud Sync** (Future Feature)
- **Settings sync**: Share configuration across Mac and iOS devices
- **Family profiles**: Sync parental control settings
- **Device history**: Track children's devices across platforms

### **AirDrop Support** (Future Feature)  
- **Configuration sharing**: Send settings via AirDrop
- **Quick setup**: Configure new devices automatically
- **Profile exchange**: Share filtering profiles between parents

---

## ✨ **Conclusion**

PocketFence-Simple on macOS provides **superior networking capabilities** compared to iOS, with full WiFi hotspot support, robust device monitoring, and seamless integration with the Apple ecosystem.

**Key Advantages:**
- ✅ **WiFi Bridge Capable** (unlike iOS limitation)
- ✅ **Native Performance** on Apple Silicon
- ✅ **System-level Integration** with macOS
- ✅ **Cross-platform Compatibility** with all devices
- ✅ **Enterprise-ready** for business environments

Your MacBook is now a powerful **parental control hub** that can manage and filter content for all family devices! 🎉