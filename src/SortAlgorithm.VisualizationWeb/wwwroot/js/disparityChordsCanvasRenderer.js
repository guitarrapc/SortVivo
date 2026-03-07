// Canvas 2D 不均衡和音レンダラー（Disparity Chords）
// 各要素の現在位置と整列後の位置を弦で結び、位置ずれを可視化する（複数Canvas対応）

window.disparityChordsCanvasRenderer = {
  instances: new Map(),      // canvasId → { canvas, ctx }
  resizeObserver: null,
  lastRenderParams: new Map(),

  // rAFループ用
  dirtyCanvases: new Set(),
  isLoopRunning: false,
  rafId: null,

  // JS 側配列コピー
  arrays: new Map(),          // canvasId → { main: Int32Array, buffers: Map }

  // OffscreenCanvas + Worker
  workers: new Map(),         // canvasId → { worker, lastWidth, lastHeight }

  // キャッシュされた Canvas サイズ
  cachedSizes: new Map(),     // canvasId → { width, height }

  // 色定義
  colors: {
    compare: '#A855F7',
    swap:    '#EF4444',
    write:   '#F97316',
    read:    '#FBBF24',
    sorted:  '#10B981'
  },

  // 三角関数 LUT（Canvas 2D fallback 用）
  _lutLength: 0,
  _cosLUT: null,
  _sinLUT: null,

  // HSL カラー LUT（Canvas 2D fallback 用）
  _colorLUTMax: -1,
  _colorLUT: null,

  /**
   * Canvas を初期化
   * @param {string} canvasId
   * @param {boolean} useWebGL - true: WebGL2 Worker / false: Canvas 2D Worker
   */
  initialize: function (canvasId, useWebGL = true) {
    const canvas = document.getElementById(canvasId);
    if (!canvas) {
      window.debugHelper.error('DisparityChords Canvas element not found:', canvasId);
      return false;
    }

    if (this.workers.has(canvasId) || this.instances.has(canvasId)) {
      window.debugHelper.warn('DisparityChords Canvas already initialized:', canvasId);
      return true;
    }

    const dpr  = window.devicePixelRatio || 1;
    const rect = canvas.getBoundingClientRect();
    this.cachedSizes.set(canvasId, { width: rect.width, height: rect.height });

    // OffscreenCanvas + Worker パス
    if (typeof canvas.transferControlToOffscreen === 'function') {
      canvas.width  = rect.width  * dpr;
      canvas.height = rect.height * dpr;

      const offscreen  = canvas.transferControlToOffscreen();
      const workerFile = useWebGL ? 'js/disparityChordsWebglWorker.js' : 'js/disparityChordsRenderWorker.js';
      const workerUrl  = new URL(workerFile, document.baseURI).href;
      const worker     = new Worker(workerUrl);
      worker.postMessage({ type: 'init', canvas: offscreen, dpr }, [offscreen]);

      this.workers.set(canvasId, { worker, lastWidth: canvas.width, lastHeight: canvas.height });
      this.instances.set(canvasId, { canvas, ctx: null });

      this._ensureResizeObserver();
      this.resizeObserver.observe(canvas);

      window.debugHelper.log('DisparityChords Canvas initialized (Worker):', canvasId, rect.width, 'x', rect.height, 'DPR:', dpr, 'WebGL:', useWebGL);
      return true;
    }

    // Canvas 2D フォールバック
    const ctx = canvas.getContext('2d', { alpha: false, desynchronized: true });
    canvas.width  = rect.width  * dpr;
    canvas.height = rect.height * dpr;
    ctx.scale(dpr, dpr);

    this.instances.set(canvasId, { canvas, ctx });
    this._ensureResizeObserver();
    this.resizeObserver.observe(canvas);

    window.debugHelper.log('DisparityChords Canvas initialized (Canvas2D):', canvasId, rect.width, 'x', rect.height, 'DPR:', dpr);
    return true;
  },

  _ensureResizeObserver: function () {
    if (this.resizeObserver) return;
    this.resizeObserver = new ResizeObserver(entries => {
      for (const entry of entries) {
        const canvas   = entry.target;
        const canvasId = canvas.id;
        const instance = this.instances.get(canvasId);
        if (!instance) continue;

        const dpr      = window.devicePixelRatio || 1;
        const rect     = canvas.getBoundingClientRect();
        const newW     = rect.width  * dpr;
        const newH     = rect.height * dpr;

        const workerInfo = this.workers.get(canvasId);
        if (workerInfo) {
          if (workerInfo.lastWidth !== newW || workerInfo.lastHeight !== newH) {
            workerInfo.lastWidth  = newW;
            workerInfo.lastHeight = newH;
            this.cachedSizes.set(canvasId, { width: rect.width, height: rect.height });
            workerInfo.worker.postMessage({ type: 'resize', newWidth: newW, newHeight: newH, dpr });
            window.debugHelper.log('DisparityChords Worker resize:', canvasId, rect.width, 'x', rect.height);
          }
        } else {
          const { ctx } = instance;
          if (canvas.width !== newW || canvas.height !== newH) {
            canvas.width  = newW;
            canvas.height = newH;
            ctx.scale(dpr, dpr);
            this.cachedSizes.set(canvasId, { width: rect.width, height: rect.height });
            // LUT を無効化
            this._lutLength = 0;
            window.debugHelper.log('DisparityChords Canvas auto-resized:', canvasId, rect.width, 'x', rect.height);
            const lastParams = this.lastRenderParams.get(canvasId);
            if (lastParams) {
              requestAnimationFrame(() => this.renderInternal(canvasId, lastParams));
            }
          }
        }
      }
    });
  },

  /**
   * 新しいソートがロードされたとき（SortVersion 変化時）に C# から呼ばれる。
   */
  setArray: function (canvasId, mainArray, bufferArrays, compareIndices, swapIndices, readIndices, writeIndices, isSortCompleted, showCompletionHighlight) {
    const workerInfo = this.workers.get(canvasId);
    if (workerInfo) {
      workerInfo.worker.postMessage({
        type: 'setArray',
        mainArray, bufferArrays,
        compareIndices, swapIndices, readIndices, writeIndices,
        isSortCompleted: isSortCompleted || false,
        showCompletionHighlight: showCompletionHighlight || false
      });
      return;
    }
    // Canvas 2D パス
    let entry = this.arrays.get(canvasId);
    if (!entry) {
      entry = { main: null, buffers: new Map() };
      this.arrays.set(canvasId, entry);
    }
    entry.main = new Int32Array(mainArray);
    entry.buffers.clear();
    if (bufferArrays) {
      for (const [idStr, arr] of Object.entries(bufferArrays)) {
        entry.buffers.set(parseInt(idStr), new Int32Array(arr));
      }
    }
    this._scheduleRender(canvasId, compareIndices, swapIndices, readIndices, writeIndices, isSortCompleted, showCompletionHighlight);
  },

  /**
   * 通常の再生フレームで C# から呼ばれる（高速パス）。
   */
  applyFrame: function (canvasId, mainDelta, bufferDeltas, compareIndices, swapIndices, readIndices, writeIndices, isSortCompleted, showCompletionHighlight) {
    const workerInfo = this.workers.get(canvasId);
    if (workerInfo) {
      workerInfo.worker.postMessage({
        type: 'applyFrame',
        mainDelta, bufferDeltas,
        compareIndices, swapIndices, readIndices, writeIndices,
        isSortCompleted: isSortCompleted || false,
        showCompletionHighlight: showCompletionHighlight || false
      });
      return;
    }
    const entry = this.arrays.get(canvasId);
    if (!entry || !entry.main) return;

    if (mainDelta) {
      for (let k = 0; k < mainDelta.length; k += 2) {
        entry.main[mainDelta[k]] = mainDelta[k + 1];
      }
    }
    if (bufferDeltas) {
      for (const [idStr, delta] of Object.entries(bufferDeltas)) {
        const bid = parseInt(idStr);
        let buf = entry.buffers.get(bid);
        if (!buf) {
          buf = new Int32Array(entry.main.length);
          entry.buffers.set(bid, buf);
        }
        for (let k = 0; k < delta.length; k += 2) {
          buf[delta[k]] = delta[k + 1];
        }
      }
    }
    if (isSortCompleted && entry.buffers.size > 0) entry.buffers.clear();
    this._scheduleRender(canvasId, compareIndices, swapIndices, readIndices, writeIndices, isSortCompleted, showCompletionHighlight);
  },

  _scheduleRender: function (canvasId, compareIndices, swapIndices, readIndices, writeIndices, isSortCompleted, showCompletionHighlight) {
    this.lastRenderParams.set(canvasId, {
      compareIndices, swapIndices, readIndices, writeIndices,
      isSortCompleted: isSortCompleted || false,
      showCompletionHighlight: showCompletionHighlight !== undefined ? showCompletionHighlight : false
    });
    this.dirtyCanvases.add(canvasId);
    if (!this.isLoopRunning) this.startLoop();
  },

  /**
   * データを更新（シーク後・リセット後の全量更新フォールバック）
   */
  updateData: function (canvasId, array, compareIndices, swapIndices, readIndices, writeIndices, isSortCompleted, bufferArrays, showCompletionHighlight) {
    this.setArray(canvasId, array, bufferArrays, compareIndices, swapIndices, readIndices, writeIndices, isSortCompleted, showCompletionHighlight);
  },

  startLoop: function () {
    if (this.isLoopRunning) return;
    this.isLoopRunning = true;
    const self = this;
    const tick = () => {
      if (self.dirtyCanvases.size > 0) {
        self.dirtyCanvases.forEach(canvasId => {
          if (self.instances.has(canvasId)) {
            const params = self.lastRenderParams.get(canvasId);
            if (params) self.renderInternal(canvasId, params);
          }
        });
        self.dirtyCanvases.clear();
        self.rafId = requestAnimationFrame(tick);
      } else {
        self.isLoopRunning = false;
        self.rafId = null;
      }
    };
    this.rafId = requestAnimationFrame(tick);
  },

  // ─── Canvas 2D LUT ヘルパー ─────────────────────────────────────────────────

  _buildTrigLUT: function (n) {
    if (this._lutLength === n) return;
    this._lutLength = n;
    const step = (2 * Math.PI) / n;
    this._cosLUT = new Float64Array(n);
    this._sinLUT = new Float64Array(n);
    for (let i = 0; i < n; i++) {
      const angle = i * step - Math.PI / 2;
      this._cosLUT[i] = Math.cos(angle);
      this._sinLUT[i] = Math.sin(angle);
    }
  },

  _buildColorLUT: function (maxValue) {
    if (this._colorLUTMax === maxValue) return;
    this._colorLUTMax = maxValue;
    this._colorLUT = new Array(maxValue + 1);
    for (let v = 0; v <= maxValue; v++) {
      const hue = (v / maxValue) * 360;
      this._colorLUT[v] = `hsla(${hue.toFixed(1)}, 70%, 65%, 0.55)`;
    }
  },

  /**
   * Canvas 2D フォールバック描画
   */
  renderInternal: function (canvasId, params) {
    const instance = this.instances.get(canvasId);
    if (!instance) return;
    const { canvas, ctx } = instance;
    if (!canvas || !ctx) return;

    const { compareIndices, swapIndices, readIndices, writeIndices, isSortCompleted, showCompletionHighlight } = params;
    const entry = this.arrays.get(canvasId);
    if (!entry || !entry.main) return;
    const array = entry.main;
    const n = array.length;

    const size = this.cachedSizes.get(canvasId);
    if (!size) return;
    const width  = size.width;
    const height = size.height;

    ctx.fillStyle = '#1A1A1A';
    ctx.fillRect(0, 0, width, height);
    if (n === 0) return;

    const cx   = width  / 2;
    const cy   = height / 2;
    const R    = Math.min(width, height) * 0.44;
    const dotR = n <= 64 ? 4 : n <= 256 ? 3 : 2;

    // 背景円リング
    ctx.strokeStyle = 'rgba(255,255,255,0.07)';
    ctx.lineWidth = 1;
    ctx.beginPath();
    ctx.arc(cx, cy, R, 0, 2 * Math.PI);
    ctx.stroke();

    let maxValue = 0;
    for (let i = 0; i < n; i++) {
      if (array[i] > maxValue) maxValue = array[i];
    }
    if (maxValue === 0) maxValue = 1;

    this._buildTrigLUT(n);
    this._buildColorLUT(maxValue);
    const cosLUT   = this._cosLUT;
    const sinLUT   = this._sinLUT;
    const colorLUT = this._colorLUT;

    const compareSet = new Set(compareIndices);
    const swapSet    = new Set(swapIndices);
    const readSet    = new Set(readIndices);
    const writeSet   = new Set(writeIndices);

    // 整列後インデックス
    const sortedIdx = new Int32Array(n);
    for (let i = 0; i < n; i++) {
      sortedIdx[i] = Math.round((array[i] / maxValue) * (n - 1));
    }

    if (showCompletionHighlight) {
      ctx.fillStyle = this.colors.sorted;
      for (let i = 0; i < n; i++) {
        ctx.beginPath();
        ctx.arc(cx + cosLUT[i] * R, cy + sinLUT[i] * R, dotR, 0, 2 * Math.PI);
        ctx.fill();
      }
      return;
    }

    // 通常弦
    for (let i = 0; i < n; i++) {
      if (compareSet.has(i) || swapSet.has(i) || readSet.has(i) || writeSet.has(i)) continue;
      const si = sortedIdx[i];
      if (si === i) continue;
      ctx.strokeStyle = colorLUT[array[i]];
      ctx.lineWidth = n <= 64 ? 1.5 : 1;
      ctx.beginPath();
      ctx.moveTo(cx + cosLUT[i]  * R, cy + sinLUT[i]  * R);
      ctx.lineTo(cx + cosLUT[si] * R, cy + sinLUT[si] * R);
      ctx.stroke();
    }

    // ハイライト弦
    const highlightBuckets = [
      [compareIndices, this.colors.compare],
      [writeIndices,   this.colors.write],
      [readIndices,    this.colors.read],
      [swapIndices,    this.colors.swap],
    ];
    ctx.lineWidth = n <= 64 ? 2.5 : n <= 256 ? 2 : 1.5;
    for (const [indices, color] of highlightBuckets) {
      if (!indices || indices.length === 0) continue;
      ctx.strokeStyle = color;
      ctx.beginPath();
      for (const i of indices) {
        if (i < 0 || i >= n) continue;
        const si = sortedIdx[i];
        if (si === i) continue;
        ctx.moveTo(cx + cosLUT[i]  * R, cy + sinLUT[i]  * R);
        ctx.lineTo(cx + cosLUT[si] * R, cy + sinLUT[si] * R);
      }
      ctx.stroke();
    }

    // 通常ドット
    for (let i = 0; i < n; i++) {
      if (compareSet.has(i) || swapSet.has(i) || readSet.has(i) || writeSet.has(i)) continue;
      const v   = array[i];
      const hue = (v / maxValue) * 360;
      ctx.fillStyle = `hsl(${hue.toFixed(1)}, 70%, 65%)`;
      ctx.beginPath();
      ctx.arc(cx + cosLUT[i] * R, cy + sinLUT[i] * R, dotR, 0, 2 * Math.PI);
      ctx.fill();
    }

    // ハイライトドット
    for (const [indices, color] of highlightBuckets) {
      if (!indices || indices.length === 0) continue;
      ctx.fillStyle = color;
      ctx.beginPath();
      for (const i of indices) {
        if (i < 0 || i >= n) continue;
        ctx.moveTo(cx + cosLUT[i] * R + dotR + 1, cy + sinLUT[i] * R);
        ctx.arc(cx + cosLUT[i] * R, cy + sinLUT[i] * R, dotR + 1, 0, 2 * Math.PI);
      }
      ctx.fill();
    }

    // 正位置ドット（緑）
    ctx.fillStyle = this.colors.sorted;
    ctx.beginPath();
    for (let i = 0; i < n; i++) {
      if (sortedIdx[i] !== i) continue;
      if (compareSet.has(i) || swapSet.has(i) || readSet.has(i) || writeSet.has(i)) continue;
      ctx.moveTo(cx + cosLUT[i] * R + dotR, cy + sinLUT[i] * R);
      ctx.arc(cx + cosLUT[i] * R, cy + sinLUT[i] * R, dotR, 0, 2 * Math.PI);
    }
    ctx.fill();
  },

  /**
   * クリーンアップ
   */
  dispose: function (canvasId) {
    if (canvasId) {
      const workerInfo = this.workers.get(canvasId);
      if (workerInfo) {
        workerInfo.worker.postMessage({ type: 'dispose' });
        workerInfo.worker.terminate();
        this.workers.delete(canvasId);
      }
      const canvas = document.getElementById(canvasId);
      if (canvas && this.resizeObserver) {
        this.resizeObserver.unobserve(canvas);
      }
      const deleted = this.instances.delete(canvasId);
      if (deleted) {
        console.log('DisparityChords Canvas instance disposed:', canvasId);
      } else {
        console.warn('DisparityChords Canvas instance not found for disposal:', canvasId);
      }
      this.lastRenderParams.delete(canvasId);
      this.dirtyCanvases.delete(canvasId);
      this.arrays.delete(canvasId);
      this.cachedSizes.delete(canvasId);
    } else {
      this.workers.forEach(info => {
        info.worker.postMessage({ type: 'dispose' });
        info.worker.terminate();
      });
      this.workers.clear();
      if (this.rafId) {
        cancelAnimationFrame(this.rafId);
        this.rafId = null;
      }
      this.isLoopRunning = false;
      this.dirtyCanvases.clear();
      if (this.resizeObserver) {
        this.resizeObserver.disconnect();
        this.resizeObserver = null;
      }
      this.instances.clear();
      this.lastRenderParams.clear();
      this.arrays.clear();
      this.cachedSizes.clear();
    }
  }
};
