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

// ImageData 高速描画パス用キャッシュ
// n × drawImage（Chrome NP 200ms+）→ putImageData 1回（~10ms）に削減する
let preScaledPixels = null;   // Uint8ClampedArray: キャンバス物理幅に事前スケールした行ピクセル
let preScaledNumRows = 0;     // preScaledPixels の行数（= imageNumRows）
let preScaledPhysW = 0;       // preScaledPixels を構築した物理キャンバス幅
let outputImageData = null;   // 再利用可能な出力 ImageData（physW × physH）
let rowMappingBuffer = null;  // 再利用可能な逆引きマップ（物理行 → 配列インデックス）

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

// imageBitmap をキャンバス物理幅に事前スケールし、行ピクセルをキャッシュする。
// setImageBitmap / resize 時に呼び出すことで draw() の高速パスが有効になる。
function buildPreScaledPixels() {
  preScaledPixels = null;
  preScaledNumRows = 0;
  preScaledPhysW = 0;
  if (!imageBitmap || imageNumRows <= 0 || !offscreen) return;
  const physW = offscreen.width;
  const physH = offscreen.height;
  const lb = calcLetterboxPhys(physW, physH);
  if (lb.renderW <= 0) return;
  try {
    // imageBitmap を lb.renderW × imageNumRows の一時 OffscreenCanvas に描画して行データを抽出
    const tmp = new OffscreenCanvas(lb.renderW, imageNumRows);
    const tmpCtx = tmp.getContext('2d', { alpha: false });
    tmpCtx.imageSmoothingEnabled = true;
    tmpCtx.drawImage(imageBitmap, 0, 0, lb.renderW, imageNumRows);
    const imgData = tmpCtx.getImageData(0, 0, lb.renderW, imageNumRows);
    preScaledPixels = imgData.data; // Uint8ClampedArray: row0_pixels, row1_pixels, ...
    preScaledNumRows = imageNumRows;
    preScaledPhysW = lb.renderW;
  } catch (_) {
    preScaledPixels = null;
  }
}

