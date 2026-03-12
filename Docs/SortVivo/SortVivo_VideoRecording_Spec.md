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
| ビットレート | 8 Mbps（`videoBitsPerSecond: 8_000_000`） |
| フレームレート | 30 fps |
| データ収集間隔 | 100 ms（`recorder.start(100)`） |
| 解像度 | キャプチャ対象要素の表示サイズ（`getBoundingClientRect()`） |

### キャプチャ対象

| 要素 | セレクター | 描画方法 |
|------|-----------|---------|
| 可視化エリア全体 | `#visualization-area` | 背景色 `#0a0a1a` で塗りつぶし |
| コントロールバー背景 | `.play-control-bar` | 背景色 `#111827` で塗りつぶし |
| ソート canvas | `canvas`（エリア内全て） | `drawImage()` で相対位置に描画 |
| アルゴリズム名ラベル | `.sort-card__algo-name`, `.sort-card__header-title` | `fillText()` で描画（bold 14px） |
| 統計値オーバーレイ | `.sort-card__stats-value` | `fillText()` で描画（12px monospace） |

### キャプチャ方式

1. オフスクリーン `<canvas>` を生成（対象要素と同サイズ）
2. `setInterval(1000 / fps)` で定期的にフレームをキャプチャ
3. 各フレームで:
   - 背景を `#0a0a1a` で塗りつぶし
   - コントロールバー背景を描画
   - エリア内の全 `<canvas>` 要素を相対位置で `drawImage()`
   - アルゴリズム名ラベルを `fillText()` で描画
   - 統計値を `fillText()` で描画
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
| `videoRecorder.startRecording` | `selector: string`, `fps: number` | `bool`（成功/失敗） |
| `videoRecorder.stopRecording` | `filename: string` | `void` |

### PlayControlBar パラメータ

| パラメータ | 型 | 用途 |
|-----------|-----|------|
| `IsRecording` | `bool` | 録画中かどうか |
| `RecordDisabled` | `bool` | ボタン無効化 |
| `OnToggleRecord` | `EventCallback` | 録画トグルイベント |

### 録画開始の前提条件

- `#visualization-area` 内に `<canvas>` 要素が 1 つ以上存在すること
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
    "recordStop": "Stop Recording / 録画停止"
  }
}
```
