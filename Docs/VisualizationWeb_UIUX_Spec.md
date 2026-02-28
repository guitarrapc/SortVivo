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

### 1.4 核心コンセプト: 「UI Card」によるコンポーネントのグルーピング

**課題**: メイン画面とチュートリアルで「アルゴリズム選択」「再生コントロール」「速度制御」など概念的に共通する要素があるが、パラメータやインタラクションの詳細が異なるため、単純なコンポーネント共有ができない。

**解決策**: UI 要素をユーザーの意図（「何を？」「どう見せる？」「どの速さで？」「どう操作する？」）でグルーピングし、各グループを **UI Card** として扱う。カード単位でレイアウトの柔軟性を持たせることで、メインとチュートリアルでカードの「組み合わせ」と「配置」を変えるだけで統一感のある UI を実現する。

| UI Card | 関心 | メイン画面 | チュートリアル |
|---------|------|-----------|---------------|
| 🎯 Sort Target | 何をソートするか | Algorithm, Array Size, Scramble Pattern | Algorithm のみ |
| 🎨 Display | どう見せるか | Visualization Mode | ❌（MarbleRenderer 固定） |
| ⚡ Speed | どの速さで再生するか | Ops/Frame, Speed Multiplier | Speed (ms) |
| ▶ Playback | 再生をどう操作するか | Play/Pause, Stop, Sound, Status | Play/Pause, Step Nav, Step Counter |
| 🚀 Action | 実行する | Add & Generate, Count, Complexity | ❌（アルゴリズム選択時に自動ロード） |

**カード配置の自由度:**

同じ UI Card セットでも、配置先によって見た目が変わる。

| 配置先 | メイン画面 | チュートリアル |
|--------|-----------|---------------|
| サイドバー（PC） | カード縦積み | — |
| ツールバー（横） | — | カード横並び |
| オーバーレイ（モバイル） | カード縦積み | カード縦積み |

**メリット:**
- カード内の要素は固定だが、「どのカードをどこに配置するか」は自由に変えられる
- QuickAccessPanel 内のカード配置 → サイドバー縦積み / ツールバー横並び / モバイルアコーディオン
- メイン / チュートリアルで同じ論理グループでも、中身のウィジェットは独立して実装可能
- 将来のカード追加（例: Filter Card、Export Card）にも自然に対応

---

## 2. Layer 分類

### 2.1 Layer 1: Always Visible（常時表示）

| 要素 | 説明 |
|------|------|
| ヘッダー | アプリ名、☰（Quick Access パネル開閉）、🎓（Tutorial）、⚙（Settings）のアイコンボタン |
| 再生コントロール | ▶/⏸, ⏹, 🔊/🔇 の3つのアイコンボタン（可視化エリアの上） |
| Sort Card | 可視化 + Stats Summary |
| シークバー | N=1 時のみ、Sort Card の下に表示 |
| ステータス | N>1 時の完了数表示（再生コントロール横） |

**ヘッダーアイコンボタン仕様:**

| ボタン | アイコン | 表示条件 | サイズ | tooltip |
|--------|---------|---------|-------|---------|
| Hamburger Menu | ☰ | **PC**: 常時表示<br>**タブレット/スマホ**: 常時表示 | 28×28px | PC: "Show/Hide Quick Access Panel"<br>Mobile: "Open Quick Access Panel" |
| Tutorial | 🎓 | 常時表示 | 40×40px (PC) / 36×36px (タブレット) / 32×32px (スマホ) | "Go to tutorial" |
| Settings | ⚙ | 常時表示 | 40×40px (PC) / 36×36px (タブレット) / 32×32px (スマホ) | "Settings" |

**ハンバーガーメニューボタン（☰）の動作:**

| デバイス | 動作 |
|---------|------|
| PC | Quick Access パネルの折りたたみ/展開をトグル（`IsCollapsed` の切替） |
| タブレット/スマホ | Quick Access パネルのオーバーレイ表示（`IsOpen` の切替） |

- スタイル: 控えめな背景（`transparent`）、1px ボーダー（`--border-color-light`）、角丸 6px
- ホバー時: 背景 `--bg-hover`
- テキストなし、アイコンのみでシンプルかつ統一感のあるデザイン

### 2.2 Layer 2: Quick Access（操作パネル）

UI Card（§1.4）の論理グループごとにカード化して配置する。

**パネル構成:**

1. **アルゴリズム説明カード**（パネル最上部）
   - 現在選択されているアルゴリズムの概要を表示
   - ラベル: `CURRENT ALGORITHM`
   - アルゴリズム名（大きめ、明るい緑 `--color-tutorial-accent`）
   - "How it works:" の説明文（`TutorialDescription`から抽出）
   - 背景: 緑系グラデーション（`--color-tutorial-bg-start` → `--color-tutorial-bg-end`）
   - テキスト色: ベージュ・タン系（`--color-tutorial-text`）

2. **UI Card セクション**

| UI Card | 要素 | 説明 |
|---------|------|------|
| 🎯 Sort Target | Algorithm 選択 | ドロップダウン（カテゴリグループ対応）+ ? リンク |
| | Array Size | ドロップダウン（推奨サイズ表示） |
| | Scramble Pattern | ドロップダウン（カテゴリグループ対応） |
| 🎨 Display | Visualization Mode | ドロップダウン（BarChart / Circular / Spiral / DotPlot / Picture系） |
| ⚡ Speed | Operations Per Frame | スライダー + 値表示 |
| | Speed Multiplier | スライダー + 値表示 + 実効FPS |
| 🚀 Action | Add & Generate | 選択 Algo の Upsert + 全カード新配列再実行（§7.3 参照） |
| | Complexity | 選択中アルゴリズムの計算量表示（読み取り専用） |

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

テキストなし、SVGアイコンのみの大きめボタンを3つ横に並べる。

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

**アイコン仕様:**

すべてのボタンアイコンはSVGで実装され、`currentColor`を使用して背景色に応じた色を継承する。

