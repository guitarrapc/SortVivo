'use strict';
// WebGL2 螺旋描画 Worker (Phase 6 - Spiral)
// WebGL2 で gl.POINTS による GPU 直接レンダリング。
// WebGL2 利用不可の場合は Canvas 2D にフォールバック。
// メッセージインタフェースは spiralRenderWorker.js と完全互換。

// ── 共有状態 ─────────────────────────────────────────────
let offscreen = null;
let gl = null;    // WebGL2RenderingContext (WebGL2 使用時)
let ctx = null;   // Canvas2DRenderingContext (フォールバック時)
let dpr = 1;
let useWebGL = false;

// WebGL リソース
let program = null;
let vao = null;
let pointVBO = null;   // 動的: ドットごとのデータ (x, y, r, g, b)
let pointData = null;  // Float32Array、毎フレーム再利用
let pointVBOCap = 0;   // GPU バッファ容量 (floats)
const POINT_STRIDE = 5; // floats / dot: x, y, r, g, b

// Uniform locations (キャッシュ)
let uCanvasW = null;
let uCanvasH = null;
let uPointSize = null;

// 色定義 [0, 1] RGB (WebGL)
const RGB = {
  compare: [168 / 255, 85 / 255, 247 / 255],
  swap: [239 / 255, 68 / 255, 68 / 255],
  write: [249 / 255, 115 / 255, 22 / 255],
  read: [251 / 255, 191 / 255, 36 / 255],
  sorted: [16 / 255, 185 / 255, 129 / 255],
  buffer: [6 / 255, 182 / 255, 212 / 255],
};

// 色定義 CSS 文字列 (Canvas 2D フォールバック用)
const colors = {
  compare: '#A855F7',
  swap: '#EF4444',
  write: '#F97316',
  read: '#FBBF24',
  sorted: '#10B981',
};

// HSL (h[0-360], s[0-100], l[0-100]) → RGB [0, 1]
function hslToRgb(h, s, l) {
  s /= 100; l /= 100;
  const c = (1 - Math.abs(2 * l - 1)) * s;
  const x = c * (1 - Math.abs((h / 60) % 2 - 1));
  const m = l - c / 2;
  let r, g, b;
  if (h < 60) { r = c; g = x; b = 0; }
  else if (h < 120) { r = x; g = c; b = 0; }
  else if (h < 180) { r = 0; g = c; b = x; }
  else if (h < 240) { r = 0; g = x; b = c; }
  else if (h < 300) { r = x; g = 0; b = c; }
  else { r = c; g = 0; b = x; }
  return [r + m, g + m, b + m];
}

// RGB float LUT（WebGL 用）
let rgbLUTMax = -1;
let rgbLUT = null; // Float32Array: [r0,g0,b0, r1,g1,b1, ...]

function buildRgbLUT(maxValue) {
  if (rgbLUTMax === maxValue) return;
  rgbLUTMax = maxValue;
  rgbLUT = new Float32Array((maxValue + 1) * 3);
  for (let v = 0; v <= maxValue; v++) {
    const [r, g, b] = hslToRgb((v / maxValue) * 360, 70, 60);
    rgbLUT[v * 3] = r;
    rgbLUT[v * 3 + 1] = g;
    rgbLUT[v * 3 + 2] = b;
  }
}

// HSL カラー LUT (Canvas 2D フォールバック用)
let colorLUTMax = -1;
let colorLUT = null;

function buildColorLUT(maxValue) {
  if (colorLUTMax === maxValue) return;
  colorLUTMax = maxValue;
  colorLUT = new Array(maxValue + 1);
  for (let v = 0; v <= maxValue; v++) {
    const hue = (v / maxValue) * 360;
    colorLUT[v] = `hsl(${hue}, 70%, 60%)`;
  }
}

// 螺旋パラメータ
const SPIRAL_TURNS = 3;
const SPIRAL_MIN_RADIUS_RATIO = 0.08;
const SPIRAL_MAX_RADIUS_RATIO = 0.46;

// 螺旋座標 LUT（配列長・キャンバスサイズが変わったときのみ再構築）
// posLUTX[i] = cos(theta_i), posLUTY[i] = sin(theta_i)（角度のみ、インデックスから決定）
// 半径は描画時に要素の値から動的に計算する
let posLUTLength = 0;
let posLUTCanvasW = 0;
let posLUTCanvasH = 0;
let posLUTX = null; // Float32Array: cos(theta_i)
let posLUTY = null; // Float32Array: sin(theta_i)
let spiralCx = 0;
let spiralCy = 0;
let spiralMaxR = 0;

