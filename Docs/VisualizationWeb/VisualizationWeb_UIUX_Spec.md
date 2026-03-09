# SortViz - UI/UX 仕様書

## 1. 設計思想

### 1.1 テーマ

- 直感的に操作できる
- ソートの状況や統計情報が一目で分かる
- 普段は不要だが時に設定したい操作は設定画面で設定できる
- デバイスごとに適切なレイアウトがある
- チュートリアルのUI/UXも統一感を持たせる

### 1.2 「見る → 操作する → 設定する」3層モデル

| Layer | 名称 | 概要 | アクセス方法 |
|-------|------|------|-------------|
| Layer 1 | Always Visible | 常に画面に見えている要素 | 常時表示 |
| Layer 2 | Quick Access | 常時表示の操作パネル | 左サイドバー（PC）/ 上段（タブレット・スマホ） |
| Layer 3 | Settings | 初回設定したらしばらく変えない設定 | Settings モーダル |

### 1.3 「Sort Card」統一パターン

Comparison Mode トグルは廃止。Sort Card の枚数 N がすべてを決定する（N=1: 単一表示、N>1: 比較表示）。

```
┌─ Sort Card ──────────────────────────┐
│ [Algorithm Name]  [O(n log n)]  [✖]  │  ← カードヘッダー
├──────────────────────────────────────┤
│      Visualization (Bar/Circle/...)  │  ← 可視化
├──────────────────────────────────────┤
│ ⏱ 1.23ms  Cmp: 141K  Swp: 6.8K  45%│  ← Stats Summary
└──────────────────────────────────────┘
```

**N=1 と N>1 の挙動の違い:**

| 機能 | N=1 | N>1 |
|------|-----|-----|
| シークバー | 表示 | 表示 |
| サウンド | 有効 | 無効 |
| Sort Card の ✖ ボタン | 非表示 | 表示 |
| ステータステキスト | 完了時のみ表示 | "X/N completed" 表示 |

**➕ Add & Generate（Quick Access の主要ボタン）の Upsert 動作:**

| 条件 | 動作 |
|------|------|
| 選択 Algo が未登録 かつ N < N_max かつ配列条件（サイズ・パターン）が前回と同じ | 追加のみ (N→N+1)、既存カード再実行なし・追加分だけ計測 |
| 選択 Algo が未登録 かつ N < N_max かつ配列条件が変わった（またはカードが 0 枚） | 追加 (N→N+1)、全カード新配列で再実行 |
| 選択 Algo がすでに存在する | 配列再生成のみ (N 不変)、全カード再実行 |
| N が上限かつ選択 Algo が未登録 | disabled |

### 1.4 「UI Card」によるコンポーネントのグルーピング

UI 要素をユーザーの意図でグルーピングし、各グループを **UI Card** として扱う。メインとチュートリアルで同じカード概念（タイトル・アイコン・背景色）を共有することでデザイン上の統一感を保つ。実装はページごとに独立し、CSS クラス（`.ui-card`, `.ui-card__title`）で統一する。

| UI Card | 関心 | メイン画面 | チュートリアル |
|---------|------|-----------|---------------|
| 🎯 Sort Setup | 何をどう実行するか | Algorithm（+ ? リンク + Complexity）, Array Size, Scramble Pattern, Add & Generate, Count | ❌ |
| 🎨 Appearance | どう見せるか | Visualization Mode | ❌ |
| ⚡ Speed | どの速さで再生するか | Ops Per Frame, Speed | Interval (ms) |
| ▶ Playback | 再生をどう操作するか | Play/Pause, Stop, Sound, Status | Play/Pause, Step Nav |

### 1.5 モーダル共通パターン

すべてのモーダル・オーバーレイダイアログはスマートフォン UI 慣習に統一する。

**ヘッダー構造:**

```
[✕]  [タイトル                    ]  [アクション群（任意）]
```

| 要素 | 配置 | スタイル |
|------|------|----------|
| 閉じるボタン（✕） | 左端 | 背景なし（フラット）、ホバー時文字色 `#EF4444` |
| タイトル | 閉じるボタンの右、`flex: 1` | 通常テキスト |
| アクション群 | 右端（任意） | コンポーネント固有 |

**対象コンポーネント:**

| コンポーネント | アクション群 |
|----------------|--------------|
| `ComparisonStatsTable` | Sort by セレクト、Copy ドロップダウン |
| `SettingsModal` | なし |

### 1.6 ブラウザ完結型アーキテクチャ  ← 新規サブセクション

本アプリケーションは**完全にブラウザ内で動作するサーバーレス設計**です。

- **初回ロード**: WebAssembly（.NET runtime）、CSS、JavaScript を取得
- **実行環境**: Blazor WebAssembly により、すべてのソート処理・統計計算・可視化をブラウザ内で実行
- **サーバー通信**: 一切なし（初回アセット取得後はオフラインでも動作可能）
- **Picture Sort**: 画像もブラウザのみで処理（サーバーへのアップロードなし）

ユーザーのプライバシーを保護し、低レイテンシーで高速な操作体験を提供します。

---

## 2. Layer 分類

### 2.1 Layer 1: Always Visible（常時表示）

