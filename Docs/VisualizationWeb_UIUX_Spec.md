# VisualizationWeb UI/UX 仕様書

## 1. 設計思想

### 1.1 テーマ

- 直感的に操作できる
- ソートの状況や統計情報が一目で分かる
- 普段は不要だが時に設定したい操作は設定画面で設定できる
- デバイスごとに適切なレイアウトがある
- チュートリアルのUI/UXも統一感を持たせる

### 1.2 核心コンセプト: 「見る → 操作する → 設定する」3層モデル

ユーザーの操作頻度に応じて情報を3層に分類し、アクセスしやすさに差をつける。

| Layer | 名称 | 概要 | アクセス方法 |
|-------|------|------|-------------|
| Layer 1 | Always Visible | 常に画面に見えている要素 | 常時表示 |
| Layer 2 | Quick Access | ワンタップで開く操作パネル | サイドバー / オーバーレイ / ボトムシート |
| Layer 3 | Settings | 初回設定したらしばらく変えない設定 | Settings モーダル |

### 1.3 核心コンセプト: 「Sort Card」統一パターン

**モード切替なし — Sort Card の枚数 N がすべてを決定する**

「単一モード」と「比較モード」は別モードではなく、表示中の Sort Card 枚数 N の違いに過ぎない。
Comparison Mode トグルは廃止。N=1 が「単一表示」、N>1 が「比較表示」として暗黙的に決まる。

現在の `ComparisonGridItem`（ヘッダー + 可視化 + Stats Summary）のパターンを全 N の基本単位とする。

```
┌─ Sort Card ──────────────────────────┐
│ [Algorithm Name]  [O(n log n)]  [✖]  │  ← カードヘッダー
├──────────────────────────────────────┤
│                                      │
│      Visualization (Bar/Circle/...)  │  ← 可視化
│                                      │
├──────────────────────────────────────┤
│ ⏱ 1.23ms  Cmp: 141K  Swp: 6.8K  45%│  ← Stats Summary
└──────────────────────────────────────┘
```

**N=1（単一表示）と N>1（比較表示）の挙動の違い:**

| 機能 | N=1 | N>1 |
|------|-----|-----|
| シークバー | 表示 | 非表示（各アルゴリズムの操作数が異なるため） |
| サウンド | 有効 | 無効 |
| Sort Card の ✖ ボタン | 非表示（削除不可） | 表示 |
| ステータステキスト | 非表示 | "X/N completed" 表示 |

**Quick Access パネルのボタン:**

| ボタン | 動作 | 常時表示 |
|--------|------|---------|
| ➕ Add & Generate | 選択 Algo を Upsert（なければ追加 N→N+1、あれば配列再生成のみ N 不変）。全カード新配列で再実行 | ✅（N が上限かつ選択 Algo 未登録の場合は disabled） |

**メリット:**
- Sort Card N 枚がグリッドで表示（N=1 はフルサイズ）
- 統計表示のUI/UXが完全に同一（コンポーネント共有）
- トグルによるモード切替という概念が不要になり、操作フローがシンプルになる
- サイドバーから Statistics セクションを完全に削除（Layer 2 がスリムになる）

---

## 2. Layer 分類

### 2.1 Layer 1: Always Visible（常時表示）

| 要素 | 説明 |
|------|------|
| ヘッダー | アプリ名、現在のアルゴリズム名、Tutorial / Settings ボタン |
| 再生コントロール | ▶/⏸, ⏹, 🔊/🔇 の3つのアイコンボタン（可視化エリアの上） |
| Sort Card | 可視化 + Stats Summary |
| シークバー | N=1 時のみ、Sort Card の下に表示 |
| ステータス | N>1 時の完了数表示（再生コントロール横） |

### 2.2 Layer 2: Quick Access（操作パネル）

| 要素 | 説明 |
|------|------|
| Algorithm 選択 | ドロップダウン（カテゴリグループ対応） |
| Array Size | ドロップダウン（推奨サイズ表示） |
| Array Pattern | ドロップダウン（カテゴリグループ対応） |
| Visualization Mode | ドロップダウン（BarChart / Circular / Spiral / DotPlot / Picture系） |
| Operations Per Frame | スライダー + 値表示 |
| Speed Multiplier | スライダー + 値表示 + 実効FPS |
| Add & Generate | 選択 Algo の Upsert + 全カード新配列再実行（§7.3 参照） |
| Complexity | 選択中アルゴリズムの計算量表示（読み取り専用） |

### 2.3 Layer 3: Settings（設定モーダル）

| 要素 | 説明 | デフォルト |
|------|------|-----------|
| Renderer | Canvas 2D / WebGL 切替 | Canvas 2D |
| Auto Reset on Complete | ソート完了時に自動リセット | OFF |
| Instant Mode | アニメーション無しで即座に結果表示 | OFF |
| Sound Volume | 音量スライダー (0%〜100%) | 70% |
| Auto Switch to Recommended Size | アルゴリズム変更時に推奨サイズへ自動変更 | OFF |
| Category Filter | アルゴリズム一覧のカテゴリフィルタ | All |
| Debug Log | コンソールにデバッグログを出力 | OFF |