function buildPosLUT(n, width, height) {
  if (posLUTLength === n && posLUTCanvasW === width && posLUTCanvasH === height) return;
  posLUTLength = n;
  posLUTCanvasW = width;
  posLUTCanvasH = height;
  posLUTX = new Float32Array(n);
  posLUTY = new Float32Array(n);
  spiralCx = width / 2;
  spiralCy = height / 2;
  const minDim = Math.min(width, height);
  spiralMaxR = minDim * SPIRAL_MAX_RADIUS_RATIO;
  const twoPI = 2 * Math.PI;
  for (let i = 0; i < n; i++) {
    const t = n <= 1 ? 0 : i / (n - 1);
    const theta = t * SPIRAL_TURNS * twoPI - Math.PI / 2;
    posLUTX[i] = Math.cos(theta);
    posLUTY[i] = Math.sin(theta);
  }
}

// 配列状態
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
      // キャンバスサイズ変更で座標 LUT を無効化（次回 draw で再構築）
      posLUTLength = 0;
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
  // - a_pos: ドットの CSS px 座標 (x, y)
  // - a_rgb: ドットの色 [0,1]
  // - gl_PointSize: ドットの物理ピクセルサイズ
  const vsSource = `#version 300 es
precision mediump float;

in vec2  a_pos;
in vec3  a_rgb;

uniform float u_canvasW;
uniform float u_canvasH;
uniform float u_pointSize;

out vec3 v_rgb;

void main() {
    gl_Position = vec4(
        (a_pos.x / u_canvasW) * 2.0 - 1.0,
        1.0 - (a_pos.y / u_canvasH) * 2.0,
        0.0, 1.0
    );
    gl_PointSize = u_pointSize;
    v_rgb = a_rgb;
}`;

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
    console.error('[spiralWebglWorker] program link error:', gl.getProgramInfoLog(program));
    return false;
  }

  uCanvasW = gl.getUniformLocation(program, 'u_canvasW');
  uCanvasH = gl.getUniformLocation(program, 'u_canvasH');
  uPointSize = gl.getUniformLocation(program, 'u_pointSize');

  const aPos = gl.getAttribLocation(program, 'a_pos');
  const aRGB = gl.getAttribLocation(program, 'a_rgb');

  const INIT_CAP = 8192 * POINT_STRIDE;
  pointData = new Float32Array(INIT_CAP);
  pointVBOCap = INIT_CAP;
  pointVBO = gl.createBuffer();
  gl.bindBuffer(gl.ARRAY_BUFFER, pointVBO);
  gl.bufferData(gl.ARRAY_BUFFER, INIT_CAP * Float32Array.BYTES_PER_ELEMENT, gl.DYNAMIC_DRAW);

  vao = gl.createVertexArray();
  gl.bindVertexArray(vao);

  const FSIZE = Float32Array.BYTES_PER_ELEMENT;
  gl.bindBuffer(gl.ARRAY_BUFFER, pointVBO);

  // a_pos: offset 0 (x, y)
  gl.enableVertexAttribArray(aPos);
  gl.vertexAttribPointer(aPos, 2, gl.FLOAT, false, POINT_STRIDE * FSIZE, 0);

  // a_rgb: offset 2 * FSIZE
  gl.enableVertexAttribArray(aRGB);
  gl.vertexAttribPointer(aRGB, 3, gl.FLOAT, false, POINT_STRIDE * FSIZE, 2 * FSIZE);

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
    console.error('[spiralWebglWorker] shader compile error:', gl.getShaderInfoLog(shader));
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

function draw() {
  if (!renderParams) return;
  if (useWebGL) drawWebGL();
  else drawCanvas2D();
}

// ── GPU バッファ容量確保 ──────────────────────────────────
function ensurePointCapacity(count) {
  const needed = count * POINT_STRIDE;
  if (needed <= pointVBOCap) return;
  let newCap = pointVBOCap;
  while (newCap < needed) newCap *= 2;
  pointData = new Float32Array(newCap);
  pointVBOCap = newCap;
  gl.bindBuffer(gl.ARRAY_BUFFER, pointVBO);
  gl.bufferData(gl.ARRAY_BUFFER, newCap * Float32Array.BYTES_PER_ELEMENT, gl.DYNAMIC_DRAW);
}