| 要素 | 説明 |
|------|------|
| ヘッダー | アプリ名（中央）、?（Tutorial）、⚙（Settings）（SVG アイコン） |
| 再生コントロール | ▶/⏸, ⏹, 🗑, 🔊/🔇 + n=N バッジ + ステータステキスト（可視化エリアの上） |
| Sort Card | 可視化 + Stats Summary |
| シークバー | 常時表示（N=0 の空状態でも表示） |
| ステータス | N=1 時: 完了時のみ "✅ All Completed!"、N>1 時: "X/N completed"（再生コントロール横） |

**ヘッダーボタン仕様:**

| ボタン | 動作（PC） | 動作（タブレット/スマホ） |
|--------|-----------|----------------------|
| ?（SVG: 丸の中に ?） | `/tutorial/{currentAlgorithm}` に遷移 | 同左 |
| ⚙（SVG: 歯車） | Settings モーダルを開く | 同左 |

### 2.2 Layer 2: Quick Access（操作パネル）

UI Card の論理グループでカード化して縦積み表示する。

| UI Card | 要素 |
|---------|------|
| アルゴリズム説明カード | 選択中アルゴリズムの名前・説明（緑系グラデーション背景） |
| 🎯 Sort Setup | Algorithm（ドロップダウン + ? リンク + Complexity）、Array Size、Scramble Pattern、Add & Generate（Upsert）、Count |
| 🎨 Appearance | Visualization Mode |
| ⚡ Speed | Ops Per Frame（スライダー）、Speed（スライダー + FPS） |

### 2.3 Layer 3: Settings（設定モーダル）

| 要素 | デフォルト |
|------|-----------|
| **🎨 Rendering** | |
| Renderer（Canvas 2D / WebGL） | WebGL |
| **▶ Playback** | |
| Auto Reset on Complete | OFF |
| Instant Mode | OFF |
| **🔊 Sound** | |
| Sound Volume（0〜100%） | 50% |
| **🔧 Advanced** | |
| Scroll to Sort Panel after Add & Generate | ON |
| Auto Switch to Recommended Size | OFF |
| Category Filter | All |
| Debug Log | OFF |

---

## 3. 再生コントロール

### 3.1 配置

可視化エリア（Sort Card）の **上** に固定配置する。

### 3.2 ボタン構成

テキストなし、SVGアイコン（28×28px）の4ボタン横並び。

| デバイス | ボタンサイズ (height × max-width) |
|---------|----------|
| PC | 60px × 180px |
| タブレット | 40px × 100px |
| スマホ | 40px × 100px |

### 3.3 ボタンスタイル

全ボタンで **半透明背景 + ボーダー + テキスト色** のパターンを採用。ホバー時は `transform: scale()` を使わず、背景色とボーダー色のみ変化。

| ボタン | 状態 | アイコン | 背景 | テキスト | ボーダー | tooltip |
|--------|------|---------|--------|--------|----------|----------|
| Play/Pause | 停止/一時停止中 | ▶ | `rgba(59,130,246,0.2)` | `#60a5fa` | `rgba(59,130,246,0.5)` | "Play" |
| Play/Pause | 再生中 | ⏸ | `rgba(168,85,247,0.2)` | `#c084fc` | `rgba(168,85,247,0.5)` | "Pause" |
| Stop | 常時 | ⏹ | `rgba(239,68,68,0.2)` | `#f87171` | `rgba(239,68,68,0.5)` | "Stop" |
| Clear All | 常時 | 🗑 | `rgba(249,115,22,0.2)` | `#fb923c` | `rgba(249,115,22,0.5)` | "Clear All" |
| Sound | ON | 🔊 | `rgba(250,189,47,0.2)` | `var(--color-sound-on)` = `#fabd2f` | `var(--color-sound-border)` = `rgba(250,189,47,0.5)` | "Sound ON" |
| Sound | OFF | 🔇 | `rgba(107,114,128,0.2)` | `#9ca3af` | `rgba(107,114,128,0.5)` | "Sound OFF" |
| Sound | N>1 時無効 | 🔇 | disabled (`opacity: 0.35`) | - | - | "Sound not available in Comparison Mode" |

**ホバー時**: 背景の `alpha` を `0.35` に增加、ボーダーを不透明に
**アクティブ時**: 背景の `alpha` を `0.5` に增加、ボーダーを少し濃く

### 3.4 ボタン無効化条件

| ソートカード枚数 N | Play/Pause | Stop | Clear All | Sound |
|---|---|---|---|---|
| **N = 0** | **無効** 🚫 | **無効** 🚫 | **無効** 🚫 | 有効 |
| **N = 1** | 有効 | 有効 | 有効 | 有効 |
| **N > 1** | 有効 | 有効 | 有効 | 無効 |

※ N=0 時：「何もソートが追加されていない状態でPlay/Stop/Clear Allボタンを押せる」違和感を防止。

### 3.5 補助操作

- 可視化エリアクリック: 再生/一時停止トグル
- N≥1 時: 再生コントロール横にステータス表示（完了時 `"✅ All Completed!"`、それ以外 `"X/N completed"`）
- 再生コントロール横に `n=N`（配列サイズ）バッジを表示（配列が存在する場合）

---

## 4. Sort Card

### 4.1 構造

