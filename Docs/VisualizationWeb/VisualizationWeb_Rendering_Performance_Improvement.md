# VisualizationWeb - 描画パフォーマンス改善仕様書

## 1. 目標

| シナリオ | 現在の上限 | 目標 |
|---------|-----------|------|
| Single Sort (BarChart) | 4096要素で軽快 | **16384要素で60 FPS** |
| Comparison Mode (BarChart) | 1024 × 4ソートまで軽快 | **4096 × 4ソートで60 FPS** |

---

## 2. 現在のアーキテクチャと描画パイプライン

```
PlaybackService (C#)
  ↓ StateChanged event
ComparisonGridItem / Index.razor (C#)
  ↓ InvokeAsync(StateHasChanged)
Blazor 差分検出 & 再レンダリング
  ↓ OnAfterRenderAsync
CanvasChartRenderer.razor (C#)
  ↓ JS.InvokeVoidAsync("canvasRenderer.render", …)  ← JS Interop境界
canvasRenderer.js (JavaScript)
  ↓ Canvas 2D fillRect() × N要素
ブラウザ Canvas 合成 & 画面表示
```

Comparison Mode では上記パイプラインが **Canvasの数だけ並列に走る**（4ソート = 4パイプライン）。

---

## 3. ボトルネック分析

### 3.1 JS Interop のシリアライズコスト（最大ボトルネック）

毎フレーム、C# → JS へ以下のデータを JSON シリアライズ＆デシリアライズして転送している：

| データ | 16384要素時のサイズ(概算) |
|--------|-------------------------|
| `MainArray` (int[]) | 64 KB (16384 × 4B) |
| `CompareIndices` (int[]) | 数十バイト |
| `SwapIndices` (int[]) | 数十バイト |
| `ReadIndices` / `WriteIndices` | 数十バイト |
| `BufferArrays` (Dict → Object) | 最大64 KB × バッファ数 |

**問題点：**
- `JS.InvokeVoidAsync` は内部で `System.Text.Json` によるシリアライズを行う
- **16384 int の配列 → JSON 文字列 "[ 1, 2, 3, … ]"** に変換 → JS 側で再パース
- Comparison Mode 4 Canvas: **256 KB+/フレーム** の JSON シリアライズ・デシリアライズ
- Blazor WASM はシングルスレッドのため、シリアライズ中は UI がブロックされる

**計測ポイント：**
```
[CanvasChartRenderer] RenderCanvas start
  ├─ .ToArray() 呼び出し: HashSet<int> → int[] のコピー
  ├─ JS.InvokeVoidAsync: JSON serialize (C#) → transfer → JSON parse (JS)
  └─ 合計: 1フレームあたり 2-8ms (16384要素時)
```

### 3.2 Canvas 2D の個別 fillRect() 呼び出し

`barChartCanvasRenderer.js` の `renderInternal` は要素ごとに `fillRect()` を呼ぶ：

```javascript
for (let i = 0; i < arrayLength; i++) {
    ctx.fillStyle = color;        // 色の切り替え(状態変更)
    ctx.fillRect(x, y, w, h);    // 個別描画
}
```

- 16384要素 = **16384回の fillRect + 色切り替え**
- Canvas 2D は GPU バッチングが限定的で、`fillStyle` 変更のたびにバッチが切れる
- 同色バーをまとめて一度に描画するだけでも大幅改善可能

### 3.3 `Math.max(...array)` のスタックオーバーフローリスク

```javascript
const maxValue = Math.max(...array);
```

- スプレッド演算子は全要素を関数引数として展開する
- **16384要素で問題なし** だが、**65536+** でスタックオーバーフローの可能性
- ループで最大値を求める方法に変更すべき

### 3.4 Blazor 再レンダリングサイクルのオーバーヘッド

```
StateChanged → InvokeAsync(StateHasChanged)
  → Blazor diff → OnAfterRenderAsync → JS Interop
```

- 毎フレーム Blazor のコンポーネントツリー差分検出が走る
- `CanvasChartRenderer` 自体の DOM は `<canvas>` 1つで変化しないが、差分検出のコスト自体が発生
- Comparison Mode では 4 コンポーネント × 差分検出

### 3.5 不要な配列アロケーション

```csharp
// CanvasChartRenderer.razor - 毎フレーム呼ばれる
State.CompareIndices.ToArray(),  // HashSet → 新しい int[] を毎回生成
State.SwapIndices.ToArray(),
State.ReadIndices.ToArray(),
State.WriteIndices.ToArray(),
```

- 60 FPS × 4 Canvas = **240回/秒** の不要なアロケーション
- GC 圧力の増加

---

## 4. 改善戦略（優先度順）

### Phase 1: JS 側自律レンダリング（効果: 大 / 工数: 小）

**概要：** 配列データの転送と描画を分離し、JS 側で `requestAnimationFrame` ループを回す。

**現在：**
```
C# StateChanged (60fps) → JS.InvokeVoidAsync("render", array, ...) 毎回
```

**改善後：**
```
C# StateChanged → JS.InvokeVoidAsync("updateData", array, ...)  ← データ更新のみ
JS requestAnimationFrame loop → 前回と同じデータなら描画スキップ ← 描画は JS が自律
```

**実装：**

```javascript
// canvasRenderer.js に追加
window.canvasRenderer = {
    // ... 既存プロパティ ...
    pendingData: new Map(),    // Canvas ID → 最新データ
    rafId: null,               // requestAnimationFrame ID
    isRunning: false,

    // C# から呼ばれる: データ更新のみ（描画しない）
    updateData: function(canvasId, array, compareIndices, swapIndices,
                         readIndices, writeIndices, isSortCompleted,
                         bufferArrays, showCompletionHighlight) {
        this.pendingData.set(canvasId, {
            array, compareIndices, swapIndices, readIndices, writeIndices,
            isSortCompleted, bufferArrays, showCompletionHighlight,
            dirty: true
        });
        if (!this.isRunning) this.startLoop();
    },

    // rAF 駆動の描画ループ
    startLoop: function() {
        this.isRunning = true;
        const loop = () => {
            let anyDirty = false;
            this.pendingData.forEach((data, canvasId) => {
                if (data.dirty) {
                    this.renderInternal(canvasId, data);
                    data.dirty = false;
                    anyDirty = true;
                }
            });
            if (anyDirty || this.pendingData.size > 0) {
                this.rafId = requestAnimationFrame(loop);
            } else {
                this.isRunning = false;
            }
        };
        this.rafId = requestAnimationFrame(loop);
    },

    stopLoop: function() {
        if (this.rafId) cancelAnimationFrame(this.rafId);
        this.isRunning = false;
    }
};
```

**期待効果：**
- C# からの `JS.InvokeVoidAsync` が描画完了を待たなくなる
- ブラウザの VSync に同期した最適タイミングで描画
- データ転送と描画の非同期化

---

### Phase 2: 同色バッチ描画（効果: 大 / 工数: 小）

**概要：** 同じ色のバーをまとめて描画し、`fillStyle` の切り替え回数を最小化する。

**現在：** 要素ごとに色判定 → fillStyle 設定 → fillRect（最大 16384 回の色切り替え）

