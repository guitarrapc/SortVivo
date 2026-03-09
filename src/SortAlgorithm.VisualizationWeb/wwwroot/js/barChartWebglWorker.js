'use strict';
// WebGL2 描画 Worker (Phase 6)
// WebGL2 で GPU 直接レンダリング。WebGL2 利用不可の場合は Canvas 2D にフォールバック。
// メッセージインタフェースは renderWorker.js と完全互換。

// ── 共有状態 ─────────────────────────────────────────────
let offscreen = null;
let gl = null;   // WebGL2RenderingContext (WebGL2 使用時)
let ctx = null;   // Canvas2DRenderingContext (フォールバック時)
let dpr = 1;
let useWebGL = false;

// WebGL リソース
let program = null;
let vao = null;
let quadVBO = null;      // 静的: ユニット矩形 6 頂点 × vec2
let instanceVBO = null;      // 動的: バーごとのインスタンスデータ
let instanceData = null;      // Float32Array、毎フレーム再利用
let instanceVBOCap = 0;         // GPU バッファ容量 (floats)
const INST_STRIDE = 4;         // floats / bar: height, r, g, b

// Uniform locations (キャッシュ)
let uTotalBarWidth = null;
let uBarWidth = null;
let uGap = null;
let uCanvasW = null;
let uCanvasH = null;
let uDpr = null;
let uSectionOriginY = null;
let uSectionHeight = null;

// 色定義 [0, 1] RGB (WebGL)
const COLORS = {
  normal: [59 / 255, 130 / 255, 246 / 255],
  compare: [168 / 255, 85 / 255, 247 / 255],
  swap: [239 / 255, 68 / 255, 68 / 255],
  write: [249 / 255, 115 / 255, 22 / 255],
  read: [251 / 255, 191 / 255, 36 / 255],
  sorted: [16 / 255, 185 / 255, 129 / 255],
  buffer: [6 / 255, 182 / 255, 212 / 255],
};

// 色定義 CSS 文字列 (Canvas 2D フォールバック用)
const colors = {
  normal: '#3B82F6',
  compare: '#A855F7',
  swap: '#EF4444',
  write: '#F97316',
  read: '#FBBF24',
  sorted: '#10B981',
};

// ── HSL カラー LUT ──────────────────────────────────────────────

function hslToRgb(h, s, l) {
  s /= 100; l /= 100;
  const c = (1 - Math.abs(2 * l - 1)) * s;
  const x = c * (1 - Math.abs((h / 60) % 2 - 1));
  const m = l - c / 2;
  let r, g, b;
  if      (h < 60)  { r = c; g = x; b = 0; }
  else if (h < 120) { r = x; g = c; b = 0; }
  else if (h < 180) { r = 0; g = c; b = x; }
  else if (h < 240) { r = 0; g = x; b = c; }
  else if (h < 300) { r = x; g = 0; b = c; }
  else              { r = c; g = 0; b = x; }
  return [r + m, g + m, b + m];
}

// RGB float LUT（WebGL 用: [r,g,b, ...]）
let rgbLUTMax = -1;
let rgbLUT = null;

function buildRgbLUT(maxValue) {
  if (rgbLUTMax === maxValue) return;
  rgbLUTMax = maxValue;
  rgbLUT = new Float32Array((maxValue + 1) * 3);
  for (let v = 0; v <= maxValue; v++) {
    const [r, g, b] = hslToRgb((v / maxValue) * 360, 70, 60);
    rgbLUT[v * 3] = r; rgbLUT[v * 3 + 1] = g; rgbLUT[v * 3 + 2] = b;
  }
}

// HSL カラー LUT（Canvas 2D フォールバック用）
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
let arrays = { main: null, buffers: new Map() };
let renderParams = null;
let isDirty = false;
let isLoopRunning = false;

const _raf = typeof requestAnimationFrame !== 'undefined'
  ? cb => requestAnimationFrame(cb)
  : cb => setTimeout(cb, 1000 / 60);

