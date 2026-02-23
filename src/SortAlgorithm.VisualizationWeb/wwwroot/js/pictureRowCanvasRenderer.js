// 画像行モード メインスレッドレンダラー（Picture Row Mode）
// 画像をアップロードし、ソートアルゴリズムで行を並べ替える様子を可視化する

window.pictureRowCanvasRenderer = {
  instances: new Map(),        // canvasId → { canvas, ctx }
  resizeObserver: null,
  lastRenderParams: new Map(), // canvasId → 最後の描画パラメータ

  // rAFループ用（Canvas 2D フォールバック用）
  dirtyCanvases: new Set(),
  isLoopRunning: false,
  rafId: null,

  // JS 側配列コピー（Canvas 2D フォールバック用）
  arrays: new Map(), // canvasId → { main: Int32Array }

  // OffscreenCanvas + Worker
  workers: new Map(), // canvasId → { worker, lastWidth, lastHeight }

  // キャッシュされた Canvas サイズ
  cachedSizes: new Map(), // canvasId → { width, height }

  // 画像データ（Canvas 2D フォールバック用）
  _images: new Map(), // canvasId → { img: HTMLImageElement | null, numRows: number }

  // 画像 Blob キャッシュ（同じ dataUrl の atob 処理を1回に削減）
  // ComparisonMode で複数 Canvas が同じ画像を使う場合に有効
  _imageBlobCache: new Map(),   // dataUrl → { blob, mimeType }
  _imageBlobCacheKeys: [],      // FIFO キー管理（最大 5 件）
  _maxBlobCacheSize: 5,

  // ImageBitmap デコードキャッシュ（メインスレッドで 1 回だけ createImageBitmap を実行）
  // ComparisonMode の 6 インスタンスが同じ画像を使う場合に JPEG デコードを共有する
  _bitmapDecodeCache: new Map(),  // dataUrl → Promise<ImageBitmap>
  _bitmapDecodeCacheKeys: [],     // FIFO キー管理（最大 3 件）
  _maxBitmapDecodeSize: 3,

  // HSL カラー LUT（Canvas 2D fallback 用）
  _colorLUTMax: -1,
  _colorLUT: null,

  /**
   * Canvas を初期化
   * @param {string} canvasId
   * @param {boolean} useWebGL - 無視（画像行モードは常に Canvas 2D Worker を使用）
   */
  initialize: function (canvasId, useWebGL) {
    const canvas = document.getElementById(canvasId);
    if (!canvas) {
      window.debugHelper.error('PictureRow Canvas element not found:', canvasId);
      return false;
    }

    if (this.workers.has(canvasId) || this.instances.has(canvasId)) {
      window.debugHelper.warn('PictureRow Canvas already initialized:', canvasId);
      return true;
    }

    const dpr = window.devicePixelRatio || 1;
    const rect = canvas.getBoundingClientRect();
    this.cachedSizes.set(canvasId, { width: rect.width, height: rect.height });

    // OffscreenCanvas + Worker パス
    if (typeof canvas.transferControlToOffscreen === 'function') {
      canvas.width = rect.width * dpr;
      canvas.height = rect.height * dpr;

      const offscreen = canvas.transferControlToOffscreen();
      const workerUrl = new URL('js/pictureRowRenderWorker.js', document.baseURI).href;
      const worker = new Worker(workerUrl);
      worker.postMessage({ type: 'init', canvas: offscreen, dpr }, [offscreen]);

      this.workers.set(canvasId, { worker, lastWidth: canvas.width, lastHeight: canvas.height });
      this.instances.set(canvasId, { canvas, ctx: null });

      this._ensureResizeObserver();
      this.resizeObserver.observe(canvas);

      window.debugHelper.log('PictureRow Canvas initialized (Worker):', canvasId, rect.width, 'x', rect.height, 'DPR:', dpr);
      return true;
    }

    // フォールバック: Canvas 2D パス
    const ctx = canvas.getContext('2d', { alpha: false, desynchronized: true });
    canvas.width = rect.width * dpr;
    canvas.height = rect.height * dpr;
    ctx.scale(dpr, dpr);

    this.instances.set(canvasId, { canvas, ctx });
    this._ensureResizeObserver();
    this.resizeObserver.observe(canvas);

    window.debugHelper.log('PictureRow Canvas initialized (Canvas2D):', canvasId, rect.width, 'x', rect.height, 'DPR:', dpr);
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
   * @param {number} numRows - 画像を分割する行数（= 配列サイズ）
   */
  _cacheBlob: function (dataUrl, blob, mimeType) {
    if (this._imageBlobCache.has(dataUrl)) return;
    if (this._imageBlobCacheKeys.length >= this._maxBlobCacheSize) {
      const oldKey = this._imageBlobCacheKeys.shift();
      this._imageBlobCache.delete(oldKey);
    }
    this._imageBlobCache.set(dataUrl, { blob, mimeType });
    this._imageBlobCacheKeys.push(dataUrl);
  },

  /**
   * メインスレッドで createImageBitmap を 1 回だけ実行し Promise をキャッシュする。
   * ComparisonMode の複数 Worker が同じ画像を参照する場合に JPEG デコードを共有できる。
   * @param {string} dataUrl
   * @param {Blob} blob
   * @returns {Promise<ImageBitmap>}
   */
  _getBitmapDecodePromise: function (dataUrl, blob) {
    let promise = this._bitmapDecodeCache.get(dataUrl);
    if (!promise) {
      promise = createImageBitmap(blob);
      if (this._bitmapDecodeCacheKeys.length >= this._maxBitmapDecodeSize) {
        const oldKey = this._bitmapDecodeCacheKeys.shift();
        this._bitmapDecodeCache.delete(oldKey);
      }
      this._bitmapDecodeCache.set(dataUrl, promise);
      this._bitmapDecodeCacheKeys.push(dataUrl);
    }
    return promise;
  },

  setImage: async function (canvasId, dataUrl, numRows) {
    if (!dataUrl || numRows <= 0) return;

    window.debugHelper.log('PictureRow setImage:', canvasId, 'numRows:', numRows);

    const workerInfo = this.workers.get(canvasId);
    if (workerInfo) {
      // Blob キャッシュを利用（同じ dataUrl なら atob は1回だけ実行）
      let cached = this._imageBlobCache.get(dataUrl);
      if (!cached) {
        try {
          const base64 = dataUrl.split(',')[1];
          const mimeMatch = dataUrl.match(/data:([^;]+);/);
          const mimeType = mimeMatch ? mimeMatch[1] : 'image/png';
          const binaryString = atob(base64);
          const bytes = new Uint8Array(binaryString.length);
          for (let i = 0; i < binaryString.length; i++) {
            bytes[i] = binaryString.charCodeAt(i);
          }
          const blob = new Blob([bytes.buffer], { type: mimeType });
          this._cacheBlob(dataUrl, blob, mimeType);
          cached = { blob, mimeType };
        } catch (err) {
          window.debugHelper.error('PictureRow setImage blob decode error:', err);
          return;
        }
      }

      // Worker の現在の画像を即座にクリアしてバーモード fallback にする
      workerInfo.worker.postMessage({ type: 'clearImage' });

      // メインスレッドで JPEG を 1 回だけデコード（ComparisonMode の 6 Worker が共有）
      // 各 Worker には createImageBitmap(sharedBitmap) で独立コピーを転送する
      const self = this;
      this._getBitmapDecodePromise(dataUrl, cached.blob)
        .then(function (sharedBitmap) {
          // Worker が既に破棄されていれば何もしない
          if (!self.workers.has(canvasId)) return;
          // 共有 bitmap から Worker 専用コピーを生成（JPEG 再デコードなし・ピクセルコピーのみ）
          return createImageBitmap(sharedBitmap);
        })
        .then(function (workerBitmap) {
          if (!workerBitmap) return;
          const wi = self.workers.get(canvasId);
          if (!wi) {
            workerBitmap.close();
            return;
          }
          // Transferable として Worker へ転送（ゼロコピー）
          wi.worker.postMessage(
            { type: 'setImageBitmap', bitmap: workerBitmap, numRows },
            [workerBitmap]
          );
          window.debugHelper.log('PictureRow setImageBitmap sent:', canvasId, 'numRows:', numRows);
        })
        .catch(function (err) {
          window.debugHelper.error('PictureRow setImage decode error:', canvasId, err);
        });
      return;
    }

    // Canvas 2D フォールバック: HTMLImageElement を生成
    const self = this;
    let cached = this._imageBlobCache.get(dataUrl);
    if (!cached) {
      const base64 = dataUrl.split(',')[1];
      const mimeMatch = dataUrl.match(/data:([^;]+);/);
      const mimeType = mimeMatch ? mimeMatch[1] : 'image/png';
      const binaryString = atob(base64);
      const bytes = new Uint8Array(binaryString.length);
      for (let i = 0; i < binaryString.length; i++) {
        bytes[i] = binaryString.charCodeAt(i);
      }
      const blob = new Blob([bytes.buffer], { type: mimeType });
      this._cacheBlob(dataUrl, blob, mimeType);
      cached = { blob, mimeType };
    }
    const blobUrl = URL.createObjectURL(cached.blob);
    const img = new Image();
    img.onload = function () {
      URL.revokeObjectURL(blobUrl);
      self._images.set(canvasId, { img, numRows });
      const lastParams = self.lastRenderParams.get(canvasId);
      if (lastParams) {
        self.dirtyCanvases.add(canvasId);
        self.startLoop();
      }
    };
    img.src = blobUrl;
    this._images.set(canvasId, { img: null, numRows });
  },

  /**
   * 画像をクリアする
   */
  clearImage: function (canvasId) {
    this._images.delete(canvasId);
    const workerInfo = this.workers.get(canvasId);
    if (workerInfo) {
      workerInfo.worker.postMessage({ type: 'clearImage' });
    }
  },

  /**
   * 新しいソートがロードされたとき（SortVersion 変化時）
   */
  setArray: function (canvasId, mainArray, bufferArrays, compareIndices, swapIndices, readIndices, writeIndices, isSortCompleted, showCompletionHighlight) {
    const workerInfo = this.workers.get(canvasId);
    if (workerInfo) {
      workerInfo.worker.postMessage({
        type: 'setArray',
        mainArray,
        bufferArrays,
        compareIndices,
        swapIndices,
        readIndices,
        writeIndices,
        isSortCompleted: isSortCompleted || false,
        showCompletionHighlight: showCompletionHighlight || false
      });
      return;
    }
    // Canvas 2D フォールバック
    let entry = this.arrays.get(canvasId);
    if (!entry) { entry = { main: null }; this.arrays.set(canvasId, entry); }
    entry.main = new Int32Array(mainArray);
    this._scheduleRender(canvasId, compareIndices, swapIndices, readIndices, writeIndices, isSortCompleted, showCompletionHighlight);
  },

  /**
   * 通常再生フレーム（差分更新）
   */
  applyFrame: function (canvasId, mainDelta, bufferDeltas, compareIndices, swapIndices, readIndices, writeIndices, isSortCompleted, showCompletionHighlight) {
    const workerInfo = this.workers.get(canvasId);
    if (workerInfo) {
      workerInfo.worker.postMessage({
        type: 'applyFrame',
        mainDelta,
        bufferDeltas,
        compareIndices,
        swapIndices,
        readIndices,
        writeIndices,
        isSortCompleted: isSortCompleted || false,
        showCompletionHighlight: showCompletionHighlight || false
      });
      return;
    }
    // Canvas 2D フォールバック
    const entry = this.arrays.get(canvasId);
    if (!entry || !entry.main) return;
    if (mainDelta) {
      for (let k = 0; k < mainDelta.length; k += 2) {
        entry.main[mainDelta[k]] = mainDelta[k + 1];
      }
    }
    this._scheduleRender(canvasId, compareIndices, swapIndices, readIndices, writeIndices, isSortCompleted, showCompletionHighlight);
  },

  /**
   * 全量更新フォールバック（シーク後・リセット後）
   */
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
   * Canvas 2D フォールバック用の描画処理
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

    const rowH = height / n;
    const { compareIndices, swapIndices, readIndices, writeIndices,
            isSortCompleted, showCompletionHighlight } = params;

    const compareSet = new Set(compareIndices);
    const swapSet    = new Set(swapIndices);
    const readSet    = new Set(readIndices);
    const writeSet   = new Set(writeIndices);

    const imgEntry = this._images.get(canvasId);
    const img = imgEntry?.img;
    const numRows = imgEntry?.numRows || 0;

    if (img && numRows > 0 && img.complete) {
      // 画像行モード
      const srcRowH = img.naturalHeight / numRows;
      // サブピクセル描画を抑制
      ctx.imageSmoothingEnabled = false;
      // 値を 0..numRows-1 に正規化（全パターン対応）
      let minVal = array[0];
      for (let i = 1; i < n; i++) { if (array[i] < minVal) minVal = array[i]; }

      if (showCompletionHighlight) {
        for (let i = 0; i < n; i++) {
          const rowIdx = array[i] - minVal;
          if (rowIdx < 0 || rowIdx >= numRows) continue;
          const dstY = Math.round(i * rowH);
          const dstH = Math.max(1, Math.round((i + 1) * rowH) - dstY);
          ctx.drawImage(img, 0, rowIdx * srcRowH, img.naturalWidth, srcRowH, 0, dstY, width, dstH);
        }
        ctx.fillStyle = 'rgba(16,185,129,0.3)';
        ctx.fillRect(0, 0, width, height);
      } else {
        for (let i = 0; i < n; i++) {
          const rowIdx = array[i] - minVal;
          if (rowIdx < 0 || rowIdx >= numRows) continue;
          const dstY = Math.round(i * rowH);
          const dstH = Math.max(1, Math.round((i + 1) * rowH) - dstY);
          ctx.drawImage(img, 0, rowIdx * srcRowH, img.naturalWidth, srcRowH, 0, dstY, width, dstH);

          let overlay = null;
          if (swapSet.has(i))         overlay = 'rgba(239,68,68,0.55)';
          else if (compareSet.has(i)) overlay = 'rgba(168,85,247,0.5)';
          else if (writeSet.has(i))   overlay = 'rgba(249,115,22,0.45)';
          else if (readSet.has(i))    overlay = 'rgba(251,191,36,0.35)';
          if (overlay) {
            ctx.fillStyle = overlay;
            ctx.fillRect(0, dstY, width, dstH);
          }
        }
      }
    } else {
      // 画像なし: グラデーションバー
      let maxValue = 0;
      for (let i = 0; i < n; i++) { if (array[i] > maxValue) maxValue = array[i]; }
      if (maxValue === 0) maxValue = 1;
      this._buildColorLUT(maxValue);
      const colorLUT = this._colorLUT;

      if (showCompletionHighlight) {
        ctx.fillStyle = '#10B981';
        for (let i = 0; i < n; i++) ctx.fillRect(0, i * rowH, width, rowH + 0.5);
      } else {
        for (let i = 0; i < n; i++) {
          let color;
          if (swapSet.has(i))         color = '#EF4444';
          else if (compareSet.has(i)) color = '#A855F7';
          else if (writeSet.has(i))   color = '#F97316';
          else if (readSet.has(i))    color = '#FBBF24';
          else                        color = colorLUT[array[i]] || '#3B82F6';
          ctx.fillStyle = color;
          ctx.fillRect(0, i * rowH, width, rowH + 0.5);
        }
      }
    }
  },

  // ドロップゾーン登録（dropZoneId → { element, handlers }）
  dropZones: new Map(),

  /**
   * ドラッグ＆ドロップをセットアップする
   * @param {string} dropZoneId - ドロップゾーン要素のID
   * @param {DotNetObjectReference} dotNetRef - Blazor コールバック参照
   */
  setupDropZone: function (dropZoneId, dotNetRef) {
    const el = document.getElementById(dropZoneId);
    if (!el) {
      window.debugHelper.warn('PictureRow: dropZone element not found:', dropZoneId);
      return;
    }

    // 既に登録済みの場合は先に解除
    this.disposeDropZone(dropZoneId);

    let dragDepth = 0; // 子要素への出入りを無視するためのカウンタ

    const onDragEnter = (e) => {
      e.preventDefault();
      e.stopPropagation();
      dragDepth++;
      if (dragDepth === 1) dotNetRef.invokeMethodAsync('OnDragStateChanged', true);
    };

    const onDragOver = (e) => {
      e.preventDefault();
      e.stopPropagation();
      // ファイルのドロップのみ受け付ける
      if (e.dataTransfer) e.dataTransfer.dropEffect = 'copy';
    };

    const onDragLeave = (e) => {
      e.preventDefault();
      e.stopPropagation();
      dragDepth--;
      if (dragDepth <= 0) {
        dragDepth = 0;
        dotNetRef.invokeMethodAsync('OnDragStateChanged', false);
      }
    };

    const onDrop = (e) => {
      e.preventDefault();
      e.stopPropagation();
      dragDepth = 0;
      dotNetRef.invokeMethodAsync('OnDragStateChanged', false);

      const files = e.dataTransfer?.files;
      if (!files || files.length === 0) return;

      const file = files[0];
      if (!file.type.startsWith('image/')) {
        window.debugHelper.warn('PictureRow: dropped file is not an image:', file.type);
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
    window.debugHelper.log('PictureRow: dropZone registered:', dropZoneId);
  },

  /**
   * ドロップゾーンのイベントリスナーを解除する
   * @param {string} dropZoneId
   */
  disposeDropZone: function (dropZoneId) {
    const entry = this.dropZones.get(dropZoneId);
    if (!entry) return;
    const { el, onDragEnter, onDragOver, onDragLeave, onDrop } = entry;
    el.removeEventListener('dragenter', onDragEnter);
    el.removeEventListener('dragover',  onDragOver);
    el.removeEventListener('dragleave', onDragLeave);
    el.removeEventListener('drop',      onDrop);
    this.dropZones.delete(dropZoneId);
    window.debugHelper.log('PictureRow: dropZone disposed:', dropZoneId);
  },

  /**
   * Canvas を破棄する
   */
  dispose: function (canvasId) {
    window.debugHelper.log('PictureRow Canvas dispose:', canvasId);

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
