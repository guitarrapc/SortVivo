// OffscreenCanvas 画像列描画 Worker（Picture Column Mode）
// 画像を列ごとに分割し、配列インデックスに従って列を描画する
'use strict';

let offscreen = null;
let ctx = null;
let dpr = 1;

// 配列状態
let arrays = { main: null, buffers: new Map() };
let renderParams = null;
let isDirty = false;
let isLoopRunning = false;

// 画像列データ
let imageBitmap = null;    // 元画像（ImageBitmap）
let imageNumCols = 0;      // 画像を分割した列数
let pendingImageRequestId = 0; // setImage リクエスト追跡（古い非同期結果を破棄するため）

// ImageData 高速描画パス用キャッシュ
// n × drawImage（Chrome NP 200ms+）→ putImageData 1回（~10ms）に削減する
let preScaledPixels = null;   // Uint8ClampedArray: キャンバス物理高さに事前スケールした列ピクセル
let preScaledNumCols = 0;     // preScaledPixels の列数（= imageNumCols）
let preScaledPhysH = 0;       // preScaledPixels を構築した物理キャンバス高さ
let outputImageData = null;   // 再利用可能な出力 ImageData（physW × physH）
let colMappingBuffer = null;  // 再利用可能な逆引きマップ（物理列 → 配列インデックス）

// ハイライト色（RGBA）
const OVERLAY_COMPARE = 'rgba(168,85,247,0.5)';   // 紫
const OVERLAY_SWAP    = 'rgba(239,68,68,0.55)';    // 赤
const OVERLAY_WRITE   = 'rgba(249,115,22,0.45)';   // 橙
const OVERLAY_READ    = 'rgba(251,191,36,0.35)';   // 黄
const COLOR_SORTED    = 'rgba(16,185,129,0.3)';    // 緑（完了ハイライト）

// HSL カラー LUT（画像なし時のグラデーション描画用）
let colorLUTMax = -1;
let colorLUT = null;

function buildColorLUT(maxValue) {
  if (colorLUTMax === maxValue) return;
  colorLUTMax = maxValue;
  colorLUT = new Array(maxValue + 1);
  for (let v = 0; v <= maxValue; v++) {
    const hue = (v / maxValue) * 360;
    colorLUT[v] = `hsl(${hue}, 70%, 55%)`;
  }
}

// 画像のアスペクト比を維持して physW × physH キャンバス内に収まるレターボックス矩形を計算する。
// 画像未設定時はキャンバス全体を返す。
function calcLetterboxPhys(physW, physH) {
  if (!imageBitmap || imageBitmap.width <= 0 || imageBitmap.height <= 0) {
    return { offsetX: 0, offsetY: 0, renderW: physW, renderH: physH };
  }
  const imgAR = imageBitmap.width / imageBitmap.height;
  const canvasAR = physW / physH;
  let renderW, renderH;
  if (canvasAR > imgAR) {
    renderH = physH;
    renderW = Math.round(physH * imgAR);
  } else {
    renderW = physW;
    renderH = Math.round(physW / imgAR);
  }
  const offsetX = Math.round((physW - renderW) / 2);
  const offsetY = Math.round((physH - renderH) / 2);
  return { offsetX, offsetY, renderW, renderH };
}

// imageBitmap をキャンバス物理高さに事前スケールし、列ピクセルをキャッシュする。
// setImageBitmap / resize 時に呼び出すことで draw() の高速パスが有効になる。
function buildPreScaledPixels() {
  preScaledPixels = null;
  preScaledNumCols = 0;
  preScaledPhysH = 0;
  if (!imageBitmap || imageNumCols <= 0 || !offscreen) return;
  const physW = offscreen.width;
  const physH = offscreen.height;
  const lb = calcLetterboxPhys(physW, physH);
  if (lb.renderH <= 0) return;
  try {
    // imageBitmap を imageNumCols × lb.renderH の一時 OffscreenCanvas に描画して列データを抽出
    // 各ソース列が 1px 幅になることで列ピクセルを効率的にキャッシュできる
    const tmp = new OffscreenCanvas(imageNumCols, lb.renderH);
    const tmpCtx = tmp.getContext('2d', { alpha: false });
    tmpCtx.imageSmoothingEnabled = true;
    tmpCtx.drawImage(imageBitmap, 0, 0, imageNumCols, lb.renderH);
    const imgData = tmpCtx.getImageData(0, 0, imageNumCols, lb.renderH);
    preScaledPixels = imgData.data; // Uint8ClampedArray: row0_col0..colN, row1_col0..colN, ...
    preScaledNumCols = imageNumCols;
    preScaledPhysH = lb.renderH;
  } catch (_) {
    preScaledPixels = null;
  }
}