// ── メッセージハンドラ ────────────────────────────────────
self.onmessage = function (e) {
  const msg = e.data;
  switch (msg.type) {
    case 'init': {
      offscreen = msg.canvas;
      dpr = msg.dpr || 1;
      useWebGL = initWebGL(offscreen);
      if (!useWebGL) {
        ctx = offscreen.getContext('2d', { alpha: false });
        ctx.scale(dpr, dpr);
      }
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
        showCompletionHighlight: msg.showCompletionHighlight || false,
      };
      scheduleDraw();
      break;
    }
    case 'applyFrame': {
      if (!arrays.main) break;
      if (msg.mainDelta) {
        const d = msg.mainDelta;
        for (let k = 0; k < d.length; k += 2) arrays.main[d[k]] = d[k + 1];
      }
      if (msg.bufferDeltas) {
        for (const [idStr, delta] of Object.entries(msg.bufferDeltas)) {
          const bid = parseInt(idStr);
          let buf = arrays.buffers.get(bid);
          if (!buf) {
            buf = new Int32Array(arrays.main.length);
            arrays.buffers.set(bid, buf);
          }
          for (let k = 0; k < delta.length; k += 2) buf[delta[k]] = delta[k + 1];
        }
      }
      if (msg.isSortCompleted && arrays.buffers.size > 0) arrays.buffers.clear();
      renderParams = {
        compareIndices: msg.compareIndices,
        swapIndices: msg.swapIndices,
        readIndices: msg.readIndices,
        writeIndices: msg.writeIndices,
        isSortCompleted: msg.isSortCompleted || false,
        showCompletionHighlight: msg.showCompletionHighlight || false,
      };
      scheduleDraw();
      break;
    }
    case 'resize': {
      if (!offscreen) break;
      offscreen.width = msg.newWidth;
      offscreen.height = msg.newHeight;
      dpr = msg.dpr || dpr;
      if (useWebGL) {
        gl.viewport(0, 0, offscreen.width, offscreen.height);
      } else {
        ctx.scale(dpr, dpr);
      }
      if (renderParams && arrays.main) scheduleDraw();
      break;
    }
    case 'dispose': {
      cleanup();
      break;
    }
  }
};