| ボタン | アイコン | SVG仕様 |
|--------|---------|---------|
| Play | ▶ | `<polygon>` で三角形（右向き） |
| Pause | ⏸ | 2つの `<rect>` で縦の長方形 |
| Stop | ⏹ | `<rect>` で正方形 |
| Sound ON | 🔊 | スピーカー（`<polygon>`）+ 音波2本（`<path>`、ストローク） |
| Sound OFF | 🔇 | スピーカー（`<polygon>`）+ 斜線2本（`<line>`） |

SVGサイズ: `28×28px` (ボタン内)

### 3.3 ボタン状態

| ボタン | 状態 | アイコン | 背景色 | tooltip |
|--------|------|---------|--------|---------|
| Play/Pause | 停止/一時停止中 | `▶` (SVG) | `var(--color-play)` = `#3b82f6`（青） | "Play" |
| Play/Pause | 再生中 | `⏸` (SVG) | `var(--color-pause)` = `#a855f7`（紫） | "Pause" |
| Stop | 常時 | `⏹` (SVG) | `var(--color-stop)` = `#e53e3e`（赤） | "Stop" |
| Sound | ON | 🔊 (SVG) | `var(--color-sound-on)` = `#22c55e`（鮮やかな緑） | "Sound ON" |
| Sound | OFF | 🔇 (SVG) | `var(--color-sound-off)` = `#6b7280`（グレー）+ `opacity: 0.6` | "Sound OFF" |
| Sound | N>1 時無効 | 🔇 (SVG) | disabled（`opacity: 0.35`, `cursor: not-allowed`） | "Sound not available in Comparison Mode" |

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
│ 🎨 Sort Visualization                                       [🎓] [⚙]│
├───────────┬──────────────────────────────────────────────────────────────────┤
│ Quick     │                                                                  │
│ Access    │  [▶]  [⏹]  [🔊]                                                 │
│ Panel     │                                                                  │
│           │  ┌─ Sort Card ──────────────────────────────────────────────┐    │
│ ┌──────┐  │  │ QuickSort                          O(n log n)             │    │
│ │🎯 Sort│  │  ├──────────────────────────────────────────────────────────┤    │
│ │Target │  │  │                                                          │    │
│ │Algo   │  │  │          ▂▄▆█▇▅▃▁▃▅▇█▆▄▂▁▃▅▇█▆▄▂                         │    │
│ │Size   │  │  │          ││││││││││││││││││││││││                         │    │
│ │Pattern│  │  │          ││││││││││││││││││││││││                         │    │
│ └──────┘  │  │                                                          │    │
│ ┌──────┐  │  │  [Buffer Array if applicable]                            │    │
│ │🎨 Disp│  │  ├──────────────────────────────────────────────────────────┤    │
│ │Mode   │  │  │ ⏱ 1.23ms │ Cmp: 141,473 │ Swp: 6,892 │ Rd: 87K │ 45%  │    │
│ └──────┘  │  └──────────────────────────────────────────────────────────┘    │
│ ┌──────┐  │                                                                  │
│ │⚡Speed│  │  ◀━━━━━━━━━━━━●━━━━━━━━━━━━━━━━▶ 45%  2,345/5,200 ops          │
│ │Ops/fr │  │                                                                  │
│ │Multi  │  │                                                                  │
│ └──────┘  │                                                                  │
│ ┌──────┐  │                                                                  │
│ │🚀 Act │  │                                                                  │
│ │Add&Gen│  │                                                                  │
│ │(1/9)  │  │                                                                  │
│ └──────┘  │                                                                  │
└───────────┴──────────────────────────────────────────────────────────────────┘
```

#### N>1（比較表示）

```
┌──────────────────────────────────────────────────────────────────────────────┐
│ 🎨 Sort Visualization                                       [🎓] [⚙]│
├───────────┬──────────────────────────────────────────────────────────────────┤
│ Quick     │                                                                  │
│ Access    │  [▶]  [⏹]  [🔊]                           2/4 completed         │
│ Panel     │                                                                  │
│ ┌──────┐  │  ┌─ Sort Card ───────────┐ ┌─ Sort Card ───────────┐            │
│ │🎯 Sort│  │  │ QuickSort    O(nlogn) │ │ MergeSort    O(nlogn) │            │
│ │Target │  │  │ ▂▄▆█▇▅▃▁▃▅▇█▆▄▂        │ │ ▂▃▅▇█▆▄▁▃▅▇█▆▄▂▁     │            │
│ └──────┘  │  │ ⏱1.2ms Cmp:141K 45%  │ │ ⏱1.8ms Cmp:89K  78%  │            │
│ ┌──────┐  │  └───────────────────────┘ └───────────────────────┘            │
│ │🎨 Disp│  │  ┌─ Sort Card ───────────┐ ┌─ Sort Card ───────────┐            │
│ └──────┘  │  │ HeapSort     O(nlogn) │ │ BubbleSort   O(n²)    │            │
│ ┌──────┐  │  │ ▁▃▅▇█▆▄▂▁▃▅▇█▆▄▂       │ │ ▂▃▅▇█▇▅▃▁▃▅▇█▆▄▂     │            │
│ │⚡Speed│  │  │ ⏱0.9ms Cmp:67K 100%  │ │ ⏱15ms Cmp:512K  12%  │            │
│ └──────┘  │  └───────────────────────┘ └───────────────────────┘            │
│ ┌──────┐  │                                                                  │
│ │🚀 Act │  │                                                 [◀ 📊] Stats   │
│ │(4/9)  │  │                                                                  │
│ └──────┘  │                                                                  │
└───────────┴──────────────────────────────────────────────────────────────────┘
```

### 5.3 タブレット レイアウト（768px〜1279px）

#### N=1（単一表示）

```
┌────────────────────────────────────────────────────────┐
│ [☰] 🎨 Sort Visualization                [🎓] [⚙]   │
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
   (🎯 Sort Target, 🎨 Display, ⚡ Speed, 🚀 Action
    の UI Card が縦積みで表示)
