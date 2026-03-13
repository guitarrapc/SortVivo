'use strict';
// Video recording for the sort visualization content area.
//
// Captures each sort-card (header + canvas + stats) by compositing them onto
// an offscreen canvas, then encodes the result into a WebM via MediaRecorder.
//
// Capture target: .visualization-content (sort cards only, no control bar / seek bar)
//
// Resolution strategy:
//   The preset (1080/720/480) defines a "quality tier" — the target pixel count
//   for the LONGER side of the output video:
//     1080p → 1920 px,  720p → 1280 px,  480p → 854 px
//   The scale factor is: max(target / longerDisplaySide, devicePixelRatio).
//   This ensures:
//     - Small displays: upscaled to reach the target quality tier
//     - HiDPI displays: video matches the source canvas's native DPR resolution
//       (no downsample → no blur)
//
// Flow:
//   1. startRecording(selector, fps, targetHeight) — begins periodic sort-card compositing.
//   2. stopRecording(filename)                     — stops and triggers a .webm download.

window.videoRecorder = {
    _mediaRecorder: null,
    _chunks: [],
    _offscreen: null,
    _stream: null,
    _recording: false,
    _captureInterval: null,

    /**
     * Start recording the specified DOM element.
     * The element should contain .sort-card elements with canvas visualizations.
     * @param {string} selector     - CSS selector for the content area
     * @param {number} fps          - Frames per second (default: 30)
     * @param {number} targetHeight - Output video height in pixels (1080, 720, 480; default: 720)
     * @returns {boolean} true if recording started successfully
     */
    startRecording: function (selector, fps, targetHeight) {
        if (this._recording) return false;

        const target = document.querySelector(selector);
        if (!target) {
            console.error('[videoRecorder] Element not found:', selector);
            return false;
        }

        fps = fps || 30;
        targetHeight = targetHeight || 720;

        const canvases = target.querySelectorAll('canvas');
        if (canvases.length === 0) {
            console.error('[videoRecorder] No canvas elements found in:', selector);
            return false;
        }

        // Resolution presets: target pixel count for the LONGER side of the video.
        // This ensures consistent quality regardless of portrait/landscape orientation
        // and prevents downscaling when the display is already large.
        const longSideTargets = { 1080: 1920, 720: 1280, 480: 854 };
        const targetLongSide = longSideTargets[targetHeight] || 1920;

        // Compute scale factor from display size → target resolution
        const rect = target.getBoundingClientRect();
        const displayWidth = rect.width;
        const displayHeight = rect.height;
        const longerDisplaySide = Math.max(displayWidth, displayHeight);
        const dpr = window.devicePixelRatio || 1;

        // Scale must satisfy TWO constraints:
        //   1. Reach the target quality tier (targetLongSide pixels on the long edge)
        //   2. Never downsample the source canvas (source is rendered at CSS × DPR)
        // Using max(target/display, dpr) ensures 1:1 mapping from source pixels to
        // video pixels on HiDPI displays, eliminating the downsample blur.
        const scale = Math.max(targetLongSide / longerDisplaySide, dpr);

        // Ensure even dimensions (required by some video codecs)
        const videoWidth = Math.round(displayWidth * scale / 2) * 2;
        const videoHeight = Math.round(displayHeight * scale / 2) * 2;

        const offscreen = document.createElement('canvas');
        offscreen.width = videoWidth;
        offscreen.height = videoHeight;
        this._offscreen = offscreen;

        const ctx = offscreen.getContext('2d');

        const stream = offscreen.captureStream(fps);
        this._stream = stream;

        // Codec selection: VP9 > VP8 > default
        const mimeType = MediaRecorder.isTypeSupported('video/webm;codecs=vp9')
            ? 'video/webm;codecs=vp9'
            : MediaRecorder.isTypeSupported('video/webm;codecs=vp8')
                ? 'video/webm;codecs=vp8'
                : 'video/webm';

        // Scale bitrate with resolution (base 20 Mbps at 1080p for crisp screen recording)
        const bitrate = Math.round(20_000_000 * (videoWidth * videoHeight) / (1920 * 1080));

        console.log(`[videoRecorder] Recording: ${videoWidth}x${videoHeight} (scale=${scale.toFixed(2)}, display=${Math.round(displayWidth)}x${Math.round(displayHeight)}, bitrate=${(bitrate / 1_000_000).toFixed(1)}Mbps, codec=${mimeType})`);

        this._chunks = [];
        const recorder = new MediaRecorder(stream, {
            mimeType: mimeType,
            videoBitsPerSecond: bitrate,
        });
        recorder.ondataavailable = (e) => {
            if (e.data && e.data.size > 0) this._chunks.push(e.data);
        };
        this._mediaRecorder = recorder;
        recorder.start(100);

        // Compositing loop ──────────────────────────────────────────────
        // All drawing uses video-pixel coordinates directly (no ctx.scale).
        // This ensures:
        //   - Fonts are rasterized at the exact video pixel size (sharp glyphs)
        //   - Canvas drawImage uses Lanczos resampling (imageSmoothingQuality='high')
        //     for best quality when the source DPR differs from the video scale
        //   - No intermediate transform that might degrade quality
        const intervalMs = 1000 / fps;
        this._captureInterval = setInterval(() => {
            const targetRect = target.getBoundingClientRect();

            // Helper: CSS-pixel offset → video-pixel offset
            const S = scale; // closure alias
            const ox = (el) => (el.left - targetRect.left) * S;
            const oy = (el) => (el.top - targetRect.top) * S;
            const sw = (el) => el.width * S;
            const sh = (el) => el.height * S;

            // Background
            ctx.setTransform(1, 0, 0, 1, 0, 0);
            ctx.fillStyle = '#0a0a1a';
            ctx.fillRect(0, 0, videoWidth, videoHeight);

            // Sort-card backgrounds, borders, headers ─────────────────────
            for (const card of target.querySelectorAll('.sort-card')) {
                const cr = card.getBoundingClientRect();
                const cx = ox(cr), cy = oy(cr), cw = sw(cr), ch = sh(cr);

                // Card background
                ctx.fillStyle = '#1a1a1a';
                ctx.fillRect(cx, cy, cw, ch);

                // Card border (green glow if completed)
                const isCompleted = card.classList.contains('sort-card--completed');
                ctx.strokeStyle = isCompleted ? '#10B981' : '#333';
                ctx.lineWidth = 2 * S;
                ctx.strokeRect(cx, cy, cw, ch);

                // Header background
                const header = card.querySelector('.sort-card__header');
                if (header) {
                    const hr = header.getBoundingClientRect();
                    ctx.fillStyle = '#252525';
                    ctx.fillRect(ox(hr), oy(hr), sw(hr), sh(hr));
                }

                // Stats summary background
                const stats = card.querySelector('.sort-stats-summary');
                if (stats) {
                    const sr = stats.getBoundingClientRect();
                    ctx.fillStyle = '#242a27';
                    ctx.fillRect(ox(sr), oy(sr), sw(sr), sh(sr));
                }
            }

            // Canvas elements (sort visualization) ────────────────────────
            // Use Lanczos-quality resampling for best DPR→video downscale.
            ctx.imageSmoothingEnabled = true;
            ctx.imageSmoothingQuality = 'high';
            for (const canvas of target.querySelectorAll('canvas')) {
                if (canvas.width === 0 || canvas.height === 0) continue;
                const cr = canvas.getBoundingClientRect();
                try {
                    ctx.drawImage(
                        canvas,
                        0, 0, canvas.width, canvas.height,
                        ox(cr), oy(cr), sw(cr), sh(cr)
                    );
                } catch (_) {
                    // Ignore tainted canvas errors
                }
            }

            // Algorithm name labels ───────────────────────────────────────
            // Font sizes are specified in video pixels for native-resolution rasterization.
            const fontAlgo = `bold ${Math.round(14 * S)}px system-ui, -apple-system, sans-serif`;
            const fontBadge = `${Math.round(11 * S)}px system-ui, -apple-system, sans-serif`;
            for (const card of target.querySelectorAll('.sort-card')) {
                const header = card.querySelector('.sort-card__header');
                if (!header) continue;

                // Algorithm name
                let algoName = '';
                const nameSpan = header.querySelector('.sort-card__algorithm-name');
                if (nameSpan) {
                    algoName = nameSpan.textContent.trim();
                } else {
                    const sel = header.querySelector('.sort-card__algo-select');
                    if (sel && sel.selectedOptions.length > 0) {
                        algoName = sel.selectedOptions[0].textContent.trim();
                    }
                }
                if (algoName) {
                    const hr = header.getBoundingClientRect();
                    ctx.font = fontAlgo;
                    ctx.fillStyle = '#e5e7eb';
                    ctx.textBaseline = 'middle';
                    ctx.textAlign = 'start';
                    ctx.fillText(
                        algoName,
                        ox(hr) + 30 * S,
                        oy(hr) + sh(hr) / 2
                    );
                }

                // Complexity badge
                const badge = header.querySelector('.complexity-badge');
                if (badge) {
                    const br = badge.getBoundingClientRect();
                    ctx.font = fontBadge;
                    ctx.fillStyle = '#c8aa6e';
                    ctx.textBaseline = 'middle';
                    ctx.textAlign = 'center';
                    ctx.fillText(
                        badge.textContent.trim(),
                        ox(br) + sw(br) / 2,
                        oy(br) + sh(br) / 2
                    );
                    ctx.textAlign = 'start';
                }
            }

            // Stats values ────────────────────────────────────────────────
            const fontValue = `bold ${Math.round(13 * S)}px Consolas, Monaco, "Courier New", monospace`;
            const fontLabel = `${Math.round(10 * S)}px system-ui, -apple-system, sans-serif`;
            for (const statMini of target.querySelectorAll('.stat-mini')) {
                const mr = statMini.getBoundingClientRect();
                const mx = ox(mr), my = oy(mr), mw = sw(mr), mh = sh(mr);

                // stat-mini background & border
                ctx.fillStyle = 'rgba(127, 168, 111, 0.08)';
                ctx.fillRect(mx, my, mw, mh);
                ctx.strokeStyle = 'rgba(127, 168, 111, 0.2)';
                ctx.lineWidth = 1 * S;
                ctx.strokeRect(mx, my, mw, mh);

                // Value (top portion)
                const valueEl = statMini.querySelector('.value');
                if (valueEl) {
                    ctx.font = fontValue;
                    ctx.fillStyle = '#e5e7eb';
                    ctx.textBaseline = 'middle';
                    ctx.textAlign = 'center';
                    ctx.fillText(valueEl.textContent.trim(), mx + mw / 2, my + mh * 0.38);
                }

                // Label (bottom portion)
                const labelEl = statMini.querySelector('.label');
                if (labelEl) {
                    ctx.font = fontLabel;
                    ctx.fillStyle = '#9ca3af';
                    ctx.textBaseline = 'middle';
                    ctx.textAlign = 'center';
                    ctx.fillText(labelEl.textContent.trim().toUpperCase(), mx + mw / 2, my + mh * 0.72);
                }
                ctx.textAlign = 'start';
            }
        }, intervalMs);

        this._recording = true;
        return true;
    },

    /**
     * Stop recording and download the video as a .webm file.
     * @param {string} filename - The download filename (without extension)
     */
    stopRecording: function (filename) {
        if (!this._recording || !this._mediaRecorder) return;

        if (this._captureInterval) {
            clearInterval(this._captureInterval);
            this._captureInterval = null;
        }

        const recorder = this._mediaRecorder;
        const chunks = this._chunks;

        recorder.onstop = () => {
            const blob = new Blob(chunks, { type: recorder.mimeType });
            const url = URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.href = url;
            a.download = (filename || 'sortvivo-recording') + '.webm';
            document.body.appendChild(a);
            a.click();
            document.body.removeChild(a);
            URL.revokeObjectURL(url);

            this._chunks = [];
            this._mediaRecorder = null;
            if (this._stream) {
                this._stream.getTracks().forEach(t => t.stop());
                this._stream = null;
            }
            this._offscreen = null;
        };

        recorder.stop();
        this._recording = false;
    },

    /**
     * Check if currently recording.
     * @returns {boolean}
     */
    isRecording: function () {
        return this._recording;
    },
};