**改善後：** 先に全要素を色分類 → 色ごとにまとめて fillRect

```javascript
renderInternal: function(canvasId, params) {
    // ... 省略 ...

    // Phase 2: 同色バッチ描画
    // 各色のバーインデックスを分類
    const buckets = {
        normal: [],
        compare: [],
        swap: [],
        read: [],
        write: [],
        sorted: []
    };

    for (let i = 0; i < arrayLength; i++) {
        if (showCompletionHighlight) {
            buckets.sorted.push(i);
        } else if (swapSet.has(i)) {
            buckets.swap.push(i);
        } else if (compareSet.has(i)) {
            buckets.compare.push(i);
        } else if (writeSet.has(i)) {
            buckets.write.push(i);
        } else if (readSet.has(i)) {
            buckets.read.push(i);
        } else {
            buckets.normal.push(i);
        }
    }

    // 色ごとにまとめて描画（fillStyle 切り替えは最大6回）
    const colorMap = {
        normal: this.colors.normal,
        compare: this.colors.compare,
        swap: this.colors.swap,
        read: this.colors.read,
        write: this.colors.write,
        sorted: this.colors.sorted
    };

    for (const [bucket, indices] of Object.entries(buckets)) {
        if (indices.length === 0) continue;
        ctx.fillStyle = colorMap[bucket];
        for (const i of indices) {
            const value = array[i];
            const barHeight = (value / maxValue) * (sectionHeight - 20);
            const x = i * totalBarWidth + (gap / 2);
            const y = mainArrayY + (sectionHeight - barHeight);
            ctx.fillRect(x, y, barWidth, barHeight);
        }
    }
};
```

**期待効果：**
- `fillStyle` 切り替えが 16384回 → **最大6回** に削減
- Canvas 2D 内部の GPU バッチが効率化
- 特にハイライト要素が少ない通常描画時（99%+ が normal 色）で劇的改善

---

### Phase 3: データ転送の最適化（効果: 大 / 工数: 中）

#### 3a. SharedArrayBuffer / Typed Array による転送

**概要：** JSON シリアライズの代わりに、バイナリデータとして直接転送する。

Blazor WASM ではC#の `byte[]` を `Uint8Array` として JS に渡せる（`IJSUnmarshalledRuntime` / .NET 7+の `IJSStreamReference` を利用）。ただし .NET 10 では `[JSImport]`/`[JSExport]` を使った直接バインディングがより効率的。

```csharp
// C# 側: byte[] として配列をパック
// int[] → ArraySegment<byte> (メモリコピー1回)
var byteArray = MemoryMarshal.AsBytes(state.MainArray.AsSpan()).ToArray();
await JS.InvokeVoidAsync("canvasRenderer.updateDataBinary", _canvasId, byteArray, ...);
```

```javascript
// JS 側: Uint8Array → Int32Array にゼロコピー変換
updateDataBinary: function(canvasId, byteArray, ...) {
    const int32View = new Int32Array(byteArray.buffer,
                                     byteArray.byteOffset,
                                     byteArray.byteLength / 4);
    // int32View を直接使用（コピー不要）
    this.pendingData.set(canvasId, { array: int32View, ... });
}
```

**期待効果：**
- JSON シリアライズ/デシリアライズ完全排除
- 16384 int: JSON "65536 bytes text" → Binary **65536 bytes (raw)**
- パース時間: 数 ms → **ほぼ 0 ms**

#### 3b. 差分転送（Delta Updates）

**概要：** 毎フレーム全配列を送る代わりに、変更のあったインデックスと値のみ送る。

```csharp
// C# 側: 変更追跡
// PlaybackService.ApplyOperation で変更インデックスを記録
private List<(int index, int value)> _changedIndices = new();

// Swap 操作後
_changedIndices.Add((operation.Index1, arr[operation.Index1]));
_changedIndices.Add((operation.Index2, arr[operation.Index2]));
```

```javascript
// JS 側: 差分適用
updateDelta: function(canvasId, changes) {
    const data = this.pendingData.get(canvasId);
    for (const [index, value] of changes) {
        data.array[index] = value;
    }
    data.dirty = true;
}
```

**期待効果：**
- 1フレーム1操作時: 16384 int 転送 → **2-4 int 転送** (99.97%削減)
- OperationsPerFrame=100 でも: 16384 int → **200-400 int**

#### 3c. JS 側に配列コピーを保持

**概要：** 初回に全配列を JS へ転送し、以降は操作コマンド (swap/write) だけを送る。

```javascript
// 初回: 全配列を JS にコピー
setInitialArray: function(canvasId, array) {
    this.arrays.set(canvasId, new Int32Array(array));
}

// 以降: 操作だけ送信
applyOperations: function(canvasId, ops) {
    const arr = this.arrays.get(canvasId);
    for (const op of ops) {
        switch (op.type) {
            case 'swap':
                [arr[op.i], arr[op.j]] = [arr[op.j], arr[op.i]];
                break;
            case 'write':
                arr[op.i] = op.value;
                break;
            case 'rangeCopy':
                arr.set(op.values, op.destIndex);
                break;
        }
    }
    this.pendingData.get(canvasId).dirty = true;
}
```

**期待効果：**
- 配列全体の転送がロード時の1回のみ
- 毎フレーム転送データ量: **数十バイト** (操作コマンドのみ)
- Comparison Mode 4096 × 4: フレームあたり転送 **数百バイト** (現在 64KB × 4)

---

### Phase 4: OffscreenCanvas + Web Worker（効果: 大 / 工数: 大）

**概要：** 描画処理をメインスレッドから Web Worker に移動する。

```
メインスレッド:                    Worker スレッド:
  C# PlaybackService               OffscreenCanvas 描画
  ↓ postMessage(操作データ)   →     rAF ループ
  UIフリーズなし                    fillRect() × N
```

**制約：**
- `OffscreenCanvas` は Chrome 69+, Firefox 105+, Safari 16.4+ でサポート
- Blazor WASM から Worker への通信は `postMessage` + `Transferable` で行う
- SharedArrayBuffer を使えばゼロコピーデータ共有も可能
  - ただし `Cross-Origin-Opener-Policy: same-origin` と `Cross-Origin-Embedder-Policy: require-corp` ヘッダーが必要

**実装概要：**

```javascript
// worker.js
self.onmessage = function(e) {
    const { type, canvasId } = e.data;
    if (type === 'init') {
        const canvas = e.data.canvas; // OffscreenCanvas
        const ctx = canvas.getContext('2d', { alpha: false });
        // Worker 内で描画ループ
    }
    if (type === 'update') {
        // 配列データ更新 + dirty フラグ
    }
};

// メインスレッド
const offscreen = canvasElement.transferControlToOffscreen();
worker.postMessage({ type: 'init', canvas: offscreen }, [offscreen]);
```

**期待効果：**
- 描画がメインスレッドを完全にブロックしない
- C# の PlaybackService 処理と描画が並列実行
- Comparison Mode で特に効果的（4 Worker = 4 並列描画）

---

### Phase 5: ImageData ピクセルバッファ直接書き込み（効果: 中 / 工数: 中）<- 後回し

