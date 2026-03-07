// OffscreenCanvas 不均衡和音描画 Worker（Disparity Chords）
// 各要素の現在位置と整列後の位置を弦で結び、位置ずれを可視化する
'use strict';

let offscreen = null;
let ctx = null;
let dpr = 1;
let arrays = { main: null, buffers: new Map() };
let renderParams = null;
let isDirty = false;
let isLoopRunning = false;

const colors = {
  compare: '#A855F7',  // 紫
  swap:    '#EF4444',  // 赤
  write:   '#F97316',  // 橙
  read:    '#FBBF24',  // 黄
  sorted:  '#10B981'   // 緑 - ソート完了
};

// 三角関数ルックアップテーブル（配列長に対応）
let lutLength = 0;
let cosLUT = null;
let sinLUT = null;

// HSL カラールックアップテーブル（値ベース）
let colorLUTMax = -1;
let colorLUT = null;

function buildTrigLUT(n) {
  if (lutLength === n) return;
  lutLength = n;
  const step = (2 * Math.PI) / n;
  cosLUT = new Float64Array(n);
  sinLUT = new Float64Array(n);
  for (let i = 0; i < n; i++) {
    const angle = i * step - Math.PI / 2;
    cosLUT[i] = Math.cos(angle);
    sinLUT[i] = Math.sin(angle);
  }
}

function buildColorLUT(maxValue) {
  if (colorLUTMax === maxValue) return;
  colorLUTMax = maxValue;
  colorLUT = new Array(maxValue + 1);
  for (let v = 0; v <= maxValue; v++) {
    const hue = (v / maxValue) * 360;
    colorLUT[v] = `hsla(${hue.toFixed(1)}, 70%, 65%, 0.55)`;
  }
}

// requestAnimationFrame が Worker で利用可能か確認
const _raf = typeof requestAnimationFrame !== 'undefined'
  ? (cb) => requestAnimationFrame(cb)
  : (cb) => setTimeout(cb, 1000 / 60);

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
      isDirty = false;
      isLoopRunning = false;
      break;
    }
  }
};

function scheduleDraw() {
  isDirty = true;
  if (!isLoopRunning) startLoop();
}

function startLoop() {
  if (isLoopRunning) return;
  isLoopRunning = true;
  tick();
}

function tick() {
  if (isDirty && offscreen && ctx && arrays.main) {
    isDirty = false;
    draw();
    _raf(tick);
  } else {
    isLoopRunning = false;
  }
}

