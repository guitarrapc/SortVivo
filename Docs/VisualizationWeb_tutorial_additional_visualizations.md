# Tutorial 追加ビジュアライゼーション計画書

## 背景

チュートリアルでは現在、以下のビジュアライゼーションを提供している。

| Hint | 対象カテゴリ | 対象アルゴリズム数 |
|---|---|---|
| Marble（デフォルト） | 全アルゴリズム | 全 |
| HeapTree | Heap Sorts | 2（Heapsort, Bottom-up heapsort） |
| TernaryHeapTree | Heap Sorts | 1（Ternary heapsort） |
| WeakHeapTree | Heap Sorts | 1（Weak heapsort） |
| BstTree | Tree Sorts | 1（Binary tree sort BST） |
| AvlTree | Tree Sorts | 1（Binary tree sort AVL） |

本計画書では、Marble 表示だけでは操作の本質が伝わりにくいアルゴリズム群に対し、追加ビジュアライゼーションの候補を検討し、仕様を定義する。

---

## 候補一覧と優先度

| 優先度 | 候補 | 対象カテゴリ | 対象数 | 差別化 | 実装コスト |
|---|---|---|---|---|---|
| **1** | **Distribution Bucket View** | Distribution Sorts | 8種 | ◎ バケット分配の本質が見える | 中 |
| **2** | **Sorting Network Diagram** | Network Sorts (+Exchange 1種) | 2〜3種 | ◎ 教科書的ビジュアル | 中 |
| **3** | **Recursion Tree View** | Merge / Partition Sorts | ~17種 | ○ 分割統治の構造が見える | 高 |
| **4** | **Run Stack View** | Timsort 系 Merge Sorts | 3種 | ○ 適応性の理解に不可欠 | 中 |
| **5** | **Gap Subsequence Coloring** | Shell Sort 系 Insertion Sorts | 5種 | △ マーブル拡張で済む | 低 |

以下、各候補の詳細仕様を記述する。

---

## 候補 1: Distribution Bucket View（分配バケット表示）

### 概要

Distribution Sorts の本質は「要素をバケット（穴・桁スロット）に分配し、順番に回収する」点にある。マーブル表示では `IndexRead` / `IndexWrite` の羅列にしか見えず、分配構造が伝わらない。バケットを視覚的に並べ、要素がどのバケットに入り、どの順番で回収されるかを SVG で描画する。

### 対象アルゴリズム

| アルゴリズム | バケット数 | バケットラベル | パス数 | 特記 |
|---|---|---|---|---|
| LSD Radix b=10 | 10 | `0`〜`9` | 2（一の位→十の位） | `TwoDigitDecimal` 配列を使用 |
| LSD Radix b=4 | 4 | `0`〜`3` | 複数 | 2bit グループ |
| MSD Radix b=10 | 10 | `0`〜`9` | 再帰的 | `TwoDigitDecimal` 配列。再帰バケットの入れ子表示が必要 |
| MSD Radix b=4 | 4 | `0`〜`3` | 再帰的 | 同上 |
| Pigeonhole sort | 値の範囲分 | 値そのもの | 1 | Default 配列なら 9 バケット（値 1〜9） |
| Counting sort | 値の範囲分 | 値そのもの | 1（3フェーズ） | カウント→累積和→配置の3フェーズ表示 |
| Bucket sort | √n 程度 | 範囲区間 | 1 + バケット内ソート | バケット内で Insertion sort が走る |
| American flag sort | 4 or 10 | 桁値 | 再帰的 | in-place cyclic permutation |

### ビジュアルデザイン

#### LSD Radix b=10 の表示例

```
Pass 1: ones digit (一の位)

メイン配列: [53] [57] [31] [36] [82] [85] [61] [48]

分配中 → バケット:
  ┌──[0]──┐ ┌──[1]──┐ ┌──[2]──┐ ┌──[3]──┐ ┌──[4]──┐ ┌──[5]──┐ ┌──[6]──┐ ┌──[7]──┐ ┌──[8]──┐ ┌──[9]──┐
  │       │ │  31   │ │  82   │ │  53   │ │       │ │  85   │ │  36   │ │  57   │ │  48   │ │       │
  │       │ │  61   │ │       │ │       │ │       │ │       │ │       │ │       │ │       │ │       │
  └───────┘ └───────┘ └───────┘ └───────┘ └───────┘ └───────┘ └───────┘ └───────┘ └───────┘ └───────┘

回収後: [31] [61] [82] [53] [85] [36] [57] [48]

Pass 2: tens digit (十の位) → ソート完了
```

#### Pigeonhole sort の表示例

```
メイン配列: [5] [3] [8] [1] [9] [2] [7] [4]

穴（Pigeonhole）:
  ┌──[1]──┐ ┌──[2]──┐ ┌──[3]──┐ ┌──[4]──┐ ┌──[5]──┐ ┌──[6]──┐ ┌──[7]──┐ ┌──[8]──┐ ┌──[9]──┐
  │  (1)  │ │  (2)  │ │  (3)  │ │  (4)  │ │  (5)  │ │       │ │  (7)  │ │  (8)  │ │  (9)  │
  └───────┘ └───────┘ └───────┘ └───────┘ └───────┘ └───────┘ └───────┘ └───────┘ └───────┘

回収中: [1] [2] [3] [4] [5] [7] [8] [9]  ← 穴を左から順に回収
```

#### Counting sort の表示例（3フェーズ）

```
Phase 1: Count（カウント）
  値:   [1]  [2]  [3]  [4]  [5]  [6]  [7]  [8]  [9]
  回数:  1    1    1    1    1    0    1    1    1

Phase 2: Prefix Sum（累積和）
  値:   [1]  [2]  [3]  [4]  [5]  [6]  [7]  [8]  [9]
  位置:  1    2    3    4    5    5    6    7    8

Phase 3: Place（配置）
  各要素が累積和の位置に直接配置される
```

