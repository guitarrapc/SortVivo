'use strict';
// WebGL2 円形描画 Worker (Phase 6 - Circular)
// WebGL2 で GPU 直接レンダリング。WebGL2 利用不可の場合は Canvas 2D にフォールバック。
// メッセージインタフェースは circularRenderWorker.js と完全互換。

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
const INST_STRIDE = 5;         // floats / bar: inner, outer, r, g, b

// Uniform locations (キャッシュ)
let uAngleStep = null;
let uLineHalfWidth = null;
let uCenterX = null;
let uCenterY = null;
let uCanvasW = null;
let uCanvasH = null;

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

// HSL (hue[0-360], s[0-100], l[0-100]) → RGB [0, 1]
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

function valueToRgb(value, maxValue) {
  return hslToRgb((value / maxValue) * 360, 70, 60);
}

// Canvas 2D フォールバック用
function valueToHSL(value, maxValue) {
  return `hsl(${(value / maxValue) * 360}, 70%, 60%)`;
}

// 三角関数ルックアップテーブル（メイン配列）
let lutLength = 0;
let cosLUT = null;
let sinLUT = null;
// 三角関数ルックアップテーブル（バッファー配列）
let bufferLutLength = 0;
let bufferCosLUT = null;
let bufferSinLUT = null;
// HSL カラールックアップテーブル（Canvas 2D フォールバック用）
let colorLUTMax = -1;
let colorLUT = null;
// RGB float カラールックアップテーブル（WebGL 用）
let rgbLUTMax = -1;
let rgbLUT = null; // Float32Array: [r0,g0,b0, r1,g1,b1, ...]

function buildTrigLUT(arrayLength) {
  if (lutLength === arrayLength) return;
  lutLength = arrayLength;
  const angleStep = (2 * Math.PI) / arrayLength;
  cosLUT = new Float64Array(arrayLength);
  sinLUT = new Float64Array(arrayLength);
  for (let i = 0; i < arrayLength; i++) {
    const angle = i * angleStep - Math.PI / 2;
    cosLUT[i] = Math.cos(angle);
    sinLUT[i] = Math.sin(angle);
  }
}

function buildBufferTrigLUT(length) {
  if (bufferLutLength === length) return;
  bufferLutLength = length;
  const angleStep = (2 * Math.PI) / length;
  bufferCosLUT = new Float64Array(length);
  bufferSinLUT = new Float64Array(length);
  for (let i = 0; i < length; i++) {
    const angle = i * angleStep - Math.PI / 2;
    bufferCosLUT[i] = Math.cos(angle);
    bufferSinLUT[i] = Math.sin(angle);
  }
}

function buildColorLUT(maxValue) {
  if (colorLUTMax === maxValue) return;
  colorLUTMax = maxValue;
  colorLUT = new Array(maxValue + 1);
  for (let v = 0; v <= maxValue; v++) {
    const hue = (v / maxValue) * 360;
    colorLUT[v] = `hsl(${hue}, 70%, 60%)`;
  }
}

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