```
┌─ SortCard ────────────────────────────────────┐
│ CardHeader                                     │
│   [Algorithm Name ▾]  [Complexity Badge]  [✖] │
├───────────────────────────────────────────────┤
│ Visualization（クリックで再生/一時停止）        │
├───────────────────────────────────────────────┤
│ SortStatsSummary                              │
│   ⏱ time  Cmp: N  Swp: N  Progress: N%       │
│   （タブレット/スマホ: タップで展開）           │
└───────────────────────────────────────────────┘
```

### 4.2 カードヘッダー

- **アルゴリズム名（▾付き）**: クリックでインラインドロップダウン展開 → 同配列でそのカードのみアルゴリズム差し替え（配列再生成なし、他カードに影響なし）
- **Complexity Badge**: `O(n log n)` 形式（モノスペース）
- **✖ 削除ボタン**: N>1 時のみ表示

### 4.3 Stats Summary 表示項目

| カードサイズ | 表示項目 |
|-------------|---------|
| フルサイズ（単一・PC） | ⏱ 時間, Comparisons, Swaps, Reads, Writes, Progress% |
| 中サイズ（比較2〜3・PC） | ⏱ 時間, Comparisons, Swaps, Progress% |
| 小サイズ（比較4+・タブレット・スマホ） | ⏱ 時間, Cmp, Swp, %（省略形） |

### 4.4 Stats Summary タップ展開（タブレット・スマホ）

```
折りたたみ: │ ⏱ 1.23ms  Cmp: 141K  Swp: 6.8K  45% │  [∨]

展開:       │ ⏱ Execution Time: Current 0.56ms / Total 1.23ms   │
            │ 🔢 Comparisons: 141,473  Swaps: 6,892              │
            │    Index Reads: 87,308   Index Writes: 102,157      │
            │ Progress: ████████████░░░░░░░ 45%                   │
            │ Complexity: O(n log n)                    [∧]       │
```

---

## 5. デバイス別レイアウト

### 5.1 ブレークポイント

| デバイス | 幅 | Quick Access | Sort Card 配置 |
|----------|----|-------------|----------------|
| PC | ≥1280px | 左サイドバー（常時表示） | メインエリア（右列） |
| タブレット | 768〜1279px | 上段（常時表示） | 下段フル幅 |
| スマホ | 〜767px | 上段（常時表示） | 下段フル幅 |

### 5.2 PC レイアウト（≥1280px）

```
┌─────────────────────────────────────────────────────────────┐
│ Sorting Playground                               [?] [⚙]   │
├──────────┬──────────────────────────────────────────────────┤
│ Quick    │  [▶] [⏹] [🗑] [🔊]  n=1024                        │
│ Access   │  ┌─ Sort Card ───────────────────────────────┐   │
│ Panel    │  │ QuickSort        O(n log n)               │   │
│          │  │     ▂▄▆█▇▅▃▁▃▅▇█▆▄▂                       │   │
│ [各種    │  │ ⏱1.23ms │ Cmp:141K │ Swp:6.8K │ 45%       │   │
│  UI Card]│  └──────────────────────────────────────────┘   │
│          │  ◀━━━━━●━━━━━━━━━▶ 45%  2,345/5,200 ops        │
└──────────┴──────────────────────────────────────────────────┘
```

N=0 時: Sort Card にアルゴリズム名・Complexity を表示しない（まだ Add & Generate を実行していないため）

N>1 時: Sort Card をグリッド表示（"X/N completed" 表示）

### 5.3 タブレット レイアウト（768〜1279px）

```
┌──────────────────────────────────────────────────────┐
│ Sorting Playground                        [?] [⚙]   │
├──────────────────────────────────────────────────────┤
│ [Quick Access Panel: UI Cards 縦積み]                 │
├──────────────────────────────────────────────────────┤
│          [▶]      [⏹]      [🗑]      [🔊]             │
├──────────────────────────────────────────────────────┤
│  ┌─ Sort Card ─────────────────────────────────────┐ │
│  │ QuickSort                        O(n log n)     │ │
│  │      ▂▄▆█▇▅▃▁▃▅▇█▆▄▂                            │ │
│  │ ⏱1.23ms  Cmp:141K  Swp:6.8K  45%  [∨]          │ │
│  └─────────────────────────────────────────────────┘ │
│  ◀━━━━━●━━━━━━━━━▶ 45%  2,345/5,200 ops             │
└──────────────────────────────────────────────────────┘
```

N>1 時: 2列グリッド + 右下に 📊 ボタン（Stats Table モーダル）

### 5.4 スマホ レイアウト（〜767px）

N=1: フル幅 Sort Card + シークバー

N>1: **カルーセル方式**（横スワイプで1枚ずつ、ドットインジケーター + 📊 ボタン）+ シークバー

---

## 6. シークバー

Sort Card の下に常時表示（N=0 の空状態でも表示）。

```
◀━━━━━━━━━●━━━━━━━━━━━▶ 45%  12,300 / 24,600 ops
              ↑Q  ↑M
```

ドラッグまたはクリックで任意位置にジャンプ。

### 6.1 基準値（max ops）

- シークバーのmaxは**現在表示中の全カードの中で最大の総ops数**とする
- カードの追加・削除・アルゴリズム切り替えが発生した場合はmaxを再計算する
- N=0・N=1 の場合は表示中の1カード（または0）の総opsをmaxとする

### 6.2 完了マーカー（N>1 時）

