#!/bin/bash

# 🍎 PocketFence-Simple: macOS Build & Deployment Script
# Automates the complete build process for macOS deployment

set -e  # Exit on any error

echo "🍎 PocketFence-Simple: macOS Build Script"
echo "=========================================="

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Function to print colored output
print_status() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

print_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

print_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Check prerequisites
print_status "Checking prerequisites..."

# Check if .NET is installed
if ! command -v dotnet &> /dev/null; then
    print_error ".NET is not installed. Installing .NET 10..."
    curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --version preview
    export PATH="$HOME/.dotnet:$PATH"
fi

# Verify .NET version
DOTNET_VERSION=$(dotnet --version)
print_success ".NET version: $DOTNET_VERSION"

# Clean previous builds
print_status "Cleaning previous builds..."
dotnet clean
rm -rf bin obj publish

# Restore dependencies
print_status "Restoring NuGet packages..."
dotnet restore

# Build for macOS (both Intel and Apple Silicon)
print_status "Building for macOS..."

# Build debug version first
print_status "Building debug version..."
dotnet build -c Debug

# Build release version
print_status "Building release version..."
dotnet build -c Release

# Publish for Intel Macs (x64)
print_status "Publishing for Intel Macs (x64)..."
dotnet publish -c Release -r osx-x64 --self-contained true \
    -p:PublishSingleFile=true \
    -p:PublishTrimmed=true \
    -p:PublishReadyToRun=true \
    -o publish/osx-x64

# Publish for Apple Silicon Macs (ARM64)
print_status "Publishing for Apple Silicon (ARM64)..."
dotnet publish -c Release -r osx-arm64 --self-contained true \
    -p:PublishSingleFile=true \
    -p:PublishTrimmed=true \
    -p:PublishReadyToRun=true \
    -o publish/osx-arm64

# Create universal binary (optional - for advanced users)
print_status "Creating distribution packages..."