---

## 3. 再生コントロール

### 3.1 配置

可視化エリア（Sort Card）の **上** に配置する。画面下部ではなく、可視化に近い位置に置くことで直感的な操作を実現する。

### 3.2 ボタン構成

テキストなし、アイコンのみの大きめボタンを3つ横に並べる。

```
PC / タブレット:
     ┌──────┐    ┌──────┐    ┌──────┐
     │  ▶   │    │  ⏹   │    │  🔊  │
     └──────┘    └──────┘    └──────┘
      48×48        48×48       48×48

スマホ:
        ┌────┐    ┌────┐    ┌────┐
        │ ▶  │    │ ⏹  │    │ 🔊 │
        └────┘    └────┘    └────┘
         44×44     44×44      44×44
```

### 3.3 ボタン状態

| ボタン | 状態 | アイコン | 背景色 | tooltip |
|--------|------|---------|--------|---------|
| Play/Pause | 停止/一時停止中 | `▶` | `#3B82F6`（青） | "Play" |
| Play/Pause | 再生中 | `⏸` | `var(--color-compare)` = `#A855F7`（紫） | "Pause" |
| Stop | 常時 | `⏹` | `#3A3A3A`（暗灰） | "Stop" |
| Sound | ON | `🔊` | `#3A3A3A`（暗灰） | "Sound ON" |
| Sound | OFF | `🔇` | `#555`（灰）+ `opacity: 0.6` | "Sound OFF" |
| Sound | N>1 時無効 | `🔇` | disabled（`opacity: 0.35`, `cursor: not-allowed`） | "Sound not available in Comparison Mode" |

### 3.4 補助操作

- **可視化エリアクリック**: 再生/一時停止トグル（引き続き維持）
- **N>1 時**: 再生コントロール横に `"2/4 completed"` ステータスを表示

---

## 4. Sort Card

### 4.1 構造

```
┌─ SortCard ───────────────────────────────────────┐
│ CardHeader                                        │
│   [Algorithm Name ▾]  [Complexity Badge]  [✖ 削除]  │
├──────────────────────────────────────────────────┤
│ Visualization                                     │
│   BarChartRenderer / CircularRenderer / ...       │
│   (クリックで再生/一時停止)                        │
├──────────────────────────────────────────────────┤
│ SortStatsSummary                                  │
│   ⏱ time  Cmp: N  Swp: N  Progress: N%           │
│   (タップで展開 → 詳細統計表示)                    │
└──────────────────────────────────────────────────┘
```

### 4.2 カードヘッダー

- **アルゴリズム名（クリックで入れ替え）**: 左揃え、太字。末尾に `▾` を付与し、セレクタブルなことを示す。
  - `cursor: pointer`、hover 時に薄いハイライト
  - クリックでインラインドロップダウンを展開（Quick Access と同じアルゴリズムリスト）
  - 選択→ **同じ配列を使いそのカードだけアルゴリズムを差し替え**（N 変わらず、他カードに影響なし）
  - Generate & Sort との差別: **配列を再生成しない**
- **Complexity Badge**: `O(n log n)` のようなバッジ（モノスペース、小さめ）
- **✖ 削除ボタン**: N>1 の時のみ表示。N=1 時は非表示（最後の1枚は削除不可）

**アルゴリズム入れ替え インタラクションフロー:**

```
[QuickSort ▾] をクリック
  ↓
インラインドロップダウン展開（現在の選択項目にチェックマーク）
  ↓
[MergeSort] を選択
  ↓
カードのアルゴリズムが QuickSort → MergeSort に差し替わる
同じ共有配列で MergeSort を実行（統計リセット）
他カードは影響を受けない
```

### 4.3 Stats Summary

既存の `ComparisonStatsSummary` を `SortStatsSummary` にリネームし、単一/比較で共有する。

**表示内容（カードサイズに応じて切替）:**

| カードサイズ | 表示項目 |
|-------------|---------|
| フルサイズ（単一モード PC） | ⏱ 実行時間, Comparisons, Swaps, Reads, Writes, Progress % |
| 中サイズ（比較2〜3枚 PC） | ⏱ 実行時間, Comparisons, Swaps, Progress % |
| 小サイズ（比較4枚以上 / タブレット / スマホ） | ⏱ 時間, Cmp, Swp, % （省略形） |

### 4.4 Stats Summary のタップ展開（タブレット・スマホ）

PC ではフルサイズ Sort Card の Stats Summary に十分な情報量を表示できるため展開不要。タブレット・スマホでは Stats Summary をタップすると、カード内でインライン展開する。

```
折りたたみ状態:
┌──────────────────────────────────────┐
│ ⏱ 1.23ms  Cmp: 141K  Swp: 6.8K  45%│  [∨] タップで展開
└──────────────────────────────────────┘

展開状態:
┌──────────────────────────────────────┐
│ ⏱ Execution Time                     │
│   Current: 0.56ms / Total: 1.23ms   │
│   Performance: 114K ops/ms           │
│                                      │
│ 🔢 Operations                        │
│   Comparisons:   141,473             │
│   Swaps:           6,892             │
│   Index Reads:    87,308             │
│   Index Writes:  102,157             │
│                                      │
│ Progress: ████████████░░░░░░░ 45%    │
│ Complexity: O(n log n)               │
│                                      │  [∧] タップで折りたたみ
└──────────────────────────────────────┘
```