```

#### N>1（比較表示）

```
┌────────────────────────────────────────────────────────┐
│ [☰] 🎨 Comparison · 4 algos              [🎓] [⚙]   │
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
│ [☰] 🎨 QuickSort · 512   [⚙] │
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
   (🎯 Sort Target, 🎨 Display, ⚡ Speed, 🚀 Action
    の UI Card が縦積みで表示)
```

#### N>1（比較表示） — カルーセル方式

縦スクロールでカードを積むと可視化が小さくなる。横スワイプカルーセルで1枚ずつ表示し、ドットインジケーターで位置を示す。スワイプ方向が横なのでページスクロール（縦）と干渉しない。

```
┌──────────────────────────────┐
│ [☰] 🎨 Comparison · 4    [⚙] │
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
| PC | 左サイドバー（常時表示、リサイズ可能、折りたたみ可能） | ヘッダーの ☰ ボタン ／ パネル内タイトル行の ☰ ボタン |
| タブレット | 左からスライドインするオーバーレイ | ヘッダーの ☰ ボタン |
| スマホ | 左からスライドインするオーバーレイ | ヘッダーの ☰ ボタン |

**PC版の折りたたみ動作:**

- ヘッダー左端の **☰ ハンバーガーメニュー** またはパネル内タイトル行右端の **☰ ボタン** をクリックでパネルを折りたたみ/展開
- 折りたたみ状態は localStorage に保存され、次回起動時に復元
- リサイズハンドル（パネル右端）で幅を 200px～500px の範囲で調整可能
- ハンバーガーメニューボタンのスタイル：グレー系背景 (`--bg-hover`)、ホバー時に少し明るくなる

### 7.2 内容（UI Card ベース）

UI Card（§1.4）の論理グループごとにカード化して配置する。各カードはアイコン付き見出しと背景で視覚的に区切られる。

```
┌────────────────────────────────────┐
│ ⚙️ Quick Access                     │
│                                    │
│ ┌────────────────────────────────┐ │
│ │ CURRENT ALGORITHM              │ │  ← アルゴリズム説明カード
│ │                                │ │
│ │ Bubble Sort                    │ │
│ │                                │ │
│ │ Repeatedly steps through the   │ │
│ │ list, compares adjacent        │ │
│ │ elements and swaps them if     │ │
│ │ they are in the wrong order.   │ │
│ └────────────────────────────────┘ │
│                                    │
│ ┌──────────────────┐               │
│ │ 🎯 Sort Target    │               │
│ │ Algorithm        │               │
│ │ [QuickSort    ▼] ? │              ← ? でチュートリアル遷移
│ │ Array Size (推奨: 512)│           │
│ │ [512          ▼]   │             │
│ │ Scramble Pattern │               │
│ │ [🎲 Random    ▼]   │             │
│ └──────────────────┘               │
│                                    │
│ ┌──────────────────┐               │
│ │ 🎨 Display       │               │
│ │ Visualization Mode │              │
│ │ [📊 Bar Chart ▼]   │             │
│ └──────────────────┘               │
│                                    │
│ ┌──────────────────┐               │
│ │ ⚡ Speed          │               │
│ │ Ops/Frame        │               │
│ │ 10 ops/frame     │               │
│ │ [━━━●━━━━━━━━━]  │               │
│ │ Multiplier       │               │
│ │ 1.0x (60 FPS)    │               │
│ │ [━●━━━━━━━━━━━]  │               │
│ └──────────────────┘               │
│                                    │
│ ┌──────────────────┐               │
│ │ 🚀 Action        │               │
│ │ [➕ Add&Generate] │               │  ← Upsert: 選択algo を追加 or 配列再生成
│ │ (1/9 algorithms) │               │  ← 現在の N を常時表示
│ │ O(n log n)       │               │
│ └──────────────────┘               │
│                                    │
└────────────────────────────────────┘
```

**カードの視覚仕様:**

| 要素 | スタイル |
|------|---------|
| Quick Access パネル背景 | `transparent`（メイン背景 `--bg-main` が見える） |
| アルゴリズム説明カード背景 | 緑系グラデーション（`linear-gradient(135deg, var(--color-tutorial-bg-start), var(--color-tutorial-bg-end))`） |
| アルゴリズム説明カード - ラベル | `CURRENT ALGORITHM`、`font-size: 0.68rem`、`text-transform: uppercase`、`color: var(--text-secondary)` |
| アルゴリズム説明カード - タイトル | アルゴリズム名、`font-size: 1.4rem`、`color: var(--color-tutorial-accent)` = `#8ec07c`（明るい緑） |
| アルゴリズム説明カード - 本文 | `font-size: 0.9rem`、`color: var(--color-tutorial-text)` = `#bdae93`（ベージュ・タン系） |
| UI Card 背景 | `#2a2a2a`（純粋な灰色、メリハリをつけるため緑要素を削除） |
| UI Card 見出し | アイコン + ラベル、`font-size: 0.68rem`、`text-transform: uppercase`、下ボーダー付き |
| カード間マージン | `10px` |
| 角丸 | `border-radius: 6px`（UI Card）、`8px`（アルゴリズム説明カード） |

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

## 8. ページ横断の要素マッピング

### 8.1 全要素の抽出と所属

メイン画面とチュートリアルのすべての UI 要素を抽出し、対応関係と共通化可能性を整理する。

