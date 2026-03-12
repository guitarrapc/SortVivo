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
//   The scale factor is computed as max(target / longerDisplaySide, 1.0).
//   This means the video is NEVER downscaled: if the display is already larger
//   than the target, the video is recorded at native resolution (scale = 1).
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

    // Scale up to reach target, but NEVER downscale (scale >= 1)
    const scale = Math.max(targetLongSide / longerDisplaySide, 1.0);

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

    console.log(`[videoRecorder] Recording: ${videoWidth}x${videoHeight} (scale=${scale.toFixed(2)}, display=${Math.round(displayWidth)}x${Math.round(displayHeight)}, bitrate=${(bitrate/1_000_000).toFixed(1)}Mbps, codec=${mimeType})`);

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

    // Periodic compositing: paint each sort-card's elements onto the offscreen canvas.
    // ctx.scale(scale, scale) is applied each frame so all CSS-pixel coordinates
    // are automatically mapped to the target resolution.
    //
    // For canvas drawImage: imageSmoothingEnabled = false (nearest-neighbor) is used
    // because sort visualizations are discrete elements (bars, dots, etc.) that look
    // much crisper with pixel-perfect upscaling rather than bilinear interpolation.
    const intervalMs = 1000 / fps;
    const dpr = window.devicePixelRatio || 1;
    this._captureInterval = setInterval(() => {
      const targetRect = target.getBoundingClientRect();

      // Reset transform and clear
      ctx.setTransform(1, 0, 0, 1, 0, 0);
      ctx.clearRect(0, 0, offscreen.width, offscreen.height);

      // Apply scale for target resolution
      ctx.setTransform(scale, 0, 0, scale, 0, 0);

      // Background (use display dimensions; ctx.scale handles upscaling)
      ctx.fillStyle = '#0a0a1a';
      ctx.fillRect(0, 0, displayWidth, displayHeight);

      // Paint each sort-card: header background, card border, completion glow
      for (const card of target.querySelectorAll('.sort-card')) {
        const cardRect = card.getBoundingClientRect();
        const cx = cardRect.left - targetRect.left;
        const cy = cardRect.top - targetRect.top;
        const cw = cardRect.width;
        const ch = cardRect.height;

        // Card background
        ctx.fillStyle = '#1a1a1a';
        ctx.fillRect(cx, cy, cw, ch);

        // Card border (green glow if completed)
        const isCompleted = card.classList.contains('sort-card--completed');
        ctx.strokeStyle = isCompleted ? '#10B981' : '#333';
        ctx.lineWidth = 2;
        ctx.strokeRect(cx, cy, cw, ch);

        // Header background
        const header = card.querySelector('.sort-card__header');
        if (header) {
          const hRect = header.getBoundingClientRect();
          ctx.fillStyle = '#252525';
          ctx.fillRect(
            hRect.left - targetRect.left,
            hRect.top - targetRect.top,
            hRect.width,
            hRect.height
          );
        }

        // Stats summary background
        const stats = card.querySelector('.sort-stats-summary');
        if (stats) {
          const sRect = stats.getBoundingClientRect();
          ctx.fillStyle = '#242a27';
          ctx.fillRect(
            sRect.left - targetRect.left,
            sRect.top - targetRect.top,
            sRect.width,
            sRect.height
          );
        }
      }

      // Canvas elements (the actual sort visualization)
      // Use nearest-neighbor interpolation for crisp upscaling of discrete visualizations.
      // The source canvases render at displaySize * devicePixelRatio internally,
      // so we draw the full source bitmap into the scaled destination for maximum fidelity.
      ctx.imageSmoothingEnabled = false;
      for (const canvas of target.querySelectorAll('canvas')) {
        if (canvas.width === 0 || canvas.height === 0) continue;
        const cRect = canvas.getBoundingClientRect();
        const dx = cRect.left - targetRect.left;
        const dy = cRect.top - targetRect.top;
        try {
          // drawImage(source, sx, sy, sw, sh, dx, dy, dw, dh)
          // Read full internal bitmap (canvas.width × canvas.height) and draw
          // into the CSS-pixel destination rectangle (ctx.scale handles upscaling).
          ctx.drawImage(
            canvas,
            0, 0, canvas.width, canvas.height,
            dx, dy, cRect.width, cRect.height
          );
        } catch (_) {
          // Ignore tainted canvas errors (e.g. picture mode with cross-origin images)
        }
      }
      ctx.imageSmoothingEnabled = true;

      // Algorithm name labels
      // .sort-card__algorithm-name: N=1 text span
      // .sort-card__algo-select:    N>1 <select> element (read selectedOptions text)
      for (const card of target.querySelectorAll('.sort-card')) {
        const header = card.querySelector('.sort-card__header');
        if (!header) continue;

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
          const hRect = header.getBoundingClientRect();
          ctx.font = 'bold 14px system-ui, -apple-system, sans-serif';
          ctx.fillStyle = '#e5e7eb';
          ctx.textBaseline = 'middle';
          // Draw left-aligned inside the header, after the drag handle area
          ctx.fillText(
            algoName,
            hRect.left - targetRect.left + 30, // offset past drag handle
            hRect.top - targetRect.top + hRect.height / 2
          );
        }

        // Complexity badge
        const badge = header.querySelector('.complexity-badge');
        if (badge) {
          const bRect = badge.getBoundingClientRect();
          ctx.font = '11px system-ui, -apple-system, sans-serif';
          ctx.fillStyle = '#c8aa6e';
          ctx.textBaseline = 'middle';
          ctx.textAlign = 'center';
          ctx.fillText(
            badge.textContent.trim(),
            bRect.left - targetRect.left + bRect.width / 2,
            bRect.top - targetRect.top + bRect.height / 2
          );
          ctx.textAlign = 'start';
        }
      }

      // Stats values (each .stat-mini contains .value and .label)
      for (const statMini of target.querySelectorAll('.stat-mini')) {
        const miniRect = statMini.getBoundingClientRect();
        const mx = miniRect.left - targetRect.left;
        const my = miniRect.top - targetRect.top;
        const mw = miniRect.width;
        const mh = miniRect.height;

        // stat-mini background
        ctx.fillStyle = 'rgba(127, 168, 111, 0.08)';
        ctx.fillRect(mx, my, mw, mh);
        ctx.strokeStyle = 'rgba(127, 168, 111, 0.2)';
        ctx.lineWidth = 1;
        ctx.strokeRect(mx, my, mw, mh);

        // Value (top)
        const valueEl = statMini.querySelector('.value');
        if (valueEl) {
          ctx.font = 'bold 13px Consolas, Monaco, "Courier New", monospace';
          ctx.fillStyle = '#e5e7eb';
          ctx.textBaseline = 'middle';
          ctx.textAlign = 'center';
          ctx.fillText(
            valueEl.textContent.trim(),
            mx + mw / 2,
            my + mh * 0.38
          );
        }

        // Label (bottom)
        const labelEl = statMini.querySelector('.label');
        if (labelEl) {
          ctx.font = '10px system-ui, -apple-system, sans-serif';
          ctx.fillStyle = '#9ca3af';
          ctx.textBaseline = 'middle';
          ctx.textAlign = 'center';
          ctx.fillText(
            labelEl.textContent.trim().toUpperCase(),
            mx + mw / 2,
            my + mh * 0.72
          );
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