// ── WebGL 初期化 ──────────────────────────────────────────
function initWebGL(canvas) {
  try {
    gl = canvas.getContext('webgl2', {
      alpha: false,
      antialias: false,
      depth: false,
      stencil: false,
    });
  } catch (_) { return false; }
  if (!gl) return false;

  // 頂点シェーダー
  // - a_quad: ユニット矩形の角 (per-vertex)
  // - a_height: バーの高さ CSS px (per-instance)
  // - a_rgb: バーの色 [0,1] (per-instance)
  // - gl_InstanceID: バーインデックス (built-in)
  const vsSource = `#version 300 es
precision mediump float;

in vec2  a_quad;
in float a_height;
in vec3  a_rgb;

uniform float u_totalBarWidth;
uniform float u_barWidth;
uniform float u_gap;
uniform float u_canvasW;
uniform float u_canvasH;
uniform float u_dpr;
uniform float u_sectionOriginY;
uniform float u_sectionHeight;

out vec3 v_rgb;

void main() {
    float barIndex = float(gl_InstanceID);

    // バーの左右端 (CSS px) を物理ピクセル境界にスナップし、サブピクセルギャップを防止する。
    // 左端: floor でスナップ、右端: ceil でスナップ（バーが必ず 1 物理ピクセル以上を覆う）。
    float rawLeft  = barIndex * u_totalBarWidth + u_gap * 0.5;
    float rawRight = rawLeft + u_barWidth;
    float physLeft  = floor(rawLeft  * u_dpr) / u_dpr;
    float physRight = ceil (rawRight * u_dpr) / u_dpr;
    physRight = max(physRight, physLeft + 1.0 / u_dpr);

    float x   = mix(physLeft, physRight, a_quad.x);
    float topY = u_sectionOriginY + u_sectionHeight - a_height;
    float y   = topY + a_quad.y * a_height;
    gl_Position = vec4(
        (x / u_canvasW) * 2.0 - 1.0,
        1.0 - (y / u_canvasH) * 2.0,
        0.0, 1.0
    );
    v_rgb = a_rgb;
}`;

  // フラグメントシェーダー
  const fsSource = `#version 300 es
precision mediump float;

in  vec3 v_rgb;
out vec4 outColor;

void main() { outColor = vec4(v_rgb, 1.0); }`;

  const vs = compileShader(gl.VERTEX_SHADER, vsSource);
  const fs = compileShader(gl.FRAGMENT_SHADER, fsSource);
  if (!vs || !fs) return false;

  program = gl.createProgram();
  gl.attachShader(program, vs);
  gl.attachShader(program, fs);
  gl.linkProgram(program);
  gl.deleteShader(vs);
  gl.deleteShader(fs);
  if (!gl.getProgramParameter(program, gl.LINK_STATUS)) {
    console.error('[webglWorker] program link error:', gl.getProgramInfoLog(program));
    return false;
  }

  // Uniform locations をキャッシュ
  uTotalBarWidth = gl.getUniformLocation(program, 'u_totalBarWidth');
  uBarWidth = gl.getUniformLocation(program, 'u_barWidth');
  uGap = gl.getUniformLocation(program, 'u_gap');
  uCanvasW = gl.getUniformLocation(program, 'u_canvasW');
  uCanvasH = gl.getUniformLocation(program, 'u_canvasH');
  uDpr = gl.getUniformLocation(program, 'u_dpr');
  uSectionOriginY = gl.getUniformLocation(program, 'u_sectionOriginY');
  uSectionHeight = gl.getUniformLocation(program, 'u_sectionHeight');

  // Attribute locations
  const aQuad = gl.getAttribLocation(program, 'a_quad');
  const aHeight = gl.getAttribLocation(program, 'a_height');
  const aRGB = gl.getAttribLocation(program, 'a_rgb');

  // Quad VBO: 静的ユニット矩形 (2 三角形 = 6 頂点)
  quadVBO = gl.createBuffer();
  gl.bindBuffer(gl.ARRAY_BUFFER, quadVBO);
  gl.bufferData(gl.ARRAY_BUFFER, new Float32Array([
    0, 0, 1, 0, 0, 1,  // 第1三角形
    1, 0, 1, 1, 0, 1,  // 第2三角形
  ]), gl.STATIC_DRAW);

  // Instance VBO: 動的 (バーごとのデータ)
  const INIT_CAP = 8192 * INST_STRIDE;
  instanceData = new Float32Array(INIT_CAP);
  instanceVBOCap = INIT_CAP;
  instanceVBO = gl.createBuffer();
  gl.bindBuffer(gl.ARRAY_BUFFER, instanceVBO);
  gl.bufferData(gl.ARRAY_BUFFER, INIT_CAP * Float32Array.BYTES_PER_ELEMENT, gl.DYNAMIC_DRAW);

  // VAO: 属性バインディングを記録
  vao = gl.createVertexArray();
  gl.bindVertexArray(vao);

  const FSIZE = Float32Array.BYTES_PER_ELEMENT;

  // a_quad: per-vertex (divisor = 0)
  gl.bindBuffer(gl.ARRAY_BUFFER, quadVBO);
  gl.enableVertexAttribArray(aQuad);
  gl.vertexAttribPointer(aQuad, 2, gl.FLOAT, false, 0, 0);
  gl.vertexAttribDivisor(aQuad, 0);

  // per-instance 属性 (divisor = 1)
  gl.bindBuffer(gl.ARRAY_BUFFER, instanceVBO);

  // a_height: offset 0
  gl.enableVertexAttribArray(aHeight);
  gl.vertexAttribPointer(aHeight, 1, gl.FLOAT, false, INST_STRIDE * FSIZE, 0);
  gl.vertexAttribDivisor(aHeight, 1);

  // a_rgb: offset 1 * FSIZE
  gl.enableVertexAttribArray(aRGB);
  gl.vertexAttribPointer(aRGB, 3, gl.FLOAT, false, INST_STRIDE * FSIZE, 1 * FSIZE);
  gl.vertexAttribDivisor(aRGB, 1);

  gl.bindVertexArray(null);

  gl.clearColor(26 / 255, 26 / 255, 26 / 255, 1.0); // #1A1A1A
  gl.viewport(0, 0, canvas.width, canvas.height);

  return true;
}

