// OffscreenCanvas 描画 Worker（Phase 4）
// メインスレッドから描画処理を完全に分離し、独立した rAF ループで描画する
'use strict';

let offscreen = null;
let ctx = null;
let dpr = 1;
let arrays = { main: null, buffers: new Map() };
let renderParams = null;
let isDirty = false;
let isLoopRunning = false;

const colors = {
  normal: '#3B82F6',  // 青
  compare: '#A855F7',  // 紫
  swap: '#EF4444',  // 赤
  write: '#F97316',  // 橙
  read: '#FBBF24',  // 黄
  sorted: '#10B981'   // 緑 - ソート完了
};

// HSL カラー LUT
let colorLUTMax = -1;
let colorLUT = null;

function buildColorLUT(maxValue) {
  if (colorLUTMax === maxValue) return;
  colorLUTMax = maxValue;
  colorLUT = new Array(maxValue + 1);
  for (let v = 0; v <= maxValue; v++) {
    const hue = (v / maxValue) * 360;
    colorLUT[v] = `hsl(${hue.toFixed(1)}, 70%, 60%)`;
  }
}

// requestAnimationFrame が Worker で利用可能か確認（利用不可の場合は setTimeout でフォールバック）
const _raf = typeof requestAnimationFrame !== 'undefined'
  ? (cb) => requestAnimationFrame(cb)
  : (cb) => setTimeout(cb, 1000 / 60);