**概要：** `fillRect()` の代わりに `ImageData` のピクセルバッファに直接書き込む。

```javascript
renderViaImageData: function(canvasId, params) {
    const { canvas, ctx } = this.instances.get(canvasId);
    const width = canvas.width;
    const height = canvas.height;

    // ピクセルバッファを取得（または再利用）
    const imageData = ctx.createImageData(width, height);
    const pixels = imageData.data; // Uint8ClampedArray

    // 背景色で塗りつぶし
    for (let i = 0; i < pixels.length; i += 4) {
        pixels[i] = 26; pixels[i+1] = 26; pixels[i+2] = 26; pixels[i+3] = 255;
    }

    // バーを直接ピクセル書き込み
    for (let i = 0; i < arrayLength; i++) {
        const barHeight = ...;
        const x = Math.floor(i * totalBarWidth);
        const barW = Math.max(1, Math.floor(barWidth));
        const yStart = Math.floor(mainArrayY + sectionHeight - barHeight);
        const yEnd = Math.floor(mainArrayY + sectionHeight);

        const [r, g, b] = this.getColorRGB(i, ...);

        for (let py = yStart; py < yEnd; py++) {
            for (let px = x; px < x + barW && px < width; px++) {
                const offset = (py * width + px) * 4;
                pixels[offset] = r;
                pixels[offset + 1] = g;
                pixels[offset + 2] = b;
                pixels[offset + 3] = 255;
            }
        }
    }

    ctx.putImageData(imageData, 0, 0);
}
```

**期待効果：**
- Canvas API 呼び出しオーバーヘッド排除（`fillRect` × N → `putImageData` × 1）
- 16384バーの場合、多くが1-2ピクセル幅なので書き込み量は少ない
- ただし高 DPI ディスプレイではピクセル数が増えるため注意

**注意：**
- 高 DPI (2x) で 1920×1080 Canvas = 3840×2160 = 8.3M pixels × 4 = 33MB/フレーム
- `ImageData` 再利用（`createImageData` を毎フレーム呼ばない）で軽減
- 大画面・高 DPI では Phase 4 (Worker) との組み合わせが効果的

---

### Phase 6: WebGL レンダラー（効果: 最大 / 工数: 大）

**概要：** Canvas 2D を WebGL に置き換え、GPU で直接描画する。

```
C# → JS (操作データのみ) → WebGL Vertex Buffer → GPU 描画
```

**実装アプローチ：**

```javascript
// バーを四角形（2つの三角形）としてインスタンス描画
// 各バーの属性: position(x), height, color

// 頂点シェーダー
const vsSource = `
    attribute vec2 a_position;    // バーの四隅
    attribute float a_barIndex;   // バーのインデックス
    attribute float a_barHeight;  // バーの高さ（正規化）
    attribute vec3 a_color;       // バーの色

    uniform vec2 u_resolution;

    void main() {
        // バーの位置とサイズを計算
        float barWidth = 2.0 / float(u_arrayLength);
        float x = -1.0 + a_barIndex * barWidth + a_position.x * barWidth;
        float y = -1.0 + a_position.y * a_barHeight * 2.0;
        gl_Position = vec4(x, y, 0.0, 1.0);
    }
`;
```

**期待効果：**
- 16384バー → **1回の drawArraysInstanced 呼び出し** で描画
- GPU の並列処理で描画時間が **0.1ms 以下**
- Comparison Mode 4096 × 4 = 16384バー でも余裕
- 65536+ 要素にもスケール可能

**注意：**
- WebGL のセットアップコードが複雑
- WebGL コンテキスト数の制限（ブラウザごとに8-16程度）
  - Comparison Mode 9 Canvas で制限に達する可能性
  - 対策: 単一 Canvas に複数ビューポートで描画

---

## 5. C# 側の改善

### 5a. HashSet.ToArray() の排除

```csharp
// 現在: 毎フレーム新しい配列を生成
State.CompareIndices.ToArray()  // GC 圧力

// 改善: 再利用可能なバッファを使用
private int[] _compareBuffer = new int[64];

private int[] GetIndicesArray(HashSet<int> set) {
    if (set.Count > _compareBuffer.Length)
        _compareBuffer = new int[set.Count * 2];
    set.CopyTo(_compareBuffer);
    return _compareBuffer; // 注: Length ≠ Count なので Count も渡す
}
```

または Phase 3c（JS側にコピー保持）を採用すれば、ハイライトインデックスのみの転送でよい。

### 5b. ShouldRender() オーバーライド

```csharp
// CanvasChartRenderer.razor
protected override bool ShouldRender()
{
    // Canvas は JS 側で描画するため、Blazor の DOM 差分は不要
    // ただし、State の有無が変わった場合（canvas 要素の表示/非表示）のみ再レンダリング
    var shouldRender = _previousHasData != (State?.MainArray.Length > 0);
    _previousHasData = State?.MainArray.Length > 0;
    return shouldRender || !_isInitialized;
}
```

これにより **Blazor の差分検出コスト自体を排除** できる。JS 側の描画は `OnAfterRenderAsync` に依存せず、Phase 1 の自律レンダリングで行う。

### 5c. JS Interop 呼び出し頻度制御

```csharp
// 描画の間引き: 前回の JS Interop から一定時間経過していなければスキップ
private DateTime _lastJsCall = DateTime.MinValue;
private const double MIN_JS_CALL_INTERVAL_MS = 16.0; // 60 FPS 上限

private async Task RenderCanvas()
{
    var now = DateTime.UtcNow;
    if ((now - _lastJsCall).TotalMilliseconds < MIN_JS_CALL_INTERVAL_MS)
        return; // スキップ
    _lastJsCall = now;

    await JS.InvokeVoidAsync("canvasRenderer.updateData", ...);
}
```

---

## 6. 実装ロードマップ

### Phase 1 + 2: 即時効果（推定工数: 1-2日）

| 改善 | 変更ファイル | 期待効果 |
|------|-------------|---------|
| JS 自律 rAF ループ | `barChartCanvasRenderer.js`, `circularCanvasRenderer.js` | 描画タイミング最適化 |
| 同色バッチ描画 | `barChartCanvasRenderer.js` | fillStyle 切り替え 16384→6回 |
| `Math.max(...array)` 修正 | `barChartCanvasRenderer.js`, `circularCanvasRenderer.js` | スタックオーバーフロー防止 |
| `ShouldRender()` 追加 | `CanvasChartRenderer.razor`, `CircularRenderer.razor` | Blazor 差分排除 |

**期待される改善：**
- Single Sort 16384 要素: 30-40 FPS → **55-60 FPS**
- Comparison Mode 2048 × 4: カクつき → **スムーズ**

### Phase 3: データ転送最適化（推定工数: 2-3日）

| 改善 | 変更ファイル | 期待効果 |
|------|-------------|---------|
| JS 側配列保持 + 操作コマンド転送 | `barChartCanvasRenderer.js`, `CanvasChartRenderer.razor`, `PlaybackService.cs` | 転送量 99% 削減 |
| HashSet.ToArray() 排除 | `CanvasChartRenderer.razor`, `CircularRenderer.razor` | GC 圧力削減 |
| JS Interop 頻度制御 | `CanvasChartRenderer.razor` | 不要な Interop 排除 |

