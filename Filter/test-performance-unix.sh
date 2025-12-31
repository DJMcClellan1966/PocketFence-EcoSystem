#!/bin/bash
# PocketFence Universal Filter - Unix Performance Test
# Cross-platform proxy filter performance validation

set -e

# Test parameters
TEST_DURATION=${1:-30}
CONCURRENT_CONNECTIONS=${2:-50}
OUTPUT_PATH="filter-performance-unix-$(date +%Y-%m-%d_%H-%M-%S).txt"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
GRAY='\033[0;37m'
NC='\033[0m' # No Color

# Test results storage
declare -A TEST_RESULTS
TEST_LOG=()

# Helper functions
log_test() {
    local message="$1"
    local color="${2:-$NC}"
    local timestamp=$(date +"%H:%M:%S.%3N")
    local log_entry="[$timestamp] $message"
    echo -e "${color}${log_entry}${NC}"
    TEST_LOG+=("$log_entry")
}

check_dependencies() {
    log_test "🔧 Checking dependencies..." "$CYAN"
    
    # Check if curl is available
    if ! command -v curl &> /dev/null; then
        log_test "❌ curl is required but not installed" "$RED"
        exit 1
    fi
    
    # Check if netstat/ss is available
    if ! command -v netstat &> /dev/null && ! command -v ss &> /dev/null; then
        log_test "❌ netstat or ss is required but not installed" "$RED"
        exit 1
    fi
    
    # Check if bc is available for calculations
    if ! command -v bc &> /dev/null; then
        log_test "❌ bc is required but not installed" "$RED"
        exit 1
    fi
    
    log_test "✅ All dependencies available" "$GREEN"
}