---

## 5. デバイス別レイアウト

### 5.1 ブレークポイント

| デバイス | 幅 | サイドバー | Sort Card配置 |
|----------|-----|----------|--------------|
| PC | ≥1280px | 常時表示（左カラム） | メインエリア |
| タブレット | 768px〜1279px | オーバーレイ（☰で開閉） | フル幅 |
| スマホ | 〜767px | ボトムシート（↑で展開） | フル幅 |

### 5.2 PC レイアウト（≥1280px）

#### N=1（単一表示）

```
┌──────────────────────────────────────────────────────────────────────────────┐
│ 🎨 Sort Visualization                              [🎓 Tutorial] [⚙ Settings]│
├───────────┬──────────────────────────────────────────────────────────────────┤
│ Quick     │                                                                  │
│ Access    │  [▶]  [⏹]  [🔊]                                                 │
│ Panel     │                                                                  │
│           │  ┌─ Sort Card ──────────────────────────────────────────────┐    │
│ Algorithm │  │ QuickSort                          O(n log n)             │    │
│ [QuickSrt]│  ├──────────────────────────────────────────────────────────┤    │
│ Size [512]│  │                                                          │    │
│ Pattern   │  │          ▂▄▆█▇▅▃▁▃▅▇█▆▄▂▁▃▅▇█▆▄▂                         │    │
│ [Random]  │  │          ││││││││││││││││││││││││                         │    │
│ Mode      │  │          ││││││││││││││││││││││││                         │    │
│ [BarChart]│  │                                                          │    │
│           │  │  [Buffer Array if applicable]                            │    │
│ Speed     │  ├──────────────────────────────────────────────────────────┤    │
│ [━━●━━━]  │  │ ⏱ 1.23ms │ Cmp: 141,473 │ Swp: 6,892 │ Rd: 87K │ 45%  │    │
│ Ops/frame │  └──────────────────────────────────────────────────────────┘    │
│ [━●━━━━]  │                                                                  │
│           │  ◀━━━━━━━━━━━━●━━━━━━━━━━━━━━━━▶ 45%  2,345/5,200 ops          │
│ ────────  │                                                                  │
│ [+ Add & Generate]│                                                             │
│ (1/9)     │                                                                  │
└───────────┴──────────────────────────────────────────────────────────────────┘
```

#### N>1（比較表示）

```
┌──────────────────────────────────────────────────────────────────────────────┐
│ 🎨 Sort Visualization                              [🎓 Tutorial] [⚙ Settings]│
├───────────┬──────────────────────────────────────────────────────────────────┤
│ Quick     │                                                                  │
│ Access    │  [▶]  [⏹]  [🔊]                           2/4 completed         │
│ Panel     │                                                                  │
│           │  ┌─ Sort Card ───────────┐ ┌─ Sort Card ───────────┐            │
│ Algorithm │  │ QuickSort    O(nlogn) │ │ MergeSort    O(nlogn) │            │
│ [QuickSrt]│  │ ▂▄▆█▇▅▃▁▃▅▇█▆▄▂        │ │ ▂▃▅▇█▆▄▁▃▅▇█▆▄▂▁     │            │
│ Size [512]│  │ ⏱1.2ms Cmp:141K 45%  │ │ ⏱1.8ms Cmp:89K  78%  │            │
│ Pattern   │  └───────────────────────┘ └───────────────────────┘            │
│ [Random]  │  ┌─ Sort Card ───────────┐ ┌─ Sort Card ───────────┐            │
│ Mode      │  │ HeapSort     O(nlogn) │ │ BubbleSort   O(n²)    │            │
│ [BarChart]│  │ ▁▃▅▇█▆▄▂▁▃▅▇█▆▄▂       │ │ ▂▃▅▇█▇▅▃▁▃▅▇█▆▄▂     │            │
│ Speed/Ops │  │ ⏱0.9ms Cmp:67K 100%  │ │ ⏱15ms Cmp:512K  12%  │            │
│           │  └───────────────────────┘ └───────────────────────┘            │
│ [+ Add & Generate]│                                                             │
│ (4/9)     │                                                 [◀ 📊] Stats   │
└───────────┴──────────────────────────────────────────────────────────────────┘
```

### 5.3 タブレット レイアウト（768px〜1279px）

#### N=1（単一表示）