| 要素 | メイン画面 | チュートリアル | UI Card | 共通化 |
|------|-----------|--------------|---------|--------|
| アルゴリズム選択 | QAP: select + ? リンク | toolbar: select | 🎯 Sort Target | △ 部分共通（select UI は同形だがバインド先が異なる） |
| Array Size | QAP: select + 推奨表示 | ❌（固定配列） | 🎯 Sort Target | ✕ メイン専用 |
| Scramble Pattern | QAP: select | ❌（固定配列） | 🎯 Sort Target | ✕ メイン専用 |
| Visualization Mode | QAP: select | ❌（MarbleRenderer 固定） | 🎨 Display | ✕ メイン専用 |
| Operations Per Frame | QAP: slider + 値 | ❌ | ⚡ Speed | ✕ メイン専用 |
| Speed Multiplier | QAP: slider + 値 + FPS | ❌ | ⚡ Speed | ✕ メイン専用 |
| Speed (ms) | ❌ | toolbar: range slider | ⚡ Speed | ✕ Tutorial 専用 |
| 再生/一時停止 | PlayControlBar: ▶/⏸ | toolbar: ▶/⏸ | ▶ Playback | ✅ 概念共通（トグル動作同一） |
| 停止 | PlayControlBar: ⏹ | ❌ | ▶ Playback | ✕ メイン専用 |
| ステップ操作 | ❌ | toolbar: ◀◀ ◀ ▶ ▶▶ | ▶ Playback | ✕ Tutorial 専用 |
| ステップカウンター | ❌ | toolbar: "N / M" | ▶ Playback | ✕ Tutorial 専用 |
| サウンド | PlayControlBar: 🔊/🔇 | ❌ | ▶ Playback | ✕ メイン専用 |
| ステータステキスト | PlayControlBar: "2/4 completed" | ❌ | ▶ Playback | ✕ メイン専用 |
| Add & Generate | QAP: button + count | ❌ | 🚀 Action | ✕ メイン専用 |
| Complexity 表示 | QAP: text / SortCard: badge | 説明パネル: badge | 🚀 Action | △ 表示形式は異なるが情報は同一 |
| カードヘッダー | SortCard: algo + complexity + ✖ | ❌ | Sort Card | ✕ メイン専用 |
| 可視化 | SortCard → Renderers（Canvas） | MarbleRenderer（HTML） | Sort Card | ✕ 完全に別 |
| 統計サマリー | SortStatsSummary | ❌ | Sort Card | ✕ メイン専用 |
| シークバー | SeekBar（N≤1 時のみ） | ❌ | Layer 1 | ✕ メイン専用 |
| 比較統計テーブル | ComparisonStatsTable（modal） | ❌ | Layer 1 | ✕ メイン専用 |
| 説明パネル | ❌ | tutorial-description-panel | ❌ | ✕ Tutorial 専用 |
| ナラティブ | ❌ | tutorial-narrative-panel | ❌ | ✕ Tutorial 専用 |
| キーボードヒント | ❌ | ctrl-keyboard-hint | ❌ | ✕ Tutorial 専用 |

### 8.2 共通化の可能性マトリクス

| UI Card | メインで使用 | Tutorial で使用 | 共通コンポーネント化 | 判定理由 |
|---------|:-----------:|:--------------:|:------------------:|---------|
| 🎯 Sort Target | ✅（全要素） | ⚠️（Algorithm のみ） | △ 部分共通 | Algorithm select は同形。Size/Pattern は Tutorial に不要 |
| 🎨 Display | ✅ | ❌ | ✕ メイン専用 | Tutorial は MarbleRenderer 固定 |
| ⚡ Speed | ✅ | ⚠️（概念は同じ） | △ UI 共通化可能 | 粒度は違うが「速さ制御」という関心は同一 |
| ▶ Playback | ✅ | ⚠️（Play/Pause のみ共通） | △ Shell 共通 + スロット | Play/Pause は同一だがステップ操作は Tutorial 独自 |
| 🚀 Action | ✅ | ❌ | ✕ メイン専用 | Tutorial はアルゴリズム選択時に自動ロード |

### 8.3 共通化しないという判断

UI Card はあくまで **論理的なグルーピング単位** であり、メインとチュートリアルで同じ Blazor コンポーネントを共有することを目的としない。

**理由:**
- メインの ⚡ Speed は `OperationsPerFrame` (int, 1-1000) と `SpeedMultiplier` (float, 0.5-100) の2スライダー
- チュートリアルの ⚡ Speed は `autoPlaySpeedMs` (int, 200-2000) の1スライダー
- パラメータの型・範囲・意味が異なり、無理に共通化すると条件分岐だらけの複雑なコンポーネントになる

**UI Card の価値:**
- 共通コンポーネント化ではなく、**デザイン上の統一感** を保証する仕組み
- 同じカードタイトル・アイコン・背景色・角丸を使うことで、ユーザーが「これは速度の設定だ」と直感的に認識できる
- 実装はページごとに独立しつつ、見た目の一貫性は CSS クラス（`.ui-card`, `.ui-card__title`）で保証

### 8.4 チュートリアル ツールバーの UI Card マッピング

現在のチュートリアルツールバーは1本の横バーに全要素が並んでいる:

```
現在:
┌──────────────────────────────────────────────────────────────┐
│ [Algorithm ▼]  Step N/M  [◀◀] [◀] [▶] [▶] [▶▶]  Speed: [━●━] 800ms │
└──────────────────────────────────────────────────────────────┘
```

これを UI Card の論理グループで区切ると:

```
カード化後:
┌──────────────────────────────────────────────────────────────────┐
│ ┌─ 🎯 ──────┐  ┌─ ▶ ──────────────────────────┐  ┌─ ⚡ ────────┐ │
│ │ [Algo  ▼]  │  │ Step N/M  [◀◀][◀][▶][▶][▶▶] │  │ Speed [━●━] │ │
│ └────────────┘  └──────────────────────────────┘  │ 800ms       │ │
│                                                    └─────────────┘ │
└──────────────────────────────────────────────────────────────────┘
```

**PC**: 3カードを横並び（flex）
**タブレット**: 3カードを横並び（やや縮小）
**スマホ**: Algorithm + Playback を1行目、Speed を2行目に wrap

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
| 背景 | `#111827` | `var(--bg-main)` = `#1a1f27` |
| パネル | `#1f2937` | `var(--bg-panel)` = `#1e2530` |
| カード | — | `var(--bg-card)` = `#272f3b` |
| ボーダー | `#374151` | `var(--border-color)` = `#374151` |
| アクセントカラー | `#3b82f6` | `var(--color-accent)` = `#7fa86f` |
| ボタンプライマリ | `#1d4ed8` | `var(--color-play)` = `#3b82f6` |
| ボタンセカンダリ | `#374151` | `var(--bg-hover)` = `#323c4d` |
| テキストプライマリ | `#ffffff` | `var(--text-primary)` = `#ffffff` |
| テキストセカンダリ | — | `var(--text-secondary)` = `#b8c1d0` |