function draw() {
  if (!offscreen || !ctx || !arrays.main || !renderParams) return;

  const {
    compareIndices, swapIndices, readIndices, writeIndices,
    isSortCompleted, showCompletionHighlight
  } = renderParams;

  const array = arrays.main;
  const n = array.length;
  const width  = offscreen.width / dpr;
  const height = offscreen.height / dpr;

  // 背景
  ctx.fillStyle = '#1A1A1A';
  ctx.fillRect(0, 0, width, height);
  if (n === 0) return;

  // 中心・半径
  const cx = width  / 2;
  const cy = height / 2;
  const R  = Math.min(width, height) * 0.44; // 弦の端点を置く円の半径
  const dotR = n <= 64 ? 4 : n <= 256 ? 3 : 2; // 現在位置のドット半径

  // 背景円リング（薄い枠）
  ctx.strokeStyle = 'rgba(255,255,255,0.07)';
  ctx.lineWidth = 1;
  ctx.beginPath();
  ctx.arc(cx, cy, R, 0, 2 * Math.PI);
  ctx.stroke();

  // 最大値をループで取得
  let maxValue = 0;
  for (let i = 0; i < n; i++) {
    if (array[i] > maxValue) maxValue = array[i];
  }
  if (maxValue === 0) maxValue = 1;

  buildTrigLUT(n);
  buildColorLUT(maxValue);

  // 操作対象インデックスの Set
  const compareSet = new Set(compareIndices);
  const swapSet    = new Set(swapIndices);
  const readSet    = new Set(readIndices);
  const writeSet   = new Set(writeIndices);

  // 整列後インデックスを計算（値を0-based index に正規化）
  // sortedIdx[i] = floor((array[i] / maxValue) * (n - 1) + 0.5)
  const sortedIdx = new Int32Array(n);
  for (let i = 0; i < n; i++) {
    sortedIdx[i] = Math.round((array[i] / maxValue) * (n - 1));
  }

  if (showCompletionHighlight) {
    // ソート完了: 弦なし、全要素をリング上に緑ドットで表示
    ctx.fillStyle = colors.sorted;
    for (let i = 0; i < n; i++) {
      const x = cx + cosLUT[i] * R;
      const y = cy + sinLUT[i] * R;
      ctx.beginPath();
      ctx.arc(x, y, dotR, 0, 2 * Math.PI);
      ctx.fill();
    }
    return;
  }

  // ─── 弦の描画（通常）───────────────────────────────────────────────────────

  // 1. 通常弦（HSL グラデーション）
  //    同じ strokeStyle を持つ弦はまとめて描画できないため個別に beginPath
  for (let i = 0; i < n; i++) {
    if (compareSet.has(i) || swapSet.has(i) || readSet.has(i) || writeSet.has(i)) continue;
    const si = sortedIdx[i];
    if (si === i) continue; // 正位置にある要素は弦不要
    const x0 = cx + cosLUT[i]  * R;
    const y0 = cy + sinLUT[i]  * R;
    const x1 = cx + cosLUT[si] * R;
    const y1 = cy + sinLUT[si] * R;
    ctx.strokeStyle = colorLUT[array[i]];
    ctx.lineWidth = n <= 64 ? 1.5 : 1;
    ctx.beginPath();
    ctx.moveTo(x0, y0);
    ctx.lineTo(x1, y1);
    ctx.stroke();
  }

  // 2. ハイライト弦（compare / write / read / swap の優先度順）
  const highlightBuckets = [
    [compareIndices, colors.compare],
    [writeIndices,   colors.write],
    [readIndices,    colors.read],
    [swapIndices,    colors.swap],
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
      const x0 = cx + cosLUT[i]  * R;
      const y0 = cy + sinLUT[i]  * R;
      const x1 = cx + cosLUT[si] * R;
      const y1 = cy + sinLUT[si] * R;
      ctx.moveTo(x0, y0);
      ctx.lineTo(x1, y1);
    }
    ctx.stroke();
  }

  // ─── 現在位置ドット（弦の端点を強調）───────────────────────────────────────
  // 1. 通常ドット（HSL グラデーション、不透明）
  for (let i = 0; i < n; i++) {
    if (compareSet.has(i) || swapSet.has(i) || readSet.has(i) || writeSet.has(i)) continue;
    const x = cx + cosLUT[i] * R;
    const y = cy + sinLUT[i] * R;
    const v = array[i];
    const hue = (v / maxValue) * 360;
    ctx.fillStyle = `hsl(${hue.toFixed(1)}, 70%, 65%)`;
    ctx.beginPath();
    ctx.arc(x, y, dotR, 0, 2 * Math.PI);
    ctx.fill();
  }

  // 2. ハイライトドット
  for (const [indices, color] of highlightBuckets) {
    if (!indices || indices.length === 0) continue;
    ctx.fillStyle = color;
    ctx.beginPath();
    for (const i of indices) {
      if (i < 0 || i >= n) continue;
      ctx.moveTo(cx + cosLUT[i] * R + dotR, cy + sinLUT[i] * R);
      ctx.arc(cx + cosLUT[i] * R, cy + sinLUT[i] * R, dotR + 1, 0, 2 * Math.PI);
    }
    ctx.fill();
  }

  // ─── 正位置ドット（緑: 弦なし = ソート済み要素）────────────────────────────
  ctx.fillStyle = colors.sorted;
  ctx.beginPath();
  for (let i = 0; i < n; i++) {
    if (sortedIdx[i] !== i) continue;
    if (compareSet.has(i) || swapSet.has(i) || readSet.has(i) || writeSet.has(i)) continue;
    ctx.moveTo(cx + cosLUT[i] * R + dotR, cy + sinLUT[i] * R);
    ctx.arc(cx + cosLUT[i] * R, cy + sinLUT[i] * R, dotR, 0, 2 * Math.PI);
  }
  ctx.fill();
}