// ── WebGL 描画 ────────────────────────────────────────────
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

  buildPosLUT(arrayLength, cssW, cssH);

  let maxValue = 0;
  for (let i = 0; i < arrayLength; i++) if (array[i] > maxValue) maxValue = array[i];
  if (maxValue === 0) maxValue = 1;

  buildRgbLUT(maxValue);

  const dotRadius = arrayLength <= 64 ? 5 : arrayLength <= 256 ? 3 : arrayLength <= 1024 ? 2 : 1;
  const pointSize = dotRadius * 2 * dpr;

  const compareSet = new Set(compareIndices);
  const swapSet = new Set(swapIndices);
  const readSet = new Set(readIndices);
  const writeSet = new Set(writeIndices);

  // バッファー配列の数（ソート完了時はゼロ扱い）
  const bufferCount = (isSortCompleted || showCompletionHighlight) ? 0 : arrays.buffers.size;
  const showBuffers = bufferCount > 0;

  // メイン配列 + バッファー配列の合計点数を見積もり
  let totalPoints = arrayLength;
  if (showBuffers) {
    arrays.buffers.forEach(buf => { if (buf) totalPoints += buf.length; });
  }
  ensurePointCapacity(totalPoints);

  let ptr = 0;

  // メイン配列のドットを VBO に書き込み
  for (let i = 0; i < arrayLength; i++) {
    const v = array[i];
    const rr = (v / maxValue) * spiralMaxR;
    const x = spiralCx + rr * posLUTX[i];
    const y = spiralCy + rr * posLUTY[i];
    let r, g, b;
    if (showCompletionHighlight) {
      [r, g, b] = RGB.sorted;
    } else if (swapSet.has(i)) {
      [r, g, b] = RGB.swap;
    } else if (compareSet.has(i)) {
      [r, g, b] = RGB.compare;
    } else if (writeSet.has(i)) {
      [r, g, b] = RGB.write;
    } else if (readSet.has(i)) {
      [r, g, b] = RGB.read;
    } else {
      const vi = v * 3;
      r = rgbLUT[vi]; g = rgbLUT[vi + 1]; b = rgbLUT[vi + 2];
    }
    pointData[ptr++] = x;
    pointData[ptr++] = y;
    pointData[ptr++] = r;
    pointData[ptr++] = g;
    pointData[ptr++] = b;
  }

  // バッファー配列のドットをボトムバンドに並べて VBO に追記
  if (showBuffers) {
    const sortedBufferIds = [...arrays.buffers.keys()].sort((a, b) => a - b);
    const bufferBandH = Math.min(cssH * 0.18 / bufferCount, 80);

    for (let bi = 0; bi < sortedBufferIds.length; bi++) {
      const bufferId = sortedBufferIds[bi];
      const bufferArray = arrays.buffers.get(bufferId);
      if (!bufferArray || bufferArray.length === 0) continue;

      const bufferLength = bufferArray.length;
      let bufferMaxValue = 0;
      for (let i = 0; i < bufferLength; i++) if (bufferArray[i] > bufferMaxValue) bufferMaxValue = bufferArray[i];
      if (bufferMaxValue === 0) bufferMaxValue = 1;

      const bufferSectionY = cssH - (bufferCount - bi) * bufferBandH;
      const bufferUsableH = bufferBandH - 16;
      const bufferXStep = cssW / bufferLength;

      ensurePointCapacity(ptr / POINT_STRIDE + bufferLength);

      for (let i = 0; i < bufferLength; i++) {
        const x = (i + 0.5) * bufferXStep;
        const y = bufferSectionY + bufferUsableH - (bufferArray[i] / bufferMaxValue) * bufferUsableH;
        pointData[ptr++] = x;
        pointData[ptr++] = y;
        pointData[ptr++] = RGB.buffer[0];
        pointData[ptr++] = RGB.buffer[1];
        pointData[ptr++] = RGB.buffer[2];
      }
      totalPoints = ptr / POINT_STRIDE;
    }
  }

  gl.clear(gl.COLOR_BUFFER_BIT);
  gl.useProgram(program);
  gl.uniform1f(uCanvasW, cssW);
  gl.uniform1f(uCanvasH, cssH);
  gl.uniform1f(uPointSize, pointSize);

  gl.bindBuffer(gl.ARRAY_BUFFER, pointVBO);
  gl.bufferSubData(gl.ARRAY_BUFFER, 0, pointData, 0, ptr);
  gl.bindVertexArray(vao);
  gl.drawArrays(gl.POINTS, 0, ptr / POINT_STRIDE);
  gl.bindVertexArray(null);
}

