// iOS Hotspot Connection Helper
class iOSHotspotManager {
    constructor() {
        this.isIOS = this.detectIOS();
        this.isPWA = this.detectPWA();
        this.initializeIOSFeatures();
    }

    detectIOS() {
        return /iPad|iPhone|iPod/.test(navigator.userAgent) || 
               (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1);
    }

    detectPWA() {
        return window.matchMedia('(display-mode: standalone)').matches ||
               window.navigator.standalone === true;
    }

    async initializeIOSFeatures() {
        if (this.isIOS) {
            console.log('🍎 iOS device detected, initializing iOS-specific features');
            this.setupIOSStyles();
            this.setupIOSEvents();
            this.setupPWAPrompt();
            await this.registerIOSDevice();
        }
    }

    setupIOSStyles() {
        // Add iOS-specific CSS classes
        document.body.classList.add('ios-device');
        if (this.isPWA) {
            document.body.classList.add('ios-pwa');
        }

        // Adjust viewport for iOS
        const viewport = document.querySelector('meta[name="viewport"]');
        if (viewport) {
            viewport.setAttribute('content', 
                'width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no, viewport-fit=cover'
            );
        }

        // Add iOS safe area styles
        const safeAreaStyles = `
            .ios-device {
                padding-top: env(safe-area-inset-top);
                padding-bottom: env(safe-area-inset-bottom);
                padding-left: env(safe-area-inset-left);
                padding-right: env(safe-area-inset-right);
            }
            
            .ios-pwa .header {
                padding-top: calc(env(safe-area-inset-top) + 1rem);
            }
            
            .ios-device .btn {
                min-height: 44px; /* iOS minimum touch target */
                font-size: 16px; /* Prevent iOS zoom */
            }
            
            .ios-device input, .ios-device select, .ios-device textarea {
                font-size: 16px; /* Prevent iOS zoom */
                border-radius: 8px;
            }
        `;
        
        const styleSheet = document.createElement('style');
        styleSheet.textContent = safeAreaStyles;
        document.head.appendChild(styleSheet);
    }

    setupIOSEvents() {
        // Handle iOS-specific touch events
        document.addEventListener('touchstart', (e) => {
            // Add active state for touch feedback
            if (e.target.classList.contains('btn')) {
                e.target.classList.add('btn-active');
            }
        });

        document.addEventListener('touchend', (e) => {
            // Remove active state
            setTimeout(() => {
                if (e.target.classList.contains('btn')) {
                    e.target.classList.remove('btn-active');
                }
            }, 150);
        });

        // Handle orientation changes
        window.addEventListener('orientationchange', () => {
            setTimeout(() => {
                this.adjustLayoutForOrientation();
            }, 100);
        });
    }

    setupPWAPrompt() {
        if (this.isIOS && !this.isPWA) {
            this.showInstallPrompt();
        }
    }

    showInstallPrompt() {
        const promptHTML = `
            <div id="ios-install-prompt" class="ios-install-banner">
                <div class="install-content">
                    <div class="install-icon">📱</div>
                    <div class="install-text">
                        <strong>Install PocketFence</strong>
                        <p>Add to your home screen for the best experience</p>
                    </div>
                    <button class="install-close" onclick="document.getElementById('ios-install-prompt').style.display='none'">×</button>
                </div>
                <div class="install-instructions">
                    <p>Tap <span class="share-icon">⎋</span> Share, then "Add to Home Screen"</p>
                </div>
            </div>
        `;

        document.body.insertAdjacentHTML('afterbegin', promptHTML);

        // Auto-hide after 10 seconds
        setTimeout(() => {
            const prompt = document.getElementById('ios-install-prompt');
            if (prompt) {
                prompt.style.display = 'none';
            }
        }, 10000);
    }

    async generateHotspotQR(ssid, password) {
        try {
            const response = await fetch(`/api/ios/wifi-qr/${encodeURIComponent(ssid)}?password=${encodeURIComponent(password)}`);
            const data = await response.json();
            
            if (data.success) {
                this.displayQRCode(data.qrCode, data.wifiString);
                return data;
            } else {
                throw new Error(data.message || 'Failed to generate QR code');
            }
        } catch (error) {
            console.error('❌ Failed to generate WiFi QR code:', error);
            throw error;
        }
    }