```
┌────────────────────────────────────────────────────────┐
│ [☰] 🎨 Sort Visualization             [🎓] [⚙]        │
├────────────────────────────────────────────────────────┤
│          [▶]        [⏹]        [🔊]                    │
├────────────────────────────────────────────────────────┤
│  ┌─ Sort Card ─────────────────────────────────────┐   │
│  │ QuickSort                        O(n log n)     │   │
│  ├─────────────────────────────────────────────────┤   │
│  │                                                 │   │
│  │      Visualization (tap to Play/Pause)          │   │
│  │      ▂▄▆█▇▅▃▁▃▅▇█▆▄▂▁▃▅▇█▆▄▂                    │   │
│  │                                                 │   │
│  ├─────────────────────────────────────────────────┤   │
│  │ ⏱1.23ms  Cmp:141K  Swp:6.8K  Rd:87K  45%  [∨] │   │
│  └─────────────────────────────────────────────────┘   │
│                                                        │
│  ◀━━━━━━━━━●━━━━━━━━━▶ 45%  2,345/5,200 ops           │
└────────────────────────────────────────────────────────┘

 ☰ タップ → 左からオーバーレイ
   (Algorithm, Size, Pattern, Mode, Speed, Ops/frame,
    Add & Generate)
```

#### N>1（比較表示）

```
┌────────────────────────────────────────────────────────┐
│ [☰] 🎨 Comparison · 4 algos           [🎓] [⚙]        │
├────────────────────────────────────────────────────────┤
│      [▶]      [⏹]      [🔊]       2/4 completed       │
├────────────────────────────────────────────────────────┤
│  ┌─ Sort Card ────────┐ ┌─ Sort Card ────────┐        │
│  │ QuickSort  O(nlogn)│ │ MergeSort  O(nlogn)│        │
│  │ ▂▄▆█▇▅▃▁▃▅▇█▆▄▂     │ │ ▂▃▅▇█▆▄▁▃▅▇█▆▄▂▁  │        │
│  │ ⏱1.2ms Cmp:141K 45│ │ ⏱1.8ms Cmp:89K  78│        │
│  └────────────────────┘ └────────────────────┘        │
│  ┌─ Sort Card ────────┐ ┌─ Sort Card ────────┐        │
│  │ HeapSort   O(nlogn)│ │ BubbleSort   O(n²) │        │
│  │ ▁▃▅▇█▆▄▂▁▃▅▇█▆▄▂    │ │ ▂▃▅▇█▇▅▃▁▃▅▇█▆▄▂  │        │
│  │ ⏱0.9ms Cmp:67K 100│ │ ⏱15ms Cmp:512K  12│        │
│  └────────────────────┘ └────────────────────┘        │
│                                            [📊] Stats  │
└────────────────────────────────────────────────────────┘

 📊 タップ → フルスクリーンモーダルで ComparisonStatsTable 表示
```

### 5.4 スマホ レイアウト（〜767px）

#### N=1（単一表示）

```
┌──────────────────────────────┐
│ [☰] 🎨 QuickSort · 512 [⚙] │
├──────────────────────────────┤
│    [▶]      [⏹]      [🔊]   │
├──────────────────────────────┤
│ ┌─ Sort Card ──────────────┐ │
│ │ QuickSort     O(n log n) │ │
│ ├──────────────────────────┤ │
│ │                          │ │
│ │   ▂▄▆█▇▅▃▁▃▅▇█▆▄▂         │ │
│ │   (tap to Play/Pause)    │ │
│ │                          │ │
│ ├──────────────────────────┤ │
│ │ ⏱1.2ms Cmp:141K Swp:6.8K│ │
│ └──────────────────────────┘ │
├──────────────────────────────┤
│ ◀━━━━━●━━━━━━━▶ 45%         │
└──────────────────────────────┘

 ☰ タップ → 左からオーバーレイ
   (Algorithm, Size, Pattern, Mode, Speed, Ops/frame,
    Add & Generate)
```

#### N>1（比較表示） — カルーセル方式

縦スクロールでカードを積むと可視化が小さくなる。横スワイプカルーセルで1枚ずつ表示し、ドットインジケーターで位置を示す。スワイプ方向が横なのでページスクロール（縦）と干渉しない。

```
┌──────────────────────────────┐
│ [☰] 🎨 Comparison · 4  [⚙] │
├──────────────────────────────┤
│    [▶]      [⏹]      [🔇]   │  2/4 completed
├──────────────────────────────┤
│                              │
│  ← swipe →                   │
│  ┌─ Sort Card ─────────────┐ │
│  │ QuickSort     O(n log n)│ │
│  ├──────────────────────────┤ │
│  │                          │ │
│  │   ▂▄▆█▇▅▃▁▃▅▇█▆▄▂        │ │
│  │                          │ │
│  ├──────────────────────────┤ │
│  │ ⏱1.2ms Cmp:141K Swp:6.8K│ │
│  └──────────────────────────┘ │
│                              │
│         ● ○ ○ ○              │  ← ドットインジケーター
│                              │
│                     [📊]     │  ← Stats Table (モーダル)
└──────────────────────────────┘
```

---

## 6. シークバー

### 6.1 配置

| 条件 | 配置 |
|------|------|
| N=1 | Sort Card の下に表示 |
| N>1 | 非表示（各アルゴリズムの操作数が異なるため共通シークバーは不適） |

### 6.2 表示内容

```
◀━━━━━━━━━━━━●━━━━━━━━━━━━━━━━▶ 45%  2,345 / 5,200 ops
```