// ── WebGL2 初期化 ──────────────────────────────────────────
function initWebGL(canvas) {
  try {
    gl = canvas.getContext('webgl2', {
      alpha: false,
      antialias: true,   // 円形描画でアンチエイリアスを有効化
      depth: false,
      stencil: false,
    });
  } catch (_) { return false; }
  if (!gl) return false;

  // 頂点シェーダー:
  //   a_quad      : ユニット矩形の角 (sideT, radialT) per-vertex
  //   a_inner     : 内側半径 CSS px per-instance
  //   a_outer     : 外側半径 CSS px per-instance
  //   a_rgb       : バー色 [0,1] per-instance
  //   gl_InstanceID: バーインデックス (built-in)
  //
  // 各バーは角度方向に沿った薄いクワッドとして描画する。
  //   radial 方向 = dir = (cos(angle), sin(angle))
  //   垂直方向    = perp = (-sin(angle), cos(angle))
  const vsSource = `#version 300 es
precision mediump float;

in vec2  a_quad;
in float a_inner;
in float a_outer;
in vec3  a_rgb;

uniform float u_angleStep;
uniform float u_lineHalfWidth;
uniform float u_centerX;
uniform float u_centerY;
uniform float u_canvasW;
uniform float u_canvasH;

out vec3 v_rgb;

void main() {
    float barIndex = float(gl_InstanceID);
    float angle = barIndex * u_angleStep - 3.14159265358979 / 2.0;

    vec2 dir  = vec2(cos(angle), sin(angle));
    vec2 perp = vec2(-sin(angle), cos(angle));

    // a_quad.y [0,1] → [inner, outer]（放射方向）
    // a_quad.x [0,1] → [-halfW, +halfW]（垂直方向）
    float radius     = mix(a_inner, a_outer, a_quad.y);
    float perpOffset = (a_quad.x - 0.5) * 2.0 * u_lineHalfWidth;

    vec2 pos = vec2(u_centerX, u_centerY) + dir * radius + perp * perpOffset;

    gl_Position = vec4(
        (pos.x / u_canvasW) * 2.0 - 1.0,
        1.0 - (pos.y / u_canvasH) * 2.0,
        0.0, 1.0
    );
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
    console.error('[circularWebglWorker] program link error:', gl.getProgramInfoLog(program));
    return false;
  }

  // Uniform locations をキャッシュ
  uAngleStep = gl.getUniformLocation(program, 'u_angleStep');
  uLineHalfWidth = gl.getUniformLocation(program, 'u_lineHalfWidth');
  uCenterX = gl.getUniformLocation(program, 'u_centerX');
  uCenterY = gl.getUniformLocation(program, 'u_centerY');
  uCanvasW = gl.getUniformLocation(program, 'u_canvasW');
  uCanvasH = gl.getUniformLocation(program, 'u_canvasH');

  // Attribute locations
  const aQuad = gl.getAttribLocation(program, 'a_quad');
  const aInner = gl.getAttribLocation(program, 'a_inner');
  const aOuter = gl.getAttribLocation(program, 'a_outer');
  const aRGB = gl.getAttribLocation(program, 'a_rgb');

  // Quad VBO: 静的ユニット矩形 (sideT, radialT) × 6 頂点
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

  // a_inner: offset 0
  gl.enableVertexAttribArray(aInner);
  gl.vertexAttribPointer(aInner, 1, gl.FLOAT, false, INST_STRIDE * FSIZE, 0 * FSIZE);
  gl.vertexAttribDivisor(aInner, 1);

  // a_outer: offset 1 * FSIZE
  gl.enableVertexAttribArray(aOuter);
  gl.vertexAttribPointer(aOuter, 1, gl.FLOAT, false, INST_STRIDE * FSIZE, 1 * FSIZE);
  gl.vertexAttribDivisor(aOuter, 1);

  // a_rgb: offset 2 * FSIZE
  gl.enableVertexAttribArray(aRGB);
  gl.vertexAttribPointer(aRGB, 3, gl.FLOAT, false, INST_STRIDE * FSIZE, 2 * FSIZE);
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
    console.error('[circularWebglWorker] shader compile error:', gl.getShaderInfoLog(shader));
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

// ── WebGL2 描画 ────────────────────────────────────────────

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
 */
function uploadAndDrawInstanced(count, angleStep, lineHalfWidth, centerX, centerY) {
  gl.uniform1f(uAngleStep, angleStep);
  gl.uniform1f(uLineHalfWidth, lineHalfWidth);
  gl.uniform1f(uCenterX, centerX);
  gl.uniform1f(uCenterY, centerY);

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

  // セクション分割
  const bufferCount = (isSortCompleted || showCompletionHighlight) ? 0 : arrays.buffers.size;
  const showBuffers = bufferCount > 0;

  // 円のジオメトリ計算（CSS px 単位）
  const centerX = cssW / 2;
  const centerY = cssH / 2;
  const maxRadius = Math.min(cssW, cssH) * 0.45;
  const minRadius = maxRadius * 0.2;

  let mainMinRadius, mainMaxRadius;
  let ringWidth = 0;

  if (showBuffers) {
    const totalRings = 1 + bufferCount;
    ringWidth = (maxRadius * 0.8) / totalRings;
    mainMinRadius = minRadius;
    mainMaxRadius = minRadius + ringWidth;
  } else {
    mainMinRadius = minRadius;
    mainMaxRadius = maxRadius;
  }

  // 最大値（ループで計算）
  let maxValue = 0;
  for (let i = 0; i < arrayLength; i++) if (array[i] > maxValue) maxValue = array[i];
  if (maxValue === 0) maxValue = 1;

  // RGB LUT を構築（最大値が変わったときのみ再構築）
  buildRgbLUT(maxValue);

  const angleStep = (2 * Math.PI) / arrayLength;
  // lineHalfWidth: Canvas 2D の lineWidth の半分に対応（1px → 0.5px 半幅）
  const lineHalfWidth = arrayLength <= 64 ? 1.5 : arrayLength <= 256 ? 1.0 : arrayLength <= 1024 ? 0.75 : 0.5;

  gl.clear(gl.COLOR_BUFFER_BIT);
  gl.useProgram(program);
  gl.uniform1f(uCanvasW, cssW);
  gl.uniform1f(uCanvasH, cssH);
  gl.bindVertexArray(vao);

  // ── メイン配列 ──────────────────────────────────────
  ensureInstanceCapacity(arrayLength);

  if (showCompletionHighlight) {
    const [r, g, b] = RGB.sorted;
    for (let i = 0; i < arrayLength; i++) {
      const outer = mainMinRadius + (array[i] / maxValue) * (mainMaxRadius - mainMinRadius);
      const base = i * INST_STRIDE;
      instanceData[base] = mainMinRadius;
      instanceData[base + 1] = outer;
      instanceData[base + 2] = r;
      instanceData[base + 3] = g;
      instanceData[base + 4] = b;
    }
  } else {
    const cmpSet = new Set(compareIndices);
    const swpSet = new Set(swapIndices);
    const rdSet = new Set(readIndices);
    const wrSet = new Set(writeIndices);

    for (let i = 0; i < arrayLength; i++) {
      const outer = mainMinRadius + (array[i] / maxValue) * (mainMaxRadius - mainMinRadius);
      const base = i * INST_STRIDE;
      instanceData[base] = mainMinRadius;
      instanceData[base + 1] = outer;

      let r, g, b;
      if (swpSet.has(i)) { [r, g, b] = RGB.swap; }
      else if (cmpSet.has(i)) { [r, g, b] = RGB.compare; }
      else if (wrSet.has(i)) { [r, g, b] = RGB.write; }
      else if (rdSet.has(i)) { [r, g, b] = RGB.read; }
      else { const vi = array[i] * 3; r = rgbLUT[vi]; g = rgbLUT[vi + 1]; b = rgbLUT[vi + 2]; }

      instanceData[base + 2] = r;
      instanceData[base + 3] = g;
      instanceData[base + 4] = b;
    }
  }

  uploadAndDrawInstanced(arrayLength, angleStep, lineHalfWidth, centerX, centerY);

  // ── バッファー配列を同心円リングとして描画 ──────────
  if (showBuffers) {
    const sortedBufferIds = [...arrays.buffers.keys()].sort((a, b) => a - b);
    const [br, bg, bb] = RGB.buffer;

    for (let bufferIndex = 0; bufferIndex < sortedBufferIds.length; bufferIndex++) {
      const bufferArray = arrays.buffers.get(sortedBufferIds[bufferIndex]);
      if (!bufferArray || bufferArray.length === 0) continue;

      const ringIndex = bufferIndex + 1;
      const bufferMinRadius = minRadius + ringIndex * ringWidth;
      const bufferMaxRadius = bufferMinRadius + ringWidth;

      let bufferMaxValue = 0;
      const bufferLength = bufferArray.length;
      for (let i = 0; i < bufferLength; i++) {
        if (bufferArray[i] > bufferMaxValue) bufferMaxValue = bufferArray[i];
      }
      if (bufferMaxValue === 0) bufferMaxValue = 1;

      const bufAngleStep = (2 * Math.PI) / bufferLength;
      const bufLineHalfWidth = bufferLength <= 64 ? 1.5 : bufferLength <= 256 ? 1.0 : bufferLength <= 1024 ? 0.75 : 0.5;

      ensureInstanceCapacity(bufferLength);
      for (let i = 0; i < bufferLength; i++) {
        const outer = bufferMinRadius + (bufferArray[i] / bufferMaxValue) * (bufferMaxRadius - bufferMinRadius);
        const base = i * INST_STRIDE;
        instanceData[base] = bufferMinRadius;
        instanceData[base + 1] = outer;
        instanceData[base + 2] = br;
        instanceData[base + 3] = bg;
        instanceData[base + 4] = bb;
      }
      uploadAndDrawInstanced(bufferLength, bufAngleStep, bufLineHalfWidth, centerX, centerY);
    }
  }

  gl.bindVertexArray(null);
}

// ── Canvas 2D 描画（WebGL2 フォールバック） ───────────────
// circularRenderWorker.js の draw() と同一ロジック
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

  ctx.fillStyle = '#1A1A1A';
  ctx.fillRect(0, 0, width, height);

  if (arrayLength === 0) return;

  const centerX = width / 2;
  const centerY = height / 2;
  const maxRadius = Math.min(width, height) * 0.45;
  const minRadius = maxRadius * 0.2;

  let mainMinRadius, mainMaxRadius;
  let ringWidth = 0;

  if (showBuffers) {
    const totalRings = 1 + bufferCount;
    ringWidth = (maxRadius * 0.8) / totalRings;
    mainMinRadius = minRadius;
    mainMaxRadius = minRadius + ringWidth;
  } else {
    mainMinRadius = minRadius;
    mainMaxRadius = maxRadius;
  }

  let maxValue = 0;
  for (let i = 0; i < arrayLength; i++) {
    if (array[i] > maxValue) maxValue = array[i];
  }

  const compareSet = new Set(compareIndices);
  const swapSet = new Set(swapIndices);
  const readSet = new Set(readIndices);
  const writeSet = new Set(writeIndices);

  // LUT を構築（配列サイズ・最大値が変わったときのみ再構築）
  buildTrigLUT(arrayLength);
  buildColorLUT(maxValue);

  const lineWidth = arrayLength <= 64 ? 3 : arrayLength <= 256 ? 2 : arrayLength <= 1024 ? 1.5 : 1;

  ctx.lineWidth = lineWidth;
  if (showCompletionHighlight) {
    ctx.strokeStyle = colors.sorted;
    ctx.beginPath();
    for (let i = 0; i < arrayLength; i++) {
      const radius = mainMinRadius + (array[i] / maxValue) * (mainMaxRadius - mainMinRadius);
      const ci = cosLUT[i], si = sinLUT[i];
      ctx.moveTo(centerX + ci * mainMinRadius, centerY + si * mainMinRadius);
      ctx.lineTo(centerX + ci * radius, centerY + si * radius);
    }
    ctx.stroke();
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
      const radius = mainMinRadius + (array[i] / maxValue) * (mainMaxRadius - mainMinRadius);
      const ci = cosLUT[i], si = sinLUT[i];
      ctx.strokeStyle = colorLUT[array[i]];
      ctx.beginPath();
      ctx.moveTo(centerX + ci * mainMinRadius, centerY + si * mainMinRadius);
      ctx.lineTo(centerX + ci * radius, centerY + si * radius);
      ctx.stroke();
    }

    const highlightBuckets = [
      [compareBucket, colors.compare],
      [writeBucket, colors.write],
      [readBucket, colors.read],
      [swapBucket, colors.swap],
    ];

    for (const [indices, color] of highlightBuckets) {
      if (indices.length === 0) continue;
      ctx.strokeStyle = color;
      ctx.beginPath();
      for (const i of indices) {
        const radius = mainMinRadius + (array[i] / maxValue) * (mainMaxRadius - mainMinRadius);
        const ci = cosLUT[i], si = sinLUT[i];
        ctx.moveTo(centerX + ci * mainMinRadius, centerY + si * mainMinRadius);
        ctx.lineTo(centerX + ci * radius, centerY + si * radius);
      }
      ctx.stroke();
    }
  }

  if (showBuffers) {
    const sortedBufferIds = [...arrays.buffers.keys()].sort((a, b) => a - b);

    for (let bufferIndex = 0; bufferIndex < sortedBufferIds.length; bufferIndex++) {
      const bufferId = sortedBufferIds[bufferIndex];
      const bufferArray = arrays.buffers.get(bufferId);
      if (!bufferArray || bufferArray.length === 0) continue;

      const ringIndex = bufferIndex + 1;
      const bufferMinRadius = minRadius + ringIndex * ringWidth;
      const bufferMaxRadius = bufferMinRadius + ringWidth;

      let bufferMaxValue = 0;
      const bufferLength = bufferArray.length;
      for (let i = 0; i < bufferLength; i++) {
        if (bufferArray[i] > bufferMaxValue) bufferMaxValue = bufferArray[i];
      }

      buildBufferTrigLUT(bufferLength);
      const bufferLineWidth = bufferLength <= 64 ? 3 : bufferLength <= 256 ? 2 : bufferLength <= 1024 ? 1.5 : 1;

      ctx.strokeStyle = '#06B6D4';
      ctx.lineWidth = bufferLineWidth;
      ctx.beginPath();
      for (let i = 0; i < bufferLength; i++) {
        const radius = bufferMinRadius + (bufferArray[i] / bufferMaxValue) * (bufferMaxRadius - bufferMinRadius);
        const ci = bufferCosLUT[i], si = bufferSinLUT[i];
        ctx.moveTo(centerX + ci * bufferMinRadius, centerY + si * bufferMinRadius);
        ctx.lineTo(centerX + ci * radius, centerY + si * radius);
      }
      ctx.stroke();

      const labelAngle = -Math.PI / 2;
      const labelRadius = bufferMaxRadius + 15;
      const labelX = centerX + Math.cos(labelAngle) * labelRadius;
      const labelY = centerY + Math.sin(labelAngle) * labelRadius;

      ctx.fillStyle = '#888';
      ctx.font = '12px monospace';
      ctx.textAlign = 'center';
      ctx.fillText(`Buf#${bufferId}`, labelX, labelY);
    }
  }

  ctx.fillStyle = '#2A2A2A';
  ctx.beginPath();
  ctx.arc(centerX, centerY, minRadius, 0, 2 * Math.PI);
  ctx.fill();
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