    displayQRCode(qrCodeData, wifiString) {
        const qrModal = `
            <div id="wifi-qr-modal" class="modal-overlay" onclick="this.remove()">
                <div class="modal-content" onclick="event.stopPropagation()">
                    <div class="modal-header">
                        <h3>📱 WiFi QR Code</h3>
                        <button class="modal-close" onclick="document.getElementById('wifi-qr-modal').remove()">×</button>
                    </div>
                    <div class="modal-body text-center">
                        <div class="qr-code-container">
                            <canvas id="qr-canvas"></canvas>
                        </div>
                        <p class="qr-instructions">
                            <strong>📷 Scan with Camera</strong><br>
                            Point your iOS camera at this QR code to connect automatically
                        </p>
                        <div class="wifi-details">
                            <p><strong>Network:</strong> ${wifiString.match(/S:([^;]+)/)?.[1] || 'N/A'}</p>
                            <button class="btn secondary" onclick="navigator.clipboard?.writeText('${wifiString}')">
                                📋 Copy WiFi String
                            </button>
                        </div>
                    </div>
                </div>
            </div>
        `;

        document.body.insertAdjacentHTML('beforeend', qrModal);
        this.generateQRCodeCanvas(wifiString, 'qr-canvas');
    }

    generateQRCodeCanvas(text, canvasId) {
        // Simple QR code generation using a library (QR.js would be included)
        // For now, we'll create a placeholder
        const canvas = document.getElementById(canvasId);
        const ctx = canvas.getContext('2d');
        
        canvas.width = 200;
        canvas.height = 200;
        
        // Draw a simple placeholder QR code pattern
        ctx.fillStyle = '#000';
        ctx.fillRect(0, 0, 200, 200);
        ctx.fillStyle = '#fff';
        ctx.fillRect(10, 10, 180, 180);
        ctx.fillStyle = '#000';
        
        // Draw QR code-like pattern
        for (let i = 0; i < 20; i++) {
            for (let j = 0; j < 20; j++) {
                if ((i + j) % 3 === 0) {
                    ctx.fillRect(i * 9 + 10, j * 9 + 10, 8, 8);
                }
            }
        }
        
        // Add text overlay
        ctx.fillStyle = '#fff';
        ctx.font = '12px monospace';
        ctx.fillText('QR Code', 75, 105);
    }