- ドラッグまたはクリックで任意の位置にジャンプ
- 現在位置 / 総操作数を表示

---

## 7. Quick Access パネル（Layer 2）

### 7.1 デバイス別表示方式

| デバイス | 表示方式 | 開閉方法 |
|----------|---------|----------|
| PC | 左サイドバー（常時表示、リサイズ可能、折りたたみ可能） | ◀/▶ ボタン |
| タブレット | 左からスライドインするオーバーレイ | ☰ ボタンタップ |
| スマホ | 左からスライドインするオーバーレイ | ☰ ボタンタップ |

### 7.2 内容

```
┌────────────────────────┐
│ ⚙️ Quick Access         │
│                        │
│ Algorithm              │
│ [QuickSort        ▼] ? │  ← ? でチュートリアル遷移
│                        │
│ Array Size (推奨: 512) │
│ [512              ▼]   │
│                        │
│ Array Pattern          │
│ [🎲 Random        ▼]   │
│                        │
│ Visualization Mode     │
│ [📊 Bar Chart     ▼]   │
│                        │
│ ─────────────────────  │
│                        │
│ Operations Per Frame   │
│ 10 ops/frame           │
│ [━━━●━━━━━━━━━━━━━━━]  │
│                        │
│ Speed Multiplier       │
│ 1.0x (60 FPS)          │
│ [━●━━━━━━━━━━━━━━━━━]  │
│                        │
│ ─────────────────────  │
│                        │
│ [➕ Add & Generate]     │  ← Upsert: 選択algo を追加 or 配列再生成
│ (1 / 9 algorithms)    │  ← 現在の N を常時表示
│                        │
│ Complexity: O(n log n) │
└────────────────────────┘
```

### 7.3 操作の対比

**配列を再生成するかどうか** が操作の最大の違い。

| 操作 | 配列 | N の変化 | 専用ヶ所 |
|------|------|--------|----------|
| ➕ Add & Generate | **新規生成（全カード）** | Upsert（下記参照） | Quick Access |
| [Algo ▾] インライン切替 | 流用（そのカードのみ再実行） | **N 不変** | カードヘッダー |

**➕ Add & Generate の Upsert 動作:**

| 条件 | 動作 |
|------|------|
| 選択 Algo が比較中に **存在しない**（かつ N < N_max） | 追加 (N→N+1)。全カード新配列で再実行 |
| 選択 Algo が比較中に **すでに存在する** | 配列再生成のみ (N 不変)。全カード新配列で再実行 |
| N が上限 かつ 選択 Algo が未登録 | disabled |

> **N=0 について**: 初回ロード時はアプリが自動で N=1 状態に初期化するため、通常 N=0 の状態はユーザーに見えない。
---

## 9. 比較モード

### 9.1 概要

N>1 の時に暗默的に「比較表示」になる。Comparison Mode トグルは存在しない。複数アルゴリズムを同時実行し、Sort Card をグリッド表示する。

### 9.2 グリッドレイアウト

| アルゴリズム数 | PC | タブレット | スマホ |
|--------------|-----|----------|--------|
| 1 | 1列 | 1列 | 1枚 |
| 2 | 2列 | 2列 | カルーセル |
| 3 | 3列 | 2列 | カルーセル |
| 4 | 2×2 | 2×2 | カルーセル |
| 5〜6 | 3×2 | 2×3 | カルーセル |
| 7〜9 | 3×3 | 2列スクロール | カルーセル |

### 9.3 ComparisonStatsTable

| デバイス | 表示方式 |
|----------|---------|
| PC | 右ドッキングパネル（トグルボタンで開閉） |
| タブレット | 📊 ボタンタップ → フルスクリーンモーダル |
| スマホ | 📊 ボタンタップ → フルスクリーンモーダル |

### 9.4 シークバー

N>1 時はシークバーを非表示にする。各アルゴリズムの操作数が異なるため、共通のシークバーは意味をなさない。Play/Stop で一斉制御する。

---

## 10. チュートリアル UI 統一

### 10.1 カラースキーム統一

チュートリアルページの独自カラーをメインと統一する。

| 項目 | 現在（Tutorial） | 統一後 |
|------|-----------------|--------|
| 背景 | `#111827` | `var(--bg-dark)` = `#1A1A1A` |
| パネル | `#1f2937` | `var(--bg-panel)` = `#2A2A2A` |
| ボーダー | `#374151` | `#3A3A3A` |
| アクセントカラー | `#3b82f6` | `var(--color-compare)` = `#A855F7` |
| ボタン | `#1d4ed8` / `#374151` | メインと同じ `button` / `button.primary` スタイル |

### 10.2 ヘッダー統一

チュートリアルページのヘッダーもメインと同じスタイルのヘッダーバーを使用する。

```
┌──────────────────────────────────────────────────────────────────────────────┐
│ ← Back  🎓 Tutorial: BubbleSort                                       [⚙]  │
├──────────────────────────────────────────────────────────────────────────────┤
│ [Algorithm ▼]  [◀◀] [◀] [▶] [▶] [▶▶]  Speed: [━━●━━]  800ms              │
└──────────────────────────────────────────────────────────────────────────────┘
```