### 10.2 ヘッダー統一

チュートリアルページのヘッダーもメインと同じスタイルのヘッダーバーを使用する。

```
┌──────────────────────────────────────────────────────────────────────────────┐
│ ← Back  🎓 Tutorial: BubbleSort                                       [⚙]  │
└──────────────────────────────────────────────────────────────────────────────┘
```

**ヘッダータイトルのスタイル統一:**

| プロパティ | 値 | 説明 |
|-----------|-----|------|
| `font-size` | `2.25rem` | メインページと同じサイズ |
| `font-weight` | `400` | メインページと同じウェイト |
| `position` | `absolute` (中央配置) | メインページと同じレイアウト |
| `margin` | `0` | 余白なし |
| `white-space` | `nowrap` | 改行しない |

### 10.3 ツールバーの UI Card 化

チュートリアルのツールバーを UI Card（§1.4）の論理グループに沿ってカード化する。メインの QuickAccessPanel と同じカード概念（タイトル・アイコン・背景色）を共有し、デザイン上の統一感を持たせる。

**カード構成:**

| UI Card | 要素 | メインとの対応 |
|---------|------|--------------|
| 🎯 Sort Target | Algorithm select | メインの Algorithm select と同じ関心 |
| ▶ Playback | Step Counter, ◀◀ ◀ ▶ ▶ ▶▶ | メインの Play/Pause/Stop と同じ関心 |
| ⚡ Speed | Speed slider (200-2000ms) + 値表示 | メインの Ops/Frame + Multiplier と同じ関心 |

**PC レイアウト:**

```
┌──────────────────────────────────────────────────────────────────┐
│ ┌─ 🎯 Sort Target ──┐  ┌─ ▶ Playback ─────────────────────────┐ │
│ │ [BubbleSort    ▼]  │  │ Step 3/12  [◀◀] [◀] [▶] [▶] [▶▶]   │ │
│ └────────────────────┘  └──────────────────────────────────────┘ │
│                                                                  │
│ ┌─ ⚡ Speed ─────────────────────────────────────────────────────┐ │
│ │ Speed: [━━━━━━━●━━━━━━━━━━━] 800ms                            │ │
│ └────────────────────────────────────────────────────────────────┘ │
└──────────────────────────────────────────────────────────────────┘
```

**タブレット:**

```
┌──────────────────────────────────────────────────────────┐
│ ┌─ 🎯 ──────┐  ┌─ ▶ ──────────────────┐  ┌─ ⚡ ────────┐ │
│ │ [Algo  ▼]  │  │ 3/12 [◀◀][◀][▶][▶▶] │  │ [━━●━] 800ms│ │
│ └────────────┘  └──────────────────────┘  └─────────────┘ │
└──────────────────────────────────────────────────────────┘
```

**スマホ:**

```
┌────────────────────────────────────┐
│ ┌─ 🎯 ──────┐  ┌─ ⚡ ────────────┐ │  1行目
│ │ [Algo  ▼]  │  │ [━━●━━] 800ms  │ │
│ └────────────┘  └────────────────┘ │
│ ┌─ ▶ ────────────────────────────┐ │  2行目
│ │ 3/12  [◀◀] [◀] [▶] [▶] [▶▶]   │ │
│ └────────────────────────────────┘ │
└────────────────────────────────────┘
```

### 10.4 コンテキスト遷移

**メイン → チュートリアル:**
- Quick Access パネルの 🎯 Sort Target カード内、アルゴリズム選択横に `?` アイコン
- クリックで `/tutorial/{currentAlgorithm}` に遷移

**チュートリアル → メイン:**
- 「▶ Try this algorithm」ボタン（ヘッダー内）
- クリックでメインに戻り、チュートリアルのアルゴリズムが選択された状態で遷移

### 10.5 チュートリアルのレスポンシブ対応

| デバイス | マーブルサイズ | ツールバー | Description パネル |
|----------|-------------|----------|-------------------|
| PC | 56px（現状維持） | カード横並び（2行） | 常時表示 |
| タブレット | 48px | カード横並び（1行、縮小） | 常時表示 |
| スマホ | 36px | カード2行 wrap | 折りたたみ可能 |

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

### 11.2 After（改訂案 — UI Card ベース）

```
Index.razor
├── Header（タイトル + Tutorial + Settings ⚙）
├── Sidebar / Overlay（デバイス別）
│   └── QuickAccessPanel
│       ├── 🎯 SortTargetCard    ← Algorithm, Array Size, Scramble Pattern
│       ├── 🎨 DisplayCard      ← Visualization Mode
│       ├── ⚡ SpeedCard        ← Ops/Frame, Speed Multiplier
│       └── 🚀 ActionCard       ← Add & Generate, Count, Complexity
├── PlayControlBar                  ← ▶ Playback Card（Layer 1）
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

TutorialPage.razor
├── Header（← Back + Tutorialタイトル）
│   ├── [☰] ハンバーガーメニュー（モバイル/タブレットのみ表示）
│   ├── ← Back リンク（常時表示、z-index で前面配置）
│   └── "Sort Visualization Tutorial" タイトル（中央配置）
├── QuickAccessPanel（PC: 常時表示、モバイル: オーバーレイ）
│   ├── 🎯 Sort Target Card    ← Algorithm select のみ
│   └── ⚡ Speed Card          ← Interval slider (ms)
├── TutorialPlayControlBar（メインエリア上部）
│   ├── Step Counter（"N / M" または "Initial"）
│   ├── ▶ Playback buttons    ← ◀◀ ◀ ▶/⏸ ▶ ▶▶
│   └── Keyboard Hint（右端、グレー表示）
├── DescriptionPanel（アルゴリズム説明、グラデーション背景）
├── NarrativePanel（ステップ説明）
├── MarbleArea（MarbleRenderer × N）
└── KeyboardHint（モバイルは非表示）
```