// ImageData を使った高速描画。n × drawImage（GPU コマンド n 個）を
// TypedArray.set() × physH 回 + putImageData × 1 回に置き換える。
// Chrome DevTools で NP > 200ms を示すボトルネックを解消する。
// 戻り値: true = 描画成功、false = フォールバックが必要
function drawFast() {
  if (!preScaledPixels) return false;
  const physW = offscreen.width;
  const physH = offscreen.height;
  const lb = calcLetterboxPhys(physW, physH);
  // キャンバスサイズまたはレターボックスが変わっていたらキャッシュを再構築
  if (preScaledPhysW !== lb.renderW) {
    buildPreScaledPixels();
    if (!preScaledPixels) return false;
  }
  const array = arrays.main;
  const n = array.length;
  if (n === 0) return true;

  // 出力バッファを再利用（サイズ変化時のみ再確保）
  if (!outputImageData || outputImageData.width !== physW || outputImageData.height !== physH) {
    outputImageData = new ImageData(physW, physH);
    rowMappingBuffer = new Int32Array(physH);
  }

  const out = outputImageData.data;
  const srcStride = lb.renderW * 4;  // 事前スケール済みピクセルの行幅（レターボックス幅）
  const dstStride = physW * 4;       // 出力バッファの行幅（キャンバス全幅）

  // 背景色 #1A1A1A で初期化（RGBA リトルエンディアン: R=0x1A,G=0x1A,B=0x1A,A=0xFF）
  new Uint32Array(out.buffer).fill(0xFF1A1A1A);

  // 最小値を求めて行インデックスを正規化
  let minVal = array[0];
  for (let i = 1; i < n; i++) { if (array[i] < minVal) minVal = array[i]; }

  const rowH_phys = lb.renderH / n;

  // 逆引きマップ構築
  const rowMap = rowMappingBuffer;
  rowMap.fill(-1);
  for (let i = 0; i < n; i++) {
    const dstY = lb.offsetY + Math.round(i * rowH_phys);
    const dstH = Math.max(1, Math.round((i + 1) * rowH_phys) - Math.round(i * rowH_phys));
    const end = Math.min(dstY + dstH, lb.offsetY + lb.renderH);
    for (let r = dstY; r < end; r++) rowMap[r] = i;
  }

  // 各物理行の対応するレターボックス領域にピクセルをコピー
  for (let py = lb.offsetY; py < lb.offsetY + lb.renderH; py++) {
    const i = rowMap[py];
    if (i < 0) continue;
    const rowIdx = array[i] - minVal;
    if (rowIdx < 0 || rowIdx >= preScaledNumRows) continue;
    const srcOff = rowIdx * srcStride;
    const dstOff = py * dstStride + lb.offsetX * 4;
    out.set(preScaledPixels.subarray(srcOff, srcOff + srcStride), dstOff);
  }

  // 物理ピクセル座標で一括転送（putImageData は ctx.scale 変換を無視する）
  ctx.putImageData(outputImageData, 0, 0);

  // ハイライトオーバーレイを CSS 座標系で描画（ctx.scale(dpr,dpr) が有効）
  const { compareIndices, swapIndices, readIndices, writeIndices, showCompletionHighlight } = renderParams;
  const cssOffsetX = lb.offsetX / dpr;
  const cssOffsetY = lb.offsetY / dpr;
  const cssRenderW = lb.renderW / dpr;
  const cssRenderH = lb.renderH / dpr;
  if (showCompletionHighlight) {
    ctx.fillStyle = COLOR_SORTED;
    ctx.fillRect(cssOffsetX, cssOffsetY, cssRenderW, cssRenderH);
  } else {
    const rowH_css = cssRenderH / n;
    const applyOverlay = function (indices, color) {
      if (!indices || indices.length === 0) return;
      ctx.fillStyle = color;
      for (let k = 0; k < indices.length; k++) {
        const idx = indices[k];
        const dy = cssOffsetY + Math.round(idx * rowH_css);
        const dh = Math.max(1, Math.round((idx + 1) * rowH_css) - Math.round(idx * rowH_css));
        ctx.fillRect(cssOffsetX, dy, cssRenderW, dh);
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
if (imageBitmap && imageNumRows > 0 && drawFast()) return;

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
    const lb = calcLetterboxPhys(W, H);
    const cssOffsetX = lb.offsetX / dpr;
    const cssOffsetY = lb.offsetY / dpr;
    const cssRenderW = lb.renderW / dpr;
    const cssRenderH = lb.renderH / dpr;
    const rowH = cssRenderH / n;

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
        const localY = Math.round(i * rowH);
        const dstY = cssOffsetY + localY;
        const dstH = Math.max(1, Math.round((i + 1) * rowH) - localY);
        ctx.drawImage(imageBitmap, 0, srcY, imgW, srcRowH, cssOffsetX, dstY, cssRenderW, dstH);
      }
      ctx.fillStyle = COLOR_SORTED;
      ctx.fillRect(cssOffsetX, cssOffsetY, cssRenderW, cssRenderH);
    } else {
      for (let i = 0; i < n; i++) {
        const rowIdx = array[i] - minVal;
        if (rowIdx < 0 || rowIdx >= imageNumRows) continue;
        const srcY = rowIdx * srcRowH;
        const localY = Math.round(i * rowH);
        const dstY = cssOffsetY + localY;
        const dstH = Math.max(1, Math.round((i + 1) * rowH) - localY);

        // 画像行を描画
        ctx.drawImage(imageBitmap, 0, srcY, imgW, srcRowH, cssOffsetX, dstY, cssRenderW, dstH);

        // ハイライトオーバーレイ
        let overlay = null;
        if (swapSet.has(i))    overlay = OVERLAY_SWAP;
        else if (compareSet.has(i)) overlay = OVERLAY_COMPARE;
        else if (writeSet.has(i))   overlay = OVERLAY_WRITE;
        else if (readSet.has(i))    overlay = OVERLAY_READ;

        if (overlay) {
          ctx.fillStyle = overlay;
          ctx.fillRect(cssOffsetX, dstY, cssRenderW, dstH);
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
      preScaledPixels = null; // 高速パスキャッシュを無効化してバーモード fallback にする
      if (arrays.main && renderParams) scheduleDraw();

      createImageBitmap(blobOrBuffer).then(function (bmp) {
        // 後続の setImage が来ていた場合は古い結果を破棄
        if (requestId !== pendingImageRequestId) { bmp.close(); return; }
        imageBitmap = bmp;
        imageNumRows = msg.numRows;
        buildPreScaledPixels(); // 高速描画用ピクセルキャッシュを再構築
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
      preScaledPixels = null;
      preScaledNumRows = 0;
      preScaledPhysW = 0;
      if (arrays.main && renderParams) scheduleDraw();
      break;
    }

    case 'setImageBitmap': {
      // メインスレッドでデコード済みの ImageBitmap を直接受け取る（非同期処理不要）
      if (imageBitmap) { imageBitmap.close(); }
      imageBitmap = msg.bitmap;
      imageNumRows = msg.numRows;
      buildPreScaledPixels(); // 高速描画用ピクセルキャッシュを構築（n × drawImage 廃止）
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
        buildPreScaledPixels(); // キャンバスサイズ変化時にピクセルキャッシュを再構築
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
      preScaledPixels = null;
      preScaledNumRows = 0;
      preScaledPhysW = 0;
      outputImageData = null;
      rowMappingBuffer = null;
      break;
    }
  }
};