各アルゴリズムが完了した位置にシークバー上のマーカーを表示する。

| 項目 | 仕様 |
|------|------|
| マーカー形状 | シークバートラック上の小さな縦線（アルゴリズムごとに色分け） |
| ホバー表示 | アルゴリズム名と完了ops数を tooltip で表示 |
| 重なり処理 | 同位置に複数マーカーが重なる場合、先頭1つ＋`+N`バッジに集約 |
| `+N`バッジのホバー | 集約されたアルゴリズム名を全て tooltip に列挙 |

### 6.3 シーク時の挙動

| 対象カード | シーク後の表示 | シーク後に ▶ を押した場合 |
|-----------|--------------|-------------------------|
| 未完了カード | シーク位置のopsに対応するフレームを表示 | そのopsから再生開始 |
| 完了済みカード | 完了最終フレームを表示（固定） | 何もしない（完了状態を維持） |
| 全カード完了済みの位置にシーク後に ▶ | — | 何もしない（全完了状態を維持） |

---

## 7. Quick Access パネル（Layer 2）

### 7.1 デバイス別表示方式

| デバイス | 表示方式 | UI Card 折りたたみ |
|----------|----------|--------------------|
| PC | 左サイドバー（常時表示、幅 280px 固定） | 不可（常に全展開） |
| タブレット | 上段（常時表示、UI Cards 縦積み） | 可（タイトルタップでトグル） |
| スマホ | 上段（常時表示、UI Cards 縦積み） | 可（タイトルタップでトグル） |

### 7.2 カード視覚仕様

| 要素 | スタイル |
|------|---------|
| パネル背景 | `transparent` |
| アルゴリズム説明カード背景 | 緑系グラデーション（`--color-tutorial-bg-start` → `--color-tutorial-bg-end`） |
| アルゴリズム説明カード - タイトル | `font-size: 1.4rem`、`color: --color-tutorial-accent` |
| アルゴリズム説明カード - 本文 | `font-size: 0.9rem`、`color: --color-tutorial-text` |
| UI Card 背景 | `#2a2a2a` |
| UI Card 見出し | アイコン + ラベル、`font-size: 0.68rem`、uppercase、下ボーダー |
| 角丸 | UI Card: 6px、アルゴリズム説明カード: 8px |
| カード間マージン | 10px |

### 7.3 操作の対比

**配列を再生成するかどうか** が最大の違い。

| 操作 | 配列 | N の変化 | 場所 |
|------|------|--------|------|
| ➕ Add & Generate | 新規生成（全カード） | Upsert（§1.3 参照） | Quick Access |
| [Algo ▾] インライン切替 | 流用（そのカードのみ再実行） | N 不変 | カードヘッダー |

### 7.4 UI Card 折りたたみ（タブレット・スマホのみ）

タブレット・スマホでは上段に常時表示されるため、カードを折りたたんで可視化エリアを広く確保できる。

**デフォルト状態:**

| UI Card | デフォルト |
|---------|-----------|
| 🎯 Sort Setup | 展開 |
| 🎨 Appearance | 折りたたみ |
| ⚡ Speed | 折りたたみ |

**トグルの挙動:**

| 状態 | タイトル行 | アイコン |
|------|-----------|---------|
| 展開中 | `border-bottom` あり | シェブロン ∨（`var(--text-secondary)`） |
| 折りたたみ中 | `border-bottom` なし（コンパクトなタブ風） | シェブロン ›（`var(--color-accent)`、常時アクセントカラーで「開けること」をアピール） |
| ホバー時 | テキスト色が `var(--text-primary)` に変化 | アクセントカラーに変化 |
| タップ時（`:active`） | 背景フラッシュ `rgba(255,255,255,0.06)` | — |

---

## 8. ページ横断の要素マッピング

UI Card は CSS クラスによるデザイン統一を目的とし、コンポーネント共有は行わない（パラメータの型・範囲・意味が異なるため、無理に共通化すると条件分岐だらけになる）。

| 要素 | メイン画面 | チュートリアル | UI Card | 共通化 |
|------|-----------|--------------|---------|--------|
| アルゴリズム選択 | QAP: select + ? リンク | toolbar: select | 🎯 Sort Setup | △ UI 同形、バインド先が異なる |
| Array Size / Pattern | QAP: select | ❌ | 🎯 Sort Setup | ✕ メイン専用 |
| Visualization Mode | QAP: select | ❌ | 🎨 Appearance | ✕ メイン専用 |
| Ops/Frame + Speed | QAP: slider × 2 | ❌ | ⚡ Speed | ✕ メイン専用 |
| Interval (ms) | ❌ | cards panel: slider | ⚡ Speed | ✕ Tutorial 専用 |
| Play/Pause | PlayControlBar | playback bar | ▶ Playback | ✅ 概念共通 |
| Stop / Clear All / Sound | PlayControlBar | ❌ | ▶ Playback | ✕ メイン専用 |
| Step 操作 | ❌ | playback bar | ▶ Playback | ✕ Tutorial 専用 |
| Step Counter | ❌ | narrative panel | — | ✕ Tutorial 専用 |
| Add & Generate | QAP | ❌ | 🎯 Sort Setup | ✕ メイン専用 |
| Complexity | QAP Sort Setup 行内 / SortCard badge | 説明パネル badge | 🎯 Sort Setup | △ 情報同一、表示形式が異なる |
| 可視化 | Canvas Renderers | MarbleRenderer (HTML) | Sort Card | ✕ 完全に別 |
| 統計サマリー | SortStatsSummary | ❌ | Sort Card | ✕ メイン専用 |

