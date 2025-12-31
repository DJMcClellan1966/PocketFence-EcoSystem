#!/bin/bash
# PocketFence Universal Filter - Cross-Platform Build Script

echo "🚀 Building PocketFence Universal Active Filter for all platforms..."
echo "GPT4All-style optimization enabled"
echo

# Build for Windows
echo "Building for Windows x64..."
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true -o builds/windows-x64

echo "Building for Windows ARM64..."
dotnet publish -c Release -r win-arm64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true -o builds/windows-arm64

# Build for macOS
echo "Building for macOS Intel..."
dotnet publish -c Release -r osx-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true -o builds/macos-intel

echo "Building for macOS Apple Silicon..."
dotnet publish -c Release -r osx-arm64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true -o builds/macos-apple-silicon

# Build for Linux
echo "Building for Linux x64..."
dotnet publish -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true -o builds/linux-x64

echo "Building for Linux ARM64..."
dotnet publish -c Release -r linux-arm64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true -o builds/linux-arm64

echo "Building for Linux ARM32 (Raspberry Pi)..."
dotnet publish -c Release -r linux-arm --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true -o builds/linux-arm32

echo
echo "✅ Universal builds completed!"
echo "📦 Builds available in ./builds/ directory"
echo
echo "📋 Platform Support:"
echo "  🖥️  Windows (x64, ARM64)"
echo "  🍎 macOS (Intel, Apple Silicon)"  
echo "  🐧 Linux (x64, ARM64, ARM32/Pi)"
echo "  📱 Android (via proxy settings)"
echo "  📱 iOS (limited proxy support)"
echo
echo "🎯 Each build is optimized like GPT4All:"
echo "  ✅ Single-file executable (~12-15MB)"
echo "  ✅ No external dependencies" 
echo "  ✅ Local processing only"
echo "  ✅ Cross-platform compatibility"
echo
echo "🧪 Performance Testing Available:"
echo "   Windows: .\\test-performance-windows.ps1"
echo "   Unix:    ./test-performance-unix.sh"
echo "   Python:  python test-performance-python.py"