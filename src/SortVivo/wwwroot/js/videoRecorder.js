'use strict';
// Video recording for the visualization area.
//
// Uses html2canvas to capture periodic snapshots of the DOM element and
// encodes them into a WebM video via MediaRecorder + CanvasCaptureMediaStream.
//
// Flow:
//   1. startRecording(selector, fps) — begins periodic DOM→canvas snapshots.
//   2. stopRecording()               — stops and triggers a .webm download.

window.videoRecorder = {
  _mediaRecorder: null,
  _chunks: [],
  _rafId: null,
  _offscreen: null,
  _stream: null,
  _recording: false,
  _captureInterval: null,

  /**
   * Start recording the specified DOM element.
   * @param {string} selector - CSS selector for the element to capture
   * @param {number} fps      - Frames per second (default: 30)
   * @returns {boolean} true if recording started successfully
   */
  startRecording: function (selector, fps) {
    if (this._recording) return false;

    const target = document.querySelector(selector);
    if (!target) {
      console.error('[videoRecorder] Element not found:', selector);
      return false;
    }

    fps = fps || 30;

    // Collect all canvas elements inside the target (sort visualizations)
    const canvases = target.querySelectorAll('canvas');
    if (canvases.length === 0) {
      console.error('[videoRecorder] No canvas elements found in:', selector);
      return false;
    }

    // Determine bounding rect of target for the output video dimensions
    const rect = target.getBoundingClientRect();
    const width = Math.round(rect.width);
    const height = Math.round(rect.height);

    // Create an offscreen canvas that composites the target area each frame
    const offscreen = document.createElement('canvas');
    offscreen.width = width;
    offscreen.height = height;
    this._offscreen = offscreen;

    const ctx = offscreen.getContext('2d');

    // Capture stream from the offscreen canvas
    const stream = offscreen.captureStream(fps);
    this._stream = stream;

    // Set up MediaRecorder
    const mimeType = MediaRecorder.isTypeSupported('video/webm;codecs=vp9')
      ? 'video/webm;codecs=vp9'
      : MediaRecorder.isTypeSupported('video/webm;codecs=vp8')
        ? 'video/webm;codecs=vp8'
        : 'video/webm';

    this._chunks = [];
    const recorder = new MediaRecorder(stream, {
      mimeType: mimeType,
      videoBitsPerSecond: 8_000_000,
    });
    recorder.ondataavailable = (e) => {
      if (e.data && e.data.size > 0) this._chunks.push(e.data);
    };
    this._mediaRecorder = recorder;
    recorder.start(100); // collect data every 100ms

    // Periodically composite all visible canvases onto the offscreen canvas.
    // The target rect is used as the reference frame so each canvas is drawn
    // at its correct position within the captured area.
    const intervalMs = 1000 / fps;
    this._captureInterval = setInterval(() => {
      const currentRect = target.getBoundingClientRect();

      // Draw background (dark theme)
      ctx.fillStyle = '#0a0a1a';
      ctx.fillRect(0, 0, offscreen.width, offscreen.height);

      // Draw non-canvas content background elements
      const controlBar = target.querySelector('.play-control-bar');
      if (controlBar) {
        const barRect = controlBar.getBoundingClientRect();
        ctx.fillStyle = '#111827';
        ctx.fillRect(
          barRect.left - currentRect.left,
          barRect.top - currentRect.top,
          barRect.width,
          barRect.height
        );
      }

      // Draw each canvas at its relative position
      for (const canvas of target.querySelectorAll('canvas')) {
        if (canvas.width === 0 || canvas.height === 0) continue;
        const cRect = canvas.getBoundingClientRect();
        try {
          ctx.drawImage(
            canvas,
            cRect.left - currentRect.left,
            cRect.top - currentRect.top,
            cRect.width,
            cRect.height
          );
        } catch (e) {
          // Ignore tainted canvas errors
        }
      }

      // Draw algorithm name labels (from sort-card headers)
      const labels = target.querySelectorAll('.sort-card__algo-name, .sort-card__header-title');
      ctx.font = 'bold 14px system-ui, sans-serif';
      ctx.fillStyle = '#e5e7eb';
      ctx.textBaseline = 'top';
      for (const label of labels) {
        const lRect = label.getBoundingClientRect();
        ctx.fillText(
          label.textContent.trim(),
          lRect.left - currentRect.left,
          lRect.top - currentRect.top
        );
      }

      // Draw stats overlays
      const statsElements = target.querySelectorAll('.sort-card__stats-value');
      ctx.font = '12px "Courier New", monospace';
      ctx.fillStyle = '#9ca3af';
      for (const stat of statsElements) {
        const sRect = stat.getBoundingClientRect();
        ctx.fillText(
          stat.textContent.trim(),
          sRect.left - currentRect.left,
          sRect.top - currentRect.top
        );
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

      // Cleanup
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