// ── Canvas 2D フォールバック描画 ──────────────────────────
function drawCanvas2D() {
  if (!ctx || !arrays.main || !renderParams) return;

  const {
    compareIndices, swapIndices, readIndices, writeIndices,
    isSortCompleted, showCompletionHighlight
  } = renderParams;
  const array = arrays.main;
  const width = offscreen.width / dpr;
  const height = offscreen.height / dpr;
  const arrayLength = array.length;

  ctx.fillStyle = '#1A1A1A';
  ctx.fillRect(0, 0, width, height);

  if (arrayLength === 0) return;

  let maxValue = 0;
  for (let i = 0; i < arrayLength; i++) if (array[i] > maxValue) maxValue = array[i];
  if (maxValue === 0) maxValue = 1;

  buildColorLUT(maxValue);
  buildPosLUT(arrayLength, width, height);

  const dotRadius = arrayLength <= 64 ? 5 : arrayLength <= 256 ? 3 : arrayLength <= 1024 ? 2 : 1;
  const dotSize = dotRadius * 2;

  const compareSet = new Set(compareIndices);
  const swapSet = new Set(swapIndices);
  const readSet = new Set(readIndices);
  const writeSet = new Set(writeIndices);

  if (showCompletionHighlight) {
    ctx.fillStyle = colors.sorted;
    for (let i = 0; i < arrayLength; i++) {
      const rr = (array[i] / maxValue) * spiralMaxR;
      const x = spiralCx + rr * posLUTX[i];
      const y = spiralCy + rr * posLUTY[i];
      ctx.fillRect(x - dotRadius, y - dotRadius, dotSize, dotSize);
    }
  } else {
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

    for (const i of normalBucket) {
      const rr = (array[i] / maxValue) * spiralMaxR;
      const x = spiralCx + rr * posLUTX[i];
      const y = spiralCy + rr * posLUTY[i];
      ctx.fillStyle = colorLUT[array[i]];
      ctx.fillRect(x - dotRadius, y - dotRadius, dotSize, dotSize);
    }

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
        const rr = (array[i] / maxValue) * spiralMaxR;
        const x = spiralCx + rr * posLUTX[i];
        const y = spiralCy + rr * posLUTY[i];
        ctx.fillRect(x - dotRadius, y - dotRadius, dotSize, dotSize);
      }
    }
  }

  // バッファー配列をボトムバンドに描画（ソート完了時は非表示）
  const bufferCount = (isSortCompleted || showCompletionHighlight) ? 0 : arrays.buffers.size;
  if (bufferCount > 0) {
    const sortedBufferIds = [...arrays.buffers.keys()].sort((a, b) => a - b);
    const bufferBandH = Math.min(height * 0.18 / bufferCount, 80);

    for (let bi = 0; bi < sortedBufferIds.length; bi++) {
      const bufferId = sortedBufferIds[bi];
      const bufferArray = arrays.buffers.get(bufferId);
      if (!bufferArray || bufferArray.length === 0) continue;

      const bufferLength = bufferArray.length;
      let bufferMaxValue = 0;
      for (let i = 0; i < bufferLength; i++) if (bufferArray[i] > bufferMaxValue) bufferMaxValue = bufferArray[i];
      if (bufferMaxValue === 0) bufferMaxValue = 1;

      const bufferSectionY = height - (bufferCount - bi) * bufferBandH;
      const bufferUsableH = bufferBandH - 16;
      const bufferXStep = width / bufferLength;
      const bufferDotRadius = bufferLength <= 64 ? 3 : bufferLength <= 256 ? 2 : 1;
      const bufferDotSize = bufferDotRadius * 2;

      ctx.fillStyle = '#06B6D4';
      for (let i = 0; i < bufferLength; i++) {
        const x = (i + 0.5) * bufferXStep - bufferDotRadius;
        const y = bufferSectionY + bufferUsableH - (bufferArray[i] / bufferMaxValue) * bufferUsableH - bufferDotRadius;
        ctx.fillRect(x, y, bufferDotSize, bufferDotSize);
      }

      ctx.fillStyle = '#888';
      ctx.font = '12px monospace';
      ctx.fillText(`Buffer #${bufferId}`, 10, bufferSectionY + 14);
    }
  }
}

// ── クリーンアップ ─────────────────────────────────────────
function cleanup() {
  if (useWebGL && gl) {
    if (vao) { gl.deleteVertexArray(vao); vao = null; }
    if (pointVBO) { gl.deleteBuffer(pointVBO); pointVBO = null; }
    if (program) { gl.deleteProgram(program); program = null; }
    gl = null;
  }
  offscreen = null;
  ctx = null;
  arrays = { main: null, buffers: new Map() };
  renderParams = null;
  isDirty = false;
  isLoopRunning = false;
}