**UI Card の対応関係:**

| UI Card | Index.razor での配置 | TutorialPage.razor での配置 |
|---------|---------------------|---------------------------|
| 🎯 Sort Target | QuickAccessPanel 内（縦積み） | TutorialToolbar 内（横並び） |
| 🎨 Display | QuickAccessPanel 内（縦積み） | —（不要） |
| ⚡ Speed | QuickAccessPanel 内（縦積み） | TutorialToolbar 内（横並び） |
| ▶ Playback | PlayControlBar（Layer 1） | TutorialToolbar 内（横並び） |
| 🚀 Action | QuickAccessPanel 内（縦積み） | —（不要） |

### 11.3 新規 / リネーム / 廃止コンポーネント

| コンポーネント | 状態 | UI Card | 説明 |
|--------------|------|---------|------|
| `SortCard.razor` | **新規** | Sort Card | CardHeader + Visualization + SortStatsSummary の統一カード |
| `SortCardGrid.razor` | **新規** | Sort Card | 比較モード時のグリッドコンテナ（既存 ComparisonGrid ベース） |
| `SortStatsSummary.razor` | **リネーム** | Sort Card | `ComparisonStatsSummary` → `SortStatsSummary`（タップ展開対応追加） |
| `PlayControlBar.razor` | **新規** | ▶ Playback | ▶/⏸, ⏹, 🔊/🔇 の3ボタン + ステータス表示 |
| `QuickAccessPanel.razor` | **新規** | — | サイドバーの Layer 2 コンテナ（UI Card を縦積み） |
| `SettingsModal.razor` | **新規** | — | Layer 3 の設定モーダル |
| `StatisticsPanel.razor` | **廃止** | — | Sort Card 内の SortStatsSummary に統合 |
| `NormalModeControls.razor` | **廃止** | — | QuickAccessPanel + PlayControlBar + SettingsModal に分散 |
| `ComparisonModeControls.razor` | **廃止** | — | QuickAccessPanel に統合 |
| `ModeControlPanel.razor` | **廃止** | — | PlayControlBar + SeekBar に分散 |
| `ComparisonGrid.razor` | **廃止** | — | SortCardGrid に置き換え |
| `ComparisonGridItem.razor` | **廃止** | — | SortCard に置き換え |
| `ComparisonStatsSummary.razor` | **廃止** | — | SortStatsSummary にリネーム |

> † UI Card は CSS クラス（`.ui-card`, `.ui-card__title`）とマークアップパターンで実現する。
> 　各コンポーネントは独立して実装され、カード単位の共通 Blazor コンポーネントは作らない（§8.3 参照）。

---

## 12. 実装優先度

| 優先度 | 変更 | 影響範囲 |
|--------|------|---------|
| **P1** | Sort Card パターン導入（単一/比較統一） | 新 `SortCard`, `SortCardGrid`, `SortStatsSummary` |
| **P1** | 再生コントロールを可視化の上に移動 | 新 `PlayControlBar`, `Index.razor`, CSS |
| **P1** | サイドバーから Statistics 削除、Quick Access 化 | 新 `QuickAccessPanel`, `Index.razor` |
| **P2** | QuickAccessPanel の UI Card グルーピング導入 | `QuickAccessPanel.razor`, `app.css` |
| **P2** | Settings Modal 導入 | 新 `SettingsModal`, `Index.razor` |
| **P2** | レスポンシブ3段階（PC/タブレット/スマホ） | `app.css` の media queries |
| **P2** | Stats Summary タップ展開 | `SortStatsSummary` |
| **P2** | チュートリアルツールバーの UI Card 化 | `TutorialPage.razor`, `TutorialPage.razor.css` |
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

### 14.1 タイポグラフィ

#### フォントファミリー

モダンでスタイリッシュ、かつ読みやすさを兼ね備えたフォントスタックを採用。

**フォント優先順位:**

```css
font-family: 'Inter', -apple-system, BlinkMacSystemFont, 'Segoe UI', 'Noto Sans JP', 'Hiragino Sans', 'Yu Gothic UI', sans-serif;
```

| フォント | 用途 | 説明 |
|---------|------|------|
| `Inter` | 欧文（主要） | モダンなサンセリフ体、テック系で人気。Google Fonts から読み込み |
| `-apple-system` / `BlinkMacSystemFont` | システムフォント（macOS/iOS） | macOS San Francisco、iOS のネイティブフォント |
| `Segoe UI` | システムフォント（Windows） | Windows のネイティブフォント |
| `Noto Sans JP` | 日本語（主要） | Google が開発した読みやすい日本語フォント |
| `Hiragino Sans` | 日本語（macOS） | macOS のネイティブ日本語フォント（ヒラギノ角ゴ） |
| `Yu Gothic UI` | 日本語（Windows） | Windows のネイティブ日本語フォント |
| `sans-serif` | フォールバック | 最終フォールバック |

**フォントレンダリング最適化:**

```css
-webkit-font-smoothing: antialiased;
-moz-osx-font-smoothing: grayscale;
```

これにより、macOS/iOS で滑らかなフォントレンダリングを実現。

**Google Fonts 読み込み:**

```css
@import url('https://fonts.googleapis.com/css2?family=Inter:wght@300;400;500;600;700&display=swap');
```

**利点:**
- ✅ モダンでスタイリッシュな外観
- ✅ 高い可読性（英語・日本語とも）
- ✅ 各 OS のネイティブフォントで高速レンダリング
- ✅ テック系アプリケーションとの親和性

### 14.2 テーマカラーシステム

#### 基本カラーパレット

アプリケーション全体で使用する統一されたカラーシステム。

**背景色:**