# Intel Mac package
cd publish/osx-x64
tar -czf ../PocketFence-Simple-osx-x64.tar.gz ./*
cd ../..

# Apple Silicon package  
cd publish/osx-arm64
tar -czf ../PocketFence-Simple-osx-arm64.tar.gz ./*
cd ../..

# Create deployment directory
mkdir -p deploy/macOS

# Copy binaries and create installer script
cp publish/PocketFence-Simple-osx-x64.tar.gz deploy/macOS/
cp publish/PocketFence-Simple-osx-arm64.tar.gz deploy/macOS/
cp DEPLOYMENT_MACOS.md deploy/macOS/

# Create universal installer script
cat > deploy/macOS/install.sh << 'EOF'
#!/bin/bash

# PocketFence-Simple macOS Installer
echo "🍎 PocketFence-Simple macOS Installer"
echo "====================================="

# Detect architecture
ARCH=$(uname -m)
if [[ "$ARCH" == "arm64" ]]; then
    PACKAGE="PocketFence-Simple-osx-arm64.tar.gz"
    echo "✅ Detected Apple Silicon Mac (ARM64)"
elif [[ "$ARCH" == "x86_64" ]]; then
    PACKAGE="PocketFence-Simple-osx-x64.tar.gz"
    echo "✅ Detected Intel Mac (x64)"
else
    echo "❌ Unsupported architecture: $ARCH"
    exit 1
fi

# Create installation directory
INSTALL_DIR="$HOME/Applications/PocketFence-Simple"
mkdir -p "$INSTALL_DIR"

# Extract package
echo "📦 Extracting $PACKAGE..."
tar -xzf "$PACKAGE" -C "$INSTALL_DIR"

# Make executable
chmod +x "$INSTALL_DIR/PocketFence-Simple"

# Create symlink for command line access
sudo ln -sf "$INSTALL_DIR/PocketFence-Simple" /usr/local/bin/pocketfence

# Create desktop shortcut (optional)
SHORTCUT_DIR="$HOME/Desktop"
if [[ -d "$SHORTCUT_DIR" ]]; then
    cat > "$SHORTCUT_DIR/PocketFence-Simple.command" << 'SHORTCUT_EOF'
#!/bin/bash
cd "$HOME/Applications/PocketFence-Simple"
./PocketFence-Simple
SHORTCUT_EOF
    chmod +x "$SHORTCUT_DIR/PocketFence-Simple.command"
    echo "🖥️  Desktop shortcut created"
fi

echo ""
echo "✅ Installation complete!"
echo ""
echo "🚀 To start PocketFence-Simple:"
echo "   Option 1: Double-click Desktop shortcut"
echo "   Option 2: Run 'pocketfence' in Terminal"
echo "   Option 3: Navigate to $INSTALL_DIR and run ./PocketFence-Simple"
echo ""
echo "🌐 Once running, access the dashboard at: http://localhost:5000"
echo "📱 iOS/Android devices can access at: http://[your-mac-ip]:5000"
echo ""
echo "📖 For complete setup guide, see: DEPLOYMENT_MACOS.md"
EOF

chmod +x deploy/macOS/install.sh

# Generate build information
cat > deploy/macOS/BUILD_INFO.txt << EOF
PocketFence-Simple - macOS Build Information
==========================================

Build Date: $(date)
.NET Version: $DOTNET_VERSION
Host OS: $(uname -s)
Host Architecture: $(uname -m)

Package Contents:
- PocketFence-Simple-osx-x64.tar.gz     (Intel Macs)
- PocketFence-Simple-osx-arm64.tar.gz   (Apple Silicon Macs)
- install.sh                            (Universal installer)
- DEPLOYMENT_MACOS.md                   (Setup guide)

Installation:
1. Run: chmod +x install.sh && ./install.sh
2. Follow the prompts for your Mac architecture
3. Start PocketFence and access at http://localhost:5000

Requirements:
- macOS 10.14 (Mojave) or newer
- Administrator privileges for network features
- WiFi capability for hotspot functionality

Features:
✅ Cross-platform .NET 10 with preview features
✅ macOS Internet Sharing integration
✅ iOS/Android device detection and management
✅ Progressive Web App support for mobile devices
✅ Real-time network monitoring and content filtering
✅ AI-powered threat detection and parental controls
EOF

# Create README for deployment
cat > deploy/macOS/README.md << 'EOF'
# 🍎 PocketFence-Simple for macOS

## Quick Installation

```bash
# Make installer executable and run
chmod +x install.sh
./install.sh
```

## Manual Installation

### Intel Macs:
```bash
tar -xzf PocketFence-Simple-osx-x64.tar.gz
cd PocketFence-Simple-osx-x64
chmod +x PocketFence-Simple
./PocketFence-Simple
```

### Apple Silicon Macs:
```bash
tar -xzf PocketFence-Simple-osx-arm64.tar.gz
cd PocketFence-Simple-osx-arm64
chmod +x PocketFence-Simple
./PocketFence-Simple
```

## Access Dashboard

- **Local**: http://localhost:5000
- **Mobile devices**: http://[your-mac-ip]:5000

## Documentation

See `DEPLOYMENT_MACOS.md` for complete setup guide.
EOF

# Test the builds
print_status "Testing builds..."

# Test Intel build
if [[ -f "publish/osx-x64/PocketFence-Simple" ]]; then
    print_success "Intel Mac build: OK"
else
    print_error "Intel Mac build: FAILED"
fi

# Test Apple Silicon build
if [[ -f "publish/osx-arm64/PocketFence-Simple" ]]; then
    print_success "Apple Silicon build: OK"
else
    print_error "Apple Silicon build: FAILED"
fi

# Display summary
echo ""
print_success "🎉 macOS build process completed!"
echo ""
echo "📦 Build artifacts:"
echo "   • deploy/macOS/PocketFence-Simple-osx-x64.tar.gz"
echo "   • deploy/macOS/PocketFence-Simple-osx-arm64.tar.gz"
echo "   • deploy/macOS/install.sh"
echo "   • deploy/macOS/DEPLOYMENT_MACOS.md"
echo ""
echo "🚀 Deployment options:"
echo "   1. Share the deploy/macOS/ folder"
echo "   2. Upload to GitHub releases"
echo "   3. Distribute via direct download"
echo ""
echo "📱 To test on this Mac:"
echo "   cd deploy/macOS"
echo "   ./install.sh"
echo ""
print_warning "Note: Requires administrator privileges for network features"

# Optional: Open deployment folder
if command -v open &> /dev/null; then
    read -p "Open deployment folder in Finder? (y/n): " -n 1 -r
    echo
    if [[ $REPLY =~ ^[Yy]$ ]]; then
        open deploy/macOS
    fi
fi