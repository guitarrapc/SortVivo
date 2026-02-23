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

    if (showCompletionHighlight) {
      for (let i = 0; i < n; i++) {
        const blockIdx = array[i] - minVal;
        if (blockIdx < 0 || blockIdx >= imageNumBlocks) continue;
        const dstRow = Math.floor(i / cols);
        const dstCol = i % cols;
        const srcRow = Math.floor(blockIdx / srcCols);
        const srcCol = blockIdx % srcCols;
        ctx.drawImage(
          imageBitmap,
          srcCol * srcBlockW, srcRow * srcBlockH, srcBlockW, srcBlockH,
          dstCol * blockW,    dstRow * blockH,    blockW,    blockH
        );
      }
      ctx.fillStyle = COLOR_SORTED;
      ctx.fillRect(0, 0, cssW, cssH);
    } else {
      for (let i = 0; i < n; i++) {
        const blockIdx = array[i] - minVal;
        if (blockIdx < 0 || blockIdx >= imageNumBlocks) continue;
        const dstRow = Math.floor(i / cols);
        const dstCol = i % cols;
        const dstX   = dstCol * blockW;
        const dstY   = dstRow * blockH;
        const srcRow = Math.floor(blockIdx / srcCols);
        const srcCol = blockIdx % srcCols;
        ctx.drawImage(
          imageBitmap,
          srcCol * srcBlockW, srcRow * srcBlockH, srcBlockW, srcBlockH,
          dstX,               dstY,               blockW,    blockH
        );

        // ハイライトオーバーレイ
        let overlay = null;
        if (swapSet.has(i))         overlay = OVERLAY_SWAP;
        else if (compareSet.has(i)) overlay = OVERLAY_COMPARE;
        else if (writeSet.has(i))   overlay = OVERLAY_WRITE;
        else if (readSet.has(i))    overlay = OVERLAY_READ;

        if (overlay) {
          ctx.fillStyle = overlay;
          ctx.fillRect(dstX, dstY, blockW + 0.5, blockH + 0.5);
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
      const blob = new Blob([msg.imageBuffer], { type: msg.mimeType || 'image/png' });
      createImageBitmap(blob).then(function (bmp) {
        imageBitmap = bmp;
        imageNumBlocks = msg.numBlocks;
        if (arrays.main && renderParams) scheduleDraw();
      }).catch(function () {
        imageBitmap = null;
        imageNumBlocks = 0;
        if (arrays.main && renderParams) scheduleDraw();
      });
      break;
    }

    case 'clearImage': {
      imageBitmap = null;
      imageNumBlocks = 0;
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
        if (renderParams && arrays.main) scheduleDraw();
      }
      break;
    }

    case 'dispose': {
      offscreen = null;
      ctx = null;
      arrays = { main: null, buffers: new Map() };
      renderParams = null;
      imageBitmap = null;
      imageNumBlocks = 0;
      break;
    }
  }
};
