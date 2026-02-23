// 画像列モード メインスレッドレンダラー（Picture Column Mode）
// 画像をアップロードし、ソートアルゴリズムで列を並べ替える様子を可視化する

window.pictureColumnCanvasRenderer = {
  instances: new Map(),
  resizeObserver: null,
  lastRenderParams: new Map(),

  dirtyCanvases: new Set(),
  isLoopRunning: false,
  rafId: null,

  arrays: new Map(),   // canvasId → { main: Int32Array }
  workers: new Map(),  // canvasId → { worker, lastWidth, lastHeight }
  cachedSizes: new Map(),

  _images: new Map(),  // canvasId → { img: HTMLImageElement | null, numCols: number }

  _colorLUTMax: -1,
  _colorLUT: null,

  /**
   * Canvas を初期化
   * @param {string} canvasId
   * @param {boolean} useWebGL - 無視（画像列モードは常に Canvas 2D Worker を使用）
   */
  initialize: function (canvasId, useWebGL) {
    const canvas = document.getElementById(canvasId);
    if (!canvas) {
      window.debugHelper.error('PictureColumn Canvas element not found:', canvasId);
      return false;
    }

    if (this.workers.has(canvasId) || this.instances.has(canvasId)) {
      window.debugHelper.warn('PictureColumn Canvas already initialized:', canvasId);
      return true;
    }

    const dpr = window.devicePixelRatio || 1;
    const rect = canvas.getBoundingClientRect();
    this.cachedSizes.set(canvasId, { width: rect.width, height: rect.height });

    if (typeof canvas.transferControlToOffscreen === 'function') {
      canvas.width = rect.width * dpr;
      canvas.height = rect.height * dpr;

      const offscreen = canvas.transferControlToOffscreen();
      const workerUrl = new URL('js/pictureColumnRenderWorker.js', document.baseURI).href;
      const worker = new Worker(workerUrl);
      worker.postMessage({ type: 'init', canvas: offscreen, dpr }, [offscreen]);

      this.workers.set(canvasId, { worker, lastWidth: canvas.width, lastHeight: canvas.height });
      this.instances.set(canvasId, { canvas, ctx: null });

      this._ensureResizeObserver();
      this.resizeObserver.observe(canvas);

      window.debugHelper.log('PictureColumn Canvas initialized (Worker):', canvasId, rect.width, 'x', rect.height, 'DPR:', dpr);
      return true;
    }

    const ctx = canvas.getContext('2d', { alpha: false, desynchronized: true });
    canvas.width = rect.width * dpr;
    canvas.height = rect.height * dpr;
    ctx.scale(dpr, dpr);

    this.instances.set(canvasId, { canvas, ctx });
    this._ensureResizeObserver();
    this.resizeObserver.observe(canvas);

    window.debugHelper.log('PictureColumn Canvas initialized (Canvas2D):', canvasId, rect.width, 'x', rect.height, 'DPR:', dpr);
    return true;
  },

  _ensureResizeObserver: function () {
    if (this.resizeObserver) return;
    const self = this;
    this.resizeObserver = new ResizeObserver(entries => {
      for (const entry of entries) {
        const canvas = entry.target;
        const canvasId = canvas.id;
        const instance = self.instances.get(canvasId);
        if (!instance) continue;

        const dpr = window.devicePixelRatio || 1;
        const rect = canvas.getBoundingClientRect();
        const newWidth = rect.width * dpr;
        const newHeight = rect.height * dpr;

        const workerInfo = self.workers.get(canvasId);
        if (workerInfo) {
          if (workerInfo.lastWidth !== newWidth || workerInfo.lastHeight !== newHeight) {
            workerInfo.lastWidth = newWidth;
            workerInfo.lastHeight = newHeight;
            self.cachedSizes.set(canvasId, { width: rect.width, height: rect.height });
            workerInfo.worker.postMessage({ type: 'resize', newWidth, newHeight, dpr });
          }
        } else {
          const { ctx } = instance;
          if (canvas.width !== newWidth || canvas.height !== newHeight) {
            canvas.width = newWidth;
            canvas.height = newHeight;
            ctx.scale(dpr, dpr);
            self.cachedSizes.set(canvasId, { width: rect.width, height: rect.height });
            const lastParams = self.lastRenderParams.get(canvasId);
            if (lastParams) {
              requestAnimationFrame(() => self.renderInternal(canvasId, lastParams));
            }
          }
        }
      }
    });
  },

  /**
   * 画像をアップロードしてレンダラーに設定する
   * @param {string} canvasId
   * @param {string} dataUrl - data:image/...;base64,... 形式
   * @param {number} numCols - 画像を分割する列数（= 配列サイズ）
   */
  setImage: async function (canvasId, dataUrl, numCols) {
    if (!dataUrl || numCols <= 0) return;

    window.debugHelper.log('PictureColumn setImage:', canvasId, 'numCols:', numCols);

    const workerInfo = this.workers.get(canvasId);
    if (workerInfo) {
      try {
        const base64 = dataUrl.split(',')[1];
        const mimeMatch = dataUrl.match(/data:([^;]+);/);
        const mimeType = mimeMatch ? mimeMatch[1] : 'image/png';

        const binaryString = atob(base64);
        const bytes = new Uint8Array(binaryString.length);
        for (let i = 0; i < binaryString.length; i++) {
          bytes[i] = binaryString.charCodeAt(i);
        }
        const buffer = bytes.buffer;

        workerInfo.worker.postMessage(
          { type: 'setImage', imageBuffer: buffer, mimeType, numCols },
          [buffer]
        );
      } catch (err) {
        window.debugHelper.error('PictureColumn setImage error:', err);
      }
      return;
    }

    const self = this;
    const img = new Image();
    img.onload = function () {
      self._images.set(canvasId, { img, numCols });
      const lastParams = self.lastRenderParams.get(canvasId);
      if (lastParams) {
        self.dirtyCanvases.add(canvasId);
        self.startLoop();
      }
    };
    img.src = dataUrl;
    this._images.set(canvasId, { img: null, numCols });
  },

  clearImage: function (canvasId) {
    this._images.delete(canvasId);
    const workerInfo = this.workers.get(canvasId);
    if (workerInfo) {
      workerInfo.worker.postMessage({ type: 'clearImage' });
    }
  },

  setArray: function (canvasId, mainArray, bufferArrays, compareIndices, swapIndices, readIndices, writeIndices, isSortCompleted, showCompletionHighlight) {
    const workerInfo = this.workers.get(canvasId);
    if (workerInfo) {
      workerInfo.worker.postMessage({
        type: 'setArray',
        mainArray, bufferArrays, compareIndices, swapIndices,
        readIndices, writeIndices,
        isSortCompleted: isSortCompleted || false,
        showCompletionHighlight: showCompletionHighlight || false
      });
      return;
    }
    let entry = this.arrays.get(canvasId);
    if (!entry) { entry = { main: null }; this.arrays.set(canvasId, entry); }
    entry.main = new Int32Array(mainArray);
    this._scheduleRender(canvasId, compareIndices, swapIndices, readIndices, writeIndices, isSortCompleted, showCompletionHighlight);
  },

  applyFrame: function (canvasId, mainDelta, bufferDeltas, compareIndices, swapIndices, readIndices, writeIndices, isSortCompleted, showCompletionHighlight) {
    const workerInfo = this.workers.get(canvasId);
    if (workerInfo) {
      workerInfo.worker.postMessage({
        type: 'applyFrame',
        mainDelta, bufferDeltas, compareIndices, swapIndices,
        readIndices, writeIndices,
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
    this._scheduleRender(canvasId, compareIndices, swapIndices, readIndices, writeIndices, isSortCompleted, showCompletionHighlight);
  },

  updateData: function (canvasId, array, compareIndices, swapIndices, readIndices, writeIndices, isSortCompleted, bufferArrays, showCompletionHighlight) {
    this.setArray(canvasId, array, bufferArrays, compareIndices, swapIndices, readIndices, writeIndices, isSortCompleted, showCompletionHighlight);
  },

  _scheduleRender: function (canvasId, compareIndices, swapIndices, readIndices, writeIndices, isSortCompleted, showCompletionHighlight) {
    this.lastRenderParams.set(canvasId, {
      compareIndices, swapIndices, readIndices, writeIndices,
      isSortCompleted: isSortCompleted || false,
      showCompletionHighlight: showCompletionHighlight || false
    });
    this.dirtyCanvases.add(canvasId);
    if (!this.isLoopRunning) this.startLoop();
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

  _buildColorLUT: function (maxValue) {
    if (this._colorLUTMax === maxValue) return;
    this._colorLUTMax = maxValue;
    this._colorLUT = new Array(maxValue + 1);
    for (let v = 0; v <= maxValue; v++) {
      const hue = (v / maxValue) * 360;
      this._colorLUT[v] = `hsl(${hue}, 70%, 55%)`;
    }
  },

  /**
   * Canvas 2D フォールバック用の描画処理（列ベース）
   */
  renderInternal: function (canvasId, params) {
    const instance = this.instances.get(canvasId);
    if (!instance) return;
    const { canvas, ctx } = instance;
    if (!canvas || !ctx) return;

    const entry = this.arrays.get(canvasId);
    if (!entry || !entry.main) return;
    const array = entry.main;
    const n = array.length;

    const size = this.cachedSizes.get(canvasId);
    if (!size) return;
    const { width, height } = size;

    ctx.fillStyle = '#1A1A1A';
    ctx.fillRect(0, 0, width, height);

    if (n === 0) return;

    const colW = width / n;
    const { compareIndices, swapIndices, readIndices, writeIndices,
            isSortCompleted, showCompletionHighlight } = params;

    const compareSet = new Set(compareIndices);
    const swapSet    = new Set(swapIndices);
    const readSet    = new Set(readIndices);
    const writeSet   = new Set(writeIndices);

    const imgEntry = this._images.get(canvasId);
    const img = imgEntry?.img;
    const numCols = imgEntry?.numCols || 0;

    if (img && numCols > 0 && img.complete) {
      // 画像列モード
      const srcColW = img.naturalWidth / numCols;
      let minVal = array[0];
      for (let i = 1; i < n; i++) { if (array[i] < minVal) minVal = array[i]; }

      if (showCompletionHighlight) {
        for (let i = 0; i < n; i++) {
          const colIdx = array[i] - minVal;
          if (colIdx < 0 || colIdx >= numCols) continue;
          ctx.drawImage(img, colIdx * srcColW, 0, srcColW, img.naturalHeight, i * colW, 0, colW, height);
        }
        ctx.fillStyle = 'rgba(16,185,129,0.3)';
        ctx.fillRect(0, 0, width, height);
      } else {
        for (let i = 0; i < n; i++) {
          const colIdx = array[i] - minVal;
          if (colIdx < 0 || colIdx >= numCols) continue;
          ctx.drawImage(img, colIdx * srcColW, 0, srcColW, img.naturalHeight, i * colW, 0, colW, height);

          let overlay = null;
          if (swapSet.has(i))         overlay = 'rgba(239,68,68,0.55)';
          else if (compareSet.has(i)) overlay = 'rgba(168,85,247,0.5)';
          else if (writeSet.has(i))   overlay = 'rgba(249,115,22,0.45)';
          else if (readSet.has(i))    overlay = 'rgba(251,191,36,0.35)';
          if (overlay) {
            ctx.fillStyle = overlay;
            ctx.fillRect(i * colW, 0, colW + 0.5, height);
          }
        }
      }
    } else {
      // 画像なし: グラデーション縦バー
      let maxValue = 0;
      for (let i = 0; i < n; i++) { if (array[i] > maxValue) maxValue = array[i]; }
      if (maxValue === 0) maxValue = 1;
      this._buildColorLUT(maxValue);
      const colorLUT = this._colorLUT;

      if (showCompletionHighlight) {
        ctx.fillStyle = '#10B981';
        for (let i = 0; i < n; i++) ctx.fillRect(i * colW, 0, colW + 0.5, height);
      } else {
        for (let i = 0; i < n; i++) {
          let color;
          if (swapSet.has(i))         color = '#EF4444';
          else if (compareSet.has(i)) color = '#A855F7';
          else if (writeSet.has(i))   color = '#F97316';
          else if (readSet.has(i))    color = '#FBBF24';
          else                        color = colorLUT[array[i]] || '#3B82F6';
          ctx.fillStyle = color;
          ctx.fillRect(i * colW, 0, colW + 0.5, height);
        }
      }
    }
  },

  // ドロップゾーン登録
  dropZones: new Map(),

  setupDropZone: function (dropZoneId, dotNetRef) {
    const el = document.getElementById(dropZoneId);
    if (!el) {
      window.debugHelper.warn('PictureColumn: dropZone element not found:', dropZoneId);
      return;
    }

    this.disposeDropZone(dropZoneId);

    let dragDepth = 0;

    const onDragEnter = (e) => {
      e.preventDefault(); e.stopPropagation();
      dragDepth++;
      if (dragDepth === 1) dotNetRef.invokeMethodAsync('OnDragStateChanged', true);
    };

    const onDragOver = (e) => {
      e.preventDefault(); e.stopPropagation();
      if (e.dataTransfer) e.dataTransfer.dropEffect = 'copy';
    };

    const onDragLeave = (e) => {
      e.preventDefault(); e.stopPropagation();
      dragDepth--;
      if (dragDepth <= 0) {
        dragDepth = 0;
        dotNetRef.invokeMethodAsync('OnDragStateChanged', false);
      }
    };

    const onDrop = (e) => {
      e.preventDefault(); e.stopPropagation();
      dragDepth = 0;
      dotNetRef.invokeMethodAsync('OnDragStateChanged', false);

      const files = e.dataTransfer?.files;
      if (!files || files.length === 0) return;

      const file = files[0];
      if (!file.type.startsWith('image/')) {
        dotNetRef.invokeMethodAsync('OnDropError', 'Only image files are supported.');
        return;
      }

      const reader = new FileReader();
      reader.onload = () => {
        dotNetRef.invokeMethodAsync('OnFileDropped', reader.result, file.name, file.size);
      };
      reader.onerror = () => {
        dotNetRef.invokeMethodAsync('OnDropError', 'Failed to read the dropped file.');
      };
      reader.readAsDataURL(file);
    };

    el.addEventListener('dragenter', onDragEnter);
    el.addEventListener('dragover',  onDragOver);
    el.addEventListener('dragleave', onDragLeave);
    el.addEventListener('drop',      onDrop);

    this.dropZones.set(dropZoneId, { el, onDragEnter, onDragOver, onDragLeave, onDrop });
    window.debugHelper.log('PictureColumn: dropZone registered:', dropZoneId);
  },

  disposeDropZone: function (dropZoneId) {
    const entry = this.dropZones.get(dropZoneId);
    if (!entry) return;
    const { el, onDragEnter, onDragOver, onDragLeave, onDrop } = entry;
    el.removeEventListener('dragenter', onDragEnter);
    el.removeEventListener('dragover',  onDragOver);
    el.removeEventListener('dragleave', onDragLeave);
    el.removeEventListener('drop',      onDrop);
    this.dropZones.delete(dropZoneId);
    window.debugHelper.log('PictureColumn: dropZone disposed:', dropZoneId);
  },

  dispose: function (canvasId) {
    window.debugHelper.log('PictureColumn Canvas dispose:', canvasId);

    const workerInfo = this.workers.get(canvasId);
    if (workerInfo) {
      workerInfo.worker.postMessage({ type: 'dispose' });
      workerInfo.worker.terminate();
      this.workers.delete(canvasId);
    }

    const instance = this.instances.get(canvasId);
    if (instance && this.resizeObserver) {
      this.resizeObserver.unobserve(instance.canvas);
    }

    this.instances.delete(canvasId);
    this.arrays.delete(canvasId);
    this.lastRenderParams.delete(canvasId);
    this.cachedSizes.delete(canvasId);
    this._images.delete(canvasId);
    this.dirtyCanvases.delete(canvasId);
  }
};