### バケットの描画仕様

- **バケット枠**: `rect` SVG、角丸 4px、ボーダー `rgba(255,255,255,0.3)` 1px
- **バケットラベル**: 上部に桁値/値を表示。font-size 12px、色 `#9CA3AF`
- **マーブル**: バケット内のマーブルはメイン配列と同じ HSL カラー、直径 36px（PC）/ 30px（タブレット）/ 24px（スマホ）
- **バケット内配置**: 縦方向にスタック（FIFO順で上から下）
- **バケット幅**: viewBox ベースで均等割。バケット数に応じて自動調整
- **ハイライト**:
  - scatter 中の要素: 黄色リング `#FBBF24`（IndexRead） + 橙リング `#F97316`（IndexWrite 先のバケット）
  - gather 中の要素: 橙リング `#F97316`（回収中）
  - 現在のバケット: ボーダー色を操作色に変更 + 微グロー

### フェーズ管理

Distribution Sort の操作フローは以下の共通パターンに分解できる。

| フェーズ | 操作パターン | 表示 |
|---|---|---|
| **Scatter（分配）** | `IndexRead(main)` → `IndexWrite(temp)` | メイン配列から要素を読み、バケットに落とす |
| **Gather（回収）** | `IndexRead(temp)` / `RangeCopy(temp→main)` | バケットから順に要素を回収しメイン配列に書き戻す |
| **Count（計数）** | `IndexRead(main)` のみ（カウント表更新） | ヒストグラム棒が伸びる |
| **Prefix Sum（累積和）** | 内部計算（操作なし or IndexWrite on count array） | 棒の値が累積和に変化 |

### `DistributionSnapshot` データモデル

```csharp
/// <summary>
/// Distribution Sorts チュートリアル用のバケット分配スナップショット。
/// 各 TutorialStep に付属し、そのステップ時点のバケット状態を保持する。
/// </summary>
public record DistributionSnapshot
{
    /// <summary>バケット数</summary>
    public int BucketCount { get; init; }

    /// <summary>各バケットのラベル（桁値や値そのもの）</summary>
    public string[] BucketLabels { get; init; } = [];

    /// <summary>各バケットに現在入っている要素の値リスト（バケットインデックス → 値リスト）</summary>
    public int[][] Buckets { get; init; } = [];

    /// <summary>現在のフェーズ</summary>
    public DistributionPhase Phase { get; init; }

    /// <summary>現在のパス番号（LSD: 0=一の位, 1=十の位, ...）。非パス型は 0 固定</summary>
    public int PassIndex { get; init; }

    /// <summary>パスの説明ラベル（例: "ones digit", "tens digit"）</summary>
    public string PassLabel { get; init; } = string.Empty;

    /// <summary>ハイライト中のバケットインデックス（-1 = なし）</summary>
    public int ActiveBucketIndex { get; init; } = -1;

    /// <summary>ハイライト中のバケット内要素位置（-1 = なし）</summary>
    public int ActiveElementInBucket { get; init; } = -1;

    /// <summary>
    /// Counting sort 用: カウント配列の現在値。
    /// Phase == Count or PrefixSum のとき non-null。
    /// </summary>
    public int[]? Counts { get; init; }
}

/// <summary>
/// Distribution Sort のフェーズ。
/// </summary>
public enum DistributionPhase
{
    /// <summary>要素をバケットに分配中</summary>
    Scatter,

    /// <summary>バケットから要素を回収中</summary>
    Gather,

    /// <summary>要素の出現回数をカウント中（Counting sort）</summary>
    Count,

    /// <summary>累積和を計算中（Counting sort）</summary>
    PrefixSum,

    /// <summary>累積和に基づいて配置中（Counting sort）</summary>
    Place,
}
```

### 追跡ロジック（TutorialStepBuilder）

Distribution Sort のバケット追跡は、アルゴリズムごとに操作パターンが異なるため、Hint 値で分岐する。

#### LSD Radix b=10 追跡

```
state:
  bucketCount = 10
  buckets = new int[10][]  // 各バケットの要素リスト
  passIndex = 0
  currentArray = initialArray.Clone()

1. 操作列を走査
2. IndexRead(main, i) → 値を取得、現在の桁を計算 → digit = (value / 10^passIndex) % 10
3. IndexWrite(temp, pos) → buckets[digit].Add(value)
   → snapshot: Phase=Scatter, ActiveBucketIndex=digit
4. RangeCopy(temp→main) → 全バケットを順に回収
   → snapshot: Phase=Gather
5. passIndex++, buckets クリア、次のパスへ
```

#### Pigeonhole sort 追跡

```
state:
  minValue, maxValue → バケット数 = maxValue - minValue + 1
  bucketLabels = [minValue..maxValue]
  buckets = new int[range][]

1. IndexRead(main, i) → 値 v を取得 → bucketIndex = v - minValue
2. IndexWrite(temp, pos) → buckets[bucketIndex].Add(v)
   → snapshot: Phase=Scatter, ActiveBucketIndex=bucketIndex
3. 回収フェーズ: バケットを左から順にスキャン
   IndexRead(temp) / RangeCopy(temp→main)
   → snapshot: Phase=Gather, ActiveBucketIndex=現在回収中バケット
```

#### Counting sort 追跡

```
state:
  counts = new int[range]
  phase = Count

1. Count フェーズ: IndexRead(main, i) → counts[value - min]++
   → snapshot: Phase=Count, Counts=counts.Clone()
2. PrefixSum フェーズ: 累積和計算（操作なしの場合は最初の Place 操作直前に挿入）
   → snapshot: Phase=PrefixSum, Counts=prefixSums.Clone()
3. Place フェーズ: IndexRead(main) → IndexWrite(temp) → 配置
   → snapshot: Phase=Place, ActiveBucketIndex=digit
```