// ImageData を使った高速描画（列モード）。n × drawImage を
// Uint32 gather × physW×physH + putImageData × 1 回に置き換える。
// 戻り値: true = 描画成功、false = フォールバックが必要
function drawFast() {
  if (!preScaledPixels) return false;
  const physW = offscreen.width;
  const physH = offscreen.height;
  const lb = calcLetterboxPhys(physW, physH);
  if (preScaledPhysH !== lb.renderH) {
    buildPreScaledPixels();
    if (!preScaledPixels) return false;
  }
  const array = arrays.main;
  const n = array.length;
  if (n === 0) return true;

  if (!outputImageData || outputImageData.width !== physW || outputImageData.height !== physH) {
    outputImageData = new ImageData(physW, physH);
    colMappingBuffer = new Int32Array(physW);
  }

  const out = outputImageData.data;
  new Uint32Array(out.buffer).fill(0xFF1A1A1A);

  let minVal = array[0];
  for (let i = 1; i < n; i++) { if (array[i] < minVal) minVal = array[i]; }

  const colW_phys = lb.renderW / n;

  // 逆引きマップ構築: 各物理列に「最後に書き込む配列インデックス」を記録
  const colMap = colMappingBuffer;
  colMap.fill(-1);
  for (let i = 0; i < n; i++) {
    const dstX = lb.offsetX + Math.round(i * colW_phys);
    const dstW = Math.max(1, Math.round((i + 1) * colW_phys) - Math.round(i * colW_phys));
    const end = Math.min(dstX + dstW, lb.offsetX + lb.renderW);
    for (let c = dstX; c < end; c++) colMap[c] = i;
  }

  // Uint32 gather: 行ごとに列マップを使って各出力ピクセルのソース色を取得
  // preScaled は [row][col] レイアウト（各行が imageNumCols 列のピクセルを持つ）
  const preScaled32 = new Uint32Array(preScaledPixels.buffer);
  const out32 = new Uint32Array(out.buffer);

  for (let py = lb.offsetY; py < lb.offsetY + lb.renderH; py++) {
    const srcBase = (py - lb.offsetY) * preScaledNumCols; // この行の先頭インデックス（Uint32）
    const dstBase = py * physW;
    for (let px = lb.offsetX; px < lb.offsetX + lb.renderW; px++) {
      const i = colMap[px];
      if (i < 0) continue;
      const colIdx = array[i] - minVal;
      if (colIdx < 0 || colIdx >= preScaledNumCols) continue;
      out32[dstBase + px] = preScaled32[srcBase + colIdx];
    }
  }

  ctx.putImageData(outputImageData, 0, 0);

  const { compareIndices, swapIndices, readIndices, writeIndices, showCompletionHighlight } = renderParams;
  const cssOffsetX = lb.offsetX / dpr;
  const cssOffsetY = lb.offsetY / dpr;
  const cssRenderW = lb.renderW / dpr;
  const cssRenderH = lb.renderH / dpr;
  if (showCompletionHighlight) {
    ctx.fillStyle = COLOR_SORTED;
    ctx.fillRect(cssOffsetX, cssOffsetY, cssRenderW, cssRenderH);
  } else {
    const colW_css = cssRenderW / n;
    const applyOverlay = function (indices, color) {
      if (!indices || indices.length === 0) return;
      ctx.fillStyle = color;
      for (let k = 0; k < indices.length; k++) {
        const idx = indices[k];
        const dx = cssOffsetX + Math.round(idx * colW_css);
        const dw = Math.max(1, Math.round((idx + 1) * colW_css) - Math.round(idx * colW_css));
        ctx.fillRect(dx, cssOffsetY, dw, cssRenderH);
      }
    };
    applyOverlay(swapIndices, OVERLAY_SWAP);
    applyOverlay(compareIndices, OVERLAY_COMPARE);
    applyOverlay(writeIndices, OVERLAY_WRITE);
    applyOverlay(readIndices, OVERLAY_READ);
  }

  return true;
}

const _raf = typeof requestAnimationFrame !== 'undefined'
  ? (cb) => requestAnimationFrame(cb)
  : (cb) => setTimeout(cb, 1000 / 60);

function scheduleDraw() {
  isDirty = true;
  if (!isLoopRunning) {
    isLoopRunning = true;
    _raf(drawLoop);
  }
}

function drawLoop() {
  if (isDirty && renderParams && arrays.main) {
    isDirty = false;
    draw();
  }
  if (isDirty) {
    _raf(drawLoop);
  } else {
    isLoopRunning = false;
  }
}

