// OffscreenCanvas 画像行描画 Worker（Picture Row Mode）
// 画像を行ごとに分割し、配列インデックスに従って行を描画する
'use strict';

let offscreen = null;
let ctx = null;
let dpr = 1;

// 配列状態
let arrays = { main: null, buffers: new Map() };
let renderParams = null;
let isDirty = false;
let isLoopRunning = false;

// 画像行データ
let imageBitmap = null;    // 元画像（ImageBitmap）
let imageNumRows = 0;      // 画像を分割した行数
let pendingImageRequestId = 0; // setImage リクエスト追跡（古い非同期結果を破棄するため）

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

  const rowH = cssH / n;

  const compareSet = new Set(compareIndices);
  const swapSet    = new Set(swapIndices);
  const readSet    = new Set(readIndices);
  const writeSet   = new Set(writeIndices);

  if (imageBitmap && imageNumRows > 0) {
    // ── 画像行モード ──────────────────────────────────────────────────
    const imgW = imageBitmap.width;
    const imgH = imageBitmap.height;
    const srcRowH = imgH / imageNumRows;

    // サブピクセル描画を抑制（アンチエイリアス無効で画質向上・高速化）
    ctx.imageSmoothingEnabled = false;

    // 値を 0..imageNumRows-1 に正規化（全パターン対応）
    let minVal = array[0];
    for (let i = 1; i < n; i++) { if (array[i] < minVal) minVal = array[i]; }

    if (showCompletionHighlight) {
      for (let i = 0; i < n; i++) {
        const rowIdx = array[i] - minVal;
        if (rowIdx < 0 || rowIdx >= imageNumRows) continue;
        const srcY = rowIdx * srcRowH;
        // Math.round で整数ピクセル境界に正規化（隙間/わしかまり防止）
        const dstY = Math.round(i * rowH);
        const dstH = Math.max(1, Math.round((i + 1) * rowH) - dstY);
        ctx.drawImage(imageBitmap, 0, srcY, imgW, srcRowH, 0, dstY, cssW, dstH);
      }
      ctx.fillStyle = COLOR_SORTED;
      ctx.fillRect(0, 0, cssW, cssH);
    } else {
      for (let i = 0; i < n; i++) {
        const rowIdx = array[i] - minVal;
        if (rowIdx < 0 || rowIdx >= imageNumRows) continue;
        const srcY = rowIdx * srcRowH;

        // Math.round で整数ピクセル境界に正規化
        const dstY = Math.round(i * rowH);
        const dstH = Math.max(1, Math.round((i + 1) * rowH) - dstY);

        // 画像行を描画
        ctx.drawImage(imageBitmap, 0, srcY, imgW, srcRowH, 0, dstY, cssW, dstH);

        // ハイライトオーバーレイ
        let overlay = null;
        if (swapSet.has(i))    overlay = OVERLAY_SWAP;
        else if (compareSet.has(i)) overlay = OVERLAY_COMPARE;
        else if (writeSet.has(i))   overlay = OVERLAY_WRITE;
        else if (readSet.has(i))    overlay = OVERLAY_READ;

        if (overlay) {
          ctx.fillStyle = overlay;
          ctx.fillRect(0, dstY, cssW, dstH);
        }
      }
    }
  } else {
    // ── 画像なし: グラデーションバーで表示 ────────────────────────────
    let maxValue = 0;
    for (let i = 0; i < n; i++) {
      if (array[i] > maxValue) maxValue = array[i];
    }
    if (maxValue === 0) maxValue = 1;
    buildColorLUT(maxValue);

    if (showCompletionHighlight) {
      ctx.fillStyle = '#10B981';
      for (let i = 0; i < n; i++) {
        ctx.fillRect(0, i * rowH, cssW, rowH + 0.5);
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
        ctx.fillRect(0, i * rowH, cssW, rowH + 0.5);
      }
    }
  }

  // ソート完了後（アニメーション終了後）: バッファー不要なので何もしない
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
      imageNumRows = 0;
      if (arrays.main && renderParams) scheduleDraw();

      createImageBitmap(blobOrBuffer).then(function (bmp) {
        // 後続の setImage が来ていた場合は古い結果を破棄
        if (requestId !== pendingImageRequestId) { bmp.close(); return; }
        imageBitmap = bmp;
        imageNumRows = msg.numRows;
        if (arrays.main && renderParams) scheduleDraw();
      }).catch(function (err) {
        if (requestId !== pendingImageRequestId) return;
        // ImageBitmap 生成失敗時は画像なしモードで描画
        imageBitmap = null;
        imageNumRows = 0;
        if (arrays.main && renderParams) scheduleDraw();
      });
      break;
    }

    case 'clearImage': {
      if (imageBitmap) { imageBitmap.close(); }
      imageBitmap = null;
      imageNumRows = 0;
      if (arrays.main && renderParams) scheduleDraw();
      break;
    }

    case 'setImageBitmap': {
      // メインスレッドでデコード済みの ImageBitmap を直接受け取る（非同期処理不要）
      if (imageBitmap) { imageBitmap.close(); }
      imageBitmap = msg.bitmap;
      imageNumRows = msg.numRows;
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
      imageNumRows = 0;
      pendingImageRequestId = 0;
      break;
    }
  }
};