#### MSD Radix / American flag sort 追跡

MSD は再帰的にバケット分割するため、バケットの**入れ子表示**が必要。

```
再帰レベル 0: 全体を十の位で 10 バケットに分割
再帰レベル 1: 各バケット内を一の位で再分割

表示: 現在処理中の再帰レベルのバケットのみ表示し、
      上位のバケット境界をメイン配列上にグレーの仕切り線で示す。
```

追加フィールド:
```csharp
/// <summary>MSD 用: 現在の再帰の対象範囲（メイン配列上の開始〜終了インデックス）</summary>
public (int Start, int End)? ActiveRange { get; init; }

/// <summary>MSD 用: 上位バケットの境界インデックスリスト（メイン配列上の仕切り線位置）</summary>
public int[]? ParentBucketBoundaries { get; init; }
```

### `TutorialVisualizationHint` 拡張

```csharp
/// <summary>LSD 基数ソートのバケット分配表示（桁ごとのバケットを表示）</summary>
DigitBucketLsd,

/// <summary>MSD 基数ソートのバケット分配表示（再帰的バケット + 境界線）</summary>
DigitBucketMsd,

/// <summary>値ベースのバケット分配表示（Pigeonhole / Counting / Bucket sort）</summary>
ValueBucket,
```

### バケット配列との対応

既存の `SortOperation` は `BufferId=0`（メイン配列）と `BufferId=1`（テンポラリバッファ）を使用する。Distribution Bucket View は、テンポラリバッファへの書き込みパターンからバケット割り当てを逆算する。

| 情報源 | 逆算方法 |
|---|---|
| バケットインデックス | LSD: `(value / 10^passIndex) % 10`、Pigeonhole: `value - min` |
| 要素の値 | `IndexWrite` の `value` パラメータ |
| パス境界 | LSD: `RangeCopy(temp→main)` が来たらパス終了 |

### Renderer コンポーネント

```
Components/
  DistributionBucketRenderer.razor  ← 新規: バケット分配 SVG 描画コンポーネント
```

**パラメータ:**
```csharp
[Parameter] public DistributionSnapshot? Snapshot { get; set; }
[Parameter] public int MaxValue { get; set; }
[Parameter] public int StepIndex { get; set; }
[Parameter] public string Label { get; set; } = "Buckets";
```

### 切り替え UI

```
[🔵 Marble]  [🪣 Buckets]     ← セグメントボタン
```

- **表示条件**: `TutorialVisualizationHint` が `DigitBucketLsd` / `DigitBucketMsd` / `ValueBucket` のいずれかのとき
- **デフォルト**: Buckets（教育目的ではバケット表示が主役）

### AlgorithmRegistry 設定

```csharp
// LSD Radix b=10
tutorialVisualizationHint: TutorialVisualizationHint.DigitBucketLsd

// LSD Radix b=4
tutorialVisualizationHint: TutorialVisualizationHint.DigitBucketLsd

// MSD Radix b=10, MSD Radix b=4, American flag sort
tutorialVisualizationHint: TutorialVisualizationHint.DigitBucketMsd

// Pigeonhole sort, Counting sort, Bucket sort
tutorialVisualizationHint: TutorialVisualizationHint.ValueBucket
```

### アーキテクチャへの影響

```
Models/
  DistributionSnapshot.cs            ← 新規: バケットスナップショット + DistributionPhase enum
  TutorialVisualizationHint.cs       ← 変更: DigitBucketLsd / DigitBucketMsd / ValueBucket 追加
  TutorialStep.cs                    ← 変更: Distribution プロパティ追加

Components/
  DistributionBucketRenderer.razor   ← 新規: バケット分配 SVG 描画

Services/
  TutorialStepBuilder.cs             ← 変更: Distribution 追跡ロジック追加
  AlgorithmRegistry.cs               ← 変更: Distribution Sorts に Hint 設定

Pages/
  TutorialPage.razor                 ← 変更: IsDistributionView / GetCurrentDistribution / BucketRenderer
  TutorialPage.razor.css             ← 変更: バケットスタイル追加
```

---

## 候補 2: Sorting Network Diagram（ソーティングネットワーク図）

### 概要

Bitonic sort はデータ非依存の「比較ネットワーク」であり、教科書で見る **ワイヤ＋コンパレータ** ダイアグラムが最もフィットする。水平軸を時間（ステップ）、垂直軸をワイヤ（配列インデックス）とし、コンパレータを縦線で描画する。

### 対象アルゴリズム

| アルゴリズム | ネットワーク表示 | 備考 |
|---|---|---|
| Bitonic sort | ✅ | 反復的ビルド → マージ。コンパレータ位置が固定 |
| Bitonic sort (Recursive) | ✅ | 同一ネットワーク構造（再帰で記述） |
| Odd-even sort | ❓（要検討） | Exchange Sort だがネットワーク的性質あり。ただしデータ依存で終了する点が異なる |

### ビジュアルデザイン

```
ワイヤ0 ──●───────────────●───────────────────  [5→1]
          │               │
ワイヤ1 ──●─────●─────────│──●────────────────  [3→2]
                │         │  │
ワイヤ2 ────────●─────────●──│────────────────  [8→3]
                             │
ワイヤ3 ─────────────────────●────────────────  [1→4]

ワイヤ4 ──●───────────────●───────────────────  [9→5]
          │               │
ワイヤ5 ──●─────●─────────│──●────────────────  [2→7]
                │         │  │
ワイヤ6 ────────●─────────●──│────────────────  [7→8]
                             │
ワイヤ7 ─────────────────────●────────────────  [4→9]

          stage 1    stage 2    stage 3   ...

          ↑ 現在のコンパレータをハイライト
```