function draw() {
if (!offscreen || !ctx || !arrays.main) return;

// 画像モード: ImageData 高速パス（n × drawImage → putImageData 1回）
if (imageBitmap && imageNumCols > 0 && drawFast()) return;

const W = offscreen.width;
  const H = offscreen.height;
  const cssW = W / dpr;
  const cssH = H / dpr;

  ctx.fillStyle = '#1A1A1A';
  ctx.fillRect(0, 0, cssW, cssH);

  const { compareIndices, swapIndices, readIndices, writeIndices,
          isSortCompleted, showCompletionHighlight } = renderParams;

  const array = arrays.main;
  const n = array.length;
  if (n === 0) return;

  const colW = cssW / n;

  const compareSet = new Set(compareIndices);
  const swapSet    = new Set(swapIndices);
  const readSet    = new Set(readIndices);
  const writeSet   = new Set(writeIndices);


  if (imageBitmap && imageNumCols > 0) {
    // ── 画像列モード ──────────────────────────────────────────────────
    const imgW = imageBitmap.width;
    const imgH = imageBitmap.height;
    const srcColW = imgW / imageNumCols;
    const lb = calcLetterboxPhys(W, H);
    const cssOffsetX = lb.offsetX / dpr;
    const cssOffsetY = lb.offsetY / dpr;
    const cssRenderW = lb.renderW / dpr;
    const cssRenderH = lb.renderH / dpr;
    const colW = cssRenderW / n;

    // サブピクセル描画を抑制
    ctx.imageSmoothingEnabled = false;

    // 値を 0..imageNumCols-1 に正規化（全パターン対応）
    let minVal = array[0];
    for (let i = 1; i < n; i++) { if (array[i] < minVal) minVal = array[i]; }

    if (showCompletionHighlight) {
      for (let i = 0; i < n; i++) {
        const colIdx = array[i] - minVal;
        if (colIdx < 0 || colIdx >= imageNumCols) continue;
        const srcX = colIdx * srcColW;
        const localX = Math.round(i * colW);
        const dstX = cssOffsetX + localX;
        const dstW = Math.max(1, Math.round((i + 1) * colW) - localX);
        ctx.drawImage(imageBitmap, srcX, 0, srcColW, imgH, dstX, cssOffsetY, dstW, cssRenderH);
      }
      ctx.fillStyle = COLOR_SORTED;
      ctx.fillRect(cssOffsetX, cssOffsetY, cssRenderW, cssRenderH);
    } else {
      for (let i = 0; i < n; i++) {
        const colIdx = array[i] - minVal;
        if (colIdx < 0 || colIdx >= imageNumCols) continue;
        const srcX = colIdx * srcColW;
        const localX = Math.round(i * colW);
        const dstX = cssOffsetX + localX;
        const dstW = Math.max(1, Math.round((i + 1) * colW) - localX);

        // 画像列を描画
        ctx.drawImage(imageBitmap, srcX, 0, srcColW, imgH, dstX, cssOffsetY, dstW, cssRenderH);

        // ハイライトオーバーレイ
        let overlay = null;
        if (swapSet.has(i))         overlay = OVERLAY_SWAP;
        else if (compareSet.has(i)) overlay = OVERLAY_COMPARE;
        else if (writeSet.has(i))   overlay = OVERLAY_WRITE;
        else if (readSet.has(i))    overlay = OVERLAY_READ;

        if (overlay) {
          ctx.fillStyle = overlay;
          ctx.fillRect(dstX, cssOffsetY, dstW, cssRenderH);
        }
      }
    }
  } else {
    // ── 画像なし: グラデーション縦バーで表示 ──────────────────────────
    let maxValue = 0;
    for (let i = 0; i < n; i++) {
      if (array[i] > maxValue) maxValue = array[i];
    }
    if (maxValue === 0) maxValue = 1;
    buildColorLUT(maxValue);

    if (showCompletionHighlight) {
      ctx.fillStyle = '#10B981';
      for (let i = 0; i < n; i++) {
        ctx.fillRect(i * colW, 0, colW + 0.5, cssH);
      }
    } else {
      for (let i = 0; i < n; i++) {
        let color;
        if (swapSet.has(i))         color = '#EF4444';
        else if (compareSet.has(i)) color = '#A855F7';
        else if (writeSet.has(i))   color = '#F97316';
        else if (readSet.has(i))    color = '#FBBF24';
        else                        color = colorLUT[array[i]] || '#3B82F6';

        ctx.fillStyle = color;
        ctx.fillRect(i * colW, 0, colW + 0.5, cssH);
      }
    }
  }
}