### 10.3 コンテキスト遷移

**メイン → チュートリアル:**
- Quick Access パネルのアルゴリズム選択横に `?` アイコン
- クリックで `/tutorial/{currentAlgorithm}` に遷移

**チュートリアル → メイン:**
- 「▶ Try this algorithm」ボタン
- クリックでメインに戻り、チュートリアルのアルゴリズムが選択された状態で遷移

### 10.4 チュートリアルのレスポンシブ対応

| デバイス | マーブルサイズ | コントロール | Description パネル |
|----------|-------------|------------|-------------------|
| PC | 56px（現状維持） | 横一列 | 常時表示 |
| タブレット | 48px | 横一列（やや縮小） | 常時表示 |
| スマホ | 36px | 横一列（コンパクト） | 折りたたみ可能 |

---

## 11. コンポーネント構造

### 11.1 Before（現在）

```
Index.razor
├── Header（タイトル + Debug + Category + Tutorial）
├── Sidebar
│   ├── Settings（Algorithm, Size, Pattern, Mode, Renderer, Comparison...）
│   ├── NormalModeControls / ComparisonModeControls（Speed, Ops/Frame, Sound...）
│   ├── StatisticsPanel（単一モード専用）
│   └── Complexity
├── Visualization Area
│   ├── 単一: BarChartRenderer / CircularRenderer / ...
│   └── 比較: ComparisonGrid → ComparisonGridItem → ComparisonStatsSummary
├── ComparisonStatsTable（右ドッキング）
└── Controls（SeekBar + Reset + Play/Pause）
```

### 11.2 After（改訂案）

```
Index.razor
├── Header（タイトル + Tutorial + Settings ⚙）
├── Sidebar / Overlay（デバイス別）
│   └── QuickAccessPanel
│       ├── Algorithm, Size, Pattern, Mode
│       ├── Speed, Ops/Frame
│       ├── Comparison ON/OFF + Add
│       └── Generate & Sort
├── PlayControlBar
│   ├── [▶/⏸]  [⏹]  [🔊/🔇]
│   └── (比較モード時) "N/M completed"
├── SortCardArea
│   ├── 単一: SortCard × 1（フルサイズ）
│   └── 比較: SortCardGrid → SortCard × N
│       └── SortCard
│           ├── CardHeader（Algorithm名, Complexity, ✖）
│           ├── Visualization (既存 Renderer)
│           └── SortStatsSummary（タップ展開可能）
├── SeekBar（単一モード時のみ）
├── ComparisonStatsTable（比較モード時のみ）
└── SettingsModal（⚙ クリック時にオーバーレイ）
```

### 11.3 新規 / リネーム / 廃止コンポーネント

| コンポーネント | 状態 | 説明 |
|--------------|------|------|
| `SortCard.razor` | **新規** | CardHeader + Visualization + SortStatsSummary の統一カード |
| `SortCardGrid.razor` | **新規** | 比較モード時のグリッドコンテナ（既存 ComparisonGrid ベース） |
| `SortStatsSummary.razor` | **リネーム** | `ComparisonStatsSummary` → `SortStatsSummary`（タップ展開対応追加） |
| `PlayControlBar.razor` | **新規** | ▶/⏸, ⏹, 🔊/🔇 の3ボタン + ステータス表示 |
| `QuickAccessPanel.razor` | **新規** | サイドバーの Layer 2 コンテンツ |
| `SettingsModal.razor` | **新規** | Layer 3 の設定モーダル |
| `StatisticsPanel.razor` | **廃止** | Sort Card 内の SortStatsSummary に統合 |
| `NormalModeControls.razor` | **廃止** | QuickAccessPanel + PlayControlBar + SettingsModal に分散 |
| `ComparisonModeControls.razor` | **廃止** | QuickAccessPanel に統合 |
| `ModeControlPanel.razor` | **廃止** | PlayControlBar + SeekBar に分散 |
| `ComparisonGrid.razor` | **廃止** | SortCardGrid に置き換え |
| `ComparisonGridItem.razor` | **廃止** | SortCard に置き換え |
| `ComparisonStatsSummary.razor` | **廃止** | SortStatsSummary にリネーム |

---

## 12. 実装優先度

| 優先度 | 変更 | 影響範囲 |
|--------|------|---------|
| **P1** | Sort Card パターン導入（単一/比較統一） | 新 `SortCard`, `SortCardGrid`, `SortStatsSummary` |
| **P1** | 再生コントロールを可視化の上に移動 | 新 `PlayControlBar`, `Index.razor`, CSS |
| **P1** | サイドバーから Statistics 削除、Quick Access 化 | 新 `QuickAccessPanel`, `Index.razor` |
| **P2** | Settings Modal 導入 | 新 `SettingsModal`, `Index.razor` |
| **P2** | レスポンシブ3段階（PC/タブレット/スマホ） | `app.css` の media queries |
| **P2** | Stats Summary タップ展開 | `SortStatsSummary` |
| **P3** | チュートリアルのカラー統一 | `TutorialPage.razor.css` |
| **P3** | チュートリアル ⇔ メイン コンテキスト遷移 | 両ページ |
| **P3** | スマホ比較モードのカルーセル | `SortCardGrid` + CSS/JS |
| **P3** | ComparisonStatsTable のモーダル化（タブレット/スマホ） | `ComparisonStatsTable` |
| **P2** | 状態保持（Query String + localStorage） | 新 `StateStorageService`, `Index.razor`, JS Interop |