### ネットワーク描画仕様

- **ワイヤ**: 水平線、色 `rgba(255, 255, 255, 0.2)`、幅 1.5px
- **ワイヤラベル**: 左端にインデックス、右端に現在の値をマーブル色付きで表示
- **コンパレータ**: 2本のワイヤ間の縦線 + 両端の丸ドット（直径 6px）
  - 未処理: `rgba(255, 255, 255, 0.15)`（薄いグレー）
  - 処理済み（Swap なし）: `#6B7280`（グレー）
  - 処理済み（Swap あり）: `#EF4444`（赤、薄め）
  - **現在処理中**: 太線 3px + グロー
    - Compare のみ: `#A855F7`（紫）
    - Swap 発火: `#EF4444`（赤）
- **ステージ区切り**: 縦の破線 `rgba(255,255,255,0.1)` で並列実行可能なコンパレータ群を区分

### ネットワーク構造の事前計算

Bitonic sort のネットワーク構造はデータ非依存で決定的なため、アルゴリズム実行前に全コンパレータの位置を事前計算できる。

```csharp
/// <summary>
/// ソーティングネットワーク中の1つのコンパレータ。
/// </summary>
public record NetworkComparator
{
    /// <summary>上側ワイヤインデックス</summary>
    public int Wire1 { get; init; }

    /// <summary>下側ワイヤインデックス</summary>
    public int Wire2 { get; init; }

    /// <summary>ステージ番号（並列実行可能なグループ）</summary>
    public int Stage { get; init; }
}
```

### `NetworkSnapshot` データモデル

```csharp
/// <summary>
/// Sorting Network チュートリアル用のネットワークスナップショット。
/// </summary>
public record NetworkSnapshot
{
    /// <summary>ネットワークのワイヤ数（= 配列サイズ）</summary>
    public int WireCount { get; init; }

    /// <summary>全コンパレータのリスト（事前計算済み、不変）</summary>
    public NetworkComparator[] Comparators { get; init; } = [];

    /// <summary>現在処理中のコンパレータインデックス（-1 = なし）</summary>
    public int ActiveComparatorIndex { get; init; } = -1;

    /// <summary>各コンパレータの処理結果（true = Swap 発火、false = Swap なし、null = 未処理）</summary>
    public bool?[] ComparatorResults { get; init; } = [];

    /// <summary>各ワイヤの現在の値</summary>
    public int[] WireValues { get; init; } = [];
}
```

### 追跡ロジック（TutorialStepBuilder）

```
事前計算:
  1. BitonicSort のネットワーク構造を生成（n=8 → 24 コンパレータ、6 ステージ）
  2. NetworkComparator[] を stage 順に格納

ステップ追跡:
  comparatorIndex = 0

  Compare(i, j) →
    activeIndex = comparatorIndex
    → snapshot: ActiveComparatorIndex = activeIndex

  Swap(i, j) →
    comparatorResults[comparatorIndex] = true
    comparatorIndex++

  Compare のみ（Swap なし） →
    comparatorResults[comparatorIndex] = false
    comparatorIndex++
```

### `TutorialVisualizationHint` 拡張

```csharp
/// <summary>ソーティングネットワーク図（ワイヤ＋コンパレータ）</summary>
SortingNetwork,
```

### Renderer コンポーネント

```
Components/
  NetworkDiagramRenderer.razor  ← 新規: ソーティングネットワーク SVG 描画
```

### 切り替え UI

```
[🔵 Marble]  [🔀 Network]     ← セグメントボタン
```

### AlgorithmRegistry 設定

```csharp
// Bitonic sort, Bitonic sort (Recursive)
tutorialVisualizationHint: TutorialVisualizationHint.SortingNetwork
```

### アーキテクチャへの影響

```
Models/
  NetworkSnapshot.cs                 ← 新規: ネットワークスナップショット + NetworkComparator
  TutorialVisualizationHint.cs       ← 変更: SortingNetwork 追加
  TutorialStep.cs                    ← 変更: Network プロパティ追加

Components/
  NetworkDiagramRenderer.razor       ← 新規: ネットワーク SVG 描画

Services/
  TutorialStepBuilder.cs             ← 変更: ネットワーク事前計算 + 追跡ロジック
  AlgorithmRegistry.cs               ← 変更: Bitonic sort に SortingNetwork 設定

Pages/
  TutorialPage.razor                 ← 変更: IsNetworkView / GetCurrentNetwork / NetworkRenderer
  TutorialPage.razor.css             ← 変更: ネットワークスタイル追加
```

### 考慮事項

- **Odd-even sort の扱い**: Odd-even sort はネットワーク的性質を持つが、データ依存で終了判定するため厳密にはソーティングネットワークではない。事前にコンパレータ数が決まらないため、段階的にネットワークを伸ばす描画が必要。対応を見送るか、「最悪ケースのネットワーク」を事前計算して表示する方法がある。→ **Phase 1 では Bitonic sort 2種のみ対応し、Odd-even sort は将来検討とする。**
- **8要素の Bitonic sort**: 8 要素 = $\frac{8 \times (\log_2 8)(\log_2 8 + 1)}{4} = 24$ コンパレータ、6 ステージ。1画面に収まるサイズ。

---

## 候補 3: Recursion Tree View（再帰ツリー表示）

### 概要

Merge Sort / Quicksort の分割統治構造をツリーとして表示し、「今どの部分問題を解いているか」を示す。ノードには部分配列の内容を表示し、処理中のノードをハイライトする。

### 対象アルゴリズム