---

## 9. 比較モード

N>1 で暗黙的に比較表示。Comparison Mode トグルは存在しない。

### グリッドレイアウト

| N | PC | タブレット | スマホ |
|---|-----|----------|--------|
| 1 | 1列 | 1列 | 1枚 |
| 2 | 2列 | 2列 | カルーセル |
| 3 | 2列 | 2列 | カルーセル |
| 4 | 2×2 | 2×2 | カルーセル |
| 5〜6 | 3×2 | 2×3 | カルーセル |

### ComparisonStatsTable

| デバイス | 表示方式 |
|----------|---------|
| 全デバイス | 📊 Stats ボタン → オーバーレイモーダル |

**ヘッダー:** モーダル共通パターン（1.5）に準拠。

**コピー機能（Copy ドロップダウン）:**

| フォーマット | 内容 |
|-------------|------|
| TSV (Excel) | タブ区切り、Rank 列含む、Excel 貼り付け向け |
| JSON | インデント付き、`rank` フィールド含む、NativeAOT 対応 Source Generator 使用 |
| Markdown | GFM テーブル形式、Rank 列含む |

コピー順はその時点の Sort by 設定に従う。

---

## 10. チュートリアル UI 統一

### 10.1 カラースキーム統一

チュートリアルのハードコードカラーをメインの CSS 変数に置き換える。

| 項目 | 統一後 |
|------|--------|
| 背景 | `var(--bg-main)` |
| パネル | `var(--bg-panel)` |
| カード | `var(--bg-card)` |
| ボーダー | `var(--border-color)` |
| アクセント | `var(--color-accent)` |
| ボタンプライマリ | `var(--color-play)` |
| ボタンセカンダリ | `var(--bg-hover)` |

### 10.2 ヘッダー統一

メインと同じスタイルのヘッダーバーを使用。タイトルは `font-size: 2.25rem / font-weight: 400` で絶対配置・中央表示。

```
│ ← Back            Tutorial                              │
```

「← Back」は SVG アイコン付きのボタンで、ホバー時に `#EF4444` に変色する。ヘッダー右側にはアクション群なし。

### 10.3 ツールバーの UI Card 化

現在の1本バーを UI Card の論理グループに分割する。

| UI Card | 要素 |
|---------|------|
| 🎯 Sort Target | Algorithm select |
| ⚡ Speed | Speed slider（200〜2000ms）+ 値表示 |

**再生コントロールバー:** Sort Target・Speed カードの下に配置。

| 要素 |
|------|
| ◀◀ ◀ ▶/⏸ ▶ ▶▶（ナビゲーションボタン）、Keyboard Hint |

**Playback ボタンスタイル**: メインと同じ半透明背景 + ボーダーパターン。ナビボタン（◀◀ ◀ ▶ ▶▶）はモスグリーン系、Play/Pause は青系。

**ボタンサイズ（Tutorial）:**

| デバイス | ナビボタン (height × min-width) | Play/Pause (height × min-width) |
|---------|----------|----------|
| PC | 60px × 70px | 60px × 80px |
| タブレット | 40px × 50px | 40px × 100px |
| スマホ | 40px × 50px | 40px × 100px |

**デバイス別配置:**

| デバイス | レイアウト |
|---------|----------|
| PC | 3カード横並び（flex） |
| タブレット | 3カード横並び（縮小） |
| スマホ | Sort Target + Speed を1行目、Playback を2行目 |

### 10.4 コンテキスト遷移

- **メイン → チュートリアル**: QAP の Algorithm 選択横 `?` リンク → `/tutorial/{algo}`、またはヘッダーの `?` アイコン → `/tutorial/{currentAlgorithm}`
- **チュートリアル → メイン**: 説明パネルの "▶ Try this" リンク → メイン画面でそのアルゴリズムを選択済み（`?algo=...`）で遷移

### 10.5 レスポンシブ

| デバイス | マーブルサイズ | Description パネル |
|---------|-------------|-------------------|
| PC | 56px | 常時表示 |
| タブレット | 48px | 常時表示 |
| スマホ | 36px | 折りたたみ可能 |

---

## 11. コンポーネント構造

### 11.1 構造（After）

