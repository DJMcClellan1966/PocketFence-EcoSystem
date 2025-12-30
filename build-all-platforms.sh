#!/bin/bash
# Build script for creating platform-specific optimized binaries
# Creates downloadable binaries for the bootstrap/update system

echo "🔨 Building PocketFence AI for all platforms..."

# Clean previous builds
rm -rf releases
mkdir -p releases

# Define platforms
platforms=("win-x64" "win-arm64" "osx-x64" "osx-arm64" "linux-x64" "linux-arm64")

for platform in "${platforms[@]}"; do
    echo "Building for $platform..."
    
    # Build main application
    dotnet publish PocketFence-AI.csproj \
        -c Release \
        -r $platform \
        --self-contained \
        -p:PublishSingleFile=true \
        -p:PublishTrimmed=true \
        -p:EnableCompressionInSingleFile=true \
        -o "releases/$platform"
    
    # Copy binary to releases with standard naming
    if [[ $platform == win* ]]; then
        cp "releases/$platform/PocketFence-AI.exe" "releases/PocketFence-AI-$platform.exe"
    else
        cp "releases/$platform/PocketFence-AI" "releases/PocketFence-AI-$platform"
    fi
    
    # Build bootstrap for this platform
    dotnet publish Bootstrap/PocketFence.Bootstrap.csproj \
        -c Release \
        -r $platform \
        --self-contained \
        -p:PublishSingleFile=true \
        -p:PublishTrimmed=true \
        -p:EnableCompressionInSingleFile=true \
        -o "releases/bootstrap/$platform"
    
    # Copy bootstrap binary
    if [[ $platform == win* ]]; then
        cp "releases/bootstrap/$platform/pocketfence-bootstrap.exe" "releases/pocketfence-bootstrap-$platform.exe"
    else
        cp "releases/bootstrap/$platform/pocketfence-bootstrap" "releases/pocketfence-bootstrap-$platform"
    fi
    
    echo "✅ $platform complete"
done

# Create version file
echo "1.0.0" > releases/version.txt

# Show release sizes
echo -e "\n📊 Release Sizes:"
ls -lh releases/PocketFence-AI-* | awk '{print $9, $5}'
echo -e "\n📊 Bootstrap Sizes:"
ls -lh releases/pocketfence-bootstrap-* | awk '{print $9, $5}'

echo -e "\n✅ All builds complete! Upload releases/ folder to GitHub Releases"
echo "💡 Bootstrap executables are ~1-2MB, main executables are ~10-12MB"