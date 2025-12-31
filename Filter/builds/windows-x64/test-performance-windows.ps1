# PocketFence Universal Filter - Windows Performance Test
# Cross-platform proxy filter performance validation

param(
    [int]$TestDuration = 30,
    [int]$ConcurrentConnections = 50,
    [string]$OutputPath = "filter-performance-windows-$(Get-Date -Format 'yyyy-MM-dd_HH-mm-ss').txt"
)

$ErrorActionPreference = "Continue"
$ProgressPreference = "SilentlyContinue"

Write-Host "🧪 PocketFence Universal Filter Performance Test" -ForegroundColor Green
Write-Host "Platform: Windows $(if ([Environment]::Is64BitOperatingSystem) {'x64'} else {'x86'})" -ForegroundColor Yellow
Write-Host "Date: $(Get-Date)" -ForegroundColor Gray
Write-Host ""

# Initialize test results
$TestResults = @{}
$TestLog = @()

function Write-TestLog {
    param($Message, $Color = "White")
    $Timestamp = Get-Date -Format "HH:mm:ss.fff"
    $LogEntry = "[$Timestamp] $Message"
    Write-Host $LogEntry -ForegroundColor $Color
    $TestLog += $LogEntry
}

function Measure-FilterStartup {
    Write-TestLog "🚀 Testing Filter Startup Performance..." "Cyan"
    
    $StartupTimes = @()
    
    for ($i = 1; $i -le 5; $i++) {
        Write-TestLog "  Startup test $i/5..."
        
        $StartTime = Get-Date
        
        # Start the filter in background
        $FilterProcess = Start-Process -FilePath ".\PocketFence-Filter.exe" -PassThru -NoNewWindow -RedirectStandardOutput "temp_output.txt" -RedirectStandardError "temp_error.txt"
        
        # Wait for proxy to be ready (check for listening port)
        $ReadyTime = $null
        $Timeout = (Get-Date).AddSeconds(10)
        
        while ((Get-Date) -lt $Timeout) {
            try {
                $Connection = Test-NetConnection -ComputerName "127.0.0.1" -Port 8888 -InformationLevel Quiet
                if ($Connection) {
                    $ReadyTime = Get-Date
                    break
                }
            }
            catch { }
            Start-Sleep -Milliseconds 100
        }
        
        if ($ReadyTime) {
            $StartupTime = ($ReadyTime - $StartTime).TotalMilliseconds
            $StartupTimes += $StartupTime
            Write-TestLog "    Startup time: $([math]::Round($StartupTime, 2))ms" "Green"
        }
        else {
            Write-TestLog "    Startup timeout!" "Red"
        }
        
        # Clean shutdown
        try {
            $FilterProcess | Stop-Process -Force
            Start-Sleep -Seconds 1
        }
        catch { }
        
        # Cleanup temp files
        Remove-Item -Path "temp_output.txt", "temp_error.txt" -Force -ErrorAction SilentlyContinue
    }
    
    if ($StartupTimes.Count -gt 0) {
        $TestResults.AvgStartupTime = ($StartupTimes | Measure-Object -Average).Average
        $TestResults.MinStartupTime = ($StartupTimes | Measure-Object -Minimum).Minimum
        $TestResults.MaxStartupTime = ($StartupTimes | Measure-Object -Maximum).Maximum
        
        Write-TestLog "✅ Average startup time: $([math]::Round($TestResults.AvgStartupTime, 2))ms" "Green"
        Write-TestLog "   Min: $([math]::Round($TestResults.MinStartupTime, 2))ms, Max: $([math]::Round($TestResults.MaxStartupTime, 2))ms" "Gray"
    }
}