```
Index.razor
├── Header（タイトル "Sorting Playground" + ? + ⚙）
├── QuickAccessPanel（PC: 左サイドバー / タブレット・スマホ: 上段、いずれも常時表示）
│   ├── AlgorithmDescriptionCard
│   ├── 🎯 SortSetupCard（Algorithm + Array Size + Scramble Pattern + Add & Generate + Count）
│   ├── 🎨 AppearanceCard（Visualization Mode）
│   └── ⚡ SpeedCard（Ops Per Frame + Speed）
├── PlayControlBar（▶/⏸, ⏹, 🗑, 🔊/🔇, n=N バッジ, ステータス）
├── SortCardArea
│   └── SortCard × N（N=1: フルサイズ、N>1: グリッド）
│       ├── CardHeader（Algorithm ▾, Complexity Badge, ✖）
│       ├── Visualization（既存 Renderer）
│       └── SortStatsSummary（タップ展開対応）
├── SeekBar（N≤1 時のみ）
├── ComparisonStatsTable（N>1 時のみ）
└── SettingsModal（⚙ クリック時）

TutorialPage.razor
├── Header（← Back + タイトル "Tutorial"）
├── TutorialCardsPanel（PC: 左サイドバー / タブレット+スマホ: 上段）
│   ├── 🎯 Sort Target（Algorithm select）
│   └── ⚡ Speed（Interval slider）
├── TutorialMainArea
│   ├── TutorialPlayControlBar（◀◀ ◀ ▶/⏸ ▶ ▶▶ + Keyboard Hint）
│   ├── DescriptionPanel（アルゴリズム名 + ▶ Try this + badges + 説明テキスト）
│   ├── NarrativePanel（Step Counter "N / M" + ナラティブテキスト）
│   └── MarbleArea（MarbleRenderer × N）
```

### 11.2 コンポーネント一覧

| コンポーネント | 状態 | 説明 |
|--------------|------|------|
| `SortCard.razor` | ✅ 実装済 | CardHeader + Visualization + SortStatsSummary |
| `SortCardGrid.razor` | ✅ 実装済 | 比較モードのグリッドコンテナ + カルーセルドット |
| `SortStatsSummary.razor` | ✅ 実装済 | カードサイズ別統計表示（Full/Medium/Small） |
| `PlayControlBar.razor` | ✅ 実装済 | ▶/⏸, ⏹, 🗑, 🔊/🔇 + n=N バッジ + ステータス |
| `QuickAccessPanel.razor` | ✅ 実装済 | Layer 2 コンテナ（UI Card 縦積み） |
| `SettingsModal.razor` | ✅ 実装済 | Layer 3 設定モーダル（Rendering/Playback/Sound/Advanced） |
| `SeekBar.razor` | ✅ 実装済 | ドラッグ対応シークバー（JS Interop） |
| `ComparisonStatsTable.razor` | ✅ 実装済 | オーバーレイモーダル、Sort by + Copy 機能 |
| `BarChartRenderer.razor` | ✅ 実装済 | Canvas/WebGL バーチャート描画 |
| `CircularRenderer.razor` | ✅ 実装済 | 円形描画 |
| `SpiralRenderer.razor` | ✅ 実装済 | スパイラル描画 |
| `DotPlotRenderer.razor` | ✅ 実装済 | ドットプロット描画 |
| `PictureRowRenderer.razor` | ✅ 実装済 | 画像行描画 |
| `PictureColumnRenderer.razor` | ✅ 実装済 | 画像列描画 |
| `PictureBlockRenderer.razor` | ✅ 実装済 | 画像ブロック描画 |
| `MarbleRenderer.razor` | ✅ 実装済 | チュートリアル用マーブル描画 |

---

## 12. 実装優先度

全項目が実装済み。

| 優先度 | 変更 | 影響範囲 | 状態 |
|--------|------|---------|------|
| **P1** | Sort Card パターン導入（単一/比較統一） | `SortCard`, `SortCardGrid`, `SortStatsSummary` | ✅ |
| **P1** | 再生コントロールを可視化の上に移動 | `PlayControlBar`, `Index.razor`, CSS | ✅ |
| **P1** | サイドバーから Statistics 削除、Quick Access 化 | `QuickAccessPanel`, `Index.razor` | ✅ |
| **P2** | QuickAccessPanel の UI Card グルーピング導入 | `QuickAccessPanel.razor`, `app.css` | ✅ |
| **P2** | Settings Modal 導入 | `SettingsModal`, `Index.razor` | ✅ |
| **P2** | レスポンシブ3段階（PC/タブレット/スマホ） | `app.css` の media queries | ✅ |
| **P2** | Stats Summary タップ展開 | `SortStatsSummary` | ✅ |
| **P2** | チュートリアルツールバーの UI Card 化 | `TutorialPage.razor`, `TutorialPage.razor.css` | ✅ |
| **P3** | チュートリアルのカラー統一 | `TutorialPage.razor.css` | ✅ |
| **P3** | チュートリアル ⇔ メイン コンテキスト遷移 | 両ページ | ✅ |
| **P3** | スマホ比較モードのカルーセル | `SortCardGrid` + CSS/JS | ✅ |
| **P3** | ComparisonStatsTable のモーダル化 | `ComparisonStatsTable` | ✅ |
| **P2** | 状態保持（Query String + localStorage） | `Index.razor`（`cards` パラメータ追加・`RestoreCardsFromUrlAsync`） | ✅ |
| **P3** | ローディング画面プログレスバーのシマーアニメーション | `app.css` | ✅ |
| **P3** | ローディング画面フェードアウト遷移 | `index.html`, `loadingHelper.js`, `app.css`, `App.razor` | ✅ |

---

## 13. 状態保持

### 13.1 方式

| 性質 | 保持先 | 例 |
|------|--------|-----|
| コンテンツ状態（何を見ているか・共有したい） | URL Query String | Algorithm, Size, Pattern, Mode, Cards |
| プリファレンス（個人設定・共有不要） | localStorage | Speed, WebGL, Volume |

### 13.2 Query String パラメータ

