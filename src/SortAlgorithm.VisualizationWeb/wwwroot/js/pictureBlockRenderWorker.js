// OffscreenCanvas 画像ブロック描画 Worker（Picture Block Mode）
// 画像を 2D グリッドのブロックに分割し、配列インデックスに従ってブロックを描画する
'use strict';

let offscreen = null;
let ctx = null;
let dpr = 1;

// 配列状態
let arrays = { main: null, buffers: new Map() };
let renderParams = null;
let isDirty = false;
let isLoopRunning = false;

// 画像ブロックデータ
let imageBitmap = null;    // 元画像（ImageBitmap）
let imageNumBlocks = 0;    // 総ブロック数（= 配列サイズ）
let pendingImageRequestId = 0; // setImage リクエスト追跡（古い非同期結果を破棄するため）

// ImageData 高速描画パス用キャッシュ
// n × drawImage（Chrome NP 200ms+）→ putImageData 1回（~10ms）に削減する
let preScaledPixels = null;   // Uint8ClampedArray: physW × physH に事前スケール済み画像
let preScaledPhysW = 0;       // preScaledPixels を構築した物理キャンバス幅
let preScaledPhysH = 0;       // preScaledPixels を構築した物理キャンバス高さ
let outputImageData = null;   // 再利用可能な出力 ImageData（physW × physH）

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

/**
 * n 個のブロックを並べるグリッド寸法を計算する
 * cols = ceil(sqrt(n)), rows = ceil(n / cols)
 */
function calcGrid(n) {
  const cols = Math.ceil(Math.sqrt(n));
  const rows = Math.ceil(n / cols);
  return { cols, rows };
}

/**
 * 画像のアスペクト比を維持して physW × physH キャンバス内に収まるレターボックス矩形を計算する。
 * 画像未設定時はキャンバス全体を返す。
 */
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

/**
 * imageBitmap をレターボックス領域にスケールしてピクセルをキャッシュする。
 * ソート済み配列の状態でブロック i が (i%cols, floor(i/cols)) の位置に配置される。
 * setImageBitmap / resize 時に呼び出す。
 */
function buildPreScaledPixels() {
  preScaledPixels = null;
  preScaledPhysW = 0;
  preScaledPhysH = 0;
  if (!imageBitmap || imageNumBlocks <= 0 || !offscreen) return;
  const physW = offscreen.width;
  const physH = offscreen.height;
  const lb = calcLetterboxPhys(physW, physH);
  if (lb.renderW <= 0 || lb.renderH <= 0) return;
  try {
    const tmp = new OffscreenCanvas(lb.renderW, lb.renderH);
    const tmpCtx = tmp.getContext('2d', { alpha: false });
    tmpCtx.imageSmoothingEnabled = true;
    tmpCtx.drawImage(imageBitmap, 0, 0, lb.renderW, lb.renderH);
    const imgData = tmpCtx.getImageData(0, 0, lb.renderW, lb.renderH);
    preScaledPixels = imgData.data;
    preScaledPhysW = lb.renderW;
    preScaledPhysH = lb.renderH;
  } catch (_) {
    preScaledPixels = null;
  }
}

/**
 * ImageData を使った高速描画（ブロックモード）。n × drawImage を
 * ブロック単位 set() コピー + putImageData × 1 回に置き換える。
 * 戻り値: true = 描画成功、false = フォールバックが必要
 */
