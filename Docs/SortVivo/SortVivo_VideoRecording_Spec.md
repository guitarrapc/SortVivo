# 動画録画（Video Recording）仕様書

## 📋 概要

**目的:** ソートアルゴリズムの可視化エリアを動画として録画し、WebM ファイルとしてダウンロードできるようにする。ソートの過程を共有・保存する用途。

**設計方針:**
- ブラウザネイティブ API のみ使用（外部ライブラリなし）
- `MediaRecorder` API + `CanvasCaptureMediaStream` でエンコード
- オフスクリーン canvas に DOM 内の全 canvas を合成して録画
- 録画開始/停止のトグル UI

---

## 🎬 録画の基本仕様

### エンコード

| 項目 | 仕様 |
|------|------|
| API | `MediaRecorder` + `HTMLCanvasElement.captureStream()` |
| 出力形式 | WebM（`.webm`） |
| コーデック優先順位 | VP9 → VP8 → デフォルト |
| ビットレート | 解像度に応じた動的計算（後述） |
| フレームレート | 30 fps |
| データ収集間隔 | 100 ms（`recorder.start(100)`） |
| 解像度 | ユーザー選択（1080p / 720p / 480p） |

### 解像度選択

プリセットは「長辺のターゲットピクセル数」として動作する。表示領域のアスペクト比に関わらず一貫した品質を保証し、ダウンスケールは行わない。

| プリセット | 長辺ターゲット | ビットレート基準 |
|-----------|-------------|---------------|
| 1080p | 1920 px | ~20 Mbps |
| 720p（デフォルト） | 1280 px | ~9 Mbps |
| 480p | 854 px | ~4 Mbps |

**スケーリング方式:**

```
longSideTargets = { 1080: 1920, 720: 1280, 480: 854 }
targetLongSide = longSideTargets[preset]
longerDisplaySide = max(displayWidth, displayHeight)
scale = max(targetLongSide / longerDisplaySide, 1.0)   ← 常に ≥ 1（ダウンスケール禁止）
videoWidth = round(displayWidth × scale / 2) × 2       ← 偶数保証
videoHeight = round(displayHeight × scale / 2) × 2
bitrate = 20_000_000 × (videoWidth × videoHeight) / (1920 × 1080)
```

**設計意図:**

- 表示領域がターゲットより大きい場合（例: 1422×1683 で 1080p 選択）→ `scale = 1.14` でわずかにアップスケール、ダウンスケールしない
- 表示領域がターゲットより小さい場合（例: 800×600 で 1080p 選択）→ `scale = 2.4` で高品質アップスケール
- 長辺基準なのでポートレート/ランドスケープどちらでも一貫した品質
- ビットレートは出力ピクセル数に比例して動的調整（1080p 基準 20 Mbps）
- ソート canvas は `imageSmoothingEnabled = false`（最近傍補間）で描画し、離散要素（バー、ドット等）をシャープに維持
- 設定値は `localStorage` に永続化（`sortvis.recordingResolution`）

### キャプチャ対象

キャプチャ範囲は `.visualization-content`（ソートカード群のみ）。コントロールバーやシークバーは含まない。

| 要素 | セレクター | 描画方法 |
|------|-----------|--------|
| ソートカード背景 | `.sort-card` | 背景色 `#1a1a1a`、ボーダー `#333`（完了時 `#10B981`） |
| カードヘッダー背景 | `.sort-card__header` | 背景色 `#252525` |
| ソート canvas | `canvas`（エリア内全て） | `drawImage()` で相対位置に描画 |
| アルゴリズム名 | `.sort-card__algorithm-name` / `.sort-card__algo-select` | `fillText()` bold 14px |
| 計算量バッジ | `.complexity-badge` | `fillText()` 11px `#c8aa6e` |
| 統計サマリー背景 | `.sort-stats-summary` | 背景色 `#242a27` |
| 統計カード | `.stat-mini` | 背景 + ボーダー描画 |
| 統計値 | `.stat-mini .value` | `fillText()` bold 13px monospace |
| 統計ラベル | `.stat-mini .label` | `fillText()` 10px uppercase `#9ca3af` |

**除外対象:**

| 要素 | 理由 |
|------|------|
| `.play-control-bar` | コントロールバーは操作 UI のため録画不要 |
| `.seek-stats-row` | シークバーは操作 UI のため録画不要 |

### キャプチャ方式

1. オフスクリーン `<canvas>` を生成（`.visualization-content` と同サイズ）
2. `setInterval(1000 / fps)` で定期的にフレームをキャプチャ
3. 各フレームの描画順序:
   1. 背景を `#0a0a1a` で塗りつぶし
   2. 各 `.sort-card` のカード背景・ボーダー・ヘッダー背景・統計サマリー背景を描画
   3. エリア内の全 `<canvas>` 要素を相対位置で `drawImage()`
   4. アルゴリズム名を `fillText()` で描画（N=1 は `span`、N>1 は `select` から取得）
   5. 計算量バッジを描画
   6. 各 `.stat-mini` の背景・ボーダー・値・ラベルを描画
4. オフスクリーン canvas の `captureStream()` を `MediaRecorder` に渡してエンコード

---

## 🎮 UI 仕様

### 録画ボタン

`PlayControlBar` コンポーネントに配置。サウンドボタンの右隣。