function Measure-FilterMemory {
    Write-TestLog "🧠 Testing Memory Usage..." "Cyan"
    
    # Start filter for memory testing
    $FilterProcess = Start-Process -FilePath ".\PocketFence-Filter.exe" -PassThru -NoNewWindow -WindowStyle Hidden
    Start-Sleep -Seconds 3
    
    try {
        $MemoryReadings = @()
        
        for ($i = 1; $i -le 10; $i++) {
            $Process = Get-Process -Id $FilterProcess.Id -ErrorAction SilentlyContinue
            if ($Process) {
                $MemoryMB = [math]::Round($Process.WorkingSet64 / 1MB, 2)
                $MemoryReadings += $MemoryMB
                Write-TestLog "  Memory reading $i`: $MemoryMB MB"
            }
            Start-Sleep -Seconds 1
        }
        
        if ($MemoryReadings.Count -gt 0) {
            $TestResults.AvgMemoryMB = ($MemoryReadings | Measure-Object -Average).Average
            $TestResults.MinMemoryMB = ($MemoryReadings | Measure-Object -Minimum).Minimum
            $TestResults.MaxMemoryMB = ($MemoryReadings | Measure-Object -Maximum).Maximum
            
            Write-TestLog "✅ Average memory: $([math]::Round($TestResults.AvgMemoryMB, 2))MB" "Green"
            Write-TestLog "   Min: $([math]::Round($TestResults.MinMemoryMB, 2))MB, Max: $([math]::Round($TestResults.MaxMemoryMB, 2))MB" "Gray"
        }
    }
    finally {
        $FilterProcess | Stop-Process -Force -ErrorAction SilentlyContinue
    }
}

function Measure-FilterPerformance {
    Write-TestLog "⚡ Testing Filtering Performance..." "Cyan"
    
    # Start filter for performance testing
    $FilterProcess = Start-Process -FilePath ".\PocketFence-Filter.exe" -PassThru -NoNewWindow -WindowStyle Hidden
    Start-Sleep -Seconds 3
    
    try {
        # Test URLs for filtering
        $TestUrls = @(
            "http://example.com",
            "http://educational.site.com",
            "http://badsite-violence.com",
            "http://adult-content.com",
            "http://malware.site.com",
            "http://safe-learning.edu",
            "http://gambling-casino.com",
            "http://social-media.com"
        )
        
        $StartTime = Get-Date
        $RequestCount = 0
        $BlockedCount = 0
        $ResponseTimes = @()
        
        Write-TestLog "  Sending test requests for $TestDuration seconds..."
        
        $EndTime = (Get-Date).AddSeconds($TestDuration)
        
        while ((Get-Date) -lt $EndTime) {
            foreach ($Url in $TestUrls) {
                try {
                    $RequestStart = Get-Date
                    
                    # Send request through proxy
                    $Response = Invoke-WebRequest -Uri $Url -Proxy "http://127.0.0.1:8888" -TimeoutSec 5 -ErrorAction SilentlyContinue
                    
                    $RequestTime = ((Get-Date) - $RequestStart).TotalMilliseconds
                    $ResponseTimes += $RequestTime
                    $RequestCount++
                    
                    # Check if blocked (look for PocketFence block page)
                    if ($Response.Content -like "*PocketFence*Content Blocked*") {
                        $BlockedCount++
                    }
                }
                catch {
                    # Count timeouts/errors as processed requests
                    $RequestCount++
                }
                
                if ((Get-Date) -ge $EndTime) { break }
            }
        }
        
        $ActualDuration = ((Get-Date) - $StartTime).TotalSeconds
        
        $TestResults.RequestsPerSecond = [math]::Round($RequestCount / $ActualDuration, 2)
        $TestResults.TotalRequests = $RequestCount
        $TestResults.BlockedRequests = $BlockedCount
        $TestResults.BlockRate = if ($RequestCount -gt 0) { [math]::Round(($BlockedCount / $RequestCount) * 100, 1) } else { 0 }
        
        if ($ResponseTimes.Count -gt 0) {
            $TestResults.AvgResponseTime = [math]::Round(($ResponseTimes | Measure-Object -Average).Average, 2)
            $TestResults.MinResponseTime = [math]::Round(($ResponseTimes | Measure-Object -Minimum).Minimum, 2)
            $TestResults.MaxResponseTime = [math]::Round(($ResponseTimes | Measure-Object -Maximum).Maximum, 2)
        }
        
        Write-TestLog "✅ Performance results:" "Green"
        Write-TestLog "   Requests/sec: $($TestResults.RequestsPerSecond)" "Green"
        Write-TestLog "   Total requests: $RequestCount" "Gray"
        Write-TestLog "   Blocked: $BlockedCount ($($TestResults.BlockRate)%)" "Yellow"
        if ($ResponseTimes.Count -gt 0) {
            Write-TestLog "   Avg response: $($TestResults.AvgResponseTime)ms" "Gray"
        }
    }
    finally {
        $FilterProcess | Stop-Process -Force -ErrorAction SilentlyContinue
    }
}