| カラー名 | 値 | 用途 |
|---------|-----|------|
| `--bg-main` | `#1a1f27` | メイン背景（ページ全体、ヘッダー） |
| `--bg-card` | `#272f3b` | カード背景（Sort Card、UI Card） |
| `--bg-panel` | `#1e2530` | パネル背景（QuickAccessPanel） |
| `--bg-hover` | `#323c4d` | ホバー時の背景（カード、ボタン） |

**テキスト色:**

| カラー名 | 値 | 用途 |
|---------|-----|------|
| `--text-primary` | `#ffffff` | メインテキスト |
| `--text-secondary` | `#b8c1d0` | サブテキスト、説明 |
| `--text-disabled` | `#6b7280` | 無効状態のテキスト |
| `--text-accent` | `#7fa86f` | アクセント（リンク、強調） |

**アクセントカラー:**

| カラー名 | 値 | 用途 |
|---------|-----|------|
| `--color-accent` | `#7fa86f` | モスグリーン - メインアクセント（リンク、選択状態、アクティブ要素、ホバー時のボタン） |
| `--color-accent-hover` | `#91bd7e` | アクセントのホバー状態 |
| `--color-accent-soft` | `rgba(127, 168, 111, 0.15)` | アクセントの背景（薄い半透明） |
| `--color-accent-dark` | `#5a7a5a` | アクセントの暗いバージョン（ボタンの通常状態） |
| `--color-accent-darker` | `#6d9460` | アクセントの濃いバージョン（ボタンのアクティブ状態） |

**再生コントロールカラー:**

| カラー名 | 値 | 用途 |
|---------|-----|------|
| `--color-play` | `#3b82f6` | 青 - 再生ボタン（▶） |
| `--color-pause` | `#a855f7` | 紫 - 一時停止ボタン（⏸） |
| `--color-stop` | `#e53e3e` | 赤 - 停止ボタン（⏹） |
| `--color-sound-on` | `#22c55e` | 鮮やかな緑 - サウンドON（🔊） |
| `--color-sound-off` | `#6b7280` | グレー - サウンドOFF（🔇） |

**ソート可視化カラー:**

| カラー名 | 値 | 用途 |
|---------|-----|------|
| `--color-normal` | `#3b82f6` | 青 - 通常の要素 |
| `--color-read` | `#fbbf24` | アンバー - 読み取り操作 |
| `--color-write` | `#f97316` | オレンジ - 書き込み操作 |
| `--color-compare` | `#a855f7` | 紫 - 比較操作 |
| `--color-swap` | `#ef4444` | 赤 - スワップ操作 |
| `--color-sorted` | `#10b981` | グリーン - ソート済み |
| `--color-buffer` | `#6b7280` | グレー - バッファ配列 |

**ボーダー・区切り線:**

| カラー名 | 値 | 用途 |
|---------|-----|------|
| `--border-color` | `#374151` | 標準ボーダー |
| `--border-color-light` | `#4b5563` | 明るいボーダー（ホバー時） |
| `--border-color-focus` | `#7fa86f` | フォーカス時のボーダー |

**状態カラー:**

| カラー名 | 値 | 用途 |
|---------|-----|------|
| `--color-success` | `#10b981` | 成功、完了 |
| `--color-warning` | `#fbbf24` | 警告 |
| `--color-error` | `#ef4444` | エラー |
| `--color-info` | `#3b82f6` | 情報 |

**チュートリアル専用カラー:**

| カラー名 | 値 | 用途 |
|---------|-----|------|
| `--color-tutorial-bg-start` | `rgba(69, 133, 136, 0.15)` | チュートリアル説明カードの背景グラデーション開始色（緑系の薄い透明色） |
| `--color-tutorial-bg-end` | `rgba(104, 157, 106, 0.15)` | チュートリアル説明カードの背景グラデーション終了色（緑系の薄い透明色） |
| `--color-tutorial-accent` | `#8ec07c` | チュートリアル説明カードのアクセント・タイトル（明るい緑） |
| `--color-tutorial-text` | `#bdae93` | チュートリアル説明カードのテキスト（ベージュ・タン系） |

#### CSS カスタムプロパティ定義

```css
:root {
    /* 背景色 */
    --bg-main: #181d1a;
    --bg-card: #242a27;
    --bg-panel: #1c2320;
    --bg-hover: #2d3530;

    /* テキスト色 */
    --text-primary: #ffffff;
    --text-secondary: #b8c1d0;
    --text-disabled: #6b7280;
    --text-accent: #7fa86f;

    /* アクセントカラー */
    --color-accent: #7fa86f;
    --color-accent-hover: #91bd7e;
    --color-accent-soft: rgba(127, 168, 111, 0.15);
    --color-accent-dark: #5a7a5a;
    --color-accent-darker: #6d9460;
    --color-button-gradient-start: #5a9a4d;
    --color-button-gradient-end: #3d7a38;
    --color-button-hover-gradient-start: #6bb05d;
    --color-button-hover-gradient-end: #4a8c43;

    /* 再生コントロール */
    --color-play: #3b82f6;
    --color-pause: #a855f7;
    --color-stop: #e53e3e;
    --color-sound-on: #22c55e;
    --color-sound-off: #6b7280;

    /* ソート可視化 */
    --color-normal: #3b82f6;
    --color-read: #fbbf24;
    --color-write: #f97316;
    --color-compare: #a855f7;
    --color-swap: #ef4444;
    --color-sorted: #10b981;
    --color-buffer: #6b7280;

    /* ボーダー */
    --border-color: #374151;
    --border-color-light: #4b5563;
    --border-color-focus: #7fa86f;

    /* 状態 */
    --color-success: #10b981;
    --color-warning: #fbbf24;
    --color-error: #ef4444;
    --color-info: #3b82f6;

    /* チュートリアル専用 */
    --color-tutorial-bg-start: rgba(69, 133, 136, 0.15);
    --color-tutorial-bg-end: rgba(104, 157, 106, 0.15);
    --color-tutorial-accent: #8ec07c;
    --color-tutorial-text: #bdae93;
}
```

#### 廃止される旧カラー変数