function compileShader(type, src) {
  const shader = gl.createShader(type);
  gl.shaderSource(shader, src);
  gl.compileShader(shader);
  if (!gl.getShaderParameter(shader, gl.COMPILE_STATUS)) {
    console.error('[webglWorker] shader compile error:', gl.getShaderInfoLog(shader));
    gl.deleteShader(shader);
    return null;
  }
  return shader;
}

// ── スケジューリング ──────────────────────────────────────
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
  if (isDirty && offscreen && arrays.main) {
    isDirty = false;
    draw();
    _raf(tick);
  } else {
    isLoopRunning = false;
  }
}

// ── 描画ディスパッチャー ──────────────────────────────────
function draw() {
  if (!renderParams) return;
  if (useWebGL) drawWebGL();
  else drawCanvas2D();
}

// ── WebGL 描画 ────────────────────────────────────────────

/**
 * GPU インスタンスバッファが count 個のインスタンスを収容できるよう拡張する。
 * 拡張が必要な場合は CPU 側 Float32Array と GPU バッファを 2 倍ずつ再アロケート。
 */
function ensureInstanceCapacity(count) {
  const needed = count * INST_STRIDE;
  if (needed <= instanceVBOCap) return;
  let newCap = instanceVBOCap;
  while (newCap < needed) newCap *= 2;
  instanceData = new Float32Array(newCap);
  instanceVBOCap = newCap;
  gl.bindBuffer(gl.ARRAY_BUFFER, instanceVBO);
  gl.bufferData(gl.ARRAY_BUFFER, newCap * Float32Array.BYTES_PER_ELEMENT, gl.DYNAMIC_DRAW);
}

/**
 * instanceData の先頭 count 要素を GPU にアップロードして描画する。
 * ジオメトリ uniforms（バー幅・ギャップ・セクション原点）も同時に設定する。
 */
function uploadAndDraw(count, originY, sectionH, totalBW, bW, gp) {
  gl.uniform1f(uTotalBarWidth, totalBW);
  gl.uniform1f(uBarWidth, bW);
  gl.uniform1f(uGap, gp);
  gl.uniform1f(uSectionOriginY, originY);
  gl.uniform1f(uSectionHeight, sectionH);

  gl.bindBuffer(gl.ARRAY_BUFFER, instanceVBO);
  gl.bufferSubData(gl.ARRAY_BUFFER, 0, instanceData, 0, count * INST_STRIDE);
  gl.drawArraysInstanced(gl.TRIANGLES, 0, 6, count);
}