function drawFast() {
  if (!preScaledPixels) return false;
  const physW = offscreen.width;
  const physH = offscreen.height;
  const lb = calcLetterboxPhys(physW, physH);
  if (preScaledPhysW !== lb.renderW || preScaledPhysH !== lb.renderH) {
    buildPreScaledPixels();
    if (!preScaledPixels) return false;
  }
  const array = arrays.main;
  const n = array.length;
  if (n === 0) return true;

  if (!outputImageData || outputImageData.width !== physW || outputImageData.height !== physH) {
    outputImageData = new ImageData(physW, physH);
  }

  const out = outputImageData.data;
  new Uint32Array(out.buffer).fill(0xFF1A1A1A);

  let minVal = array[0];
  for (let i = 1; i < n; i++) { if (array[i] < minVal) minVal = array[i]; }

  const { cols, rows } = calcGrid(n);
  const blockPhysW = lb.renderW / cols;
  const blockPhysH = lb.renderH / rows;

  const preScaled32 = new Uint32Array(preScaledPixels.buffer);
  const out32 = new Uint32Array(out.buffer);

  for (let i = 0; i < n; i++) {
    const blockIdx = array[i] - minVal;
    if (blockIdx < 0 || blockIdx >= imageNumBlocks) continue;
    const dstRow = Math.floor(i / cols);
    const dstCol = i % cols;
    const srcRow = Math.floor(blockIdx / cols);
    const srcCol = blockIdx % cols;

    const dstLocalX = Math.round(dstCol * blockPhysW);
    const dstLocalY = Math.round(dstRow * blockPhysH);
    const dstX = lb.offsetX + dstLocalX;
    const dstY = lb.offsetY + dstLocalY;
    const dstW = Math.max(1, Math.round((dstCol + 1) * blockPhysW) - dstLocalX);
    const dstH = Math.max(1, Math.round((dstRow + 1) * blockPhysH) - dstLocalY);
    const srcX = Math.round(srcCol * blockPhysW);
    const srcY = Math.round(srcRow * blockPhysH);

    for (let r = 0; r < dstH; r++) {
      if (dstY + r >= physH || srcY + r >= lb.renderH) break;
      const sBase = (srcY + r) * lb.renderW + srcX;
      const dBase = (dstY + r) * physW + dstX;
      out32.set(preScaled32.subarray(sBase, sBase + dstW), dBase);
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
    const blockCssW = cssRenderW / cols;
    const blockCssH = cssRenderH / rows;
    const applyOverlay = function (indices, color) {
      if (!indices || indices.length === 0) return;
      ctx.fillStyle = color;
      for (let k = 0; k < indices.length; k++) {
        const idx = indices[k];
        const dRow = Math.floor(idx / cols);
        const dCol = idx % cols;
        const dx = cssOffsetX + Math.round(dCol * blockCssW);
        const dy = cssOffsetY + Math.round(dRow * blockCssH);
        const dw = Math.max(1, Math.round((dCol + 1) * blockCssW) - Math.round(dCol * blockCssW));
        const dh = Math.max(1, Math.round((dRow + 1) * blockCssH) - Math.round(dRow * blockCssH));
        ctx.fillRect(dx, dy, dw, dh);
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
if (imageBitmap && imageNumBlocks > 0 && drawFast()) return;

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

  const { cols, rows } = calcGrid(n);
  const blockW = cssW / cols;
  const blockH = cssH / rows;

  const compareSet = new Set(compareIndices);
  const swapSet    = new Set(swapIndices);
  const readSet    = new Set(readIndices);
  const writeSet   = new Set(writeIndices);

  // 値を 0..n-1 に正規化（全パターン対応）
  let minVal = array[0];
  for (let i = 1; i < n; i++) { if (array[i] < minVal) minVal = array[i]; }

  if (imageBitmap && imageNumBlocks > 0) {
    // ── 画像ブロックモード ────────────────────────────────────────────
    const imgW = imageBitmap.width;
    const imgH = imageBitmap.height;
    const { cols: srcCols, rows: srcRows } = calcGrid(imageNumBlocks);
    const srcBlockW = imgW / srcCols;
    const srcBlockH = imgH / srcRows;
    const lb = calcLetterboxPhys(W, H);
    const cssOffsetX = lb.offsetX / dpr;
    const cssOffsetY = lb.offsetY / dpr;
    const cssRenderW = lb.renderW / dpr;
    const cssRenderH = lb.renderH / dpr;
    const blockW = cssRenderW / cols;
    const blockH = cssRenderH / rows;

    // サブピクセル描画を抹消
    ctx.imageSmoothingEnabled = false;

    if (showCompletionHighlight) {
      for (let i = 0; i < n; i++) {
        const blockIdx = array[i] - minVal;
        if (blockIdx < 0 || blockIdx >= imageNumBlocks) continue;
        const dstRow = Math.floor(i / cols);
        const dstCol = i % cols;
        const srcRow = Math.floor(blockIdx / srcCols);
        const srcCol = blockIdx % srcCols;
        const localX = Math.round(dstCol * blockW);
        const localY = Math.round(dstRow * blockH);
        const dstX = cssOffsetX + localX;
        const dstY = cssOffsetY + localY;
        const dstW = Math.max(1, Math.round((dstCol + 1) * blockW) - localX);
        const dstH = Math.max(1, Math.round((dstRow + 1) * blockH) - localY);
        ctx.drawImage(
          imageBitmap,
          srcCol * srcBlockW, srcRow * srcBlockH, srcBlockW, srcBlockH,
          dstX, dstY, dstW, dstH
        );
      }
      ctx.fillStyle = COLOR_SORTED;
      ctx.fillRect(cssOffsetX, cssOffsetY, cssRenderW, cssRenderH);
    } else {
      for (let i = 0; i < n; i++) {
        const blockIdx = array[i] - minVal;
        if (blockIdx < 0 || blockIdx >= imageNumBlocks) continue;
        const dstRow = Math.floor(i / cols);
        const dstCol = i % cols;
        const localX = Math.round(dstCol * blockW);
        const localY = Math.round(dstRow * blockH);
        const dstX = cssOffsetX + localX;
        const dstY = cssOffsetY + localY;
        const dstW = Math.max(1, Math.round((dstCol + 1) * blockW) - localX);
        const dstH = Math.max(1, Math.round((dstRow + 1) * blockH) - localY);
        const srcRow = Math.floor(blockIdx / srcCols);
        const srcCol = blockIdx % srcCols;
        ctx.drawImage(
          imageBitmap,
          srcCol * srcBlockW, srcRow * srcBlockH, srcBlockW, srcBlockH,
          dstX, dstY, dstW, dstH
        );

        // ハイライトオーバーレイ
        let overlay = null;
        if (swapSet.has(i))         overlay = OVERLAY_SWAP;
        else if (compareSet.has(i)) overlay = OVERLAY_COMPARE;
        else if (writeSet.has(i))   overlay = OVERLAY_WRITE;
        else if (readSet.has(i))    overlay = OVERLAY_READ;

        if (overlay) {
          ctx.fillStyle = overlay;
          ctx.fillRect(dstX, dstY, dstW, dstH);
        }
      }
    }
  } else {
    // ── 画像なし: 色付きブロックで表示 ──────────────────────────────
    let maxValue = 0;
    for (let i = 0; i < n; i++) {
      if (array[i] > maxValue) maxValue = array[i];
    }
    if (maxValue === 0) maxValue = 1;
    buildColorLUT(maxValue);

    if (showCompletionHighlight) {
      ctx.fillStyle = '#10B981';
      for (let i = 0; i < n; i++) {
        const dstRow = Math.floor(i / cols);
        const dstCol = i % cols;
        ctx.fillRect(dstCol * blockW, dstRow * blockH, blockW + 0.5, blockH + 0.5);
      }
    } else {
      for (let i = 0; i < n; i++) {
        let color;
        if (swapSet.has(i))         color = '#EF4444';
        else if (compareSet.has(i)) color = '#A855F7';
        else if (writeSet.has(i))   color = '#F97316';
        else if (readSet.has(i))    color = '#FBBF24';
        else                        color = colorLUT[array[i]] || '#3B82F6';

        const dstRow = Math.floor(i / cols);
        const dstCol = i % cols;
        ctx.fillStyle = color;
        ctx.fillRect(dstCol * blockW, dstRow * blockH, blockW + 0.5, blockH + 0.5);
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
      const blobOrBuffer = msg.imageBlob
        ? msg.imageBlob
        : (msg.imageBuffer ? new Blob([msg.imageBuffer], { type: msg.mimeType || 'image/png' }) : null);
      if (!blobOrBuffer) break;

      const requestId = ++pendingImageRequestId;
      if (imageBitmap) { imageBitmap.close(); }
      imageBitmap = null;
      imageNumBlocks = 0;
      preScaledPixels = null;
      if (arrays.main && renderParams) scheduleDraw();

      createImageBitmap(blobOrBuffer).then(function (bmp) {
        if (requestId !== pendingImageRequestId) { bmp.close(); return; }
        imageBitmap = bmp;
        imageNumBlocks = msg.numBlocks;
        buildPreScaledPixels();
        if (arrays.main && renderParams) scheduleDraw();
      }).catch(function () {
        if (requestId !== pendingImageRequestId) return;
        imageBitmap = null;
        imageNumBlocks = 0;
        if (arrays.main && renderParams) scheduleDraw();
      });
      break;
    }

    case 'clearImage': {
      if (imageBitmap) { imageBitmap.close(); }
      imageBitmap = null;
      imageNumBlocks = 0;
      preScaledPixels = null;
      preScaledPhysW = 0;
      preScaledPhysH = 0;
      if (arrays.main && renderParams) scheduleDraw();
      break;
    }

    case 'setImageBitmap': {
      if (imageBitmap) { imageBitmap.close(); }
      imageBitmap = msg.bitmap;
      imageNumBlocks = msg.numBlocks;
      buildPreScaledPixels();
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
        offscreen.width  = msg.newWidth;
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
      imageNumBlocks = 0;
      pendingImageRequestId = 0;
      preScaledPixels = null;
      preScaledPhysW = 0;
      preScaledPhysH = 0;
      outputImageData = null;
      break;
    }
  }
};
