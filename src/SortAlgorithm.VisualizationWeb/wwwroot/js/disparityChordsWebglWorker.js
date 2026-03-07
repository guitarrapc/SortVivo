'use strict';
// WebGL2 不均衡和音描画 Worker（Disparity Chords）
// WebGL2 で gl.LINES 弦 + gl.POINTS ドット を GPU レンダリング。
// WebGL2 利用不可の場合は Canvas 2D にフォールバック。
// メッセージインタフェースは disparityChordsRenderWorker.js と完全互換。

// ── 共有状態 ────────────────────────────────────────────────────────────────
let offscreen  = null;
let gl         = null;   // WebGL2RenderingContext
let ctx        = null;   // Canvas2DRenderingContext（フォールバック時）
let dpr        = 1;
let useWebGL   = false;

// WebGL リソース
let program    = null;
let vbo        = null;        // 汎用再利用バッファ
let vboData    = null;        // Float32Array（毎フレーム再利用）
let vboCap     = 0;           // VBO 容量 (floats)
let vao        = null;

const STRIDE   = 6;           // x, y, r, g, b, a per vertex

// Uniform locations（キャッシュ）
let uCanvasW   = null;
let uCanvasH   = null;
let uPointSize = null;

// 色定義 [0,1] RGBA（WebGL 用）
const RGBA = {
  compare: [168/255,  85/255, 247/255, 1.0],
  swap:    [239/255,  68/255,  68/255, 1.0],
  write:   [249/255, 115/255,  22/255, 1.0],
  read:    [251/255, 191/255,  36/255, 1.0],
  sorted:  [ 16/255, 185/255, 129/255, 1.0],
};

// 色定義 CSS 文字列（Canvas 2D フォールバック用）
const colors = {
  compare: '#A855F7',
  swap:    '#EF4444',
  write:   '#F97316',
  read:    '#FBBF24',
  sorted:  '#10B981',
};

// ── LUT ─────────────────────────────────────────────────────────────────────