---

## 13. 状態保持

### 13.1 課題

Blazor WASM はステートレスであり、F5 でページリフレッシュすると選択していたアルゴリズム・要素数・パターン等がすべてデフォルト値にリセットされる。頻繁にリフレッシュしながら使うユースケース（開発中、ブックマーク、URL共有）では致命的な不便さとなる。

### 13.2 方式選定

保持すべき状態は「性質」が2種類あり、それぞれ最適な保持先が異なる。

| 性質 | 説明 | 例 | 最適な保持先 |
|------|------|-----|-------------|
| **コンテンツ状態** | 「今何を見ているか」— 共有したい、ブックマークしたい | Algorithm, Size, Pattern, Mode | **URL Query String** |
| **プリファレンス** | 「どう使いたいか」— 個人設定、他人に渡す必要なし | Speed, WebGL, Volume, Debug | **localStorage** |

#### 不採用: Session Storage

| 問題 | 説明 |
|------|------|
| 寿命が中途半端 | タブを閉じたら消える → 「昨日の続き」ができない |
| 共有不可 | URL に含まれないため「この設定見て」ができない |
| localStorage の下位互換 | 永続性でも共有性でも他の方式に劣る |

#### 採用: Query String + localStorage ハイブリッド

```
URL Query String = 「何を見ているか」
  → 共有可能、ブックマーク可能、F5で復元
  → 例: /?algo=QuickSort&size=512&pattern=Random&mode=BarChart

localStorage = 「どう使いたいか」
  → 個人設定、セッション跨ぎで永続、F5で復元
  → 例: sortvis.renderer=webgl, sortvis.speedMultiplier=10
```

### 13.3 Query String（コンテンツ状態）

#### パラメータ定義

| パラメータ | 型 | 説明 | 例 | デフォルト |
|-----------|-----|------|-----|-----------|
| `algo` | string | 選択中のアルゴリズム名 | `QuickSort` | `Bubble sort` |
| `size` | int | 配列サイズ | `512` | `64` |
| `pattern` | string | 配列パターン名 | `Random` | `🎲 Random` |
| `mode` | string | 可視化モード | `BarChart` | `BarChart` |

#### URL 例

```
/?algo=QuickSort&size=512&pattern=Random&mode=BarChart
```

**最小（デフォルトと同じ値は省略可能）:**
```
/?algo=QuickSort
```

#### チュートリアルページ

チュートリアルページは既存のルートパラメータ `/tutorial/{AlgorithmName}` をそのまま使用する。追加の Query String は不要。

#### 動作フロー

```
ページロード時:
  1. URL の Query String を解析
  2. パラメータが存在する場合 → その値で状態を初期化
  3. パラメータが存在しない場合 → デフォルト値を使用

状態変更時:
  1. ユーザーが Algorithm / Size / Pattern / Mode を変更
  2. NavigationManager.NavigateTo(newUrl, replace: true) で URL を更新
     ※ Array Size / Pattern の変更は URL に反映されるが、配列の再生成は
       次回 Add & Generate クリック時まで行わない（意図的な再実行タイミング制御）
     ※ Array Size / Pattern の変更は URL に反映されるが、配列の再生成は
       次回 Add & Generate クリック時まで行わない（意図的な再実行タイミング制御）
     ※ replace: true でブラウザ履歴を汚さない
  3. 画面は再描画されない（Blazor内部のステート変更のみ）

F5 リフレッシュ時:
  1. URL の Query String からコンテンツ状態を復元
  2. localStorage からプリファレンスを復元
  3. 前回と同じ状態で画面が表示される
```

### 13.4 localStorage（プリファレンス）

#### キー定義

| キー | 型 | 説明 | デフォルト |
|------|-----|------|-----------|
| `sortvis.renderer` | string | `canvas2d` / `webgl` | `webgl` |
| `sortvis.opsPerFrame` | int | Operations Per Frame (1-1000) | `1` |
| `sortvis.speedMultiplier` | float | 速度倍率 (0.5-100) | `10.0` |
| `sortvis.autoReset` | bool | ソート完了時に自動リセット | `false` |
| `sortvis.instantMode` | bool | アニメーション無し即座表示 | `false` |
| `sortvis.soundEnabled` | bool | サウンド ON/OFF | `false` |
| `sortvis.soundVolume` | float | 音量 (0.0-1.0) | `0.5` |
| `sortvis.autoRecommendedSize` | bool | 推奨サイズ自動切替 | `false` |
| `sortvis.categoryFilter` | string | カテゴリフィルタ | `""` (All) |
| `sortvis.debugLog` | bool | デバッグログ出力 | `false` |
| `sortvis.sidebarWidth` | int | サイドバー幅 (px) | `280` |
| `sortvis.sidebarCollapsed` | bool | サイドバー折りたたみ | `false` |