measure_filter_startup() {
    log_test "🚀 Testing Filter Startup Performance..." "$CYAN"
    
    local startup_times=()
    local executable="./PocketFence-Filter"
    
    # Check for different executable names
    if [[ -f "./PocketFence-Filter" ]]; then
        executable="./PocketFence-Filter"
    elif [[ -f "./pocketfence-filter" ]]; then
        executable="./pocketfence-filter"
    elif [[ -f "./filter" ]]; then
        executable="./filter"
    else
        log_test "❌ PocketFence-Filter executable not found" "$RED"
        return 1
    fi
    
    for i in {1..5}; do
        log_test "  Startup test $i/5..."
        
        local start_time=$(date +%s.%3N)
        
        # Start the filter in background
        $executable &
        local filter_pid=$!
        
        # Wait for proxy to be ready (check for listening port)
        local ready_time=""
        local timeout=$(($(date +%s) + 10))
        
        while [[ $(date +%s) -lt $timeout ]]; do
            if netstat -ln 2>/dev/null | grep -q ":8888 " || ss -ln 2>/dev/null | grep -q ":8888 "; then
                ready_time=$(date +%s.%3N)
                break
            fi
            sleep 0.1
        done
        
        if [[ -n "$ready_time" ]]; then
            local startup_time=$(echo "($ready_time - $start_time) * 1000" | bc)
            startup_times+=("$startup_time")
            log_test "    Startup time: ${startup_time}ms" "$GREEN"
        else
            log_test "    Startup timeout!" "$RED"
        fi
        
        # Clean shutdown
        kill $filter_pid 2>/dev/null || true
        wait $filter_pid 2>/dev/null || true
        sleep 1
    done
    
    if [[ ${#startup_times[@]} -gt 0 ]]; then
        local sum=0
        local min=${startup_times[0]}
        local max=${startup_times[0]}
        
        for time in "${startup_times[@]}"; do
            sum=$(echo "$sum + $time" | bc)
            if (( $(echo "$time < $min" | bc -l) )); then
                min=$time
            fi
            if (( $(echo "$time > $max" | bc -l) )); then
                max=$time
            fi
        done
        
        TEST_RESULTS[avg_startup_time]=$(echo "scale=2; $sum / ${#startup_times[@]}" | bc)
        TEST_RESULTS[min_startup_time]=$min
        TEST_RESULTS[max_startup_time]=$max
        
        log_test "✅ Average startup time: ${TEST_RESULTS[avg_startup_time]}ms" "$GREEN"
        log_test "   Min: ${min}ms, Max: ${max}ms" "$GRAY"
    fi
}

measure_filter_memory() {
    log_test "🧠 Testing Memory Usage..." "$CYAN"
    
    local executable="./PocketFence-Filter"
    [[ -f "./pocketfence-filter" ]] && executable="./pocketfence-filter"
    [[ -f "./filter" ]] && executable="./filter"
    
    # Start filter for memory testing
    $executable &
    local filter_pid=$!
    sleep 3
    
    local memory_readings=()
    
    for i in {1..10}; do
        if ps -p $filter_pid > /dev/null 2>&1; then
            # Get memory in KB and convert to MB
            local memory_kb=$(ps -o rss= -p $filter_pid 2>/dev/null | tr -d ' ')
            if [[ -n "$memory_kb" && "$memory_kb" =~ ^[0-9]+$ ]]; then
                local memory_mb=$(echo "scale=2; $memory_kb / 1024" | bc)
                memory_readings+=("$memory_mb")
                log_test "  Memory reading $i: ${memory_mb}MB"
            fi
        fi
        sleep 1
    done
    
    # Clean shutdown
    kill $filter_pid 2>/dev/null || true
    wait $filter_pid 2>/dev/null || true
    
    if [[ ${#memory_readings[@]} -gt 0 ]]; then
        local sum=0
        local min=${memory_readings[0]}
        local max=${memory_readings[0]}
        
        for mem in "${memory_readings[@]}"; do
            sum=$(echo "$sum + $mem" | bc)
            if (( $(echo "$mem < $min" | bc -l) )); then
                min=$mem
            fi
            if (( $(echo "$mem > $max" | bc -l) )); then
                max=$mem
            fi
        done
        
        TEST_RESULTS[avg_memory_mb]=$(echo "scale=2; $sum / ${#memory_readings[@]}" | bc)
        TEST_RESULTS[min_memory_mb]=$min
        TEST_RESULTS[max_memory_mb]=$max
        
        log_test "✅ Average memory: ${TEST_RESULTS[avg_memory_mb]}MB" "$GREEN"
        log_test "   Min: ${min}MB, Max: ${max}MB" "$GRAY"
    fi
}

measure_filter_performance() {
    log_test "⚡ Testing Filtering Performance..." "$CYAN"
    
    local executable="./PocketFence-Filter"
    [[ -f "./pocketfence-filter" ]] && executable="./pocketfence-filter"
    [[ -f "./filter" ]] && executable="./filter"
    
    # Start filter for performance testing
    $executable &
    local filter_pid=$!
    sleep 3
    
    # Test URLs for filtering
    local test_urls=(
        "http://example.com"
        "http://educational.site.com"
        "http://badsite-violence.com"
        "http://adult-content.com"
        "http://malware.site.com"
        "http://safe-learning.edu"
        "http://gambling-casino.com"
        "http://social-media.com"
    )
    
    local start_time=$(date +%s.%3N)
    local request_count=0
    local blocked_count=0
    local response_times=()
    
    log_test "  Sending test requests for $TEST_DURATION seconds..."
    
    local end_time=$(($(date +%s) + TEST_DURATION))
    
    while [[ $(date +%s) -lt $end_time ]]; do
        for url in "${test_urls[@]}"; do
            local request_start=$(date +%s.%3N)
            
            # Send request through proxy
            if response=$(curl -s -x "http://127.0.0.1:8888" --connect-timeout 5 --max-time 5 "$url" 2>/dev/null); then
                local request_end=$(date +%s.%3N)
                local request_time=$(echo "($request_end - $request_start) * 1000" | bc)
                response_times+=("$request_time")
                
                # Check if blocked (look for PocketFence block page)
                if echo "$response" | grep -q "PocketFence.*Content Blocked"; then
                    ((blocked_count++))
                fi
            fi
            
            ((request_count++))
            
            if [[ $(date +%s) -ge $end_time ]]; then
                break
            fi
        done
    done
    
    # Clean shutdown
    kill $filter_pid 2>/dev/null || true
    wait $filter_pid 2>/dev/null || true
    
    local actual_duration=$(echo "$(date +%s.%3N) - $start_time" | bc)
    
    TEST_RESULTS[requests_per_second]=$(echo "scale=2; $request_count / $actual_duration" | bc)
    TEST_RESULTS[total_requests]=$request_count
    TEST_RESULTS[blocked_requests]=$blocked_count
    
    if [[ $request_count -gt 0 ]]; then
        TEST_RESULTS[block_rate]=$(echo "scale=1; $blocked_count * 100 / $request_count" | bc)
    else
        TEST_RESULTS[block_rate]="0"
    fi
    
    if [[ ${#response_times[@]} -gt 0 ]]; then
        local sum=0
        local min=${response_times[0]}
        local max=${response_times[0]}
        
        for time in "${response_times[@]}"; do
            sum=$(echo "$sum + $time" | bc)
            if (( $(echo "$time < $min" | bc -l) )); then
                min=$time
            fi
            if (( $(echo "$time > $max" | bc -l) )); then
                max=$time
            fi
        done
        
        TEST_RESULTS[avg_response_time]=$(echo "scale=2; $sum / ${#response_times[@]}" | bc)
        TEST_RESULTS[min_response_time]=$min
        TEST_RESULTS[max_response_time]=$max
    fi
    
    log_test "✅ Performance results:" "$GREEN"
    log_test "   Requests/sec: ${TEST_RESULTS[requests_per_second]}" "$GREEN"
    log_test "   Total requests: $request_count" "$GRAY"
    log_test "   Blocked: $blocked_count (${TEST_RESULTS[block_rate]}%)" "$YELLOW"
    if [[ -n "${TEST_RESULTS[avg_response_time]}" ]]; then
        log_test "   Avg response: ${TEST_RESULTS[avg_response_time]}ms" "$GRAY"
    fi
}

test_filter_accuracy() {
    log_test "🎯 Testing Blocking Accuracy..." "$CYAN"
    
    local executable="./PocketFence-Filter"
    [[ -f "./pocketfence-filter" ]] && executable="./pocketfence-filter"
    [[ -f "./filter" ]] && executable="./filter"
    
    # Start filter for accuracy testing
    $executable &
    local filter_pid=$!
    sleep 3
    
    # Test cases: URL, Should Block, Description
    local test_cases=(
        "http://education.com false Educational site"
        "http://violence-fighting.com true Violence content"
        "http://adult-explicit.com true Adult content"
        "http://malware-virus.com true Malware site"
        "http://school-learning.edu false School site"
        "http://gambling-casino.com true Gambling site"
        "http://safe-tutorial.com false Tutorial site"
        "http://cyberbullying-hate.com true Cyberbullying content"
    )
    
    local correct_blocks=0
    local correct_allows=0
    local false_positives=0
    local false_negatives=0
    
    for test_case in "${test_cases[@]}"; do
        read -r url should_block description <<< "$test_case"
        
        if response=$(curl -s -x "http://127.0.0.1:8888" --connect-timeout 5 --max-time 5 "$url" 2>/dev/null); then
            local is_blocked=false
            if echo "$response" | grep -q "PocketFence.*Content Blocked"; then
                is_blocked=true
            fi
            
            if [[ "$should_block" == "true" && "$is_blocked" == "true" ]]; then
                ((correct_blocks++))
                log_test "  ✅ $description: Correctly blocked" "$GREEN"
            elif [[ "$should_block" == "false" && "$is_blocked" == "false" ]]; then
                ((correct_allows++))
                log_test "  ✅ $description: Correctly allowed" "$GREEN"
            elif [[ "$should_block" == "true" && "$is_blocked" == "false" ]]; then
                ((false_negatives++))
                log_test "  ❌ $description: Should block but allowed" "$RED"
            else
                ((false_positives++))
                log_test "  ⚠️  $description: Should allow but blocked" "$YELLOW"
            fi
        else
            log_test "  ❓ $description: Request failed" "$GRAY"
        fi
    done
    
    # Clean shutdown
    kill $filter_pid 2>/dev/null || true
    wait $filter_pid 2>/dev/null || true
    
    local total_tests=${#test_cases[@]}
    local correct_results=$((correct_blocks + correct_allows))
    
    if [[ $total_tests -gt 0 ]]; then
        TEST_RESULTS[filter_accuracy]=$(echo "scale=1; $correct_results * 100 / $total_tests" | bc)
    else
        TEST_RESULTS[filter_accuracy]="0"
    fi
    
    TEST_RESULTS[correct_blocks]=$correct_blocks
    TEST_RESULTS[correct_allows]=$correct_allows
    TEST_RESULTS[false_positives]=$false_positives
    TEST_RESULTS[false_negatives]=$false_negatives
    
    log_test "✅ Accuracy: ${TEST_RESULTS[filter_accuracy]}% ($correct_results/$total_tests correct)" "$GREEN"
}

test_concurrent_load() {
    log_test "🔀 Testing Concurrent Load..." "$CYAN"
    
    local executable="./PocketFence-Filter"
    [[ -f "./pocketfence-filter" ]] && executable="./pocketfence-filter"
    [[ -f "./filter" ]] && executable="./filter"
    
    # Start filter for load testing
    $executable &
    local filter_pid=$!
    sleep 3
    
    log_test "  Starting $CONCURRENT_CONNECTIONS concurrent connections..."
    
    local start_time=$(date +%s.%3N)
    local pids=()
    local temp_dir="/tmp/pocketfence-load-test-$$"
    mkdir -p "$temp_dir"
    
    # Start concurrent requests
    for i in $(seq 1 $CONCURRENT_CONNECTIONS); do
        {
            if curl -s -x "http://127.0.0.1:8888" --connect-timeout 10 --max-time 10 "http://example.com" > "$temp_dir/result_$i" 2>/dev/null; then
                echo "success" > "$temp_dir/status_$i"
            else
                echo "failed" > "$temp_dir/status_$i"
            fi
        } &
        pids+=($!)
    done
    
    # Wait for all requests to complete
    for pid in "${pids[@]}"; do
        wait $pid 2>/dev/null || true
    done
    
    local load_test_duration=$(echo "$(date +%s.%3N) - $start_time" | bc)
    
    # Count results
    local successful_requests=0
    local failed_requests=0
    
    for i in $(seq 1 $CONCURRENT_CONNECTIONS); do
        if [[ -f "$temp_dir/status_$i" ]]; then
            if grep -q "success" "$temp_dir/status_$i"; then
                ((successful_requests++))
            else
                ((failed_requests++))
            fi
        else
            ((failed_requests++))
        fi
    done
    
    # Cleanup
    rm -rf "$temp_dir"
    kill $filter_pid 2>/dev/null || true
    wait $filter_pid 2>/dev/null || true
    
    TEST_RESULTS[concurrent_connections]=$CONCURRENT_CONNECTIONS
    TEST_RESULTS[load_test_duration]=$(echo "scale=2; $load_test_duration" | bc)
    TEST_RESULTS[successful_requests]=$successful_requests
    TEST_RESULTS[failed_requests]=$failed_requests
    
    if [[ $CONCURRENT_CONNECTIONS -gt 0 ]]; then
        TEST_RESULTS[concurrent_success_rate]=$(echo "scale=1; $successful_requests * 100 / $CONCURRENT_CONNECTIONS" | bc)
    else
        TEST_RESULTS[concurrent_success_rate]="0"
    fi
    
    log_test "✅ Concurrent load results:" "$GREEN"
    log_test "   Successful: $successful_requests/$CONCURRENT_CONNECTIONS (${TEST_RESULTS[concurrent_success_rate]}%)" "$GREEN"
    log_test "   Duration: ${TEST_RESULTS[load_test_duration]}s" "$GRAY"
}

generate_report() {
    log_test "📊 Generating Performance Report..." "$CYAN"
    
    local platform=$(uname -s)
    local arch=$(uname -m)
    
    cat > "$OUTPUT_PATH" << EOF
🚀 PocketFence Universal Filter Performance Report
Generated: $(date)
Platform: $platform $arch
Test Duration: $TEST_DURATION seconds
Concurrent Connections: $CONCURRENT_CONNECTIONS

📈 STARTUP PERFORMANCE:
$(if [[ -n "${TEST_RESULTS[avg_startup_time]}" ]]; then
    echo "  Average Startup Time: ${TEST_RESULTS[avg_startup_time]}ms"
    echo "  Minimum Startup Time: ${TEST_RESULTS[min_startup_time]}ms"
    echo "  Maximum Startup Time: ${TEST_RESULTS[max_startup_time]}ms"
else
    echo "  Startup tests failed"
fi)

🧠 MEMORY USAGE:
$(if [[ -n "${TEST_RESULTS[avg_memory_mb]}" ]]; then
    echo "  Average Memory: ${TEST_RESULTS[avg_memory_mb]}MB"
    echo "  Minimum Memory: ${TEST_RESULTS[min_memory_mb]}MB"
    echo "  Maximum Memory: ${TEST_RESULTS[max_memory_mb]}MB"
else
    echo "  Memory tests failed"
fi)

⚡ FILTERING PERFORMANCE:
$(if [[ -n "${TEST_RESULTS[requests_per_second]}" ]]; then
    echo "  Requests/Second: ${TEST_RESULTS[requests_per_second]}"
    echo "  Total Requests: ${TEST_RESULTS[total_requests]}"
    echo "  Blocked Requests: ${TEST_RESULTS[blocked_requests]} (${TEST_RESULTS[block_rate]}%)"
    [[ -n "${TEST_RESULTS[avg_response_time]}" ]] && echo "  Average Response Time: ${TEST_RESULTS[avg_response_time]}ms"
    [[ -n "${TEST_RESULTS[min_response_time]}" ]] && echo "  Min Response Time: ${TEST_RESULTS[min_response_time]}ms"
    [[ -n "${TEST_RESULTS[max_response_time]}" ]] && echo "  Max Response Time: ${TEST_RESULTS[max_response_time]}ms"
else
    echo "  Performance tests failed"
fi)

🎯 BLOCKING ACCURACY:
$(if [[ -n "${TEST_RESULTS[filter_accuracy]}" ]]; then
    echo "  Overall Accuracy: ${TEST_RESULTS[filter_accuracy]}%"
    echo "  Correct Blocks: ${TEST_RESULTS[correct_blocks]}"
    echo "  Correct Allows: ${TEST_RESULTS[correct_allows]}"
    echo "  False Positives: ${TEST_RESULTS[false_positives]}"
    echo "  False Negatives: ${TEST_RESULTS[false_negatives]}"
else
    echo "  Accuracy tests failed"
fi)

🔀 CONCURRENT LOAD:
$(if [[ -n "${TEST_RESULTS[concurrent_success_rate]}" ]]; then
    echo "  Connections Tested: ${TEST_RESULTS[concurrent_connections]}"
    echo "  Success Rate: ${TEST_RESULTS[concurrent_success_rate]}%"
    echo "  Successful Requests: ${TEST_RESULTS[successful_requests]}"
    echo "  Failed Requests: ${TEST_RESULTS[failed_requests]}"
    echo "  Load Test Duration: ${TEST_RESULTS[load_test_duration]}s"
else
    echo "  Load tests failed"
fi)

🏆 PERFORMANCE GRADE:
$(
    local score=0
    local grade="F"
    
    # Startup time score (under 2000ms = 15 points)
    if [[ -n "${TEST_RESULTS[avg_startup_time]}" ]] && (( $(echo "${TEST_RESULTS[avg_startup_time]} < 2000" | bc -l) )); then
        score=$((score + 15))
    fi
    
    # Memory score (under 50MB = 20 points)
    if [[ -n "${TEST_RESULTS[avg_memory_mb]}" ]] && (( $(echo "${TEST_RESULTS[avg_memory_mb]} < 50" | bc -l) )); then
        score=$((score + 20))
    fi
    
    # Performance score (over 10 req/s = 20 points)
    if [[ -n "${TEST_RESULTS[requests_per_second]}" ]] && (( $(echo "${TEST_RESULTS[requests_per_second]} > 10" | bc -l) )); then
        score=$((score + 20))
    fi
    
    # Accuracy score (over 80% = 25 points)
    if [[ -n "${TEST_RESULTS[filter_accuracy]}" ]] && (( $(echo "${TEST_RESULTS[filter_accuracy]} > 80" | bc -l) )); then
        score=$((score + 25))
    fi
    
    # Concurrent score (over 90% success = 20 points)
    if [[ -n "${TEST_RESULTS[concurrent_success_rate]}" ]] && (( $(echo "${TEST_RESULTS[concurrent_success_rate]} > 90" | bc -l) )); then
        score=$((score + 20))
    fi
    
    # Determine grade
    if [[ $score -ge 90 ]]; then
        grade="A+"
    elif [[ $score -ge 80 ]]; then
        grade="A"
    elif [[ $score -ge 70 ]]; then
        grade="B"
    elif [[ $score -ge 60 ]]; then
        grade="C"
    elif [[ $score -ge 50 ]]; then
        grade="D"
    fi
    
    echo "  Overall Score: $score/100 - Grade: $grade"
)

📝 TEST LOG:
$(printf '%s\n' "${TEST_LOG[@]}")
EOF

    log_test "✅ Report saved to: $OUTPUT_PATH" "$GREEN"
}

# Main execution
main() {
    echo -e "${GREEN}🧪 PocketFence Universal Filter Performance Test${NC}"
    echo -e "${YELLOW}Platform: $(uname -s) $(uname -m)${NC}"
    echo -e "${GRAY}Date: $(date)${NC}"
    echo ""
    
    log_test "Starting PocketFence Universal Filter performance tests..." "$YELLOW"
    log_test "Test parameters: Duration=$TEST_DURATION""s, Connections=$CONCURRENT_CONNECTIONS" "$GRAY"
    echo ""
    
    check_dependencies
    echo ""
    
    measure_filter_startup
    echo ""
    
    measure_filter_memory
    echo ""
    
    measure_filter_performance
    echo ""
    
    test_filter_accuracy
    echo ""
    
    test_concurrent_load
    echo ""
    
    generate_report
    
    echo ""
    echo -e "${GREEN}🎉 Performance testing completed!${NC}"
    echo -e "${YELLOW}📊 Results saved to: $OUTPUT_PATH${NC}"
    
    # Show summary
    [[ -n "${TEST_RESULTS[avg_startup_time]}" ]] && echo -e "${CYAN}⚡ Startup: ${TEST_RESULTS[avg_startup_time]}ms${NC}"
    [[ -n "${TEST_RESULTS[avg_memory_mb]}" ]] && echo -e "${CYAN}🧠 Memory: ${TEST_RESULTS[avg_memory_mb]}MB${NC}"
    [[ -n "${TEST_RESULTS[requests_per_second]}" ]] && echo -e "${CYAN}🚀 Performance: ${TEST_RESULTS[requests_per_second]} req/s${NC}"
    [[ -n "${TEST_RESULTS[filter_accuracy]}" ]] && echo -e "${CYAN}🎯 Accuracy: ${TEST_RESULTS[filter_accuracy]}%${NC}"
}

# Check if script is being sourced or executed
if [[ "${BASH_SOURCE[0]}" == "${0}" ]]; then
    main "$@"
fi