| 状態 | アイコン | CSS クラス | 色 |
|------|---------|-----------|-----|
| 待機 | ⏺（丸） | `.btn-record` | 赤系（`rgba(239, 68, 68, 0.15)`） |
| 録画中 | ■（四角） | `.btn-record-active` | 赤系 + パルスアニメーション |
| 無効 | ⏺（丸）グレーアウト | `.btn-record:disabled` | `opacity: 0.35` |

### パルスアニメーション（録画中）

```css
@keyframes record-pulse {
    0%, 100% { opacity: 1; }
    50% { opacity: 0.6; }
}
```

周期: 1.2 秒

### ボタンの有効/無効条件

| 条件 | `RecordDisabled` |
|------|-----------------|
| インスタンス 0 個 | `true`（録画不可） |
| インスタンス 1 個以上 | `false`（録画可能） |

### ツールチップ

| 状態 | ツールチップ | en | ja |
|------|------------|-----|-----|
| 待機 | `playback.record` | "Record" | "録画" |
| 録画中 | `playback.recordStop` | "Stop Recording" | "録画停止" |

### 解像度セレクター

録画ボタンの右隣に配置。`<select>` ドロップダウン。

| 項目 | 仕様 |
|------|------|
| CSS クラス | `.record-resolution-select` |
| 選択肢 | `1080p` / `720p` / `480p` |
| デフォルト | `720p` |
| 録画中 | 無効化（変更不可） |
| インスタンス 0 個 | 無効化 |
| スタイル | 録画ボタンに合わせた赤系テーマ |
| 永続化 | `localStorage`（`sortvis.recordingResolution`） |

---

## 📁 ファイル名規則

ダウンロードされる WebM ファイルの命名:

```
sortvivo-{アルゴリズム名}.webm
```

| ケース | ファイル名例 |
|--------|------------|
| 単一アルゴリズム | `sortvivo-Quicksort.webm` |
| 複数アルゴリズム比較 | `sortvivo-Quicksort-MergeSort-HeapSort.webm` |
| アルゴリズム未選択時 | `sortvivo-{SelectedAlgorithm}.webm` |

---

## 🔄 状態遷移

```
[待機] --録画ボタン押下--> [録画中]
[録画中] --録画ボタン押下--> [停止 → WebM ダウンロード → 待機]
[録画中] --Clear All 押下--> [停止 → WebM ダウンロード → 待機]
```

### 録画の自動停止

| トリガー | 動作 |
|---------|------|
| 録画ボタン再押下 | 正規停止 → ダウンロード |
| Clear All | 強制停止 → ダウンロード（ファイル名: `sortvivo-recording`） |

---

## 🏗️ アーキテクチャ

### ファイル構成

| ファイル | 役割 |
|---------|------|
| `wwwroot/js/videoRecorder.js` | JS 録画エンジン（`MediaRecorder` ラッパー） |
| `Components/PlayControlBar.razor` | 録画ボタン UI |
| `Components/PlayControlBar.razor.css` | 録画ボタンスタイル |
| `Pages/Index.razor` | 録画状態管理・JS Interop 呼び出し |
| `wwwroot/locales/en.json` | 英語ローカライズ |
| `wwwroot/locales/ja.json` | 日本語ローカライズ |

### JS Interop

| C# → JS 呼び出し | 引数 | 戻り値 |
|------------------|------|--------|
| `videoRecorder.startRecording` | `selector: string`, `fps: number`, `targetHeight: number` | `bool`（成功/失敗） |
| `videoRecorder.stopRecording` | `filename: string` | `void` |

### PlayControlBar パラメータ

| パラメータ | 型 | 用途 |
|-----------|-----|------|
| `IsRecording` | `bool` | 録画中かどうか |
| `RecordDisabled` | `bool` | ボタン無効化 |
| `OnToggleRecord` | `EventCallback` | 録画トグルイベント |
| `RecordingResolution` | `int` | 選択中の解像度（1080 / 720 / 480） |
| `RecordingResolutionChanged` | `EventCallback<int>` | 解像度変更イベント |

### 録画開始の前提条件

- `.visualization-content` 内に `<canvas>` 要素が 1 つ以上存在すること
- `<canvas>` が存在しない場合、`startRecording` は `false` を返し録画は開始されない

---

## ⚠️ 制限事項・既知の制約

| 項目 | 詳細 |
|------|------|
| ブラウザ互換性 | `MediaRecorder` + `captureStream()` をサポートするブラウザが必要（Chrome, Firefox, Edge） |
| Safari | `MediaRecorder` の WebM サポートが限定的（Safari 14.1+ で部分対応） |
| DOM 要素の描画 | HTML/CSS の完全な描画は行わない（canvas + テキストのみ合成） |
| Picture モード | 画像ソート時、canvas が tainted の場合 `drawImage` がサイレントに失敗する可能性あり |
| リサイズ | 録画中のウィンドウリサイズには追従しない（開始時のサイズで固定） |
| 音声 | 音声は録画に含まれない（映像のみ） |

---

## 🌐 ローカライズキー

```json
{
  "playback": {
    "record": "Record / 録画",
    "recording": "Recording... / 録画中...",
    "recordStop": "Stop Recording / 録画停止",
    "resolution": "Resolution / 解像度"
  }
}
```