function Test-FilterAccuracy {
    Write-TestLog "🎯 Testing Blocking Accuracy..." "Cyan"
    
    # Start filter for accuracy testing
    $FilterProcess = Start-Process -FilePath ".\PocketFence-Filter.exe" -PassThru -NoNewWindow -WindowStyle Hidden
    Start-Sleep -Seconds 3
    
    try {
        # Test cases: URL, Should Block, Age Level
        $TestCases = @(
            @{ Url = "http://education.com"; ShouldBlock = $false; Description = "Educational site" },
            @{ Url = "http://violence-fighting.com"; ShouldBlock = $true; Description = "Violence content" },
            @{ Url = "http://adult-explicit.com"; ShouldBlock = $true; Description = "Adult content" },
            @{ Url = "http://malware-virus.com"; ShouldBlock = $true; Description = "Malware site" },
            @{ Url = "http://school-learning.edu"; ShouldBlock = $false; Description = "School site" },
            @{ Url = "http://gambling-casino.com"; ShouldBlock = $true; Description = "Gambling site" },
            @{ Url = "http://safe-tutorial.com"; ShouldBlock = $false; Description = "Tutorial site" },
            @{ Url = "http://cyberbullying-hate.com"; ShouldBlock = $true; Description = "Cyberbullying content" }
        )
        
        $CorrectBlocks = 0
        $CorrectAllows = 0
        $FalsePositives = 0
        $FalseNegatives = 0
        
        foreach ($TestCase in $TestCases) {
            try {
                $Response = Invoke-WebRequest -Uri $TestCase.Url -Proxy "http://127.0.0.1:8888" -TimeoutSec 5 -ErrorAction SilentlyContinue
                $IsBlocked = $Response.Content -like "*PocketFence*Content Blocked*"
                
                if ($TestCase.ShouldBlock -and $IsBlocked) {
                    $CorrectBlocks++
                    Write-TestLog "  ✅ $($TestCase.Description): Correctly blocked" "Green"
                }
                elseif (-not $TestCase.ShouldBlock -and -not $IsBlocked) {
                    $CorrectAllows++
                    Write-TestLog "  ✅ $($TestCase.Description): Correctly allowed" "Green"
                }
                elseif ($TestCase.ShouldBlock -and -not $IsBlocked) {
                    $FalseNegatives++
                    Write-TestLog "  ❌ $($TestCase.Description): Should block but allowed" "Red"
                }
                else {
                    $FalsePositives++
                    Write-TestLog "  ⚠️  $($TestCase.Description): Should allow but blocked" "Yellow"
                }
            }
            catch {
                Write-TestLog "  ❓ $($TestCase.Description): Request failed" "Gray"
            }
        }
        
        $TotalTests = $TestCases.Count
        $CorrectResults = $CorrectBlocks + $CorrectAllows
        $TestResults.FilterAccuracy = if ($TotalTests -gt 0) { [math]::Round(($CorrectResults / $TotalTests) * 100, 1) } else { 0 }
        $TestResults.CorrectBlocks = $CorrectBlocks
        $TestResults.CorrectAllows = $CorrectAllows
        $TestResults.FalsePositives = $FalsePositives
        $TestResults.FalseNegatives = $FalseNegatives
        
        Write-TestLog "✅ Accuracy: $($TestResults.FilterAccuracy)% ($CorrectResults/$TotalTests correct)" "Green"
    }
    finally {
        $FilterProcess | Stop-Process -Force -ErrorAction SilentlyContinue
    }
}

