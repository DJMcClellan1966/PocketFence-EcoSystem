#!/usr/bin/env python3
"""
PocketFence Universal Filter - Python Performance Test
Cross-platform proxy filter performance validation
"""

import sys
import time
import subprocess
import threading
import requests
try:
    import psutil  # type: ignore[import]
    HAS_PSUTIL = True
except ImportError:
    HAS_PSUTIL = False
    print("Warning: psutil not installed. Memory monitoring will be limited.")
    print("Install with: pip install psutil")
import argparse
import json
import os
from datetime import datetime
from concurrent.futures import ThreadPoolExecutor
import statistics

class FilterPerformanceTest:
    def __init__(self, test_duration=30, concurrent_connections=50, output_path=None):
        self.test_duration = test_duration
        self.concurrent_connections = concurrent_connections
        self.output_path = output_path or f"filter-performance-python-{datetime.now().strftime('%Y-%m-%d_%H-%M-%S')}.txt"
        self.test_results = {}
        self.test_log = []
        self.filter_process = None
        
        # Test URLs for filtering
        self.test_urls = [
            "http://example.com",
            "http://educational.site.com", 
            "http://badsite-violence.com",
            "http://adult-content.com",
            "http://malware.site.com",
            "http://safe-learning.edu",
            "http://gambling-casino.com",
            "http://social-media.com"
        ]
        
        # Accuracy test cases: (URL, should_block, description)
        self.test_cases = [
            ("http://education.com", False, "Educational site"),
            ("http://violence-fighting.com", True, "Violence content"),
            ("http://adult-explicit.com", True, "Adult content"),
            ("http://malware-virus.com", True, "Malware site"),
            ("http://school-learning.edu", False, "School site"),
            ("http://gambling-casino.com", True, "Gambling site"),
            ("http://safe-tutorial.com", False, "Tutorial site"),
            ("http://cyberbullying-hate.com", True, "Cyberbullying content")
        ]

    def log_test(self, message, color=None):
        """Log test message with timestamp"""
        timestamp = datetime.now().strftime("%H:%M:%S.%f")[:-3]
        log_entry = f"[{timestamp}] {message}"
        
        # Color codes for terminal output
        colors = {
            'red': '\033[0;31m',
            'green': '\033[0;32m', 
            'yellow': '\033[1;33m',
            'blue': '\033[0;34m',
            'cyan': '\033[0;36m',
            'gray': '\033[0;37m',
            'reset': '\033[0m'
        }
        
        if color and color in colors:
            print(f"{colors[color]}{log_entry}{colors['reset']}")
        else:
            print(log_entry)
            
        self.test_log.append(log_entry)

    def check_dependencies(self):
        """Check if required dependencies are available"""
        self.log_test("🔧 Checking dependencies...", "cyan")
        
        try:
            import requests
            # psutil is optional, already checked at module level
            self.log_test("✅ All required dependencies available", "green")
            if not HAS_PSUTIL:
                self.log_test("ℹ️  psutil not installed - memory monitoring limited", "yellow")
            return True
        except ImportError as e:
            self.log_test(f"❌ Missing required dependency: {e}", "red")
            return False

    def find_filter_executable(self):
        """Find the PocketFence-Filter executable"""
        possible_names = [
            "PocketFence-Filter.exe",
            "PocketFence-Filter", 
            "pocketfence-filter.exe",
            "pocketfence-filter",
            "filter.exe",
            "filter"
        ]
        
        for name in possible_names:
            if os.path.exists(name):
                return name
                
        # Check in builds directory
        for root, dirs, files in os.walk("."):
            for name in possible_names:
                if name in files:
                    return os.path.join(root, name)
                    
        return None

    def start_filter(self):
        """Start the PocketFence-Filter process"""
        executable = self.find_filter_executable()
        if not executable:
            raise FileNotFoundError("PocketFence-Filter executable not found")
            
        self.filter_process = subprocess.Popen(
            [executable],
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL
        )
        
        # Wait for proxy to start listening
        max_wait = 10
        start_time = time.time()
        
        while time.time() - start_time < max_wait:
            try:
                response = requests.get("http://127.0.0.1:8888", timeout=1)
                break
            except:
                time.sleep(0.1)
        else:
            raise RuntimeError("Filter failed to start listening on port 8888")

    def stop_filter(self):
        """Stop the PocketFence-Filter process"""
        if self.filter_process:
            try:
                self.filter_process.terminate()
                self.filter_process.wait(timeout=5)
            except subprocess.TimeoutExpired:
                self.filter_process.kill()
                self.filter_process.wait()
            finally:
                self.filter_process = None

    def measure_startup_performance(self):
        """Test filter startup performance"""
        self.log_test("🚀 Testing Filter Startup Performance...", "cyan")
        
        executable = self.find_filter_executable()
        if not executable:
            self.log_test("❌ Filter executable not found", "red")
            return
            
        startup_times = []
        
        for i in range(1, 6):
            self.log_test(f"  Startup test {i}/5...")
            
            start_time = time.time()
            
            # Start filter process
            process = subprocess.Popen(
                [executable],
                stdout=subprocess.DEVNULL,
                stderr=subprocess.DEVNULL
            )
            
            # Wait for proxy to be ready
            ready_time = None
            timeout = time.time() + 10
            
            while time.time() < timeout:
                try:
                    response = requests.get("http://127.0.0.1:8888", timeout=0.1)
                    ready_time = time.time()
                    break
                except:
                    time.sleep(0.1)
            
            if ready_time:
                startup_time = (ready_time - start_time) * 1000
                startup_times.append(startup_time)
                self.log_test(f"    Startup time: {startup_time:.2f}ms", "green")
            else:
                self.log_test("    Startup timeout!", "red")
            
            # Clean shutdown
            try:
                process.terminate()
                process.wait(timeout=5)
            except subprocess.TimeoutExpired:
                process.kill()
                process.wait()
            
            time.sleep(1)
        
        if startup_times:
            self.test_results['avg_startup_time'] = statistics.mean(startup_times)
            self.test_results['min_startup_time'] = min(startup_times)
            self.test_results['max_startup_time'] = max(startup_times)
            
            self.log_test(f"✅ Average startup time: {self.test_results['avg_startup_time']:.2f}ms", "green")
            self.log_test(f"   Min: {self.test_results['min_startup_time']:.2f}ms, Max: {self.test_results['max_startup_time']:.2f}ms", "gray")

    def measure_memory_usage(self):
        """Test memory usage"""
        self.log_test("🧠 Testing Memory Usage...", "cyan")
        
        try:
            self.start_filter()
            time.sleep(3)
            
            memory_readings = []
            
            for i in range(1, 11):
                try:
                    if HAS_PSUTIL:
                        process = psutil.Process(self.filter_process.pid)
                        memory_mb = process.memory_info().rss / 1024 / 1024
                        memory_readings.append(memory_mb)
                        self.log_test(f"  Memory reading {i}: {memory_mb:.2f}MB")
                    else:
                        self.log_test(f"  Memory reading {i}: psutil not available", "yellow")
                    time.sleep(1)
                except (psutil.NoSuchProcess if HAS_PSUTIL else OSError):
                    self.log_test("  Process ended unexpectedly", "red")
                    break
            
            if memory_readings:
                self.test_results['avg_memory_mb'] = statistics.mean(memory_readings)
                self.test_results['min_memory_mb'] = min(memory_readings)
                self.test_results['max_memory_mb'] = max(memory_readings)
                
                self.log_test(f"✅ Average memory: {self.test_results['avg_memory_mb']:.2f}MB", "green")
                self.log_test(f"   Min: {self.test_results['min_memory_mb']:.2f}MB, Max: {self.test_results['max_memory_mb']:.2f}MB", "gray")
                
        finally:
            self.stop_filter()

    def measure_filtering_performance(self):
        """Test filtering performance"""
        self.log_test("⚡ Testing Filtering Performance...", "cyan")
        
        try:
            self.start_filter()
            time.sleep(3)
            
            start_time = time.time()
            request_count = 0
            blocked_count = 0
            response_times = []
            
            self.log_test(f"  Sending test requests for {self.test_duration} seconds...")
            
            end_time = time.time() + self.test_duration
            
            while time.time() < end_time:
                for url in self.test_urls:
                    try:
                        request_start = time.time()
                        
                        response = requests.get(
                            url,
                            proxies={"http": "http://127.0.0.1:8888"},
                            timeout=5
                        )
                        
                        request_time = (time.time() - request_start) * 1000
                        response_times.append(request_time)
                        request_count += 1
                        
                        # Check if blocked
                        if "PocketFence" in response.text and "Content Blocked" in response.text:
                            blocked_count += 1
                            
                    except requests.RequestException:
                        request_count += 1  # Count failed requests
                    
                    if time.time() >= end_time:
                        break
            
            actual_duration = time.time() - start_time
            
            self.test_results['requests_per_second'] = request_count / actual_duration
            self.test_results['total_requests'] = request_count
            self.test_results['blocked_requests'] = blocked_count
            self.test_results['block_rate'] = (blocked_count / request_count) * 100 if request_count > 0 else 0
            
            if response_times:
                self.test_results['avg_response_time'] = statistics.mean(response_times)
                self.test_results['min_response_time'] = min(response_times)
                self.test_results['max_response_time'] = max(response_times)
            
            self.log_test("✅ Performance results:", "green")
            self.log_test(f"   Requests/sec: {self.test_results['requests_per_second']:.2f}", "green")
            self.log_test(f"   Total requests: {request_count}", "gray")
            self.log_test(f"   Blocked: {blocked_count} ({self.test_results['block_rate']:.1f}%)", "yellow")
            if response_times:
                self.log_test(f"   Avg response: {self.test_results['avg_response_time']:.2f}ms", "gray")
                
        finally:
            self.stop_filter()

    def test_blocking_accuracy(self):
        """Test blocking accuracy"""
        self.log_test("🎯 Testing Blocking Accuracy...", "cyan")
        
        try:
            self.start_filter()
            time.sleep(3)
            
            correct_blocks = 0
            correct_allows = 0
            false_positives = 0
            false_negatives = 0
            
            for url, should_block, description in self.test_cases:
                try:
                    response = requests.get(
                        url,
                        proxies={"http": "http://127.0.0.1:8888"},
                        timeout=5
                    )
                    
                    is_blocked = "PocketFence" in response.text and "Content Blocked" in response.text
                    
                    if should_block and is_blocked:
                        correct_blocks += 1
                        self.log_test(f"  ✅ {description}: Correctly blocked", "green")
                    elif not should_block and not is_blocked:
                        correct_allows += 1
                        self.log_test(f"  ✅ {description}: Correctly allowed", "green")
                    elif should_block and not is_blocked:
                        false_negatives += 1
                        self.log_test(f"  ❌ {description}: Should block but allowed", "red")
                    else:
                        false_positives += 1
                        self.log_test(f"  ⚠️  {description}: Should allow but blocked", "yellow")
                        
                except requests.RequestException:
                    self.log_test(f"  ❓ {description}: Request failed", "gray")
            
            total_tests = len(self.test_cases)
            correct_results = correct_blocks + correct_allows
            
            self.test_results['filter_accuracy'] = (correct_results / total_tests) * 100 if total_tests > 0 else 0
            self.test_results['correct_blocks'] = correct_blocks
            self.test_results['correct_allows'] = correct_allows
            self.test_results['false_positives'] = false_positives
            self.test_results['false_negatives'] = false_negatives
            
            self.log_test(f"✅ Accuracy: {self.test_results['filter_accuracy']:.1f}% ({correct_results}/{total_tests} correct)", "green")
            
        finally:
            self.stop_filter()

    def test_concurrent_load(self):
        """Test concurrent load handling"""
        self.log_test("🔀 Testing Concurrent Load...", "cyan")
        
        try:
            self.start_filter()
            time.sleep(3)
            
            self.log_test(f"  Starting {self.concurrent_connections} concurrent connections...")
            
            def make_request():
                try:
                    response = requests.get(
                        "http://example.com",
                        proxies={"http": "http://127.0.0.1:8888"},
                        timeout=10
                    )
                    return True
                except requests.RequestException:
                    return False
            
            start_time = time.time()
            
            with ThreadPoolExecutor(max_workers=self.concurrent_connections) as executor:
                futures = [executor.submit(make_request) for _ in range(self.concurrent_connections)]
                results = [future.result() for future in futures]
            
            load_test_duration = time.time() - start_time
            successful_requests = sum(results)
            failed_requests = len(results) - successful_requests
            
            self.test_results['concurrent_connections'] = self.concurrent_connections
            self.test_results['load_test_duration'] = load_test_duration
            self.test_results['successful_requests'] = successful_requests
            self.test_results['failed_requests'] = failed_requests
            self.test_results['concurrent_success_rate'] = (successful_requests / self.concurrent_connections) * 100
            
            self.log_test("✅ Concurrent load results:", "green")
            self.log_test(f"   Successful: {successful_requests}/{self.concurrent_connections} ({self.test_results['concurrent_success_rate']:.1f}%)", "green")
            self.log_test(f"   Duration: {load_test_duration:.2f}s", "gray")
            
        finally:
            self.stop_filter()

    def calculate_performance_grade(self):
        """Calculate overall performance grade"""
        score = 0
        
        # Startup time score (under 2000ms = 15 points)
        if 'avg_startup_time' in self.test_results and self.test_results['avg_startup_time'] < 2000:
            score += 15
            
        # Memory score (under 50MB = 20 points)
        if 'avg_memory_mb' in self.test_results and self.test_results['avg_memory_mb'] < 50:
            score += 20
            
        # Performance score (over 10 req/s = 20 points)
        if 'requests_per_second' in self.test_results and self.test_results['requests_per_second'] > 10:
            score += 20
            
        # Accuracy score (over 80% = 25 points)
        if 'filter_accuracy' in self.test_results and self.test_results['filter_accuracy'] > 80:
            score += 25
            
        # Concurrent score (over 90% success = 20 points)
        if 'concurrent_success_rate' in self.test_results and self.test_results['concurrent_success_rate'] > 90:
            score += 20
        
        if score >= 90:
            grade = "A+"
        elif score >= 80:
            grade = "A"
        elif score >= 70:
            grade = "B"
        elif score >= 60:
            grade = "C"
        elif score >= 50:
            grade = "D"
        else:
            grade = "F"
            
        return score, grade

    def generate_report(self):
        """Generate performance report"""
        self.log_test("📊 Generating Performance Report...", "cyan")
        
        platform = f"{sys.platform} {os.uname().machine}" if hasattr(os, 'uname') else sys.platform
        score, grade = self.calculate_performance_grade()
        
        report = f"""🚀 PocketFence Universal Filter Performance Report
Generated: {datetime.now()}
Platform: {platform}
Test Duration: {self.test_duration} seconds
Concurrent Connections: {self.concurrent_connections}

📈 STARTUP PERFORMANCE:
{f'''  Average Startup Time: {self.test_results.get('avg_startup_time', 'N/A'):.2f}ms
  Minimum Startup Time: {self.test_results.get('min_startup_time', 'N/A'):.2f}ms
  Maximum Startup Time: {self.test_results.get('max_startup_time', 'N/A'):.2f}ms''' if 'avg_startup_time' in self.test_results else '  Startup tests failed'}

🧠 MEMORY USAGE:
{f'''  Average Memory: {self.test_results.get('avg_memory_mb', 'N/A'):.2f}MB
  Minimum Memory: {self.test_results.get('min_memory_mb', 'N/A'):.2f}MB
  Maximum Memory: {self.test_results.get('max_memory_mb', 'N/A'):.2f}MB''' if 'avg_memory_mb' in self.test_results else '  Memory tests failed'}

⚡ FILTERING PERFORMANCE:
{f'''  Requests/Second: {self.test_results.get('requests_per_second', 'N/A'):.2f}
  Total Requests: {self.test_results.get('total_requests', 'N/A')}
  Blocked Requests: {self.test_results.get('blocked_requests', 'N/A')} ({self.test_results.get('block_rate', 'N/A'):.1f}%)
  Average Response Time: {self.test_results.get('avg_response_time', 'N/A'):.2f}ms
  Min Response Time: {self.test_results.get('min_response_time', 'N/A'):.2f}ms
  Max Response Time: {self.test_results.get('max_response_time', 'N/A'):.2f}ms''' if 'requests_per_second' in self.test_results else '  Performance tests failed'}

🎯 BLOCKING ACCURACY:
{f'''  Overall Accuracy: {self.test_results.get('filter_accuracy', 'N/A'):.1f}%
  Correct Blocks: {self.test_results.get('correct_blocks', 'N/A')}
  Correct Allows: {self.test_results.get('correct_allows', 'N/A')}
  False Positives: {self.test_results.get('false_positives', 'N/A')}
  False Negatives: {self.test_results.get('false_negatives', 'N/A')}''' if 'filter_accuracy' in self.test_results else '  Accuracy tests failed'}

🔀 CONCURRENT LOAD:
{f'''  Connections Tested: {self.test_results.get('concurrent_connections', 'N/A')}
  Success Rate: {self.test_results.get('concurrent_success_rate', 'N/A'):.1f}%
  Successful Requests: {self.test_results.get('successful_requests', 'N/A')}
  Failed Requests: {self.test_results.get('failed_requests', 'N/A')}
  Load Test Duration: {self.test_results.get('load_test_duration', 'N/A'):.2f}s''' if 'concurrent_success_rate' in self.test_results else '  Load tests failed'}

🏆 PERFORMANCE GRADE:
  Overall Score: {score}/100 - Grade: {grade}

📝 TEST LOG:
{chr(10).join(self.test_log)}
"""
        
        with open(self.output_path, 'w', encoding='utf-8') as f:
            f.write(report)
            
        self.log_test(f"✅ Report saved to: {self.output_path}", "green")
        return report

    def run_all_tests(self):
        """Run all performance tests"""
        print("🧪 PocketFence Universal Filter Performance Test")
        print(f"Platform: {sys.platform}")
        print(f"Date: {datetime.now()}")
        print()
        
        self.log_test("Starting PocketFence Universal Filter performance tests...", "yellow")
        self.log_test(f"Test parameters: Duration={self.test_duration}s, Connections={self.concurrent_connections}", "gray")
        print()
        
        if not self.check_dependencies():
            return False
        print()
        
        try:
            self.measure_startup_performance()
            print()
            
            self.measure_memory_usage()
            print()
            
            self.measure_filtering_performance()
            print()
            
            self.test_blocking_accuracy()
            print()
            
            self.test_concurrent_load()
            print()
            
            self.generate_report()
            
            print()
            print("🎉 Performance testing completed!")
            print(f"📊 Results saved to: {self.output_path}")
            
            # Show summary
            if 'avg_startup_time' in self.test_results:
                print(f"⚡ Startup: {self.test_results['avg_startup_time']:.2f}ms")
            if 'avg_memory_mb' in self.test_results:
                print(f"🧠 Memory: {self.test_results['avg_memory_mb']:.2f}MB")
            if 'requests_per_second' in self.test_results:
                print(f"🚀 Performance: {self.test_results['requests_per_second']:.2f} req/s")
            if 'filter_accuracy' in self.test_results:
                print(f"🎯 Accuracy: {self.test_results['filter_accuracy']:.1f}%")
                
            return True
            
        except Exception as e:
            self.log_test(f"❌ Test execution failed: {e}", "red")
            return False


def main():
    parser = argparse.ArgumentParser(description="PocketFence Universal Filter Performance Test")
    parser.add_argument("--duration", type=int, default=30, help="Test duration in seconds")
    parser.add_argument("--connections", type=int, default=50, help="Concurrent connections for load test")
    parser.add_argument("--output", type=str, help="Output file path")
    
    args = parser.parse_args()
    
    tester = FilterPerformanceTest(
        test_duration=args.duration,
        concurrent_connections=args.connections,
        output_path=args.output
    )
    
    success = tester.run_all_tests()
    sys.exit(0 if success else 1)


if __name__ == "__main__":
    main()