| パラメータ | 型 | 例 | デフォルト | 更新タイミング |
|-----------|-----|-----|-----------|---------------|
| `algo` | string | `Quick+sort` | `Bubble sort` | アルゴリズム選択変更時 |
| `size` | int | `1024` | `1024` | サイズ変更時 |
| `pattern` | string | `%F0%9F%8E%B2+Random` | `🎲 Random` | パターン変更時 |
| `mode` | string | `BarChart` | `BarChart` | モード変更時 |
| `cards` | string (`\|` 区切り) | `Bubble+sort%7CMerge+sort` | なし | カード追加・削除・置換時 |

URL 例: `/?algo=Quick+sort&size=1024&pattern=%F0%9F%8E%B2+Random&mode=BarChart&cards=Bubble+sort%7CMerge+sort%7CQuick+sort`

- 状態変更時は `NavigationManager.NavigateTo(url, replace: true)` で URL を更新（履歴を汚さない）
- チュートリアルは既存の `/tutorial/{AlgorithmName}` を使用

### 13.3 localStorage キー

| キー | 型 | デフォルト |
|------|-----|-----------|
| `sortvis.renderer` | string | `"true"`（WebGL） |
| `sortvis.opsPerFrame` | int | `1` |
| `sortvis.speedMultiplier` | float | `10.0` |
| `sortvis.autoReset` | bool | `false` |
| `sortvis.instantMode` | bool | `false` |
| `sortvis.soundEnabled` | bool | `false` |
| `sortvis.soundVolume` | float | `0.5` |
| `sortvis.autoRecommendedSize` | bool | `false` |
| `sortvis.categoryFilter` | string | `""` |
| `sortvis.debugLog` | bool | `false` |
| `sortvis.scrollOnGenerate` | bool | `true` |

JS Interop 経由でアクセス（プレフィックス `sortvis.` で一括読み書き）。

### 13.4 比較カードの F5 復元（`cards` パラメータ）

`cards` パラメータにより、F5 リロード後も比較カードの構成を自動復元する。URL 共有でも同じ比較状態を再現できる。

**復元フロー:**

```
OnInitialized
  URL の cards を | で分割 → _initialCardAlgorithms（レジストリ照合済み・重複除去）
      ↓
OnAfterRenderAsync(firstRender)
  1. LoadPreferencesAsync()      ← localStorage から speed / sound 等を復元
  2. RestoreCardsFromUrlAsync()
       1 枚目: pattern.Generator で新配列生成 → ComparisonMode.AddAndGenerateAsync
       2 枚目以降: 同じ配列を流用             → ComparisonMode.AddAlgorithmAsync
       完了後: SetSpeedForAll / SetAutoResetForAll / ApplySavedSoundSettings を適用
```

**`cards` パラメータが更新される操作:**

| 操作 | 更新 |
|------|------|
| Add & Generate | ✅ |
| カードヘッダーからアルゴリズム差し替え | ✅ |
| ✖ カード削除 | ✅ |
| 🗑 Clear All | ✅（`cards` パラメータを削除） |
| `algo` / `size` / `pattern` / `mode` 変更 | ✅（URL 全体を再構築） |
| `RestoreCardsFromUrlAsync` 実行中 | ❌（`_restoringFromUrl` フラグで抑制） |

---

## 14. CSS 設計方針

### 14.1 タイポグラフィ

```css
font-family: 'Inter', -apple-system, BlinkMacSystemFont, 'Noto Sans JP', 'Segoe UI', 'Hiragino Sans', 'Yu Gothic UI', sans-serif;
-webkit-font-smoothing: antialiased;
-moz-osx-font-smoothing: grayscale;
```

Inter は Google Fonts から読み込む（`wght@300;400;500;600;700`、`display=swap`）。

### 14.2 テーマカラーシステム

**背景色:**

| 変数 | 値 | 用途 |
|------|-----|------|
| `--bg-main` | `#181d1a` | ページ全体、ヘッダー |
| `--bg-card` | `#242a27` | Sort Card、UI Card |
| `--bg-panel` | `transparent` | Quick Access Panel |
| `--bg-hover` | `#2d3530` | ホバー状態 |

**テキスト色:**

| 変数 | 値 | 用途 |
|------|-----|------|
| `--text-primary` | `#ffffff` | メインテキスト |
| `--text-secondary` | `#b8c1d0` | サブテキスト |
| `--text-disabled` | `#6b7280` | 無効状態 |
| `--text-accent` | `#7fa86f` | 強調 |

**アクセントカラー（モスグリーン系）:**

| 変数 | 値 | 用途 |
|------|-----|------|
| `--color-accent` | `#7fa86f` | リンク、選択状態、アクティブ要素 |
| `--color-accent-hover` | `#91bd7e` | ホバー |
| `--color-accent-soft` | `rgba(127,168,111,0.15)` | 薄い背景 |
| `--color-accent-dark` | `#5a7a5a` | ボタン通常状態 |
| `--color-accent-darker` | `#6d9460` | ボタンアクティブ状態 |

**再生コントロール:**

| 変数 | 値 |
|------|-----|
| `--color-play` | `#3b82f6` |
| `--color-pause` | `#a855f7` |
| `--color-stop` | `#e53e3e` |
| `--color-sound-on` | `#fabd2f` |
| `--color-sound-off` | `#6b7280` |
| `--color-sound-border` | `rgba(250,189,47,0.5)` |