#### 動作フロー

```
ページロード時 (OnAfterRenderAsync firstRender):
  1. JS Interop で localStorage から全プリファレンスを一括読み込み
  2. 値が存在する場合 → その値で各サービス/設定を初期化
  3. 値が存在しない場合 → デフォルト値を使用

設定変更時:
  1. ユーザーが Settings モーダルや Quick Access パネルで設定を変更
  2. 即座に JS Interop で localStorage に書き込み
  3. アプリ内の状態も同時に更新
```

#### JS Interop

localStorage へのアクセスは JS Interop 経由で行う。一括読み書きで往復回数を最小化する。

```javascript
// 一括読み込み
window.stateStorage = {
    loadAll: function () {
        const prefix = 'sortvis.';
        const result = {};
        for (let i = 0; i < localStorage.length; i++) {
            const key = localStorage.key(i);
            if (key.startsWith(prefix)) {
                result[key] = localStorage.getItem(key);
            }
        }
        return result;
    },
    save: function (key, value) {
        localStorage.setItem(key, value);
    },
    saveAll: function (entries) {
        for (const [key, value] of Object.entries(entries)) {
            localStorage.setItem(key, value);
        }
    }
};
```

### 13.5 状態と保持先の対応一覧

| 状態 | Layer | 保持先 | 共有 | F5復元 | 永続 |
|------|-------|--------|------|--------|------|
| Algorithm | 2 | Query String | ✅ | ✅ | URL次第 |
| Array Size | 2 | Query String | ✅ | ✅ | URL次第 |
| Array Pattern | 2 | Query String | ✅ | ✅ | URL次第 |
| Visualization Mode | 2 | Query String | ✅ | ✅ | URL次第 |
| Ops Per Frame | 2 | localStorage | ❌ | ✅ | ✅ |
| Speed Multiplier | 2 | localStorage | ❌ | ✅ | ✅ |
| Renderer | 3 | localStorage | ❌ | ✅ | ✅ |
| Auto Reset | 3 | localStorage | ❌ | ✅ | ✅ |
| Instant Mode | 3 | localStorage | ❌ | ✅ | ✅ |
| Sound Enabled | 3 | localStorage | ❌ | ✅ | ✅ |
| Sound Volume | 3 | localStorage | ❌ | ✅ | ✅ |
| Auto Recommended Size | 3 | localStorage | ❌ | ✅ | ✅ |
| Category Filter | 3 | localStorage | ❌ | ✅ | ✅ |
| Debug Log | 3 | localStorage | ❌ | ✅ | ✅ |
| Sidebar Width | - | localStorage | ❌ | ✅ | ✅ |
| Sidebar Collapsed | - | localStorage | ❌ | ✅ | ✅ |

### 13.6 URL共有のユースケース

```
ユーザーA: QuickSort 512要素のBar Chart可視化を見ている
  → URL: /?algo=QuickSort&size=512&pattern=Random&mode=BarChart
  → このURLをユーザーBに共有

ユーザーB: URLを開く
  → QuickSort, 512, Random, BarChart で画面が開く（コンテンツ状態の復元）
  → Speed, Sound, WebGL等はユーザーBの localStorage の値が使われる（個人設定）
```

---

## 14. CSS 設計方針

### 14.1 CSS カスタムプロパティ（既存活用）

```css
:root {
    --bg-dark: #1A1A1A;
    --bg-panel: #2A2A2A;
    --text-light: #FFFFFF;
    --text-gray: #999;
    --color-normal: #3B82F6;
    --color-read: #FBBF24;
    --color-write: #F97316;
    --color-compare: #A855F7;
    --color-swap: #EF4444;
    --color-sorted: #10B981;
    --color-buffer: #6B7280;
}
```

### 14.2 ブレークポイント

```css
/* スマホ: デフォルト (〜767px) */

/* タブレット */
@media (min-width: 768px) { ... }

/* PC */
@media (min-width: 1280px) { ... }
```

### 14.3 Grid レイアウト

```css
/* PC: 2カラム */
@media (min-width: 1280px) {
    .visualization-page {
        grid-template-columns: var(--sidebar-width, 280px) 1fr;
        grid-template-rows: auto auto 1fr auto;
        /* rows: header | play-controls | sort-card-area + sidebar | seekbar */
    }
}

/* タブレット・スマホ: 1カラム */
@media (max-width: 1279px) {
    .visualization-page {
        grid-template-columns: 1fr;
        grid-template-rows: auto auto 1fr auto;
        /* rows: header | play-controls | sort-card-area | seekbar */
    }
}
```

---

**Document Version**: 1.1
**Last Updated**: 2025-06
**Changelog**: v1.1 - §13 状態保持（Query String + localStorage ハイブリッド）追加
**Related Documents**: `VisualizationWeb.md`, `VisualizationWeb_tutorial.md`