**期待される改善：**
- Comparison Mode 4096 × 4: **55-60 FPS**
- JS Interop あたりの転送量: 64KB → **数百バイト**

### Phase 4: Worker 並列化（推定工数: 3-5日）

| 改善 | 変更ファイル | 期待効果 |
|------|-------------|---------|
| OffscreenCanvas + Worker | 新規: `barChartRenderWorker.js`, 変更: `barChartCanvasRenderer.js` | メインスレッド解放 |
| SharedArrayBuffer | CORS ヘッダー設定、Worker コード | ゼロコピーデータ共有 |

**期待される改善：**
- Single Sort 16384 要素: 描画がメインスレッドに **影響ゼロ**
- Comparison Mode 4096 × 4: 各 Worker が独立描画

### Phase 5-6: 将来的な拡張（推定工数: 5-10日）

| 改善 | 条件 |
|------|------|
| ImageData ピクセル直書き | Phase 4 との組み合わせで効果的 |
| WebGL レンダラー | 65536+ 要素対応、最高性能が必要な場合 |

---

## 7. 各 Phase の効果予測サマリ

```
                    Single 16384       Comparison 4096×4
現在:               ~30 FPS (重い)     ~15 FPS (カクつき)
Phase 1+2:          ~55 FPS            ~35 FPS
Phase 3:            ~60 FPS            ~55 FPS
Phase 4:            60 FPS (余裕)      60 FPS (余裕)
Phase 5+6:          60 FPS (65536+可)  60 FPS (8192×4可)
```

---

## 8. 検証方法

### FPS 計測

既存のデバッグインフラを活用：

```javascript
// canvasRenderer.js 内の FPS ログ（既存）
window.debugHelper.log(`[JS Canvas] ${canvasId} JS render() FPS: ${fps.toFixed(1)}`);
```

### テストシナリオ

| # | シナリオ | 要素数 | Canvas数 | 合格基準 |
|---|---------|--------|----------|---------|
| 1 | Single BarChart | 16384 | 1 | 55+ FPS |
| 2 | Single Circular | 16384 | 1 | 55+ FPS |
| 3 | Comparison BarChart | 4096 | 4 | 50+ FPS |
| 4 | Comparison BarChart | 2048 | 9 | 45+ FPS |
| 5 | Single BarChart + Seek | 16384 | 1 | シーク応答 < 100ms |

### Chrome DevTools による計測

1. **Performance タブ**: フレーム時間、Long Task の検出
2. **Memory タブ**: GC 頻度の監視
3. **Console**: 既存の FPS ログで確認

---

## 9. リスクと制約

| リスク | 影響 | 対策 |
|--------|------|------|
| OffscreenCanvas 非対応ブラウザ | Phase 4 使用不可 | Feature detection + fallback |
| SharedArrayBuffer の CORS 要件 | ホスティング設定変更必要 | GitHub Pages は対応可能 |
| WebGL コンテキスト数制限 | Comparison 9 Canvas で問題 | 単一 Canvas 複数ビューポート |
| Blazor WASM シングルスレッド | C# 側処理がブロック | Phase 4 で描画を Worker に移動 |
| `[JSImport]`/`[JSExport]` の成熟度 | .NET 10 での安定性 | `IJSRuntime` fallback |

---

## 10. 結論

**最もコスパの高い改善は Phase 1-3**（推定工数 3-5 日）で、目標の **16384 Single Sort 60 FPS** と **4096 × 4 Comparison 55+ FPS** を達成できる見込み。

Phase 4 以降は、さらなるスケーラビリティ（65536+ 要素、8192 × 9 Comparison）が必要になった場合に検討する。

**推奨実装順序：**
1. Phase 2 (同色バッチ描画) — 既存コードの小変更で即座に効果
2. Phase 1 (JS 自律レンダリング) — Blazor-JS 間の非同期化
3. Phase 3c (JS 側配列保持) — 転送量の劇的削減
4. Phase 5b (ShouldRender) — Blazor 差分コスト排除
5. 計測してボトルネックが残る場合に Phase 4 以降を検討

---

## 11. 実装済み Phase の振り返りと残存課題

Phase 1〜6 および C# 側改善がほぼすべて実装された。以下は実装状況の要約である。

| Phase / 改善 | 状態 | 実装先 |
|---|---|---|
| Phase 1: JS 自律 rAF ループ | ✅ 実装済み | `barChartCanvasRenderer.js` `startLoop()`, `circularCanvasRenderer.js` `startLoop()` |
| Phase 2: 同色バッチ描画 | ✅ 実装済み | `barChartCanvasRenderer.js` バケット分類, `circularCanvasRenderer.js` ハイライトバケット |
| Phase 3b: 差分転送 (Delta Updates) | ✅ 実装済み | `PlaybackService.RecordDelta()`, `applyFrame()` |
| Phase 3c: JS 側配列保持 | ✅ 実装済み | `setArray()` + `arrays` Map |
| Phase 4: OffscreenCanvas + Worker | ✅ 実装済み | `barChartRenderWorker.js` (Canvas 2D), `barChartWebglWorker.js` (WebGL2) |
| Phase 6: WebGL レンダラー | ✅ 実装済み | `barChartWebglWorker.js` インスタンス描画 |
| 5a: HashSet → List 変更 | ✅ 実装済み | `VisualizationState.cs` `List<int>` |
| 5b: ShouldRender() | ✅ 実装済み | `CanvasChartRenderer.razor`, `CircularRenderer.razor` |
| Math.max スタックオーバーフロー修正 | ✅ 実装済み | ループ方式に変更済み |
| ArrayPool 再利用 | ✅ 実装済み | `PlaybackService._pooledArray` |
| SortVersion による全量/差分判定 | ✅ 実装済み | `CanvasChartRenderer.razor`, `CircularRenderer.razor` |

---

## 12. 追加改善提案（Phase 1〜6 実装後に発見）

コードベース調査により、以下の追加改善ポイントを特定した。対象はPC・タブレット・スマートフォンのブラウザからアクセスされる Blazor WASM アプリケーションである。

### 12.1 【重大】PlaybackService の SpinWait がメインスレッドをブロックする ✅ 実装済み

**問題：**

`PlaybackLoopAsync` 内の `SpinWait` は Blazor WASM のシングルスレッド環境で致命的な問題を起こす。

```csharp
// 修正前: SpinWait による CPU ビジーウェイト
var spinWait = new SpinWait();
while (sw.Elapsed.TotalMilliseconds < nextFrameTime && !cancellationToken.IsCancellationRequested)
{
    spinWait.SpinOnce(); // メインスレッドを完全ブロック ← 問題
}
```

**影響：**
- Blazor WASM はシングルスレッド。`Task.Run()` は新しいスレッドを生成しない
- `SpinWait.SpinOnce()` は CPU をビジーウェイトし、UI スレッドを完全にブロックする
- **モバイル端末では CPU 使用率が常時 100% に張り付き、バッテリー消費が激増、サーマルスロットリングを誘発**

**実装した解決策：JS `requestAnimationFrame` ドリブンループ**