function hslToRgb(h, s, l) {
  s /= 100; l /= 100;
  const c = (1 - Math.abs(2*l - 1)) * s;
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
let rgbLUT    = null;

function buildRgbLUT(maxValue) {
  if (rgbLUTMax === maxValue) return;
  rgbLUTMax = maxValue;
  rgbLUT = new Float32Array((maxValue + 1) * 3);
  for (let v = 0; v <= maxValue; v++) {
    const [r, g, b] = hslToRgb((v / maxValue) * 360, 70, 65);
    rgbLUT[v*3] = r; rgbLUT[v*3+1] = g; rgbLUT[v*3+2] = b;
  }
}

// 三角関数 LUT
let lutLength = 0;
let cosLUT    = null;
let sinLUT    = null;

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

// HSL カラー LUT（Canvas 2D フォールバック用）
let colorLUTMax = -1;
let colorLUT    = null;

function buildColorLUT(maxValue) {
  if (colorLUTMax === maxValue) return;
  colorLUTMax = maxValue;
  colorLUT = new Array(maxValue + 1);
  for (let v = 0; v <= maxValue; v++) {
    const hue = (v / maxValue) * 360;
    colorLUT[v] = `hsla(${hue.toFixed(1)}, 70%, 65%, 0.55)`;
  }
}

// 配列状態
let arrays      = { main: null, buffers: new Map() };
let renderParams = null;
let isDirty      = false;
let isLoopRunning = false;

const _raf = typeof requestAnimationFrame !== 'undefined'
  ? cb => requestAnimationFrame(cb)
  : cb => setTimeout(cb, 1000 / 60);

// ── メッセージハンドラ ────────────────────────────────────────────────────────
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
        for (const [id, arr] of Object.entries(msg.bufferArrays)) {
          arrays.buffers.set(parseInt(id), new Int32Array(arr));
        }
      }
      renderParams = {
        compareIndices: msg.compareIndices, swapIndices: msg.swapIndices,
        readIndices: msg.readIndices,       writeIndices: msg.writeIndices,
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
        for (const [id, delta] of Object.entries(msg.bufferDeltas)) {
          const bid = parseInt(id);
          let buf = arrays.buffers.get(bid);
          if (!buf) { buf = new Int32Array(arrays.main.length); arrays.buffers.set(bid, buf); }
          for (let k = 0; k < delta.length; k += 2) buf[delta[k]] = delta[k + 1];
        }
      }
      if (msg.isSortCompleted && arrays.buffers.size > 0) arrays.buffers.clear();
      renderParams = {
        compareIndices: msg.compareIndices, swapIndices: msg.swapIndices,
        readIndices: msg.readIndices,       writeIndices: msg.writeIndices,
        isSortCompleted: msg.isSortCompleted || false,
        showCompletionHighlight: msg.showCompletionHighlight || false,
      };
      scheduleDraw();
      break;
    }
    case 'resize': {
      if (!offscreen) break;
      offscreen.width  = msg.newWidth;
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

// ── WebGL2 初期化 ────────────────────────────────────────────────────────────
function initWebGL(canvas) {
  try {
    gl = canvas.getContext('webgl2', {
      alpha: false, antialias: true, depth: false, stencil: false,
    });
  } catch (_) { return false; }
  if (!gl) return false;

  // 頂点シェーダー: CSS px 座標 + RGBA カラー → クリップ座標
  // gl.LINES・gl.POINTS 共通で使用。gl_PointSize は POINTS 描画時のみ有効。
  const vsSource = `#version 300 es
precision mediump float;
in vec2  a_pos;
in vec4  a_color;
uniform float u_canvasW;
uniform float u_canvasH;
uniform float u_pointSize;
out vec4 v_color;
void main() {
    gl_Position = vec4(
        (a_pos.x / u_canvasW) * 2.0 - 1.0,
        1.0 - (a_pos.y / u_canvasH) * 2.0,
        0.0, 1.0
    );
    gl_PointSize = u_pointSize;
    v_color = a_color;
}`;

  const fsSource = `#version 300 es
precision mediump float;
in  vec4 v_color;
out vec4 outColor;
void main() { outColor = v_color; }`;

  const vs = compileShader(gl.VERTEX_SHADER,   vsSource);
  const fs = compileShader(gl.FRAGMENT_SHADER, fsSource);
  if (!vs || !fs) return false;

  program = gl.createProgram();
  gl.attachShader(program, vs);
  gl.attachShader(program, fs);
  gl.linkProgram(program);
  gl.deleteShader(vs);
  gl.deleteShader(fs);
  if (!gl.getProgramParameter(program, gl.LINK_STATUS)) {
    console.error('[disparityChordsWebglWorker] link error:', gl.getProgramInfoLog(program));
    return false;
  }

  uCanvasW   = gl.getUniformLocation(program, 'u_canvasW');
  uCanvasH   = gl.getUniformLocation(program, 'u_canvasH');
  uPointSize = gl.getUniformLocation(program, 'u_pointSize');

  const aPos   = gl.getAttribLocation(program, 'a_pos');
  const aColor = gl.getAttribLocation(program, 'a_color');

  // INIT_CAP: n=4096 の弦クワッド（4096弦 × 6頂点 × STRIDE floats = 147,456）+ ドット（4096 × STRIDE = 24,576）を余裕で収容
  // 32768 * 6 = 196,608 floats → 最大 n = floor(196,608 / (6 * 6)) = 5461 要素まで対応（弦クワッド最大ケース）
  const INIT_CAP = 32768 * STRIDE;  // 196,608 floats
  vboData = new Float32Array(INIT_CAP);
  vboCap  = INIT_CAP;
  vbo = gl.createBuffer();
  gl.bindBuffer(gl.ARRAY_BUFFER, vbo);
  gl.bufferData(gl.ARRAY_BUFFER, INIT_CAP * Float32Array.BYTES_PER_ELEMENT, gl.DYNAMIC_DRAW);

  // VAO: 属性バインド記録
  vao = gl.createVertexArray();
  gl.bindVertexArray(vao);
  gl.bindBuffer(gl.ARRAY_BUFFER, vbo);

  const FSIZE = Float32Array.BYTES_PER_ELEMENT;
  gl.enableVertexAttribArray(aPos);
  gl.vertexAttribPointer(aPos,   2, gl.FLOAT, false, STRIDE * FSIZE, 0);
  gl.enableVertexAttribArray(aColor);
  gl.vertexAttribPointer(aColor, 4, gl.FLOAT, false, STRIDE * FSIZE, 2 * FSIZE);

  gl.bindVertexArray(null);

  gl.clearColor(26/255, 26/255, 26/255, 1.0); // #1A1A1A
  // 弦は半透明で重なるため alpha blending を有効化
  gl.enable(gl.BLEND);
  gl.blendFunc(gl.SRC_ALPHA, gl.ONE_MINUS_SRC_ALPHA);
  gl.viewport(0, 0, canvas.width, canvas.height);

  return true;
}

function compileShader(type, src) {
  const s = gl.createShader(type);
  gl.shaderSource(s, src);
  gl.compileShader(s);
  if (!gl.getShaderParameter(s, gl.COMPILE_STATUS)) {
    console.error('[disparityChordsWebglWorker] shader compile error:', gl.getShaderInfoLog(s));
    gl.deleteShader(s);
    return null;
  }
  return s;
}

function ensureVBOCapacity(needed) {
  if (needed <= vboCap) return;
  let cap = vboCap;
  while (cap < needed) cap *= 2;
  vboData = new Float32Array(cap);
  vboCap  = cap;
  gl.bindBuffer(gl.ARRAY_BUFFER, vbo);
  gl.bufferData(gl.ARRAY_BUFFER, cap * Float32Array.BYTES_PER_ELEMENT, gl.DYNAMIC_DRAW);
}

/** vboData 先頭 count 頂点を GPU にアップロードして描画する */
function uploadAndDraw(count, mode) {
  gl.bindBuffer(gl.ARRAY_BUFFER, vbo);
  gl.bufferSubData(gl.ARRAY_BUFFER, 0, vboData, 0, count * STRIDE);
  gl.drawArrays(mode, 0, count);
}

// ── スケジューリング ─────────────────────────────────────────────────────────
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

// ── WebGL2 描画 ──────────────────────────────────────────────────────────────
function drawWebGL() {
  const { compareIndices, swapIndices, readIndices, writeIndices,
          isSortCompleted, showCompletionHighlight } = renderParams;
  const array = arrays.main;
  const n     = array.length;
  const cssW  = offscreen.width  / dpr;
  const cssH  = offscreen.height / dpr;

  gl.clear(gl.COLOR_BUFFER_BIT);
  if (n === 0) return;

  const cx   = cssW / 2;
  const cy   = cssH / 2;
  const R    = Math.min(cssW, cssH) * 0.44;
  const dotR = n <= 64 ? 4 : n <= 256 ? 3 : 2;

  let maxValue = 0;
  for (let i = 0; i < n; i++) if (array[i] > maxValue) maxValue = array[i];
  if (maxValue === 0) maxValue = 1;

  buildTrigLUT(n);
  buildRgbLUT(maxValue);

  gl.useProgram(program);
  gl.uniform1f(uCanvasW, cssW);
  gl.uniform1f(uCanvasH, cssH);
  gl.bindVertexArray(vao);

  const compareSet = new Set(compareIndices);
  const swapSet    = new Set(swapIndices);
  const readSet    = new Set(readIndices);
  const writeSet   = new Set(writeIndices);

  // 整列後インデックス
  const sortedIdx = new Int32Array(n);
  for (let i = 0; i < n; i++) sortedIdx[i] = Math.round((array[i] / maxValue) * (n - 1));

  // ── ソート完了ハイライト ─────────────────────────────────────────────────
  if (showCompletionHighlight) {
    ensureVBOCapacity(n * STRIDE);
    const [sr, sg, sb] = RGBA.sorted;
    for (let i = 0; i < n; i++) {
      const base = i * STRIDE;
      vboData[base]   = cx + cosLUT[i] * R;
      vboData[base+1] = cy + sinLUT[i] * R;
      vboData[base+2] = sr; vboData[base+3] = sg; vboData[base+4] = sb; vboData[base+5] = 1.0;
    }
    gl.uniform1f(uPointSize, dotR * 2);
    uploadAndDraw(n, gl.POINTS);
    gl.bindVertexArray(null);
    return;
  }

  const CHORD_ALPHA = 0.55;
  const highlightBuckets = [
    [compareIndices, RGBA.compare],
    [writeIndices,   RGBA.write],
    [readIndices,    RGBA.read],
    [swapIndices,    RGBA.swap],
  ];

  // ── 弦を描画（gl.LINES）────────────────────────────────────────────────
  // 最大 n 本 × 2頂点
  ensureVBOCapacity(n * 2 * STRIDE);

  let numChordVerts = 0;

  // 1. 通常弦（HSL グラデーション、半透明）
  for (let i = 0; i < n; i++) {
    if (compareSet.has(i) || swapSet.has(i) || readSet.has(i) || writeSet.has(i)) continue;
    const si = sortedIdx[i];
    if (si === i) continue;
    const vi = array[i] * 3;
    const r = rgbLUT[vi], g = rgbLUT[vi+1], b = rgbLUT[vi+2];
    let base = numChordVerts * STRIDE;
    vboData[base]   = cx + cosLUT[i] *R; vboData[base+1] = cy + sinLUT[i] *R;
    vboData[base+2] = r; vboData[base+3] = g; vboData[base+4] = b; vboData[base+5] = CHORD_ALPHA;
    base += STRIDE;
    vboData[base]   = cx + cosLUT[si]*R; vboData[base+1] = cy + sinLUT[si]*R;
    vboData[base+2] = r; vboData[base+3] = g; vboData[base+4] = b; vboData[base+5] = CHORD_ALPHA;
    numChordVerts += 2;
  }

  // 2. ハイライト弦（不透明、操作種別色）
  for (const [indices, [r, g, b, a]] of highlightBuckets) {
    if (!indices || indices.length === 0) continue;
    for (const i of indices) {
      if (i < 0 || i >= n) continue;
      const si = sortedIdx[i];
      if (si === i) continue;
      let base = numChordVerts * STRIDE;
      vboData[base]   = cx + cosLUT[i] *R; vboData[base+1] = cy + sinLUT[i] *R;
      vboData[base+2] = r; vboData[base+3] = g; vboData[base+4] = b; vboData[base+5] = a;
      base += STRIDE;
      vboData[base]   = cx + cosLUT[si]*R; vboData[base+1] = cy + sinLUT[si]*R;
      vboData[base+2] = r; vboData[base+3] = g; vboData[base+4] = b; vboData[base+5] = a;
      numChordVerts += 2;
    }
  }

  if (numChordVerts > 0) uploadAndDraw(numChordVerts, gl.LINES);

  // ── ドットを描画（gl.POINTS）────────────────────────────────────────────
  // 通常ドット + 正位置ドット
  ensureVBOCapacity(n * STRIDE);
  let numDots = 0;

  for (let i = 0; i < n; i++) {
    if (compareSet.has(i) || swapSet.has(i) || readSet.has(i) || writeSet.has(i)) continue;
    const base = numDots * STRIDE;
    if (sortedIdx[i] === i) {
      // 正位置ドット（緑）
      const [sr, sg, sb, sa] = RGBA.sorted;
      vboData[base]   = cx + cosLUT[i]*R; vboData[base+1] = cy + sinLUT[i]*R;
      vboData[base+2] = sr; vboData[base+3] = sg; vboData[base+4] = sb; vboData[base+5] = sa;
    } else {
      // 通常ドット（HSL）
      const vi = array[i] * 3;
      vboData[base]   = cx + cosLUT[i]*R; vboData[base+1] = cy + sinLUT[i]*R;
      vboData[base+2] = rgbLUT[vi]; vboData[base+3] = rgbLUT[vi+1]; vboData[base+4] = rgbLUT[vi+2]; vboData[base+5] = 1.0;
    }
    numDots++;
  }

  if (numDots > 0) {
    gl.uniform1f(uPointSize, dotR * 2);
    uploadAndDraw(numDots, gl.POINTS);
  }

  // ハイライトドット（大きめ、操作種別色）
  let numHighlightDots = 0;
  ensureVBOCapacity(n * STRIDE);

  for (const [indices, [r, g, b, a]] of highlightBuckets) {
    if (!indices || indices.length === 0) continue;
    for (const i of indices) {
      if (i < 0 || i >= n) continue;
      const base = numHighlightDots * STRIDE;
      vboData[base]   = cx + cosLUT[i]*R; vboData[base+1] = cy + sinLUT[i]*R;
      vboData[base+2] = r; vboData[base+3] = g; vboData[base+4] = b; vboData[base+5] = a;
      numHighlightDots++;
    }
  }

  if (numHighlightDots > 0) {
    gl.uniform1f(uPointSize, (dotR + 1) * 2);
    uploadAndDraw(numHighlightDots, gl.POINTS);
  }

  gl.bindVertexArray(null);
}

// ── Canvas 2D 描画（WebGL2 フォールバック） ──────────────────────────────────
// disparityChordsRenderWorker.js の draw() と同一ロジック
function drawCanvas2D() {
  if (!offscreen || !ctx || !arrays.main || !renderParams) return;

  const { compareIndices, swapIndices, readIndices, writeIndices,
          isSortCompleted, showCompletionHighlight } = renderParams;
  const array  = arrays.main;
  const n      = array.length;
  const width  = offscreen.width  / dpr;
  const height = offscreen.height / dpr;

  ctx.fillStyle = '#1A1A1A';
  ctx.fillRect(0, 0, width, height);
  if (n === 0) return;

  const cx   = width  / 2;
  const cy   = height / 2;
  const R    = Math.min(width, height) * 0.44;
  const dotR = n <= 64 ? 4 : n <= 256 ? 3 : 2;

  ctx.strokeStyle = 'rgba(255,255,255,0.07)';
  ctx.lineWidth = 1;
  ctx.beginPath();
  ctx.arc(cx, cy, R, 0, 2 * Math.PI);
  ctx.stroke();

  let maxValue = 0;
  for (let i = 0; i < n; i++) if (array[i] > maxValue) maxValue = array[i];
  if (maxValue === 0) maxValue = 1;

  buildTrigLUT(n);
  buildColorLUT(maxValue);

  const compareSet = new Set(compareIndices);
  const swapSet    = new Set(swapIndices);
  const readSet    = new Set(readIndices);
  const writeSet   = new Set(writeIndices);

  const sortedIdx = new Int32Array(n);
  for (let i = 0; i < n; i++) sortedIdx[i] = Math.round((array[i] / maxValue) * (n - 1));

  if (showCompletionHighlight) {
    ctx.fillStyle = colors.sorted;
    for (let i = 0; i < n; i++) {
      ctx.beginPath();
      ctx.arc(cx + cosLUT[i]*R, cy + sinLUT[i]*R, dotR, 0, 2 * Math.PI);
      ctx.fill();
    }
    return;
  }

  const c2dHighlightBuckets = [
    [compareIndices, colors.compare],
    [writeIndices,   colors.write],
    [readIndices,    colors.read],
    [swapIndices,    colors.swap],
  ];

  // 通常弦
  for (let i = 0; i < n; i++) {
    if (compareSet.has(i) || swapSet.has(i) || readSet.has(i) || writeSet.has(i)) continue;
    const si = sortedIdx[i];
    if (si === i) continue;
    ctx.strokeStyle = colorLUT[array[i]];
    ctx.lineWidth = n <= 64 ? 1.5 : 1;
    ctx.beginPath();
    ctx.moveTo(cx + cosLUT[i] *R, cy + sinLUT[i] *R);
    ctx.lineTo(cx + cosLUT[si]*R, cy + sinLUT[si]*R);
    ctx.stroke();
  }

  // ハイライト弦
  ctx.lineWidth = n <= 64 ? 2.5 : n <= 256 ? 2 : 1.5;
  for (const [indices, color] of c2dHighlightBuckets) {
    if (!indices || indices.length === 0) continue;
    ctx.strokeStyle = color;
    ctx.beginPath();
    for (const i of indices) {
      if (i < 0 || i >= n) continue;
      const si = sortedIdx[i];
      if (si === i) continue;
      ctx.moveTo(cx + cosLUT[i] *R, cy + sinLUT[i] *R);
      ctx.lineTo(cx + cosLUT[si]*R, cy + sinLUT[si]*R);
    }
    ctx.stroke();
  }

  // 通常ドット
  for (let i = 0; i < n; i++) {
    if (compareSet.has(i) || swapSet.has(i) || readSet.has(i) || writeSet.has(i)) continue;
    const v = array[i];
    const hue = (v / maxValue) * 360;
    ctx.fillStyle = `hsl(${hue.toFixed(1)}, 70%, 65%)`;
    ctx.beginPath();
    ctx.arc(cx + cosLUT[i]*R, cy + sinLUT[i]*R, dotR, 0, 2 * Math.PI);
    ctx.fill();
  }

  // ハイライトドット
  for (const [indices, color] of c2dHighlightBuckets) {
    if (!indices || indices.length === 0) continue;
    ctx.fillStyle = color;
    ctx.beginPath();
    for (const i of indices) {
      if (i < 0 || i >= n) continue;
      ctx.moveTo(cx + cosLUT[i]*R + dotR + 1, cy + sinLUT[i]*R);
      ctx.arc(cx + cosLUT[i]*R, cy + sinLUT[i]*R, dotR + 1, 0, 2 * Math.PI);
    }
    ctx.fill();
  }

  // 正位置ドット（緑）
  ctx.fillStyle = colors.sorted;
  ctx.beginPath();
  for (let i = 0; i < n; i++) {
    if (sortedIdx[i] !== i) continue;
    if (compareSet.has(i) || swapSet.has(i) || readSet.has(i) || writeSet.has(i)) continue;
    ctx.moveTo(cx + cosLUT[i]*R + dotR, cy + sinLUT[i]*R);
    ctx.arc(cx + cosLUT[i]*R, cy + sinLUT[i]*R, dotR, 0, 2 * Math.PI);
  }
  ctx.fill();
}

// ── クリーンアップ ────────────────────────────────────────────────────────────
function cleanup() {
  if (useWebGL && gl) {
    if (vao)     gl.deleteVertexArray(vao);
    if (vbo)     gl.deleteBuffer(vbo);
    if (program) gl.deleteProgram(program);
  }
  offscreen    = null;
  gl           = null;
  ctx          = null;
  arrays       = { main: null, buffers: new Map() };
  renderParams = null;
  isDirty      = false;
  isLoopRunning = false;
  vboData      = null;
  vboCap       = 0;
}