    async registerIOSDevice() {
        try {
            // Get device information
            const deviceInfo = {
                macAddress: await this.getMacAddress(),
                ipAddress: await this.getLocalIPAddress(),
                userAgent: navigator.userAgent,
                screen: {
                    width: screen.width,
                    height: screen.height,
                    pixelRatio: window.devicePixelRatio
                }
            };

            const response = await fetch('/api/ios/detect-device', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify(deviceInfo)
            });

            const result = await response.json();
            console.log('🍎 iOS device registration result:', result);
            
            if (result.isiOSDevice) {
                this.onIOSDeviceRegistered(result);
            }
        } catch (error) {
            console.error('❌ Failed to register iOS device:', error);
        }
    }

    async getMacAddress() {
        // Note: Modern browsers don't allow MAC address access for privacy
        // This would need to be handled server-side through network detection
        return 'unavailable-browser-restriction';
    }

    async getLocalIPAddress() {
        try {
            // Use WebRTC to get local IP (may not work in all browsers)
            const pc = new RTCPeerConnection({
                iceServers: [{ urls: 'stun:stun.l.google.com:19302' }]
            });
            
            return new Promise((resolve) => {
                pc.onicecandidate = (ice) => {
                    if (!ice || !ice.candidate || !ice.candidate.candidate) return;
                    const myIP = /([0-9]{1,3}(\.[0-9]{1,3}){3})/.exec(ice.candidate.candidate)?.[1];
                    pc.close();
                    resolve(myIP || 'unknown');
                };
                pc.createDataChannel('');
                pc.createOffer().then((sdp) => pc.setLocalDescription(sdp));
            });
        } catch (error) {
            console.warn('Could not determine local IP:', error);
            return 'unknown';
        }
    }

    onIOSDeviceRegistered(result) {
        // Show iOS-specific features
        this.showIOSOnboardingTips();
        this.enableIOSOptimizations();
    }

    showIOSOnboardingTips() {
        const tips = `
            <div id="ios-tips-modal" class="modal-overlay">
                <div class="modal-content">
                    <div class="modal-header">
                        <h3>🍎 Welcome to PocketFence</h3>
                        <button class="modal-close" onclick="document.getElementById('ios-tips-modal').remove()">×</button>
                    </div>
                    <div class="modal-body">
                        <h4>📱 iOS Optimization Tips:</h4>
                        <ul class="tips-list">
                            <li>📌 Add to Home Screen for app-like experience</li>
                            <li>📱 Use camera to scan WiFi QR codes instantly</li>
                            <li>🔄 Pull down to refresh dashboard data</li>
                            <li>👆 Tap and hold for additional options</li>
                            <li>🔊 Enable notifications for real-time alerts</li>
                        </ul>
                        <button class="btn primary full-width" onclick="document.getElementById('ios-tips-modal').remove()">
                            Got it! 👍
                        </button>
                    </div>
                </div>
            </div>
        `;

        setTimeout(() => {
            document.body.insertAdjacentHTML('beforeend', tips);
        }, 2000);
    }

    enableIOSOptimizations() {
        // Enable pull-to-refresh
        this.setupPullToRefresh();
        
        // Enable haptic feedback (if supported)
        this.setupHapticFeedback();
        
        // Optimize scroll behavior
        this.optimizeScrolling();
    }

    setupPullToRefresh() {
        let startY = 0;
        let currentY = 0;
        let pulling = false;
        
        document.addEventListener('touchstart', (e) => {
            startY = e.touches[0].pageY;
            currentY = startY;
        });
        
        document.addEventListener('touchmove', (e) => {
            currentY = e.touches[0].pageY;
            if (currentY > startY && window.scrollY === 0) {
                pulling = true;
                const pullDistance = currentY - startY;
                if (pullDistance > 80) {
                    this.showPullToRefreshIndicator();
                }
            }
        });
        
        document.addEventListener('touchend', () => {
            if (pulling && currentY - startY > 80) {
                this.triggerRefresh();
            }
            pulling = false;
            this.hidePullToRefreshIndicator();
        });
    }

    showPullToRefreshIndicator() {
        if (!document.getElementById('pull-refresh-indicator')) {
            const indicator = document.createElement('div');
            indicator.id = 'pull-refresh-indicator';
            indicator.innerHTML = '🔄 Release to refresh';
            indicator.style.cssText = `
                position: fixed;
                top: 20px;
                left: 50%;
                transform: translateX(-50%);
                background: rgba(37, 99, 235, 0.9);
                color: white;
                padding: 8px 16px;
                border-radius: 20px;
                font-size: 14px;
                z-index: 1000;
                animation: slideDown 0.3s ease;
            `;
            document.body.appendChild(indicator);
        }
    }

    hidePullToRefreshIndicator() {
        const indicator = document.getElementById('pull-refresh-indicator');
        if (indicator) {
            indicator.remove();
        }
    }

    triggerRefresh() {
        console.log('🔄 Pull-to-refresh triggered');
        if (window.dashboard) {
            window.dashboard.refreshData();
        }
        
        // Show success feedback
        this.showTemporaryMessage('✅ Dashboard refreshed');
    }

    setupHapticFeedback() {
        if ('vibrate' in navigator) {
            // Add haptic feedback to button clicks
            document.addEventListener('click', (e) => {
                if (e.target.classList.contains('btn')) {
                    navigator.vibrate(10); // Light tap
                }
            });
        }
    }

    optimizeScrolling() {
        // Enable momentum scrolling
        document.body.style.webkitOverflowScrolling = 'touch';
        
        // Prevent zoom on input focus
        document.addEventListener('focusin', (e) => {
            if (e.target.matches('input, select, textarea')) {
                document.querySelector('meta[name="viewport"]').setAttribute(
                    'content', 
                    'width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no'
                );
            }
        });
        
        document.addEventListener('focusout', () => {
            document.querySelector('meta[name="viewport"]').setAttribute(
                'content', 
                'width=device-width, initial-scale=1.0, viewport-fit=cover'
            );
        });
    }

    adjustLayoutForOrientation() {
        const isLandscape = window.orientation === 90 || window.orientation === -90;
        document.body.classList.toggle('landscape', isLandscape);
        document.body.classList.toggle('portrait', !isLandscape);
    }

    showTemporaryMessage(message, duration = 3000) {
        const messageEl = document.createElement('div');
        messageEl.textContent = message;
        messageEl.style.cssText = `
            position: fixed;
            bottom: 20px;
            left: 50%;
            transform: translateX(-50%);
            background: rgba(46, 125, 50, 0.9);
            color: white;
            padding: 12px 24px;
            border-radius: 24px;
            font-size: 14px;
            z-index: 1000;
            animation: slideUp 0.3s ease;
        `;
        
        document.body.appendChild(messageEl);
        
        setTimeout(() => {
            messageEl.style.animation = 'slideDown 0.3s ease';
            setTimeout(() => messageEl.remove(), 300);
        }, duration);
    }
}