function drawWebGL() {
  const {
    compareIndices, swapIndices, readIndices, writeIndices,
    isSortCompleted, showCompletionHighlight,
  } = renderParams;

  const array = arrays.main;
  const cssW = offscreen.width / dpr;
  const cssH = offscreen.height / dpr;
  const arrayLength = array.length;

  if (arrayLength === 0) { gl.clear(gl.COLOR_BUFFER_BIT); return; }

  // セクション分割（バッファー配列 + メイン配列）
  const bufferCount = (isSortCompleted || showCompletionHighlight) ? 0 : arrays.buffers.size;
  const showBuffers = bufferCount > 0;
  const totalSects = showBuffers ? 1 + bufferCount : 1;
  const sectionH = cssH / totalSects;
  const mainOriginY = showBuffers ? sectionH * bufferCount : 0;

  // バー幅・ギャップ計算（大配列でも 1px 以上を確保、スケール込み）
  const minBarW = 1.0;
  const gapRatio = arrayLength <= 256 ? 0.15 : arrayLength <= 1024 ? 0.10 : 0.05;
  const rawReqW = Math.max(cssW, arrayLength * minBarW / (1.0 - gapRatio));
  const scaleX = Math.min(1.0, cssW / rawReqW);
  const reqW = rawReqW * scaleX;
  const totalBW = reqW / arrayLength;
  const barW = totalBW * (1.0 - gapRatio);
  const gap = totalBW * gapRatio;

  // 最大値（ループで計算、スタックオーバーフロー回避）
  let maxValue = 0;
  for (let i = 0; i < arrayLength; i++) if (array[i] > maxValue) maxValue = array[i];
  if (maxValue === 0) maxValue = 1;

  const usableH = sectionH - 20; // 上部 20px をラベル用に確保

  gl.clear(gl.COLOR_BUFFER_BIT);
  gl.useProgram(program);
  gl.uniform1f(uCanvasW, cssW);
  gl.uniform1f(uCanvasH, cssH);
  gl.uniform1f(uDpr, dpr);
  gl.bindVertexArray(vao);

  // ── メイン配列 ──────────────────────────────────────
  ensureInstanceCapacity(arrayLength);

  if (showCompletionHighlight) {
    // 完了ハイライト: 全バーを sorted 色で一括描画
    const [r, g, b] = COLORS.sorted;
    for (let i = 0; i < arrayLength; i++) {
      const h = (array[i] / maxValue) * usableH;
      const base = i * INST_STRIDE;
      instanceData[base] = h; instanceData[base + 1] = r; instanceData[base + 2] = g; instanceData[base + 3] = b;
    }
  } else {
    // 通常描画: 各バーに色を割り当て
    buildRgbLUT(maxValue);
    const cmpSet = new Set(compareIndices);
    const swpSet = new Set(swapIndices);
    const rdSet = new Set(readIndices);
    const wrSet = new Set(writeIndices);
    const [cr, cg, cb] = COLORS.compare;
    const [sr, sg, sb] = COLORS.swap;
    const [wr, wg, wb] = COLORS.write;
    const [rr, rg, rb] = COLORS.read;

    for (let i = 0; i < arrayLength; i++) {
      const h = (array[i] / maxValue) * usableH;
      const base = i * INST_STRIDE;
      instanceData[base] = h;
      if (swpSet.has(i)) { instanceData[base + 1] = sr; instanceData[base + 2] = sg; instanceData[base + 3] = sb; }
      else if (cmpSet.has(i)) { instanceData[base + 1] = cr; instanceData[base + 2] = cg; instanceData[base + 3] = cb; }
      else if (wrSet.has(i)) { instanceData[base + 1] = wr; instanceData[base + 2] = wg; instanceData[base + 3] = wb; }
      else if (rdSet.has(i)) { instanceData[base + 1] = rr; instanceData[base + 2] = rg; instanceData[base + 3] = rb; }
      else {
        const off = array[i] * 3;
        instanceData[base + 1] = rgbLUT[off]; instanceData[base + 2] = rgbLUT[off + 1]; instanceData[base + 3] = rgbLUT[off + 2];
      }
    }
  }

  uploadAndDraw(arrayLength, mainOriginY, sectionH, totalBW, barW, gap);

  // ── バッファー配列 ──────────────────────────────────
  if (showBuffers) {
    const [br, bg, bb] = COLORS.buffer;
    const sortedIds = [...arrays.buffers.keys()].sort((a, b) => a - b);

    for (let bi = 0; bi < sortedIds.length; bi++) {
      const bufArr = arrays.buffers.get(sortedIds[bi]);
      if (!bufArr || bufArr.length === 0) continue;

      const bufLen = bufArr.length;
      let bufMax = 0;
      for (let i = 0; i < bufLen; i++) if (bufArr[i] > bufMax) bufMax = bufArr[i];
      if (bufMax === 0) bufMax = 1;

      const bufGapR = bufLen <= 256 ? 0.15 : bufLen <= 1024 ? 0.10 : 0.05;
      const bufRawW = Math.max(cssW, bufLen * minBarW / (1.0 - bufGapR));
      const bufScaleX = Math.min(1.0, cssW / bufRawW);
      const bufReqW = bufRawW * bufScaleX;
      const bufTotalW = bufReqW / bufLen;
      const bufBarW = bufTotalW * (1.0 - bufGapR);
      const bufGap = bufTotalW * bufGapR;
      const bufUsableH = sectionH - 20;
      const bufOriginY = bi * sectionH;

      ensureInstanceCapacity(bufLen);
      for (let i = 0; i < bufLen; i++) {
        const h = (bufArr[i] / bufMax) * bufUsableH;
        const base = i * INST_STRIDE;
        instanceData[base] = h; instanceData[base + 1] = br; instanceData[base + 2] = bg; instanceData[base + 3] = bb;
      }
      uploadAndDraw(bufLen, bufOriginY, sectionH, bufTotalW, bufBarW, bufGap);
    }
  }

  gl.bindVertexArray(null);
}