| アルゴリズム | 再帰表示 | 備考 |
|---|---|---|
| Merge sort | ✅ | 等分割、マージは子→親で表示 |
| Bottom-up merge sort | ✅（擬似再帰） | 再帰はないが、run 幅の倍増をレベルとして表示可能 |
| Rotate merge sort | ✅ | 同上 |
| Rotate merge sort (Recursive) | ✅ | 再帰的な分割を直接表示 |
| SymMerge sort | ✅ | 同上 |
| Quicksort | ✅ | 不均等分割、ピボットをノードラベルに表示 |
| Quicksort (Median3) | ✅ | 同上 |
| Quicksort (Median9) | ✅ | 同上 |
| Quicksort (DualPivot) | ✅ | 三分割（ノードが3つの子を持つ） |
| Quicksort (Stable) | ✅ | 同上 |
| BlockQuickSort | ✅ | 同上 |
| Introsort | ✅ | Quicksort → Heapsort 切り替えポイントを表示 |
| IntrosortDotnet | ✅ | 同上 |
| PDQ sort | ✅ | パターン検出をノードラベルに表示 |
| C++ std::sort | ✅ | 同上 |
| Timsort | ❓（要検討） | run 検知ベースで固定分割でない。候補 4 の Run Stack の方が適切 |
| Powersort | ❓（要検討） | 同上 |
| ShiftSort | ❓（要検討） | 同上 |

### ビジュアルデザイン

#### Merge sort の再帰ツリー

```
               [5,3,8,1,9,2,7,4]              ← depth 0: 全体
              /                  \
       [5,3,8,1]            [9,2,7,4]          ← depth 1
       /      \              /      \
    [5,3]   [8,1]        [9,2]   [7,4]         ← depth 2
    / \      / \          / \      / \
  [5] [3] [8] [1]      [9] [2]  [7] [4]        ← depth 3 (base)

現在: [5,3] をマージ中 → ノードをハイライト + 子から親への矢印
```

#### Quicksort の再帰ツリー

```
               [5,3,8,1,9,2,7,4]
                  pivot=5 ★
              /                 \
       [3,1,2,4]            [8,9,7]
         pivot=3 ★           pivot=8 ★
        /     \              /     \
     [1,2]   [4]          [7]    [9]
```

### ノード描画仕様

- **形状**: 角丸矩形、幅は要素数に比例
- **内容**: 部分配列を小さなマーブル列で表示（値は省略可、色のみ）
- **ハイライト**:
  - 処理中のノード: 操作色のボーダー + グロー
  - 完了済みノード: ソート済みの配列を緑ボーダーで表示
  - 未処理ノード: グレーアウト
- **ピボット表示（Quicksort）**: ノードラベルに `pivot=5` を表示、ピボット要素を金色 `#FBBF24` でマーク

### `RecursionSnapshot` データモデル

```csharp
/// <summary>
/// 再帰ツリー表示用のスナップショット。
/// </summary>
public record RecursionSnapshot
{
    /// <summary>全ノードの情報</summary>
    public RecursionNode[] Nodes { get; init; } = [];

    /// <summary>現在処理中のノード ID（-1 = なし）</summary>
    public int ActiveNodeId { get; init; } = -1;
}

/// <summary>
/// 再帰ツリーの1ノード。
/// </summary>
public record RecursionNode
{
    /// <summary>ノード ID</summary>
    public int Id { get; init; }

    /// <summary>親ノード ID（-1 = ルート）</summary>
    public int ParentId { get; init; } = -1;

    /// <summary>メイン配列上の開始インデックス</summary>
    public int Start { get; init; }

    /// <summary>メイン配列上の終了インデックス（排他）</summary>
    public int End { get; init; }

    /// <summary>ノードの状態</summary>
    public RecursionNodeState State { get; init; }

    /// <summary>ピボット値（Quicksort のみ、他は null）</summary>
    public int? PivotValue { get; init; }

    /// <summary>ノード内の配列スナップショット</summary>
    public int[] Values { get; init; } = [];
}

public enum RecursionNodeState
{
    /// <summary>未処理</summary>
    Pending,
    /// <summary>分割中 / パーティション中</summary>
    Active,
    /// <summary>マージ中</summary>
    Merging,
    /// <summary>完了（ソート済み）</summary>
    Completed,
}
```

### 追跡ロジックの課題

再帰構造の追跡は、`SortOperation` の低レベル操作列から再帰の開始・終了を検出する必要があり、実装コストが高い。

**検出方法の候補:**

1. **範囲ベースの推論**: `Compare` / `Swap` のインデックス範囲が狭まったら子ノードへ遷移、広がったら親ノードへ復帰
2. **ISortContext 拡張**: `OnRecursionEnter(start, end)` / `OnRecursionLeave()` をコンテキストに追加
3. **アルゴリズム固有の事前計算**: 8要素の固定配列に対して事前にノード構造を計算

→ 方法 1 は誤検出リスクが高い。方法 2 はコアライブラリへの影響が大きい。**方法 3 が最も現実的**（チュートリアルは固定配列のため）。ただし `TutorialArrayType.MultiRun` の 32 要素配列を使う Timsort 系はノード数が多くなる。

### `TutorialVisualizationHint` 拡張

```csharp
/// <summary>再帰ツリー表示（分割統治の構造を木で描画）</summary>
RecursionTree,
```

### 考慮事項

- **8 要素の再帰ツリーは depth=3 で小さい**: 教育目的には十分だが、視覚的なインパクトは候補 1・2 より弱い
- **Quicksort は不均等分割**: ピボットの選び方でツリー形状が変わるため、最悪ケース（ソート済み入力）と平均ケースの比較が教育的に有用
- **Introsort のフォールバック表示**: Quicksort → Heapsort への切り替えポイントをノードの色で示せる