`Task.Delay` は ブラウザの `setTimeout` 経由のため最小 ~16ms 程度の解像度しかなく、  
`SpeedMultiplier = 10` 時の 1.67ms 間隔を実現できず、アニメーションが規定速度で再生されないという問題があった。  
そのため `Task.Delay` ではなく、ブラウザの vsync に同期した `requestAnimationFrame` をドライバーとして採用した。

```
修正前: Task.Run (WASM = 同一スレッド)
  SpinWait(1.67ms) → 操作処理 → SpinWait → ... → Task.Yield (16ms ごと)
  問題: SpinWait がスレッドをブロック。Task.Yield 間隔に依存した不均一な再生速度。

修正後: requestAnimationFrame ドリブン
  rAF(16.67ms) → invokeMethod('OnAnimationFrame') → 操作処理 → rAF(16.67ms) → ...
  利点: vsync 同期・CPU ゼロアイドル・速度制御が正確
```

**追加ファイル: `wwwroot/js/playbackHelper.js`**

全 `PlaybackService` インスタンスを一つの rAF ループで管理する中央スケジューラー。  
Blazor WASM 専用の `dotNetRef.invokeMethod(...)` (同期呼び出し) で C# の `OnAnimationFrame()` を毎フレーム呼ぶ。

```javascript
// 単一 rAF ループで全インスタンスを処理（ComparisonMode 9 Canvas も 1 ループ）
_startLoop: function() {
    const tick = () => {
        this._instances.forEach((dotNetRef, id) => {
            const shouldContinue = dotNetRef.invokeMethod('OnAnimationFrame');
            if (!shouldContinue) toStop.push(id);
        });
        if (this._instances.size > 0) this._rafId = requestAnimationFrame(tick);
    };
    this._rafId = requestAnimationFrame(tick);
}
```

**C# 側: `[JSInvokable] bool OnAnimationFrame()`**

`SpeedMultiplier` はフレーム蓄積量で速度を表現する:

```csharp
[JSInvokable]
public bool OnAnimationFrame()
{
    // SpeedMultiplier に応じたフレーム蓄積
    // < 1.0: 複数フレームを待ってから処理（スローモーション）
    // > 1.0: 複数フレーム分の操作を一括処理（高速再生）
    _frameAccumulator += SpeedMultiplier;
    if (_frameAccumulator < 1.0) return true; // スキップ

    var framesToProcess = (int)_frameAccumulator;
    _frameAccumulator -= framesToProcess;
    if (_frameAccumulator > 3.0) _frameAccumulator = 0.0; // タブ非アクティブ後の急進防止

    // OperationsPerFrame × framesToProcess 個の操作を処理
    var effectiveOps = Math.Min(OperationsPerFrame * framesToProcess, remaining);
    // ... 操作処理 → FinalizeDeltas() → StateChanged?.Invoke() ...

    return State.CurrentOperationIndex < _operations.Count;
}
```

**SpeedMultiplier の意味論（rAF ベース）**

| SpeedMultiplier | rAF フレームあたりの処理 | 有効操作数/秒 |
|---|---|---|
| 0.1x | 10フレームに1回 (`_frameAccumulator` = 0.1/frame) | `OperationsPerFrame × 6` |
| 1x | 毎フレーム1回 | `OperationsPerFrame × 60` |
| 10x | 毎フレーム10フレーム分 | `OperationsPerFrame × 600` |
| 100x | 毎フレーム100フレーム分 | `OperationsPerFrame × 6000` |

旧設計（SpinWait）の「フレーム間隔を短縮して速度向上」から、「1フレームあたりの処理量を増やして速度向上」へ意味論が変わったが、**有効操作数/秒は同等**で視覚的な違いはない。

**期待効果：**
- SpinWait 排除により UI スレッドのブロックがゼロに
- rAF = vsync 同期のため描画タイミングが正確（Task.Delay の ~16ms 精度問題を解消）
- モバイル端末の CPU 使用率: 常時100% → **フレーム処理時のみ**
- Comparison Mode 9 Canvas でも 1 つの rAF ループで効率的に処理

**優先度：🔴 高（モバイル対応では必須）→ ✅ 実装済み**

**変更ファイル：**
- 新規: `wwwroot/js/playbackHelper.js` — rAF 中央スケジューラー
- 変更: `Services/PlaybackService.cs` — `[JSInvokable] OnAnimationFrame()` 追加、`PlaybackLoopAsync` 削除
- 変更: `Services/ComparisonModeService.cs` — `IJSRuntime` を `PlaybackService` コンストラクタへ渡す
- 変更: `wwwroot/index.html` — `playbackHelper.js` スクリプト追加

---



### 12.2 CircularRenderer に Worker/OffscreenCanvas サポートがない ✅ 実装済み

**問題：**

`CanvasChartRenderer` は `barChartCanvasRenderer.js` 経由で `barChartRenderWorker.js` / `barChartWebglWorker.js`（Worker + OffscreenCanvas）に描画を委譲している。一方、`CircularRenderer` は `circularCanvasRenderer.js` でメインスレッド上の Canvas 2D のみで描画している。

```
CanvasChartRenderer (BarChart):
  → canvasRenderer.js → Worker (renderWorker.js / webglWorker.js) ← メインスレッド解放 ✅

CircularRenderer (Circular):
  → circularCanvasRenderer.js → Canvas 2D (メインスレッド) ← ブロック ❌
```

**影響：**
- Circular モード選択時、描画処理がメインスレッドで実行され UI を阻害
- 特に Comparison Mode 4096 × 4 + Circular では顕著なカクつき
- モバイルではメインスレッドが唯一の実行コンテキストであり、より深刻

**改善案：**

`circularCanvasRenderer.js` に Worker パスを追加する。BarChart の実装パターンに合わせ、`circularRenderWorker.js` を新設する。

```javascript
// circularCanvasRenderer.js: 初期化時に Worker パスを追加
initialize: function(canvasId) {
    const canvas = document.getElementById(canvasId);
    if (!canvas) return false;

    if (typeof canvas.transferControlToOffscreen === 'function') {
        // Worker パス
        const dpr = window.devicePixelRatio || 1;
        const rect = canvas.getBoundingClientRect();
        canvas.width = rect.width * dpr;
        canvas.height = rect.height * dpr;

        const offscreen = canvas.transferControlToOffscreen();
        const workerUrl = new URL('js/circularRenderWorker.js', document.baseURI).href;
        const worker = new Worker(workerUrl);
        worker.postMessage({ type: 'init', canvas: offscreen, dpr }, [offscreen]);

        this.workers.set(canvasId, { worker, lastWidth: canvas.width, lastHeight: canvas.height });
        this.instances.set(canvasId, { canvas, ctx: null });
        // ... ResizeObserver 設定 ...
        return true;
    }

    // フォールバック: 既存の Canvas 2D パス
    // ...
}
```

**期待効果：**
- Circular モードの描画がメインスレッドから解放
- Comparison Mode + Circular でもスムーズな再生
- BarChart と同等のパフォーマンス特性を実現

**優先度：🟡 中（Circular モード使用時に影響）→ ✅ 実装済み**

---

### 12.3 毎フレームの `getBoundingClientRect()` によるレイアウトスラッシング ✅ 実装済み