// ── Canvas 2D 描画（WebGL2 フォールバック） ───────────────
// renderWorker.js の draw() と同一ロジック
function drawCanvas2D() {
  if (!offscreen || !ctx || !arrays.main || !renderParams) return;

  const {
    compareIndices, swapIndices, readIndices, writeIndices,
    isSortCompleted, showCompletionHighlight,
  } = renderParams;
  const array = arrays.main;
  const width = offscreen.width / dpr;
  const height = offscreen.height / dpr;
  const arrayLength = array.length;

  const bufferCount = (isSortCompleted || showCompletionHighlight) ? 0 : arrays.buffers.size;
  const showBuffers = bufferCount > 0;
  const totalSections = showBuffers ? (1 + bufferCount) : 1;
  const sectionHeight = height / totalSections;
  const mainArrayY = showBuffers ? sectionHeight * bufferCount : 0;

  ctx.fillStyle = '#1A1A1A';
  ctx.fillRect(0, 0, width, height);

  if (arrayLength === 0) return;

  const minBarWidth = 1.0;
  const gapRatio = arrayLength <= 256 ? 0.15 : arrayLength <= 1024 ? 0.10 : 0.05;
  const requiredWidth = Math.max(width, arrayLength * minBarWidth / (1.0 - gapRatio));
  const totalBarWidth = requiredWidth / arrayLength;
  const barWidth = totalBarWidth * (1.0 - gapRatio);
  const gap = totalBarWidth * gapRatio;

  let maxValue = 0;
  for (let i = 0; i < arrayLength; i++) {
    if (array[i] > maxValue) maxValue = array[i];
  }

  const compareSet = new Set(compareIndices);
  const swapSet = new Set(swapIndices);
  const readSet = new Set(readIndices);
  const writeSet = new Set(writeIndices);

  const scale = Math.min(1.0, width / requiredWidth);
  ctx.save();
  if (scale < 1.0) ctx.scale(scale, 1.0);

  const usableHeight = sectionHeight - 20;
  if (showCompletionHighlight) {
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

// ── クリーンアップ ────────────────────────────────────────
function cleanup() {
  if (useWebGL && gl) {
    if (vao) gl.deleteVertexArray(vao);
    if (quadVBO) gl.deleteBuffer(quadVBO);
    if (instanceVBO) gl.deleteBuffer(instanceVBO);
    if (program) gl.deleteProgram(program);
  }
  offscreen = null;
  gl = null;
  ctx = null;
  arrays = { main: null, buffers: new Map() };
  renderParams = null;
  isDirty = false;
  isLoopRunning = false;
  instanceData = null;
  instanceVBOCap = 0;
}