### アーキテクチャへの影響

```
Models/
  RecursionSnapshot.cs               ← 新規: 再帰ツリースナップショット + RecursionNode + RecursionNodeState
  TutorialVisualizationHint.cs       ← 変更: RecursionTree 追加
  TutorialStep.cs                    ← 変更: Recursion プロパティ追加

Components/
  RecursionTreeRenderer.razor        ← 新規: 再帰ツリー SVG 描画

Services/
  TutorialStepBuilder.cs             ← 変更: 再帰構造の追跡ロジック追加
  AlgorithmRegistry.cs               ← 変更: Merge / Partition Sorts に RecursionTree 設定

Pages/
  TutorialPage.razor                 ← 変更: IsRecursionView / GetCurrentRecursion / RecursionTreeRenderer
  TutorialPage.razor.css             ← 変更: 再帰ツリースタイル追加
```

### RecursionTree の見方（ユーザー向けガイド）

#### 🌳 ツリーの構造

RecursionTreeは、**分割統治アルゴリズム（Merge sort / Quicksort）がどのように問題を分割して解くか**を視覚化します。

**ノード（四角形）の意味：**
- 各ノードは**ソートする範囲**を表します（例：`[0..8)` = インデックス0〜7の8要素）
- ノード内の小さな丸は、その範囲の**実際の値**をマーブルで表示
- **親ノード**：大きな問題（全体をソート）
- **子ノード**：小さな部分問題（半分や部分範囲をソート）

**色の意味：**
- **グレー枠**：まだ処理していない範囲（Pending）
- **オレンジ枠**：今処理中の範囲（Active）
- **青緑枠**：マージ中の範囲（Merging - Merge sortのみ）
- **緑チェックマーク**：ソート完了した範囲（Completed）

#### 📖 Merge sort の読み方

**分割フェーズ（上から下へ）：**
```
              [5,3,8,1,9,2,7,4]    ← ステップ1: 全体を半分に分割
              ↙              ↘
       [5,3,8,1]          [9,2,7,4]  ← ステップ2: さらに半分に分割
       ↙    ↘           ↙    ↘
    [5,3]  [8,1]     [9,2]  [7,4]    ← ステップ3: さらに半分に
```
- 最初は**ルートノード（全体）がオレンジ**
- 次のステップで**左半分の子ノード**がオレンジに変わる（右は灰色のまま）
- これを繰り返して**葉ノード（1要素）**まで分割

**マージフェーズ（下から上へ）：**
```
    [3,5]  ← ステップ4: [5] と [3] をマージ → 青緑枠 → 完了すると緑チェック
```
- ナラティブが「**Merging sorted subarrays into range [0..2)**」と表示される
- **左右の子ノードが既にソート済み**（緑チェック）
- 親ノードが**青緑枠（Merging）**になり、2つの子を合体
- 完了すると**緑チェック**が付き、次のノードへ

**操作の流れ：**
1. `RangeCopy`: 左半分をバッファにコピー → **マージ開始シグナル** → ノードが青緑に
2. `Compare`: バッファと右半分を比較して小さい方を選択
3. `IndexWrite`: 選んだ値を書き戻す
4. 全て書き戻し完了 → **緑チェック** → 親ノードへ

#### 🎯 Quicksort の読み方

**パーティションフェーズ：**
```
           [5,3,8,1,9,2,7,4]
            pivot=5 ★         ← ステップ1: ピボット（中央値）を選ぶ
           ↙              ↘
     [3,1,2,4]          [8,9,7]  ← ステップ2: ピボット以下/以上に分割
     pivot=3 ★          pivot=8 ★
```
- **ピボット値**が黄色（`#FBBF24`）でマークされる
- ナラティブが「**Partitioning range [0..8) around pivot 5**」と表示
- `Swap`操作でピボットより小さい要素を左、大きい要素を右に移動

**分割の不均等性：**
- Merge sortは**常に等分割**（左右の子ノードのサイズが同じ）
- Quicksortは**ピボット次第で不均等**（左が3要素、右が4要素など）
- **最悪ケース**：常に最小/最大がピボットだと、片側の子が1要素、もう片側がn-1要素 → 深いツリーに

**操作の流れ：**
1. `Compare`: 中央要素（ピボット）を読み取る → **パーティション開始**
2. `Swap`: 要素を左右に振り分ける
3. パーティション完了 → **緑チェック** → 左右の子ノードを再帰的に処理

#### 💡 ナラティブの読み解き方

- **"Start sorting entire array [0..8)"** → 全体のソート開始
- **"Divide: recursively sort subrange [0..4)"** → 左半分を再帰的に処理
- **"Merging sorted subarrays into range [0..4)"** → 左右の子をマージ中
- **"Conquer: merge completed subranges back into [0..8)"** → 全体のマージ完了
- **"Partitioning range [0..8) around pivot 5"** → ピボット5で分割中
- **"Range [0..4) is now sorted"** → この範囲のソート完了

---

## 候補 4: Run Stack View（ラン・スタック表示）

### 概要

Timsort / Powersort / ShiftSort の特徴は「自然な run を検知 → スタックに push → invariant に基づいてマージ」というスタック管理にある。これを積み上げ棒グラフで表示し、run の検知・蓄積・マージの過程を可視化する。

### 対象アルゴリズム

| アルゴリズム | Run Stack 表示 | 備考 |
|---|---|---|
| Timsort | ✅ | stack invariant: `a > b + c` かつ `b > c` |
| Powersort | ✅ | power 値によるマージスケジューリング |
| ShiftSort | ✅ | 昇順/降順 run の対称検知 |

### ビジュアルデザイン