**問題：**

`barChartCanvasRenderer.js` と `circularCanvasRenderer.js` の `renderInternal()` で、毎フレーム `getBoundingClientRect()` を呼んでいる。

```javascript
// canvasRenderer.js L423
renderInternal: function(canvasId, params) {
    // ...
    const rect = canvas.getBoundingClientRect(); // ← 毎フレーム呼ばれる
    const width = rect.width;
    const height = rect.height;
    // ...
}

// circularCanvasRenderer.js L251
renderInternal: function(canvasId, params) {
    // ...
    const rect = canvas.getBoundingClientRect(); // ← 同様
    // ...
}
```

**影響：**
- `getBoundingClientRect()` はブラウザにレイアウトの再計算（リフロー）を強制する
- 60 FPS × Canvas数 = 毎秒 60-540 回のレイアウト再計算
- 特に DOM が複雑な Comparison Mode でコストが増大
- モバイルでは CPU がデスクトップより非力であり影響が大きい

**改善案：**

Canvas サイズを `ResizeObserver` コールバック時にキャッシュし、`renderInternal` ではキャッシュを使う。

```javascript
// canvasRenderer.js

// キャッシュ Map を追加
cachedSizes: new Map(), // canvasId → { width, height }

// ResizeObserver コールバック内でキャッシュ更新
_ensureResizeObserver: function() {
    this.resizeObserver = new ResizeObserver(entries => {
        for (const entry of entries) {
            const canvas = entry.target;
            const canvasId = canvas.id;
            const rect = canvas.getBoundingClientRect();
            // サイズをキャッシュ
            this.cachedSizes.set(canvasId, { width: rect.width, height: rect.height });
            // ... 既存のリサイズ処理 ...
        }
    });
},

// renderInternal ではキャッシュを使用
renderInternal: function(canvasId, params) {
    const instance = this.instances.get(canvasId);
    if (!instance) return;
    const { canvas, ctx } = instance;
    if (!canvas || !ctx) return;

    // キャッシュされたサイズを使用（getBoundingClientRect 不要）
    const size = this.cachedSizes.get(canvasId);
    if (!size) return;
    const width = size.width;
    const height = size.height;
    // ...
}
```

**期待効果：**
- `getBoundingClientRect()` 呼び出し: 毎フレーム → **リサイズ時のみ**
- レイアウトスラッシング解消
- 特に Comparison Mode 9 Canvas で 1フレームあたり最大 9回のレイアウト再計算を排除

**優先度：🔴 高（全レンダリングパスに影響、修正コストも小さい）**

---

### 12.4 CircularRenderer の三角関数・HSL文字列生成が毎フレーム発生 ✅ 実装済み

**問題：**

`circularCanvasRenderer.js` の `renderInternal` では、全要素に対して毎フレーム以下の処理が走る。

```javascript
// 1. 三角関数: 要素あたり 2回の cos + 2回の sin（moveTo + lineTo）
for (const i of normalBucket) {
    const angle = i * angleStep - Math.PI / 2;
    const radius = ...;
    ctx.moveTo(centerX + Math.cos(angle) * mainMinRadius,  // cos 1回目
               centerY + Math.sin(angle) * mainMinRadius);  // sin 1回目
    ctx.lineTo(centerX + Math.cos(angle) * radius,          // cos 2回目
               centerY + Math.sin(angle) * radius);          // sin 2回目
}

// 2. HSL 文字列生成: 通常色の要素ごとに新しい文字列を生成
ctx.strokeStyle = this.valueToHSL(array[i], maxValue);
// → `hsl(${hue}, 70%, 60%)` ← テンプレートリテラルで毎回新文字列
```

**影響：**
- 16384 要素 × 4回の三角関数 = **65536 回の `Math.cos`/`Math.sin`**
- 16384 要素分の HSL 文字列生成 = **16384 回の文字列アロケーション + GC 圧力**
- ハイライトされている要素以外はほぼ全要素が normalBucket に入る

**改善案：**

#### a. 三角関数のルックアップテーブル化

```javascript
// 配列サイズが変わったときのみ LUT を再構築
_buildTrigLUT: function(arrayLength) {
    if (this._lutLength === arrayLength) return;
    this._lutLength = arrayLength;
    const angleStep = (2 * Math.PI) / arrayLength;
    this._cosLUT = new Float64Array(arrayLength);
    this._sinLUT = new Float64Array(arrayLength);
    for (let i = 0; i < arrayLength; i++) {
        const angle = i * angleStep - Math.PI / 2;
        this._cosLUT[i] = Math.cos(angle);
        this._sinLUT[i] = Math.sin(angle);
    }
},

// renderInternal 内で LUT を使用
renderInternal: function(canvasId, params) {
    // ...
    this._buildTrigLUT(arrayLength);
    // ...
    for (const i of normalBucket) {
        const radius = ...;
        const cos_i = this._cosLUT[i];
        const sin_i = this._sinLUT[i];
        ctx.moveTo(centerX + cos_i * mainMinRadius, centerY + sin_i * mainMinRadius);
        ctx.lineTo(centerX + cos_i * radius,        centerY + sin_i * radius);
    }
}
```

#### b. HSL 文字列の事前キャッシュ

```javascript
// 配列の最大値が変わったときのみカラーテーブルを再構築
_buildColorLUT: function(maxValue) {
    if (this._colorLUTMax === maxValue) return;
    this._colorLUTMax = maxValue;
    this._colorLUT = new Array(maxValue + 1);
    for (let v = 0; v <= maxValue; v++) {
        const hue = (v / maxValue) * 360;
        this._colorLUT[v] = `hsl(${hue}, 70%, 60%)`;
    }
},

// renderInternal 内
ctx.strokeStyle = this._colorLUT[array[i]]; // 文字列生成なし
```

**期待効果：**
- 三角関数: 65536 回/フレーム → **LUT 構築時の1回のみ**（以降はメモリ参照）
- HSL 文字列: 16384 アロケーション/フレーム → **0 アロケーション/フレーム**
- Circular モードのフレーム時間が特にモバイルで大幅改善

**優先度：🟡 中（Circular モード限定だが効果は大きい）**

---

### 12.5 `FinalizeDeltas()` の毎フレーム配列アロケーション <- 見送り

**問題：**

```csharp
// PlaybackService.cs
private void FinalizeDeltas()
{
    State.MainArrayDelta = _mainDelta.Count > 0 ? _mainDelta.ToArray() : [];
    //                                              ^^^^^^^^^^^^^^^^
    //                                              毎フレーム新しい int[] を生成

    if (_bufferDeltas.Count > 0)
    {
        var result = new Dictionary<int, int[]>(_bufferDeltas.Count);
        //          ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
        //          毎フレーム新しい Dictionary を生成
        foreach (var (id, list) in _bufferDeltas)
        {
            if (list.Count > 0)
                result[id] = list.ToArray(); // ← 毎フレーム新しい int[]
        }
    }
}
```

**影響：**
- 60 FPS × (1 int[] + 場合により Dictionary + バッファー数分の int[]) = 毎秒 60+ 回のアロケーション
- Comparison Mode 4 ソート: 毎秒 240+ 回のアロケーション
- WASM の GC は世代別 GC ではないため、頻繁なアロケーションが GC Pause を誘発しやすい