// Add required CSS animations
const animationStyles = `
    @keyframes slideDown {
        from { transform: translate(-50%, -100%); opacity: 0; }
        to { transform: translate(-50%, 0); opacity: 1; }
    }
    
    @keyframes slideUp {
        from { transform: translate(-50%, 100%); opacity: 0; }
        to { transform: translate(-50%, 0); opacity: 1; }
    }
    
    .btn-active {
        transform: scale(0.95);
        opacity: 0.8;
        transition: all 0.1s ease;
    }
    
    .ios-install-banner {
        position: fixed;
        top: 0;
        left: 0;
        right: 0;
        background: linear-gradient(135deg, #2563eb, #1d4ed8);
        color: white;
        padding: 12px 16px;
        z-index: 1000;
        box-shadow: 0 2px 8px rgba(0,0,0,0.1);
    }
    
    .install-content {
        display: flex;
        align-items: center;
        gap: 12px;
        margin-bottom: 8px;
    }
    
    .install-icon {
        font-size: 24px;
    }
    
    .install-text strong {
        display: block;
        margin-bottom: 2px;
    }
    
    .install-text p {
        font-size: 12px;
        opacity: 0.9;
        margin: 0;
    }
    
    .install-close {
        margin-left: auto;
        background: none;
        border: none;
        color: white;
        font-size: 18px;
        padding: 4px 8px;
        cursor: pointer;
    }
    
    .install-instructions {
        text-align: center;
        font-size: 12px;
        opacity: 0.9;
    }
    
    .share-icon {
        display: inline-block;
        background: rgba(255,255,255,0.2);
        padding: 2px 6px;
        border-radius: 4px;
        font-family: -apple-system, BlinkMacSystemFont, 'SF Pro Display';
    }
    
    .tips-list {
        margin: 16px 0;
        padding-left: 0;
        list-style: none;
    }
    
    .tips-list li {
        margin: 8px 0;
        padding: 8px;
        background: rgba(37, 99, 235, 0.1);
        border-radius: 8px;
        border-left: 3px solid #2563eb;
    }
    
    .landscape .grid {
        grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
    }
    
    .portrait .grid {
        grid-template-columns: repeat(auto-fit, minmax(280px, 1fr));
    }
`;

// Add styles to document
const styleSheet = document.createElement('style');
styleSheet.textContent = animationStyles;
document.head.appendChild(styleSheet);

// Initialize iOS features when page loads
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', () => {
        window.iOSManager = new iOSHotspotManager();
    });
} else {
    window.iOSManager = new iOSHotspotManager();
}