**ソート可視化:**

| 変数 | 値 | 意味 |
|------|-----|------|
| `--color-normal` | `#3b82f6` | 通常 |
| `--color-read` | `#fbbf24` | 読み取り |
| `--color-write` | `#f97316` | 書き込み |
| `--color-compare` | `#a855f7` | 比較 |
| `--color-swap` | `#ef4444` | スワップ |
| `--color-sorted` | `#10b981` | ソート済み |
| `--color-buffer` | `#6b7280` | バッファ |

**ボーダー:**

| 変数 | 値 |
|------|-----|
| `--border-color` | `#374151` |
| `--border-color-light` | `#4b5563` |
| `--border-color-focus` | `#7fa86f` |

**チュートリアル専用:**

| 変数 | 値 |
|------|-----|
| `--color-tutorial-bg-start` | `rgba(69,133,136,0.15)` |
| `--color-tutorial-bg-end` | `rgba(104,157,106,0.15)` |
| `--color-tutorial-accent` | `#8ec07c` |
| `--color-tutorial-text` | `#bdae93` |

### 14.3 ブレークポイント

```css
/* スマホ: デフォルト (〜767px) */
@media (min-width: 768px) { /* タブレット */ }
@media (min-width: 1280px) { /* PC */ }
```

**最大幅制限:** ページコンテナ（`.visualization-page`, `.tutorial-page`）に以下の 3 プロパティをセットで指定する。

```css
width: 100%;
max-width: var(--page-max-width); /* 1920px */
margin: 0 auto;
```

| プロパティ | 役割 |
|---|---|
| `width: 100%` | 親（`#app`: `display:flex; flex-direction:column`）の幅いっぱいに伸ばす。`margin: auto` を設定すると flex の `align-self: stretch` が無効になりコンテンツ幅まで縮むため、明示的に指定が必要。 |
| `max-width: 1920px` | FHD（1920px）まではフル幅を維持し、2K・4K など 1920px を超える画面でのみ幅を抑制する。 |
| `margin: 0 auto` | `max-width` が効いた場合に左右の余白を均等にして中央寄せにする。 |

CSS 変数 `--page-max-width: 1920px`（`app.css` の `:root`）で一元管理。値を変更すればメインページ・チュートリアルページ両方に反映される。

### 14.4 Grid レイアウト

```css
/* PC: 2カラム（サイドバー + メイン） */
@media (min-width: 1280px) {
    .visualization-page {
        grid-template-columns: var(--sidebar-width, 280px) 1fr;
        grid-template-rows: auto auto 1fr auto; /* header | controls | cards | seekbar */
    }
}

/* タブレット・スマホ: 1カラム */
@media (max-width: 1279px) {
    .visualization-page {
        grid-template-columns: 1fr;
        grid-template-rows: auto auto 1fr auto;
    }
}
```

---

## 15. ページローディング画面

Blazor WebAssembly の初期ロード中に表示されるフルスクリーンオーバーレイ。

### 15.1 構造

`index.html` 内の `#app` の**後ろの兄弟要素**として配置する（`#app` 内部に置くと Blazor の初回レンダリングで置き換えられ消えてしまうため）。

```html
<div id="app"></div>
<div class="loading-screen">...</div>
```

### 15.2 アニメーション構成

| 要素 | アニメーション | 内容 |
|------|--------------|------|
| `.loading-bar` × 18 | `bar-sort` 2.8s infinite | バーが高さ・色を変えながら疑似ソートを演出 |
| `.loading-title` | `title-pulse` 2.4s infinite | opacity + text-shadow のパルス |
| `.loading-progress-fill` | `progress-shimmer` 1.8s infinite | グラデーションが右→左へ流れるシマー |
| `.loading-progress-track` | なし（`--blazor-load-percentage` で幅が伸びる） | 実際の読み込み進捗を反映 |

### 15.3 ページ表示への切り替え（フェードアウト）

ローディング完了後、ページに切り替わる際の急な切り替わりを防ぐためフェードアウトを実装する。

**フロー:**

```
Blazor ロード中
  → .loading-screen（position: fixed, z-index: 9999）が #app を全面オーバーレイ

App.razor OnAfterRenderAsync(firstRender)
  → loadingHelper.fadeOutLoadingScreen() を JS Interop 呼び出し
  → .loading-screen に .fade-out クラスを付与
  → opacity: 1 → 0 アニメーション（0.4s ease-out、pointer-events: none）
  → animationend イベントで display: none
  → ページが自然に現れる
```

**関連ファイル:**

| ファイル | 役割 |
|---------|------|
| `index.html` | `.loading-screen` を `#app` の兄弟要素として配置 |
| `css/app.css` | `.loading-screen`、`.loading-screen.fade-out`、`@keyframes loading-fade-out` |
| `js/loadingHelper.js` | `fadeOutLoadingScreen()` 関数（クラス付与 → `animationend` → `display:none`） |
| `App.razor` | `OnAfterRenderAsync(firstRender)` で `loadingHelper.fadeOutLoadingScreen` を呼び出す |

---

**Related Documents**: `VisualizationWeb.md`, `VisualizationWeb_tutorial.md`