**なぜ素朴な再利用バッファは使えないか（検証済み）：**

`ArraySegment<int>` で再利用バッファを指すアプローチ、または共有 `Dictionary` の参照代入は
**Blazor WASM の非同期レンダリングモデルと根本的に相性が悪く、描画崩壊を起こす**。

`playbackHelper.js` の RAF ループ構造:

```javascript
const tick = () => {
    // 1. invokeMethod('OnAnimationFrame') ← C# 同期呼び出し
    //    FinalizeDeltas() → _buffer 書き込み → StateChanged → Blazor 描画キュー登録
    // 2. requestAnimationFrame(tick)      ← 次フレームを末尾で登録
};
```

RAF tick N が `requestAnimationFrame(tick)` を**末尾で**登録するため、  
Blazor の描画キューより PlaybackService の次 tick が**先に登録される**。  
結果として次の RAF では PlaybackService tick N+1 が先に実行され:

| タイミング | 状態 |
|---|---|
| RAF tick N | `_buffer` に frame N データ書き込み、Blazor 描画キュー登録 |
| RAF tick N+1（Blazor 描画より先） | `_buffer` を frame N+1 データで**上書き** |
| Blazor 描画（遅延） | `ArraySegment(_buffer, ...)` は上書き済み → frame N+1 データを二重適用 |

`State.BufferArrayDeltas = _reuseDict`（参照代入）でも同様で、  
`_reuseDict.Clear()` が描画前に実行されて空のデータが送信される。

**正しい修正のために必要な条件：**

`FinalizeDeltas()` 単体の最適化では根本解決できない。
JS 側でデータを保持し差分コマンドのみ転送する **Phase 3c** アーキテクチャへの移行が必要。

```javascript
// Phase 3c: JS 側に配列を保持 → 操作コマンドのみ送信
applyFrame: function(canvasId, mainDelta, ...) {
    // mainDelta はフレームごとの独立した配列として受け取る
    // JS 側の arrays.main に差分適用
}
```

Phase 3c が実装されれば `FinalizeDeltas()` は「変更インデックスと値のペア」のみを構築すればよく、  
JS 側が自律的に配列を管理するため C# 側の配列アロケーションは大幅に削減できる。

**代替案（よりシンプル）：** `_mainDelta` の `List<int>` をそのまま `CollectionsMarshal.AsSpan()` で JS に渡す方法を検討する（.NET 10 の `IJSRuntime` が `Span` / `Memory` を受け付けるか確認が必要）。

**現状：**
- 元の `ToArray()` / `new Dictionary()` を維持（毎フレーム不変スナップショットを生成するため安全）
- 再利用バッファ方式は検証の結果 **採用不可** と判断

**優先度：🟢 低〜中（Phase 3c と合わせて対応。単体では安全に実装できない）**

---


### 12.6 デッドコード: `barChartCanvasRenderer.js` のウィンドウリサイズリスナー ✅ 実装済み

**問題：**

```javascript
// canvasRenderer.js L662-667
window.addEventListener('resize', () => {
    if (window.canvasRenderer.canvas) {  // ← .canvas プロパティは存在しない
        window.canvasRenderer.resize();
    }
});
```

`window.canvasRenderer.canvas` は現在のコードに存在しないプロパティであり、`ResizeObserver` が正しくリサイズ処理を行っている。このリスナーは常に no-op だが、`resize` イベントは頻繁に発火するため、不要なオーバーヘッドとなる。

**改善案：**

削除する。

```javascript
// 削除: window.addEventListener('resize', ...) ブロック全体
// ResizeObserver が全 Canvas のリサイズを自動処理済み
```

**優先度：🟢 低（動作に影響しないが、コードの清潔さ向上）**

---

### 12.7 CSS `contain` プロパティによるブラウザ合成最適化 <- 見送り

**問題：**

Canvas コンテナに CSS `contain` プロパティが設定されていない。ブラウザはコンテナ内外のレイアウト依存関係を毎フレーム計算する必要がある。

```css
/* 現在: app.css */
.bar-chart-container {
    width: 100%;
    height: 100%;
    cursor: pointer;
    /* contain プロパティなし */
}

.comparison-grid-item {
    display: flex;
    flex-direction: column;
    /* contain プロパティなし */
}
```

**影響：**

CSS `contain` が最適化するのは「メインスレッドのレイアウト・ペイント」だが、Phase 4 (OffscreenCanvas + WebGL Worker) が実装済みの現在は、Canvas への描画はすでにメインスレッド外で行われている。`fillRect()` / `drawArraysInstanced()` はブラウザの CSS レイアウトエンジンを一切触らないため、`contain` で守るべきメインスレッドのペイントコストはほぼない。

残存する影響（限定的）：
- Blazor `StateHasChanged` による統計 DOM 更新のリフローが `.comparison-grid-item` をまたいで伝播する
- `.comparison-grid-item.completed` の `box-shadow` 変化時にレイヤー再計算が発生する

**改善案：**

効果が見込める `.comparison-grid-item` への `contain: layout paint` のみ適用する。

```css
.comparison-grid-item {
    /* 統計 DOM 更新のリフローをアイテム内に隔離する */
    /* （overflow: hidden が既に存在するため paint の追加効果は小さいが無害） */
    contain: layout paint;
}
```

⚠️ **`contain: strict` は `.bar-chart-container` / `.circular-chart-container` に適用しない**

`contain: strict` は `contain: size layout paint style` の短縮形であり、`contain: size` は「この要素のサイズが子要素に依存しない」ことをブラウザに宣言する。`width: 100%; height: 100%` で親 flex に従うコンテナに指定すると、子 canvas が親サイズを参照できなくなるリスクがある。

```css
/* ❌ 適用しない */
.bar-chart-container {
    contain: strict; /* contain: size が width/height: 100% の動作と競合する可能性 */
}
```

⚠️ **`will-change: contents` は使用しない**

`contents` は CSS `will-change` の有効な値として仕様で定義されているが、主要ブラウザでの動作が不安定・未サポートな実装が多い。WebGL Worker 使用時は Canvas がすでに独立したコンポジットレイヤーになるため不要。独立レイヤーを明示したい場合は `will-change: transform` が確実だが、GPU メモリ消費が増えるためソート可視化では費用対効果が低い。

**期待効果：**
- `.comparison-grid-item` 内の統計 DOM 更新時のリフロー範囲を隣のグリッドアイテムに波及させない
- Worker 実装済みのため Canvas 描画への直接効果はない
- Comparison Mode 9 Canvas 時に微小な効果（+0〜1 FPS 相当）

**優先度：🟢 低（Worker 実装済みのため Canvas 描画への効果はなし。統計 DOM リフロー隔離のみ）**

---

### 12.8 モバイル端末向け DPR キャッピング

**問題：**

現在、Canvas の物理ピクセルサイズは `window.devicePixelRatio` をそのまま使用している。

```javascript
// canvasRenderer.js L51
const dpr = window.devicePixelRatio || 1;
canvas.width = rect.width * dpr;
canvas.height = rect.height * dpr;
```

