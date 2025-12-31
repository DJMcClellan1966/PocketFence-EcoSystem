# PocketFence Universal Filter - Windows Build Script
# GPT4All-style optimization for universal deployment

Write-Host "🚀 Building PocketFence Universal Active Filter for all platforms..." -ForegroundColor Green
Write-Host "GPT4All-style optimization enabled" -ForegroundColor Yellow
Write-Host ""

# Create builds directory
New-Item -ItemType Directory -Force -Path "builds" | Out-Null

# Build for Windows
Write-Host "Building for Windows x64..." -ForegroundColor Cyan
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true -o builds/windows-x64

Write-Host "Building for Windows ARM64..." -ForegroundColor Cyan
dotnet publish -c Release -r win-arm64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true -o builds/windows-arm64

# Build for macOS
Write-Host "Building for macOS Intel..." -ForegroundColor Cyan
dotnet publish -c Release -r osx-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true -o builds/macos-intel

Write-Host "Building for macOS Apple Silicon..." -ForegroundColor Cyan
dotnet publish -c Release -r osx-arm64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true -o builds/macos-apple-silicon

# Build for Linux
Write-Host "Building for Linux x64..." -ForegroundColor Cyan
dotnet publish -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true -o builds/linux-x64

Write-Host "Building for Linux ARM64..." -ForegroundColor Cyan
dotnet publish -c Release -r linux-arm64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true -o builds/linux-arm64

Write-Host "Building for Linux ARM32 (Raspberry Pi)..." -ForegroundColor Cyan
dotnet publish -c Release -r linux-arm --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true -o builds/linux-arm32

Write-Host ""
Write-Host "✅ Universal builds completed!" -ForegroundColor Green
Write-Host "📦 Builds available in ./builds/ directory" -ForegroundColor Yellow
Write-Host ""
Write-Host "📋 Platform Support:" -ForegroundColor White
Write-Host "  🖥️  Windows (x64, ARM64)" -ForegroundColor Gray
Write-Host "  🍎 macOS (Intel, Apple Silicon)" -ForegroundColor Gray
Write-Host "  🐧 Linux (x64, ARM64, ARM32/Pi)" -ForegroundColor Gray
Write-Host "  📱 Android (via proxy settings)" -ForegroundColor Gray
Write-Host "  📱 iOS (limited proxy support)" -ForegroundColor Gray
Write-Host ""
Write-Host "🎯 Each build is optimized like GPT4All:" -ForegroundColor White
Write-Host "  ✅ Single-file executable (~12-15MB)" -ForegroundColor Green
Write-Host "  ✅ No external dependencies" -ForegroundColor Green
Write-Host "  ✅ Local processing only" -ForegroundColor Green
Write-Host "  ✅ Cross-platform compatibility" -ForegroundColor Green

# Show file sizes
Write-Host ""
Write-Host "📊 Build Sizes:" -ForegroundColor White
Get-ChildItem -Path "builds" -Recurse -Include "*.exe","PocketFence-Filter" | 
    ForEach-Object { 
        $size = [math]::Round($_.Length / 1MB, 2)
        Write-Host "  $($_.Directory.Name): $size MB" -ForegroundColor Cyan
    }

# Offer performance testing
Write-Host ""
Write-Host "🧪 Performance Testing Available:" -ForegroundColor Yellow
Write-Host "   Windows: .\test-performance-windows.ps1" -ForegroundColor Cyan
Write-Host "   Unix:    ./test-performance-unix.sh" -ForegroundColor Cyan
Write-Host "   Python:  python test-performance-python.py" -ForegroundColor Cyan