function Test-ConcurrentLoad {
    Write-TestLog "🔀 Testing Concurrent Load..." "Cyan"
    
    # Start filter for load testing
    $FilterProcess = Start-Process -FilePath ".\PocketFence-Filter.exe" -PassThru -NoNewWindow -WindowStyle Hidden
    Start-Sleep -Seconds 3
    
    try {
        $Jobs = @()
        $StartTime = Get-Date
        
        Write-TestLog "  Starting $ConcurrentConnections concurrent connections..."
        
        for ($i = 1; $i -le $ConcurrentConnections; $i++) {
            $Job = Start-Job -ScriptBlock {
                param($ProxyUrl, $TestUrl)
                try {
                    $Response = Invoke-WebRequest -Uri $TestUrl -Proxy $ProxyUrl -TimeoutSec 10
                    return @{ Success = $true; Length = $Response.Content.Length }
                }
                catch {
                    return @{ Success = $false; Error = $_.Exception.Message }
                }
            } -ArgumentList "http://127.0.0.1:8888", "http://example.com"
            
            $Jobs += $Job
        }
        
        # Wait for all jobs to complete
        $CompletedJobs = $Jobs | Wait-Job -Timeout 30
        $Results = $CompletedJobs | Receive-Job
        $Jobs | Remove-Job -Force
        
        $LoadTestDuration = ((Get-Date) - $StartTime).TotalSeconds
        $SuccessfulRequests = ($Results | Where-Object { $_.Success }).Count
        $FailedRequests = ($Results | Where-Object { -not $_.Success }).Count
        
        $TestResults.ConcurrentConnections = $ConcurrentConnections
        $TestResults.LoadTestDuration = [math]::Round($LoadTestDuration, 2)
        $TestResults.SuccessfulRequests = $SuccessfulRequests
        $TestResults.FailedRequests = $FailedRequests
        $TestResults.ConcurrentSuccessRate = if ($ConcurrentConnections -gt 0) { [math]::Round(($SuccessfulRequests / $ConcurrentConnections) * 100, 1) } else { 0 }
        
        Write-TestLog "✅ Concurrent load results:" "Green"
        Write-TestLog "   Successful: $SuccessfulRequests/$ConcurrentConnections ($($TestResults.ConcurrentSuccessRate)%)" "Green"
        Write-TestLog "   Duration: $($TestResults.LoadTestDuration)s" "Gray"
    }
    finally {
        $FilterProcess | Stop-Process -Force -ErrorAction SilentlyContinue
    }
}