```
Run Stack:

    ┌─────────────────────┐
    │    run C (len=5)    │  ← 最新（スタック top）
    ├─────────────────────┤
    │    run B (len=8)    │  ← マージ候補
    ├─────────────────────┤
    │    run A (len=12)   │  ← 最古
    └─────────────────────┘

    invariant check: B(8) > C(5) ✓, A(12) > B(8)+C(5) ✗
    → merge B + C

    ┌─────────────────────┐
    │   run BC (len=13)   │  ← マージ結果
    ├─────────────────────┤
    │    run A (len=12)   │
    └─────────────────────┘
```

### Run 棒の描画仕様

- **形状**: 水平棒、幅は run 長に比例、高さ固定 36px
- **色**: run ごとに異なる色（HSL で割り当て）
- **ラベル**: 棒の中央に `run X (len=N)` を白文字で表示
- **マージアニメーション**: 2 つの棒が合体して 1 つの棒になる
- **invariant 表示**: スタックの隣に `a > b + c` 等の条件式と ✓/✗ を表示

### `RunStackSnapshot` データモデル

```csharp
/// <summary>
/// Run Stack 表示用のスナップショット。
/// </summary>
public record RunStackSnapshot
{
    /// <summary>現在のスタック状態（bottom → top 順）</summary>
    public RunInfo[] Stack { get; init; } = [];

    /// <summary>現在検出中の run（まだスタックに push されていない）。null = run 検出中でない</summary>
    public RunInfo? DetectingRun { get; init; }

    /// <summary>マージ中の run ペア（2 要素）。null = マージ中でない</summary>
    public (int StackIndex1, int StackIndex2)? MergingPair { get; init; }

    /// <summary>フェーズ</summary>
    public RunStackPhase Phase { get; init; }
}

/// <summary>
/// 個別の run の情報。
/// </summary>
public record RunInfo
{
    /// <summary>メイン配列上の開始インデックス</summary>
    public int Start { get; init; }

    /// <summary>run の長さ</summary>
    public int Length { get; init; }

    /// <summary>run 内の値スナップショット</summary>
    public int[] Values { get; init; } = [];

    /// <summary>Powersort 用: この run の power 値。null = 使用しない</summary>
    public int? Power { get; init; }
}

public enum RunStackPhase
{
    /// <summary>run を検出中（配列をスキャン）</summary>
    Detecting,
    /// <summary>短い run を Insertion sort で拡張中</summary>
    Extending,
    /// <summary>スタックに push</summary>
    Pushing,
    /// <summary>invariant チェック中</summary>
    CheckingInvariant,
    /// <summary>マージ中</summary>
    Merging,
    /// <summary>最終マージ（残りの run を全マージ）</summary>
    FinalMerge,
}
```

### 追跡ロジックの課題

Timsort 系のスタック操作は `SortOperation` からの逆算が困難。run の開始・終了、push、invariant チェック、マージ開始は明示的な操作として記録されていない。

**対応案:**
- **ISortContext 拡張**: `OnRunDetected(start, length)` / `OnRunMerge(run1, run2)` を追加
- **パターンマッチング**: `Compare` の連続パターンから run 検知フェーズを推論

→ ISortContext 拡張が最も正確。ただしコアライブラリへの影響があるため、Timsort 系のチュートリアル固有のフックとして設計する必要がある。

### `TutorialVisualizationHint` 拡張

```csharp
/// <summary>Run Stack 表示（Timsort 系のスタック管理を棒グラフで描画）</summary>
RunStack,
```

### アーキテクチャへの影響

```
Models/
  RunStackSnapshot.cs                ← 新規: Run Stack スナップショット + RunInfo + RunStackPhase
  TutorialVisualizationHint.cs       ← 変更: RunStack 追加
  TutorialStep.cs                    ← 変更: RunStack プロパティ追加

Components/
  RunStackRenderer.razor             ← 新規: Run Stack SVG 描画

Services/
  TutorialStepBuilder.cs             ← 変更: Run Stack 追跡ロジック追加
  AlgorithmRegistry.cs               ← 変更: Timsort 系に RunStack 設定

Pages/
  TutorialPage.razor                 ← 変更: IsRunStackView / GetCurrentRunStack / RunStackRenderer
  TutorialPage.razor.css             ← 変更: Run Stack スタイル追加
```

### 考慮事項

- **32 要素配列**: Timsort 系は `TutorialArrayType.MultiRun`（32 要素）を使用。run 数が多くスタックの高さが変動するため、表示領域の確保が必要
- **ISortContext への影響**: Run 検知のフックを追加する場合、既存のアルゴリズム実装にも変更が波及する。最小限のインターフェース拡張で対応すべき

---

## 候補 5: Gap Subsequence Coloring（ギャップ部分列の色分け）

### 概要

Shell sort の h-spaced 部分列を色分けして表示し、同じ部分列に属するマーブルが同色になることで「どの要素が Insertion sort の対象か」を一目で示す。

これは完全な別ビューではなく、**マーブル表示のアノテーション拡張**として実装可能。

### 対象アルゴリズム

| アルゴリズム | Gap 色分け | 備考 |
|---|---|---|
| Shell sort (Knuth 1973) | ✅ | gap: 1, 4, 13, ... |
| Shell sort (Sedgewick 1986) | ✅ | gap: 1, 5, 19, 41, ... |
| Shell sort (Tokuda 1992) | ✅ | gap: 1, 4, 9, 20, ... |
| Shell sort (Ciura 2001) | ✅ | gap: 1, 4, 10, 23, ... |
| Shell sort (Lee 2021) | ✅ | 同上 |

### ビジュアルデザイン