以下の変数は新しいカラーシステムに統合されます：

| 廃止 | 移行先 |
|------|--------|
| `--bg-dark` | `--bg-main` |
| `--text-light` | `--text-primary` |
| `--text-gray` | `--text-secondary` |

### 14.3 ブレークポイント

```css
/* スマホ: デフォルト (〜767px) */

/* タブレット */
@media (min-width: 768px) { ... }

/* PC */
@media (min-width: 1280px) { ... }
```

### 14.4 Grid レイアウト

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

**Document Version**: 1.8
**Last Updated**: 2025-06
**Changelog**:
- v1.8 - §2.2 Quick Access パネルにアルゴリズム説明カードを追加（CURRENT ALGORITHM、緑系グラデーション背景、TutorialDescription から "How it works:" を抽出表示）、§3.2・§3.3 再生コントロールボタンをSVGアイコンに変更（Play/Pause/Stop/Sound、28×28px、currentColor 使用）、§7.2 Quick Access パネル構成図にアルゴリズム説明カードを反映
- v1.7 - §7.1 Quick Access パネル開閉方法を変更（PC: ヘッダーの ☰ ボタン / パネル内タイトル行の ☰ ボタン）、§7.2 カード視覚仕様更新（パネル背景を transparent に、UI Card 背景を #2a2a2a に変更し緑要素を削除してメリハリを向上）、§2.1 ヘッダーボタン仕様にハンバーガーメニューボタン（☰）を追加（PC/モバイル両対応、デバイスごとに異なる動作）、§11.2 TutorialPage.razor 構造更新（ハンバーガーメニューはモバイルのみ表示、Back リンクを z-index で前面配置して視認性向上）
- v1.6 - §14.1 タイポグラフィセクション新設（Inter + システムフォントスタック採用、Google Fonts 読み込み、フォントレンダリング最適化）、§10.2 ヘッダータイトルスタイル統一詳細追記（font-size: 2.25rem, font-weight: 400）、セクション番号繰り下げ（§14.1→§14.2 テーマカラーシステム、§14.2→§14.3 ブレークポイント、§14.3→§14.4 Grid レイアウト）
- v1.5 - §2.1 ヘッダーボタンをアイコンのみに変更（🎓 Tutorial、⚙ Settings）、デバイス別サイズ仕様追加（PC: 40×40px、タブレット: 36×36px、スマホ: 32×32px）、統一スタイル `.header__icon-btn` 適用
**Document Version**: 1.9
**Last Updated**: 2025-06
**Changelog**:
- v1.9 - UI改善: 背景色を青味から緑・黒寄りに調整（#181d1a, #242a27, #1c2320, #2d3530）、ヘッダーのカード背景削除（background/border-radius削除、padding調整）、タイトルを中央寄せ・1.75remに拡大（絶対配置で中央表示）、ボタンの緑色を濃いグラデーション（#5a9a4d→#3d7a38、hover: #6bb05d→#4a8c43）に変更、Tutorialの「Try this」ボタンを説明パネル内のアルゴリズム名横に移動、Tutorialのアルゴリズム選択をメイン画面と統一（カテゴリ並び順を出現順序に変更）
- v1.8 - §2.2 Quick Access パネルにアルゴリズム説明カードを追加（CURRENT ALGORITHM、緑統計グラデーション背景、TutorialDescription から "How it works:" を抜粋表示）、§3.2・§3.3 再生コントロールボタンをSVGアイコンに変更（Play/Pause/Stop/Sound、28×28px、currentColor 使用）、§7.2 Quick Access パネル構成図にアルゴリズム説明カードを反映
- v1.7 - §7.1 Quick Access パネル開閉方法を変更（PC: ヘッダーの ☰ ボタン / パネル内タイトル行の ☰ ボタン）、§7.2 カード背景仕様変更（パネル背景を transparent に、UI Card 背景を #2a2a2a に変更し緑統計を削除してメリハリを最小）、§2.1 ヘッダーボタン仕様にハンバーガーメニューボタン（☰）を追加（PC/モバイル両対応、デバイスごとに異なる挙動）、§11.2 TutorialPage.razor 構造更新（ハンバーガーメニューはモバイルのみ表示、← Back リンクを z-index で前面配置して視認性向上）
- v1.6 - §14.1 タイポグラフィセクション新設（Inter + システムフォントスタック採用、Google Fonts 読み込み、フォントレンダリング最適化）、§10.2 ヘッダータイトルスタイル統一詳細追記（font-size: 2.25rem, font-weight: 400）、セクション番号整理（上げ）：§14.1→§14.2 テーマカラーシステム、§14.2→§14.3 ブレークポイント、§14.3→§14.4 Grid レイアウト）
- v1.5 - §2.1 ヘッダーボタンをアイコンのみに変更（🎓 Tutorial、⚙ Settings）、デバイス別サイズ仕様追加（PC: 40×40px、タブレット: 36×36px、スマホ: 32×32px）、統一スタイル `.header__icon-btn` 適用
- v1.4 - チュートリアル説明カードに緑系の落ち着いた配色を適用（薄い緑のグラデーション背景、アルゴリズム名を明るい緑 #8ec07c、本文をベージュ #bdae93）、チュートリアル専用カラー変数追加
- v1.3 - §14.2 テーマカラーシステム全面改訂（メインカラー #1a1f27、カード背景 #272f3b、アクセントカラー モスグリーン #7fa86f、再生コントロールボタン個別カラー定義: Play青/Pause紫/Stop赤/Sound鮮やかな緑#22c55e・灰）、§3.3・§10.1 カラー参照更新
- v1.2 - §1.4 UI Card コンセプト追加、§2.2 カードグルーピング、§7.2 カードベースレイアウト、§8 ページ横断要素マッピング追加、§10 チュートリアル UI Card 化、§11-12 更新
- v1.1 - §13 状態保持（Query String + localStorage ハイブリッド）追加
**Related Documents**: `VisualizationWeb.md`, `VisualizationWeb_tutorial.md`