# Using approved PowerShell verb 'New' for report generation function
function New-Report {
    Write-TestLog "📊 Generating Performance Report..." "Cyan"
    
    $Report = @"
🚀 PocketFence Universal Filter Performance Report
Generated: $(Get-Date)
Platform: Windows $(if ([Environment]::Is64BitOperatingSystem) {'x64'} else {'x86'})
Test Duration: $TestDuration seconds
Concurrent Connections: $ConcurrentConnections

📈 STARTUP PERFORMANCE:
$(if ($TestResults.AvgStartupTime) {
"  Average Startup Time: $([math]::Round($TestResults.AvgStartupTime, 2))ms
  Minimum Startup Time: $([math]::Round($TestResults.MinStartupTime, 2))ms  
  Maximum Startup Time: $([math]::Round($TestResults.MaxStartupTime, 2))ms"
} else {"  Startup tests failed"})

🧠 MEMORY USAGE:
$(if ($TestResults.AvgMemoryMB) {
"  Average Memory: $([math]::Round($TestResults.AvgMemoryMB, 2))MB
  Minimum Memory: $([math]::Round($TestResults.MinMemoryMB, 2))MB
  Maximum Memory: $([math]::Round($TestResults.MaxMemoryMB, 2))MB"
} else {"  Memory tests failed"})

⚡ FILTERING PERFORMANCE:
$(if ($TestResults.RequestsPerSecond) {
"  Requests/Second: $($TestResults.RequestsPerSecond)
  Total Requests: $($TestResults.TotalRequests)
  Blocked Requests: $($TestResults.BlockedRequests) ($($TestResults.BlockRate)%)
  Average Response Time: $($TestResults.AvgResponseTime)ms
  Min Response Time: $($TestResults.MinResponseTime)ms
  Max Response Time: $($TestResults.MaxResponseTime)ms"
} else {"  Performance tests failed"})

🎯 BLOCKING ACCURACY:
$(if ($TestResults.FilterAccuracy) {
"  Overall Accuracy: $($TestResults.FilterAccuracy)%
  Correct Blocks: $($TestResults.CorrectBlocks)
  Correct Allows: $($TestResults.CorrectAllows)
  False Positives: $($TestResults.FalsePositives)
  False Negatives: $($TestResults.FalseNegatives)"
} else {"  Accuracy tests failed"})

🔀 CONCURRENT LOAD:
$(if ($TestResults.ConcurrentSuccessRate) {
"  Connections Tested: $($TestResults.ConcurrentConnections)
  Success Rate: $($TestResults.ConcurrentSuccessRate)%
  Successful Requests: $($TestResults.SuccessfulRequests)
  Failed Requests: $($TestResults.FailedRequests)
  Load Test Duration: $($TestResults.LoadTestDuration)s"
} else {"  Load tests failed"})

🏆 PERFORMANCE GRADE:
$(
$Grade = "F"
$Score = 0

if ($TestResults.AvgStartupTime -and $TestResults.AvgStartupTime -lt 2000) { $Score += 15 }
if ($TestResults.AvgMemoryMB -and $TestResults.AvgMemoryMB -lt 50) { $Score += 20 }
if ($TestResults.RequestsPerSecond -and $TestResults.RequestsPerSecond -gt 10) { $Score += 20 }
if ($TestResults.FilterAccuracy -and $TestResults.FilterAccuracy -gt 80) { $Score += 25 }
if ($TestResults.ConcurrentSuccessRate -and $TestResults.ConcurrentSuccessRate -gt 90) { $Score += 20 }

if ($Score -ge 90) { $Grade = "A+" }
elseif ($Score -ge 80) { $Grade = "A" }
elseif ($Score -ge 70) { $Grade = "B" }
elseif ($Score -ge 60) { $Grade = "C" }
elseif ($Score -ge 50) { $Grade = "D" }

"  Overall Score: $Score/100 - Grade: $Grade"
)

📝 TEST LOG:
$($TestLog -join "`n")

"@

    $Report | Out-File -FilePath $OutputPath -Encoding UTF8
    Write-TestLog "✅ Report saved to: $OutputPath" "Green"
    
    return $Report
}

# Run all tests
try {
    Write-TestLog "Starting PocketFence Universal Filter performance tests..." "Yellow"
    Write-TestLog "Test parameters: Duration=$TestDuration seconds, Connections=$ConcurrentConnections" "Gray"
    Write-TestLog ""
    
    Measure-FilterStartup
    Write-TestLog ""
    
    Measure-FilterMemory  
    Write-TestLog ""
    
    Measure-FilterPerformance
    Write-TestLog ""
    
    Test-FilterAccuracy
    Write-TestLog ""
    
    Test-ConcurrentLoad
    Write-TestLog ""
    
    $Report = New-Report
    
    Write-Host ""
    Write-Host "🎉 Performance testing completed!" -ForegroundColor Green
    Write-Host "📊 Results saved to: $OutputPath" -ForegroundColor Yellow
    
    # Show summary
    if ($TestResults.AvgStartupTime) {
        Write-Host "⚡ Startup: $([math]::Round($TestResults.AvgStartupTime, 2))ms" -ForegroundColor Cyan
    }
    if ($TestResults.AvgMemoryMB) {
        Write-Host "🧠 Memory: $([math]::Round($TestResults.AvgMemoryMB, 2))MB" -ForegroundColor Cyan
    }
    if ($TestResults.RequestsPerSecond) {
        Write-Host "🚀 Performance: $($TestResults.RequestsPerSecond) req/s" -ForegroundColor Cyan
    }
    if ($TestResults.FilterAccuracy) {
        Write-Host "🎯 Accuracy: $($TestResults.FilterAccuracy)%" -ForegroundColor Cyan
    }
}
catch {
    Write-TestLog "❌ Test execution failed: $($_.Exception.Message)" "Red"
    exit 1
}