**影響：**
- 最新の iPhone (DPR 3.0): 390×844 CSS px → **1170×2532 物理 px** = 2,962,440 ピクセル
- DPR 2.0 の場合: 780×1688 = 1,316,640 ピクセル（DPR 3.0 の **44%**）
- Canvas 2D の `fillRect` / `stroke` は物理ピクセル数に比例してコストが増加
- WebGL でもフラグメントシェーダの実行回数が物理ピクセル数に比例
- バーチャートのバーが 1-2 CSS px 幅の場合、DPR 3.0 でも視覚的差異はほぼなし

**改善案：**

```javascript
// DPR を最大 2.0 に制限する（ソート可視化では十分な品質）
_getEffectiveDPR: function() {
    const dpr = window.devicePixelRatio || 1;
    return Math.min(dpr, 2.0); // 3x デバイスでも 2x に制限
},

initialize: function(canvasId, useWebGL = true) {
    const canvas = document.getElementById(canvasId);
    if (!canvas) return false;

    const dpr = this._getEffectiveDPR();
    // ...
}
```

**期待効果：**
- DPR 3.0 端末でのピクセル処理量: **56% 削減** (9x → 4x)
- GPU メモリ使用量の削減
- 視覚的品質への影響は軽微（バーチャートは低解像度で十分）

**適用判断基準：**
- DPR 2.0 以下: そのまま使用
- DPR 2.5 以上: 2.0 にキャッピング
- ユーザー設定で切り替え可能にしてもよい

**優先度：🟡 中（モバイル特化の最適化。デスクトップには影響なし）**

---

### 12.9 ComparisonGridItem の不要な Blazor 再レンダリング伝播　✅ 実装済み

**問題：**

```razor
<!-- ComparisonGridItem.razor -->
<div class="comparison-grid-item ...">
    <div class="comparison-header">...</div>
    <div class="comparison-visualization">
        <CanvasChartRenderer State="@Instance.State" ... />
    </div>
    <ComparisonStatsSummary State="@Instance.State" />  <!-- ← 毎フレーム再レンダリング -->
</div>
```

```csharp
// ComparisonGridItem.razor
private void OnPlaybackStateChanged()
{
    InvokeAsync(StateHasChanged);  // ← コンポーネント全体を再レンダリング
}
```

`CanvasChartRenderer.ShouldRender()` は `false` を返して DOM 差分を回避するが、親の `ComparisonGridItem` および `ComparisonStatsSummary` は毎フレーム Blazor の差分検出が走る。`ComparisonStatsSummary` は統計値（CompareCount, SwapCount, Progress%）を表示しており、値が変わるたびに DOM 更新が必要だが、DOM diff 自体のコストが無視できない。

**影響：**
- Comparison Mode 4 ソート × 60 FPS = 毎秒 240 回の `ComparisonGridItem` + `ComparisonStatsSummary` の差分検出
- 統計パネルの DOM ノード数 × 差分検出コスト

**改善案：**

```csharp
// ComparisonStatsSummary.razor に ShouldRender を追加
@code {
    [Parameter, EditorRequired]
    public VisualizationState State { get; set; } = null!;

    private ulong _lastCompareCount;
    private ulong _lastSwapCount;
    private int _lastOperationIndex;

    protected override bool ShouldRender()
    {
        // 統計値が実際に変化したときのみ再レンダリング
        var changed = _lastCompareCount != State.CompareCount
                   || _lastSwapCount != State.SwapCount
                   || _lastOperationIndex != State.CurrentOperationIndex;

        _lastCompareCount = State.CompareCount;
        _lastSwapCount = State.SwapCount;
        _lastOperationIndex = State.CurrentOperationIndex;

        return changed;
    }
}
```

**期待効果：**
- 統計値が変わらないフレーム（SpeedMultiplier が低い場合）での Blazor 差分検出を排除
- Comparison Mode での Blazor 側 CPU 負荷低減

**優先度：🟢 低（Blazor の差分検出は軽量だが、Comparison Mode 9 Canvas 時に効果的）**

---

## 13. 追加改善の実装ロードマップ

### 即時対応（推定工数: 0.5〜1日）

| # | 改善 | 変更ファイル | 優先度 |
|---|------|-------------|--------|
| 12.1 ✅ | SpinWait 排除 | `PlaybackService.cs` | 🔴 高 |
| 12.3 ✅ | getBoundingClientRect キャッシュ | `barChartCanvasRenderer.js`, `circularCanvasRenderer.js` | 🔴 高 |
| 12.6 ✅ | デッドコード削除 | `barChartCanvasRenderer.js` | 🟢 低 |
| 12.7 | CSS contain 追加 | `app.css` | 🟢 低 |

### 短期対応（推定工数: 1〜2日）

| # | 改善 | 変更ファイル | 優先度 |
|---|------|-------------|--------|
| 12.4 ✅ | Circular 三角関数 LUT + HSL キャッシュ | `circularCanvasRenderer.js` | 🟡 中 |
| 12.5 見送り | FinalizeDeltas バッファ再利用 | `PlaybackService.cs`, `VisualizationState.cs` | 🟢 低〜中 |
| 12.8 | DPR キャッピング | `barChartCanvasRenderer.js`, `circularCanvasRenderer.js`, Worker 各 js | 🟡 中 |
| 12.9　✅ | ComparisonStatsSummary ShouldRender | `ComparisonStatsSummary.razor` | 🟢 低 |

### 中期対応（推定工数: 3〜5日）

| # | 改善 | 変更ファイル | 優先度 |
|---|------|-------------|--------|
| 12.2 ✅ | CircularRenderer Worker 対応 | 新規: `circularRenderWorker.js`, `circularWebglWorker.js`, 変更: `circularCanvasRenderer.js`, `CircularRenderer.razor` | 🟡 中 |

---

## 14. 追加改善の効果予測サマリ

```
                          PC (16384 Single)   Mobile (4096 Single)   Comparison 4096×4
現在 (Phase 1-6 実装後):  60 FPS              45-55 FPS              50-55 FPS
12.1 SpinWait 排除:       60 FPS              50-58 FPS (+5-3)       55-58 FPS (+5-3)
12.3 gBCR キャッシュ:     60 FPS (+0)         55-60 FPS (+5-2)       58-60 FPS (+3-2)
12.7 CSS contain:         60 FPS (+0)         60 FPS (+0)            60 FPS (+0)         ← Worker実装済みのためCanvas描画への効果なし
12.8 DPR キャップ:        影響なし            58-60 FPS (+2-0)       58-60 FPS (+0)
12.4 Circular LUT:        60 FPS (Circular)   55-60 FPS (Circular)   改善あり
全適用:                   60 FPS              58-60 FPS              58-60 FPS
```

**特にモバイル端末（タブレット・スマートフォン）での改善効果が大きい。**
- SpinWait 排除によるバッテリー消費削減は FPS 数値に現れないが UX に直結
- DPR キャッピングはスマートフォン（DPR 3.0）で最大の効果
- CSS contain は Phase 4 Worker 実装済みの現在は Canvas 描画に効果なし（統計 DOM リフロー隔離のみ）
