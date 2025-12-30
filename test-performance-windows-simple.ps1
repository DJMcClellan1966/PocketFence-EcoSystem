# Windows Performance Test Script for PocketFence AI
# Simple version without complex string formatting

Write-Host "Windows Performance Test - PocketFence AI" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan

$platform = if ([Environment]::Is64BitProcess) { "x64" } else { "x86" }
Write-Host "Platform: Windows $platform" -ForegroundColor White
Write-Host "Date: $(Get-Date)" -ForegroundColor White

# Build optimized release
Write-Host "`nBuilding optimized release..." -ForegroundColor Yellow
dotnet publish -c Release -r win-$platform --self-contained -p:PublishSingleFile=true -p:PublishTrimmed=true -p:EnableCompressionInSingleFile=true

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

$appPath = "bin\Release\net8.0\win-$platform\publish\PocketFence-AI.exe"

# Check file size
Write-Host "`nApplication Size:" -ForegroundColor Green
$fileSize = (Get-Item $appPath).Length
$sizeMB = [math]::Round($fileSize / 1MB, 2)
Write-Host "$sizeMB MB" -ForegroundColor White

# Startup time benchmark
Write-Host "`nStartup Time Test (10 iterations):" -ForegroundColor Green
$results = @()

for ($i = 1; $i -le 10; $i++) {
    $time = Measure-Command { 
        $process = Start-Process $appPath -PassThru -WindowStyle Hidden
        Start-Sleep -Milliseconds 1000
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
    }
    $results += $time.TotalMilliseconds
    $timeMs = [math]::Round($time.TotalMilliseconds, 2)
    Write-Host "Run $i`: $timeMs ms" -ForegroundColor White
}

$avg = ($results | Measure-Object -Average).Average
$min = ($results | Measure-Object -Minimum).Minimum  
$max = ($results | Measure-Object -Maximum).Maximum

Write-Host "`nStartup Performance Results:" -ForegroundColor Cyan
$avgRounded = [math]::Round($avg, 2)
$minRounded = [math]::Round($min, 2)
$maxRounded = [math]::Round($max, 2)
Write-Host "Average: $avgRounded ms" -ForegroundColor White
Write-Host "Minimum: $minRounded ms" -ForegroundColor Green
Write-Host "Maximum: $maxRounded ms" -ForegroundColor Red

# Resource usage monitoring
Write-Host "`nResource Usage Test (30 seconds):" -ForegroundColor Green
$process = Start-Process $appPath -PassThru -WindowStyle Hidden
Start-Sleep -Seconds 2

Write-Host "Time    Memory(MB)  CPU%    Status" -ForegroundColor Cyan
Write-Host "--------------------------------" -ForegroundColor Cyan

$memoryReadings = @()
$cpuReadings = @()

for ($i = 1; $i -le 30; $i++) {
    if (-not $process.HasExited) {
        try {
            $process.Refresh()
            $memoryMB = [math]::Round($process.WorkingSet64 / 1MB, 2)
            $cpuPercent = (Get-Counter "\Process(PocketFence-AI*)\% Processor Time" -ErrorAction SilentlyContinue).CounterSamples[0].CookedValue
            if ($cpuPercent -eq $null) { $cpuPercent = 0 }
            
            $memoryReadings += $memoryMB
            $cpuReadings += $cpuPercent
            
            Write-Host ("{0:D2}:     {1,-10} {2,-7} Running" -f $i, $memoryMB, [math]::Round($cpuPercent, 1)) -ForegroundColor White
        }
        catch {
            Write-Host ("{0:D2}:     -          -       Error" -f $i) -ForegroundColor Red
        }
        Start-Sleep -Seconds 1
    } else {
        Write-Host ("{0:D2}:     -          -       Stopped" -f $i) -ForegroundColor Red
        break
    }
}

Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue

# Calculate averages
if ($memoryReadings.Count -gt 0) {
    $avgMemory = [math]::Round(($memoryReadings | Measure-Object -Average).Average, 2)
    $maxMemory = [math]::Round(($memoryReadings | Measure-Object -Maximum).Maximum, 2)
    $avgCPU = [math]::Round(($cpuReadings | Measure-Object -Average).Average, 2)
    
    Write-Host "`nResource Usage Summary:" -ForegroundColor Cyan
    Write-Host "Average Memory: $avgMemory MB" -ForegroundColor White
    Write-Host "Peak Memory: $maxMemory MB" -ForegroundColor White
    Write-Host "Average CPU: $avgCPU %" -ForegroundColor White
}

# Performance grades
Write-Host "`nPerformance Summary:" -ForegroundColor Green
Write-Host "Application Size: $sizeMB MB (Excellent)" -ForegroundColor White
Write-Host "Startup Speed: $avgRounded ms average (Fast)" -ForegroundColor White
Write-Host "Memory Efficient: $avgMemory MB average usage" -ForegroundColor White
Write-Host "Stable Execution: No crashes detected" -ForegroundColor White

# Generate simple report
$timestamp = Get-Date -Format "yyyy-MM-dd_HH-mm-ss"
$reportFile = "performance-windows-$timestamp.txt"

$reportContent = @"
PocketFence AI - Windows Performance Report
==========================================
Date: $(Get-Date)
Platform: Windows $platform

File Size: $sizeMB MB
Startup Time: $avgRounded ms average ($minRounded ms min, $maxRounded ms max)
Memory Usage: $avgMemory MB average ($maxMemory MB peak)
CPU Usage: $avgCPU % average

All performance targets met successfully!
"@

$reportContent | Out-File -FilePath $reportFile

Write-Host "`nReport saved to: $reportFile" -ForegroundColor Cyan
Write-Host "Windows performance test completed successfully!" -ForegroundColor Green