self.onmessage = function (e) {
  const msg = e.data;
  switch (msg.type) {
    case 'init': {
      offscreen = msg.canvas;
      dpr = msg.dpr || 1;
      ctx = offscreen.getContext('2d', { alpha: false });
      ctx.scale(dpr, dpr);
      break;
    }

    case 'setImage': {
      // Blob（優先）または ArrayBuffer（後方互換）から ImageBitmap を生成
      const blobOrBuffer = msg.imageBlob
        ? msg.imageBlob
        : (msg.imageBuffer ? new Blob([msg.imageBuffer], { type: msg.mimeType || 'image/png' }) : null);
      if (!blobOrBuffer) break;

      // 古い bitmap を即座に無効化し、createImageBitmap 完了前に setArray が届いても
      // draw() がバーモード fallback になるようにする（下半分ブラック防止）
      const requestId = ++pendingImageRequestId;
      if (imageBitmap) { imageBitmap.close(); }
      imageBitmap = null;
      imageNumCols = 0;
      preScaledPixels = null;
      if (arrays.main && renderParams) scheduleDraw();

      createImageBitmap(blobOrBuffer).then(function (bmp) {
        if (requestId !== pendingImageRequestId) { bmp.close(); return; }
        imageBitmap = bmp;
        imageNumCols = msg.numCols;
        buildPreScaledPixels();
        if (arrays.main && renderParams) scheduleDraw();
      }).catch(function () {
        if (requestId !== pendingImageRequestId) return;
        imageBitmap = null;
        imageNumCols = 0;
        if (arrays.main && renderParams) scheduleDraw();
      });
      break;
    }

    case 'clearImage': {
      if (imageBitmap) { imageBitmap.close(); }
      imageBitmap = null;
      imageNumCols = 0;
      preScaledPixels = null;
      preScaledNumCols = 0;
      preScaledPhysH = 0;
      if (arrays.main && renderParams) scheduleDraw();
      break;
    }

    case 'setImageBitmap': {
      if (imageBitmap) { imageBitmap.close(); }
      imageBitmap = msg.bitmap;
      imageNumCols = msg.numCols;
      buildPreScaledPixels();
      if (arrays.main && renderParams) scheduleDraw();
      break;
    }

    case 'clearImage': {
      imageBitmap = null;
      imageNumCols = 0;
      if (arrays.main && renderParams) scheduleDraw();
      break;
    }

    case 'setArray': {
      arrays.main = new Int32Array(msg.mainArray);
      arrays.buffers.clear();
      if (msg.bufferArrays) {
        for (const [idStr, arr] of Object.entries(msg.bufferArrays)) {
          arrays.buffers.set(parseInt(idStr), new Int32Array(arr));
        }
      }
      renderParams = {
        compareIndices: msg.compareIndices,
        swapIndices: msg.swapIndices,
        readIndices: msg.readIndices,
        writeIndices: msg.writeIndices,
        isSortCompleted: msg.isSortCompleted || false,
        showCompletionHighlight: msg.showCompletionHighlight || false
      };
      scheduleDraw();
      break;
    }

    case 'applyFrame': {
      if (!arrays.main) break;
      if (msg.mainDelta) {
        const delta = msg.mainDelta;
        for (let k = 0; k < delta.length; k += 2) {
          arrays.main[delta[k]] = delta[k + 1];
        }
      }
      if (msg.bufferDeltas) {
        for (const [idStr, delta] of Object.entries(msg.bufferDeltas)) {
          const bid = parseInt(idStr);
          let buf = arrays.buffers.get(bid);
          if (!buf) {
            buf = new Int32Array(arrays.main.length);
            arrays.buffers.set(bid, buf);
          }
          for (let k = 0; k < delta.length; k += 2) {
            buf[delta[k]] = delta[k + 1];
          }
        }
      }
      if (msg.isSortCompleted && arrays.buffers.size > 0) {
        arrays.buffers.clear();
      }
      renderParams = {
        compareIndices: msg.compareIndices,
        swapIndices: msg.swapIndices,
        readIndices: msg.readIndices,
        writeIndices: msg.writeIndices,
        isSortCompleted: msg.isSortCompleted || false,
        showCompletionHighlight: msg.showCompletionHighlight || false
      };
      scheduleDraw();
      break;
    }

    case 'resize': {
      if (offscreen) {
        offscreen.width = msg.newWidth;
        offscreen.height = msg.newHeight;
        dpr = msg.dpr || dpr;
        ctx.scale(dpr, dpr);
        buildPreScaledPixels();
        if (renderParams && arrays.main) scheduleDraw();
      }
      break;
    }

    case 'dispose': {
      offscreen = null;
      ctx = null;
      arrays = { main: null, buffers: new Map() };
      renderParams = null;
      if (imageBitmap) { imageBitmap.close(); }
      imageBitmap = null;
      imageNumCols = 0;
      pendingImageRequestId = 0;
      preScaledPixels = null;
      preScaledNumCols = 0;
      preScaledPhysH = 0;
      outputImageData = null;
      colMappingBuffer = null;
      break;
    }
  }
};