```
gap=4 の場合:
   [5]   [3]   [8]   [1]   [9]   [2]   [7]   [4]
    🔴    🔵    🟢    🟡    🔴    🔵    🟢    🟡
    ↑                       ↑
    同じ部分列（index 0, 4）

gap=2 の場合:
   [1]   [2]   [5]   [3]   [9]   [4]   [7]   [8]
    🔴    🔵    🔴    🔵    🔴    🔵    🔴    🔵

gap=1（最終パス）: 全て同色 → 通常の Insertion sort
```

### 実装アプローチ

マーブル表示の拡張として実装する。

```csharp
// TutorialStep に追加
/// <summary>
/// Shell sort 用: 現在の gap 値。null = Shell sort 以外
/// </summary>
public int? ShellGap { get; init; }
```

MarbleRenderer に gap 情報が渡されたとき、各マーブルの**下部インデックスの下**にサブグループカラーを小さなドットで表示する。

```
  [5]     ← マーブル（HSL 値ベース色、変更なし）
   0      ← インデックス
   ●      ← サブグループカラードット（gap % の色）
```

### 追跡ロジック

Shell sort の gap 追跡は比較的容易。`Compare` のインデックス差から gap を逆算できる。

```
Compare(i, j) where |i - j| > 1 → gap = |i - j|
gap が変化したら → 新しい gap フェーズの開始
```

### `TutorialVisualizationHint` への影響

Gap Coloring は独立した代替ビューではなくマーブルの拡張であるため、`TutorialVisualizationHint` への追加は不要。代わりに `TutorialStep.ShellGap` プロパティの有無で MarbleRenderer が自動的に色分けを適用する。

### アーキテクチャへの影響

```
Models/
  TutorialStep.cs                    ← 変更: ShellGap プロパティ追加

Components/
  MarbleRenderer.razor               ← 変更: gap 色分けドット追加

Services/
  TutorialStepBuilder.cs             ← 変更: Shell sort gap 追跡ロジック追加

Pages/
  TutorialPage.razor.css             ← 変更: gap ドットスタイル追加
```

---

## 実装ロードマップ

### Phase A: Distribution Bucket View（候補 1）

対象アルゴリズム 8 種と最多で、マーブル表示との差別化が最大。

1. `DistributionSnapshot` / `DistributionPhase` モデル作成
2. `TutorialVisualizationHint` に `DigitBucketLsd` / `DigitBucketMsd` / `ValueBucket` 追加
3. `TutorialStep.Distribution` プロパティ追加
4. `TutorialStepBuilder` に Distribution 追跡ロジック追加（LSD → Pigeonhole → Counting → MSD の順）
5. `DistributionBucketRenderer.razor` 実装
6. `TutorialPage.razor` に Distribution View の切り替えロジック追加
7. `AlgorithmRegistry` に Hint 設定
8. テスト・動作確認

### Phase B: Sorting Network Diagram（候補 2）

対象は 2 種だが、教科書的ビジュアルで教育価値が高い。

1. `NetworkSnapshot` / `NetworkComparator` モデル作成
2. `TutorialVisualizationHint` に `SortingNetwork` 追加
3. `TutorialStep.Network` プロパティ追加
4. Bitonic sort のネットワーク構造事前計算ロジック実装
5. `TutorialStepBuilder` に Network 追跡ロジック追加
6. `NetworkDiagramRenderer.razor` 実装
7. `TutorialPage.razor` に Network View の切り替えロジック追加
8. テスト・動作確認

### Phase C: Gap Subsequence Coloring（候補 5）

実装コストが低く、既存 MarbleRenderer の拡張で対応可能。

1. `TutorialStep.ShellGap` プロパティ追加
2. `TutorialStepBuilder` に gap 追跡ロジック追加
3. `MarbleRenderer.razor` に gap 色分けドット追加
4. テスト・動作確認

### Phase D: Recursion Tree View（候補 3）— 将来検討

実装コストが高く、ISortContext の拡張または固定配列の事前計算が必要。候補 1・2 の完了後に再評価する。

### Phase E: Run Stack View（候補 4）— 将来検討

ISortContext への影響が大きく、既存アルゴリズム実装への変更が必要。候補 3 と同時期に検討する。

---

## TutorialVisualizationHint 全体像（計画完了後）

```csharp
public enum TutorialVisualizationHint
{
    // 既存
    None,
    HeapTree,
    TernaryHeapTree,
    WeakHeapTree,
    BstTree,
    AvlTree,

    // Phase A: Distribution Bucket View
    DigitBucketLsd,      // LSD Radix (b=4, b=10)
    DigitBucketMsd,      // MSD Radix (b=4, b=10), American flag sort
    ValueBucket,         // Pigeonhole, Counting sort, Bucket sort

    // Phase B: Sorting Network Diagram
    SortingNetwork,      // Bitonic sort (2種)

    // Phase D: Recursion Tree（将来）
    // RecursionTree,    // Merge sort, Quicksort 系

    // Phase E: Run Stack（将来）
    // RunStack,         // Timsort, Powersort, ShiftSort
}
```

## TutorialStep プロパティ全体像（計画完了後）

```csharp
public record TutorialStep
{
    // 既存プロパティ（省略）

    // 既存: Heap / BST 用
    public int? HeapBoundary { get; init; }
    public bool[]? WeakHeapReverseBits { get; init; }
    public BstSnapshot? Bst { get; init; }

    // Phase A: Distribution Bucket View
    public DistributionSnapshot? Distribution { get; init; }

    // Phase B: Sorting Network Diagram
    public NetworkSnapshot? Network { get; init; }

    // Phase C: Gap Coloring（MarbleRenderer 拡張）
    public int? ShellGap { get; init; }

    // Phase D: Recursion Tree（将来）
    // public RecursionSnapshot? Recursion { get; init; }

    // Phase E: Run Stack（将来）
    // public RunStackSnapshot? RunStack { get; init; }
}
```