self.onmessage = function (e) {
  const msg = e.data;
  switch (msg.type) {
    case 'init': {
      offscreen = msg.canvas; // OffscreenCanvas（Transferable で受け取る）
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
      // メイン配列に差分を適用（flat: [index, value, ...]）
      if (msg.mainDelta) {
        const delta = msg.mainDelta;
        for (let k = 0; k < delta.length; k += 2) {
          arrays.main[delta[k]] = delta[k + 1];
        }
      }
      // バッファー配列に差分を適用
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
      // ソート完了時はバッファーを解放
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
        // canvas.width/height の変更でコンテキストがリセットされるため再スケール
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
  const width = offscreen.width / dpr;
  const height = offscreen.height / dpr;
  const arrayLength = array.length;

  // バッファー配列の数（ソート完了時はゼロ扱い）
  const bufferCount = (isSortCompleted || showCompletionHighlight) ? 0 : arrays.buffers.size;
  const showBuffers = bufferCount > 0;
  const totalSections = showBuffers ? (1 + bufferCount) : 1;
  const sectionHeight = height / totalSections;
  const mainArrayY = showBuffers ? sectionHeight * bufferCount : 0;

  // 背景をクリア（黒）
  ctx.fillStyle = '#1A1A1A';
  ctx.fillRect(0, 0, width, height);

  if (arrayLength === 0) return;

  // バーの幅と隙間を計算
  const minBarWidth = 1.0;
  const gapRatio = arrayLength <= 256 ? 0.15 : arrayLength <= 1024 ? 0.10 : 0.05;
  const requiredWidth = Math.max(width, arrayLength * minBarWidth / (1.0 - gapRatio));
  const totalBarWidth = requiredWidth / arrayLength;
  const barWidth = totalBarWidth * (1.0 - gapRatio);
  const gap = totalBarWidth * gapRatio;

  // 最大値を取得（ループで計算、スタックオーバーフロー回避）
  let maxValue = 0;
  for (let i = 0; i < arrayLength; i++) {
    if (array[i] > maxValue) maxValue = array[i];
  }

  // Set で高速な存在チェック
  const compareSet = new Set(compareIndices);
  const swapSet = new Set(swapIndices);
  const readSet = new Set(readIndices);
  const writeSet = new Set(writeIndices);

  // スケール調整（横スクロール対応）
  const scale = Math.min(1.0, width / requiredWidth);
  ctx.save();
  if (scale < 1.0) ctx.scale(scale, 1.0);

  // メイン配列のバーを描画（同色バッチ描画: fillStyle 切り替えを最小化）
  const usableHeight = sectionHeight - 20;
  if (showCompletionHighlight) {
    // 完了ハイライト: 全バーを1色で一括描画
    ctx.fillStyle = colors.sorted;
    for (let i = 0; i < arrayLength; i++) {
      const barHeight = (array[i] / maxValue) * usableHeight;
      ctx.fillRect(
        i * totalBarWidth + gap / 2,
        mainArrayY + sectionHeight - barHeight,
        barWidth, barHeight
      );
    }
  } else {
    // 通常描画: インデックスを色バケツに振り分けてから色ごとに一括描画
    buildColorLUT(maxValue);
    const swapBucket = [];
    const compareBucket = [];
    const writeBucket = [];
    const readBucket = [];
    const normalBucket = [];

    for (let i = 0; i < arrayLength; i++) {
      if (swapSet.has(i)) swapBucket.push(i);
      else if (compareSet.has(i)) compareBucket.push(i);
      else if (writeSet.has(i)) writeBucket.push(i);
      else if (readSet.has(i)) readBucket.push(i);
      else normalBucket.push(i);
    }

    // normal バー: 値に応じた HSL 色で 1 本ずつ描画
    for (const i of normalBucket) {
      ctx.fillStyle = colorLUT[array[i]];
      const barHeight = (array[i] / maxValue) * usableHeight;
      ctx.fillRect(
        i * totalBarWidth + gap / 2,
        mainArrayY + sectionHeight - barHeight,
        barWidth, barHeight
      );
    }
    // ハイライトバー: 色ごとにバッチ描画
    const highlightBuckets = [
      [compareBucket, colors.compare],
      [writeBucket, colors.write],
      [readBucket, colors.read],
      [swapBucket, colors.swap],
    ];
    for (const [indices, color] of highlightBuckets) {
      if (indices.length === 0) continue;
      ctx.fillStyle = color;
      for (const i of indices) {
        const barHeight = (array[i] / maxValue) * usableHeight;
        ctx.fillRect(
          i * totalBarWidth + gap / 2,
          mainArrayY + sectionHeight - barHeight,
          barWidth, barHeight
        );
      }
    }
  }

  ctx.restore();

  // バッファー配列を描画（ソート完了時は非表示）
  if (showBuffers) {
    const sortedBufferIds = [...arrays.buffers.keys()].sort((a, b) => a - b);

    for (let bufIdx = 0; bufIdx < sortedBufferIds.length; bufIdx++) {
      const bid = sortedBufferIds[bufIdx];
      const bufArr = arrays.buffers.get(bid);
      const bufferY = bufIdx * sectionHeight;

      if (!bufArr || bufArr.length === 0) continue;

      let bufMax = 0;
      for (let i = 0; i < bufArr.length; i++) {
        if (bufArr[i] > bufMax) bufMax = bufArr[i];
      }

      const bufReqW = Math.max(width, bufArr.length * minBarWidth / (1.0 - gapRatio));
      const bufTotalW = bufReqW / bufArr.length;
      const bufBarW = bufTotalW * (1.0 - gapRatio);
      const bufGap = bufTotalW * gapRatio;
      const bufScale = Math.min(1.0, width / bufReqW);

      ctx.save();
      if (bufScale < 1.0) ctx.scale(bufScale, 1.0);

      // バッファー配列のバーを描画（単色なので fillStyle は1回）
      const bufUsableH = sectionHeight - 20;
      ctx.fillStyle = '#06B6D4';
      for (let i = 0; i < bufArr.length; i++) {
        const h = (bufArr[i] / bufMax) * bufUsableH;
        ctx.fillRect(
          i * bufTotalW + bufGap / 2,
          bufferY + sectionHeight - h,
          bufBarW, h
        );
      }

      ctx.restore();

      ctx.fillStyle = '#888';
      ctx.font = '12px monospace';
      ctx.fillText(`Buffer #${bid}`, 10, bufferY + 20);
    }

    ctx.fillStyle = '#888';
    ctx.font = '12px monospace';
    ctx.fillText('Main Array', 10, mainArrayY + 20);
  }
}
