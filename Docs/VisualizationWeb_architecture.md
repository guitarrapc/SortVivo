# SortViz - ソートアルゴリズム可視化ウェブアプリケーション 仕様書

## 1. プロジェクト概要

ソートアルゴリズムの動作をリアルタイムでグラフィカルに可視化するウェブアプリケーション。教育目的で、ソートアルゴリズムがどのように動作しているかを視覚的に理解できるようにする。

### プロジェクト名
`SortAlgorithm.VisualizationWeb`

### 配置場所
`sandbox/SortAlgorithm.VisualizationWeb/`

### サイト名

SortViz

### 技術スタック
- **フロントエンド**: Blazor WebAssembly (.NET 10)
- **グラフィックス**: HTML5 Canvas / SVG
- **状態管理**: Blazor Component State
- **アニメーション**: JavaScript Interop (requestAnimationFrame)

## 2. 機能要件

### 2.1 ソート動作の可視化

#### 2.1.1 可視化モード

複数の可視化モードをサポートし、ユーザーが切り替え可能：

**1. 棒グラフモード（Bar Chart Mode）**
- 配列の各要素を垂直の棒グラフ（バー）として表示
- バーの高さ = 要素の値
- バーの幅 = 画面サイズと要素数に応じて自動調整
- 画面の大部分（80-90%）を可視化エリアとして使用
- 添付画像のように、メイン配列を画面右側に大きく表示

**2. 円形モード（Circular Mode）**
- 配列要素を円形（または楕円形）に配置
- 各要素を円周上の線分や扇形として表示
- 要素の値に応じて色のグラデーション（HSV/HSL）を使用
- 中心から外側に向かって線分を描画
- 添付画像の円形可視化のような表現

**3. 螺旋モード（Spiral Mode）** ※将来的な拡張
- 要素を螺旋状に配置
- ソートの進行を視覚的に追跡しやすい

**4. ドットモード（Dot Plot Mode）** ※将来的な拡張
- 散布図のように要素を点として表示
- 位置の移動を視覚化

**5. 不均衡和音モード (Disparity Chords Mode)**
- 配列要素の現在位置と、本来あるべき位置との差を線で見せるタイプ
- どれだけ遠くにずれている要素が残っているかを強調する。
- 次のようなイメージで、要素の位置ずれを線の長さで表現
  - 各要素 = 円周上の点
  - その要素の「今いる位置」と「並び替え後の位置」の関係 = chord（弦・線）」
  - 線が長いほど、位置ずれが大きい
  - 線が減っていくほど、整列に近づく

**6. 画像行モード（Picture Row Mode）**
- アップロードされた画像を行ごとに分割して要素として扱う
- 画像行をソートして、ソートの進行を画像の変化として視覚化

**7. 画像列モード（Picture Column Mode）**
- アップロードされた画像を列ごとに分割して要素として扱う
- 画像列をソートして、ソートの進行を画像の変化として視覚化

**8. 画像ブロックモード（Picture Block Mode）**
- アップロードされた画像を分割数ごとのブロックに分割して要素として扱う
- 画像列をソートして、ソートの進行を画像の変化として視覚化

#### 2.1.2 表示領域の最大化
- **可視化エリア**: 画面の80-90%を使用（添付画像参照）
- **統計情報エリア**: 画面左側または下部に配置（10-20%）
- **コントロールパネル**: コンパクトに配置（オーバーレイまたは固定ヘッダー）
- レスポンシブ対応で画面サイズに応じて自動調整

#### 2.1.3 操作ごとの色分け
各操作タイプに応じてバーの色を動的に変更：

| 操作タイプ | 色 | 説明 |
|----------|-----|------|
| 通常状態 | `#3B82F6` (青) | 操作対象外の要素 |
| Index Read | `#FBBF24` (黄) | インデックス読み込みアクセス |
| Index Write | `#F97316` (橙) | インデックス書き込みアクセス |
| Compare | `#A855F7` (紫) | 比較操作中の要素 |
| Swap | `#EF4444` (赤) | スワップ操作中の要素 |
| Sorted | `#10B981` (緑) | ソート済み要素 |
| Buffer | `#6B7280` (灰) | バッファー配列の要素 |

#### 2.1.4 複数配列の可視化

**棒グラフモードの場合:**
- **メイン配列**: 画面右側の大部分を占める主要領域（添付画像参照）
- **バッファー配列**: メイン配列の上部または下部に独立した領域として表示
- 各配列は水平方向に並べるか、垂直に積み重ねて表示
- バッファーIDに基づいて配列を識別

**円形モードの場合:**
- **メイン配列**: 中央の大きな円形領域
- **バッファー配列**: 外側の同心円または別の円形領域として表示
- 複数のバッファーがある場合は、複数の同心円リングで表現

### 2.2 再生制御（動画プレイヤー方式）

#### 2.2.1 初期状態
- **停止状態**: アプリケーション起動時、またはリセット後は停止状態
- ソート済みデータが生成され、すべての操作が記録済み
- 可視化エリアには初期状態（ソート前）の配列が表示される

#### 2.2.2 再生/一時停止の切り替え
**グラフクリック操作:**
- **停止中にクリック**: 再生を開始（現在位置から再生）
- **再生中にクリック**: 一時停止（現在位置で停止）
- **可視化エリア全体がクリック可能**（棒グラフ、円形表示どちらも）

**視覚的フィードバック:**
- 停止中: カーソルホバーで再生アイコン（▶）を表示
- 再生中: カーソルホバーで一時停止アイコン（⏸）を表示

#### 2.2.3 シークバー（タイムラインスクラバー）
**機能:**
- ソート操作の進捗を視覚的に表示
- ドラッグまたはクリックで任意の位置にジャンプ可能
- 操作総数に対する現在位置をパーセンテージで表示

**UI要素:**
```
[━━━━━━━●━━━━━━━━━━] 45% (操作 2,345 / 5,200)
0:00:15.234 / 0:00:34.000
```

**操作:**
- **クリック**: クリック位置にジャンプ
- **ドラッグ**: スクラバーをドラッグして細かく位置調整
- **キーボード**: 左右矢印キーで前後に移動（オプション）

#### 2.2.4 再生速度設定
- **方式**: Operations Per Frame（1フレームあたりの操作数）
- **フレームレート**: 60 FPS固定
- **設定範囲**: 1〜1000 ops/frame
- **デフォルト**: 10 ops/frame（600 ops/sec）
- **UI**: スライダーで調整
- **実効速度**:
  - 1 ops/frame = 60 ops/sec（詳細観察用）
  - 10 ops/frame = 600 ops/sec（標準速度）
  - 100 ops/frame = 6,000 ops/sec（高速）
  - 1000 ops/frame = 60,000 ops/sec（超高速）

**速度の目安（QuickSort 256要素、約2,000操作の場合）:**
- 1 ops/frame: 約33秒
- 10 ops/frame: 約3.3秒
- 100 ops/frame: 約0.33秒
- 1000 ops/frame: 約0.03秒

#### 2.2.5 制御ボタン
- **[⏹ Reset]**: 初期状態に戻す（停止状態、シークバーを先頭に戻す）
  - ショートカット: `R` キー（オプション）

**削除されるボタン（グラフクリックとシークバーで代替）:**
- ~~再生ボタン~~ → グラフクリックで再生
- ~~一時停止ボタン~~ → グラフクリックで一時停止
- ~~ステップ実行ボタン~~ → シークバーで細かく調整可能
- ~~スキップボタン~~ → シークバーで最後にジャンプ

### 2.3 要素数の制限

アルゴリズムの時間計算量に応じて最大要素数を制限：

| 時間計算量 | 最大要素数 | 対象アルゴリズム例 |
|-----------|-----------|------------------|
| O(n!) ~ O(∞) | 10 | BogoSort, SlowSort, StoogeSort (Joke Sorts) |
| O(n²) | 256 | BubbleSort, SelectionSort, InsertionSort, OddEvenSort, CocktailShakerSort, GnomeSort, DoubleSelectionSort, CycleSort, PancakeSort, BinaryInsertSort |
| O(n log n) ~ O(nk) | 2048 | QuickSort系, MergeSort系, HeapSort系, IntroSort, PDQSort, StdSort, TimSort, PowerSort, ShellSort, CountingSort, BucketSort, RadixSort系, BitonicSort系, TreeSort系 |

**特別な制限**:
- Joke Sortsは極端に遅いため、要素数を10に制限
- 並列ソート（BitonicSortParallel）は要素数を2のべき乗に自動調整

### 2.4 統計情報の表示

ソート実行中に以下の情報をリアルタイム更新：

```
┌─────────────────────────────────────┐
│ Algorithm: QuickSort                │
│ Array Size: 512                     │
│ Status: Sorting... (45%)            │
├─────────────────────────────────────┤
│ Main Array Operations:              │
│   - Comparisons:     1,234          │
│   - Swaps:             567          │
│   - Index Reads:     2,345          │
│   - Index Writes:    1,456          │
├─────────────────────────────────────┤
│ Buffer Array Operations:            │
│   - Index Reads:       512          │
│   - Index Writes:      512          │
├─────────────────────────────────────┤
│ Elapsed Time: 00:00:15.234          │
│ Playback Speed: 30 FPS              │
└─────────────────────────────────────┘
```

### 2.5 アルゴリズム選択

実装されているすべてのソートアルゴリズムから選択可能：

1. **Exchange Sorts** (交換ソート) - O(n²)
   - BubbleSort
   - CocktailShakerSort
   - CombSort
   - OddEvenSort

2. **Selection Sorts** (選択ソート) - O(n²)
   - SelectionSort
   - DoubleSelectionSort
   - CycleSort
   - PancakeSort

3. **Insertion Sorts** (挿入ソート) - O(n²) ~ O(n log n)
   - InsertionSort
   - BinaryInsertSort
   - ShellSort
   - GnomeSort

4. **Merge Sorts** (マージソート) - O(n log n)
   - MergeSort
   - TimSort
   - PowerSort
   - ShiftSort

5. **Heap Sorts** (ヒープソート) - O(n log n)
   - HeapSort
   - BottomupHeapSort
   - WeakHeapSort
   - SmoothSort
   - TernaryHeapSort

6. **Partition Sorts** (分割ソート) - O(n log n)
   - QuickSort
   - QuickSortMedian3
   - QuickSortMedian9
   - QuickSortDualPivot
   - BlockQuickSort
   - StableQuickSort
   - IntroSort
   - PDQSort
   - StdSort

7. **Adaptive Sorts** (適応ソート) - O(n log n)
   - DropMergeSort

8. **Distribution Sorts** (分散ソート) - O(n) ~ O(nk)
   - CountingSort
   - BucketSort
   - RadixLSD4Sort
   - RadixLSD10Sort

9. **Network Sorts** (ソーティングネットワーク) - O(log²n)
   - BitonicSort
   - BitonicSortFill
   - BitonicSortParallel

10. **Tree Sorts** (ツリーソート) - O(n log n)
   - BinaryTreeSort
   - BalancedBinaryTreeSort

11. **Joke Sorts** (ジョークソート) - O(n!) ~ O(∞)
    - BogoSort
    - SlowSort
    - StoogeSort

**注意**: Joke Sortsカテゴリは教育目的のみで、実用性はありません。可視化でも要素数を極めて小さく制限（例: 最大10要素）します。

## 3. 技術設計

### 3.1 アーキテクチャ

```
┌─────────────────────────────────────────────────────────┐
│                   Blazor WebAssembly                    │
├─────────────────────────────────────────────────────────┤
│  UI Components                                          │
│  ├─ VisualizationPage.razor (メインページ)              │
│  ├─ SortCanvas.razor (Canvas描画コンポーネント)         │
│  │   ├─ BarChartRenderer (棒グラフ描画)                 │
│  │   └─ CircularRenderer (円形描画)                     │
│  ├─ SeekBar.razor (タイムラインシークバー)              │
│  ├─ StatisticsPanel.razor (統計情報表示)                │
│  └─ AlgorithmSelector.razor (アルゴリズム選択)          │
├─────────────────────────────────────────────────────────┤
│  Services                                               │
│  ├─ VisualizationService (可視化ロジック)               │
│  ├─ PlaybackService (再生制御・シーク処理)              │
│  ├─ AnimationService (アニメーション制御)                │
│  └─ SortExecutor (ソート実行と操作記録)                 │
├─────────────────────────────────────────────────────────┤
│  Models                                                 │
│  ├─ SortOperation (操作イベント)                        │
│  ├─ VisualizationState (可視化状態)                     │
│  └─ ArraySnapshot (配列スナップショット)                │
├─────────────────────────────────────────────────────────┤
│  JavaScript Interop                                     │
│  └─ CanvasRenderer.js (Canvas描画最適化)                │
└─────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────┐
│            SortAlgorithm Library (既存)                 │
│  ├─ ISortAlgorithm<T>                                   │
│  ├─ VisualizationContext (操作コールバック)             │
│  └─ StatisticsContext (統計収集)                        │
└─────────────────────────────────────────────────────────┘
```

### 3.2 データフロー

#### 3.2.1 操作記録フェーズ
1. ユーザーがアルゴリズムと配列サイズを選択
2. `SortExecutor` が `VisualizationContext` を使用してソート実行
3. すべての操作（Compare, Swap, Read, Write, RangeCopy）をイベントとして記録
4. 操作イベントリストを `AnimationService` に渡す

#### 3.2.2 再生フェーズ（シークバー方式）
1. 初期状態: `PlaybackService` は停止状態、シークバーは位置0
2. **グラフクリック** または **シークバークリック** で再生開始
3. `PlaybackService` が FPS に基づいてタイマーを設定
4. 各フレームで次の操作イベントを取得し、現在位置を進める
5. `VisualizationState` を更新（操作対象インデックス、色情報）
6. `SortCanvas` が状態変化を検知して再描画
7. `SeekBar` と `StatisticsPanel` が統計情報と進捗を更新
8. **グラフクリック** で一時停止、**シークバードラッグ** で位置変更

#### 3.2.3 シーク処理
1. ユーザーがシークバーをクリック/ドラッグ
2. `PlaybackService` が指定された操作インデックスに移動
3. その時点までの操作を順次適用して配列状態を再構築
4. `VisualizationState` を更新して画面を再描画
5. 再生状態（再生中/停止中）は維持される

### 3.3 主要データモデル

#### SortOperation
```csharp
public enum OperationType
{
    Compare,
    Swap,
    IndexRead,
    IndexWrite,
    RangeCopy
}

public record SortOperation
{
    public OperationType Type { get; init; }
    public int Index1 { get; init; }
    public int Index2 { get; init; }
    public int BufferId1 { get; init; }
    public int BufferId2 { get; init; }
    public int Length { get; init; } // For RangeCopy
    public int[]? ArraySnapshot { get; init; } // Optional: 配列のスナップショット
}
```

#### VisualizationState
```csharp
public enum VisualizationMode
{
    BarChart,
    Circular,
    Spiral,
    DotPlot
}

public class VisualizationState
{
    public int[] MainArray { get; set; }
    public Dictionary<int, int[]> BufferArrays { get; set; } // BufferId -> Array
    public HashSet<int> CompareIndices { get; set; }
    public HashSet<int> SwapIndices { get; set; }
    public HashSet<int> ReadIndices { get; set; }
    public HashSet<int> WriteIndices { get; set; }
    public int CurrentOperationIndex { get; set; }
    public int TotalOperations { get; set; }
    public SortStatistics Statistics { get; set; }
    public VisualizationMode Mode { get; set; } = VisualizationMode.BarChart;
    public PlaybackState PlaybackState { get; set; } = PlaybackState.Stopped;
}

public enum PlaybackState
{
    Stopped,  // 停止中（初期状態、リセット後）
    Playing,  // 再生中
    Paused    // 一時停止中
}
```

### 3.4 VisualizationContext の活用

既存の `VisualizationContext` を使用して操作を記録：

```csharp
var operations = new List<SortOperation>();
var context = new VisualizationContext(
    onCompare: (i, j, result, bufferIdI, bufferIdJ) =>
    {
        operations.Add(new SortOperation
        {
            Type = OperationType.Compare,
            Index1 = i,
            Index2 = j,
            BufferId1 = bufferIdI,
            BufferId2 = bufferIdJ
        });
    },
    onSwap: (i, j, bufferId) =>
    {
        operations.Add(new SortOperation
        {
            Type = OperationType.Swap,
            Index1 = i,
            Index2 = j,
            BufferId1 = bufferId
        });
    },
    onIndexRead: (index, bufferId) =>
    {
        operations.Add(new SortOperation
        {
            Type = OperationType.IndexRead,
            Index1 = index,
            BufferId1 = bufferId
        });
    },
    onIndexWrite: (index, bufferId) =>
    {
        operations.Add(new SortOperation
        {
            Type = OperationType.IndexWrite,
            Index1 = index,
            BufferId1 = bufferId
        });
    },
    onRangeCopy: (sourceIndex, destIndex, length, sourceBufferId, destBufferId) =>
    {
        operations.Add(new SortOperation
        {
            Type = OperationType.RangeCopy,
            Index1 = sourceIndex,
            Index2 = destIndex,
            Length = length,
            BufferId1 = sourceBufferId,
            BufferId2 = destBufferId
        });
    }
);

// ソート実行
var span = new SortSpan<int>(array, context);
sortAlgorithm.Sort(span);
```

## 4. UI/UX 設計

### 4.1 レイアウト（棒グラフモード）

添付画像を参考に、可視化エリアを画面の大部分に配置：

```
┌─────────────────────────────────────────────────────────────────────────┐
│  SortViz                                   [Bar][Circle] Mode    [?][⚙]│
├──────────────────────────┬──────────────────────────────────────────────┤
│ Algorithm: QuickSort     │                                              │
│ Size: [512 ▼] [Generate] │  ┌────────────────────────────────────────┐  │
│                          │  │ Click to Play/Pause                    │  │
│ ────────────────────     │  │         Main Array (Interactive)       │  │
│ Statistics               │  │ ▂▄▆█▇▅▃▁▃▅▇█▆▄▂▁▃▅▇█▆▄▂▁▃▅▇█▆▄▂       │  │
│ ────────────────────     │  │ ││││││││││││││││││││││││││││││││││     │  │
│ Comparisons              │  │ ││││││││││││││││││││││││││││││││││     │  │
│ 141,473                  │  │ ││││││││││││││││││││││││││││││││││     │  │
│                          │  │ ││││││││││││││││││││││││││││││││││     │  │
│ Swaps                    │  │ ││││││││││││││││││││││││││││││││││     │  │
│ 6,892                    │  │ ││││││││││││││││││││││││││││││││││     │  │
│                          │  └────────────────────────────────────────┘  │
│ Index Reads              │                                              │
│ 87,308                   │  Buffer Array (if applicable)                │
│                          │  ┌────────────────────────────────────────┐  │
│ Index Writes             │  │ ▁▂▃▄▅▆▇█▇▆▅▄▃▂▁                         │  │
│ 102,157                  │  └────────────────────────────────────────┘  │
│                          │                                              │
│ Buffer Writes            ├──────────────────────────────────────────────┤
│ 102,157                  │  ◀─────────────●─────────────▶ 45%          │
│                          │  [⏹ Reset]  Speed: [━━━━●━━] 30 FPS         │
│ Progress: 45%            │  0:15.234 / 0:34.000 (2,345 / 5,200 ops)    │
│ Status: Sorting...       │                                              │
└──────────────────────────┴──────────────────────────────────────────────┘

【レイアウトの特徴】
- 左側（20-25%）: アルゴリズム選択、統計情報、設定
- 右側（75-80%）: 可視化エリア（メイン配列 + バッファー配列）
- 下部: 再生コントロール（固定またはフローティング）
```

### 4.2 レイアウト（円形モード）

```
┌─────────────────────────────────────────────────────────────────────────┐
│  SortViz                                   [Bar][Circle] Mode    [?][⚙]│
├──────────────────────────┬──────────────────────────────────────────────┤
│ Algorithm: InsertionSort │                                              │
│ Size: [256 ▼] [Generate] │  ┌────────────────────────────────────────┐  │
│                          │  │ Click to Play/Pause                    │  │
│ ────────────────────     │  │      Circular Visualization            │  │
│ Statistics               │  │                                        │  │
│ ────────────────────     │  │            ╱───╲                       │  │
│ Comparisons              │  │          ╱       ╲                     │  │
│ 1,234                    │  │        ╱    ●     ╲                    │  │
│                          │  │       │            │                   │  │
│ Swaps                    │  │       │   Array    │                   │  │
│ 567                      │  │       │            │                   │  │
│                          │  │        ╲         ╱                     │  │
│ Progress: 23%            │  │          ╲─────╱                       │  │
│ Status: Sorting...       │  │                                        │  │
│                          │  │   (円形に配置された要素)               │  │
│                          │  │  色のグラデーションで値を表現          │  │
│                          │  │  操作中の要素を強調表示                │  │
│                          │  └────────────────────────────────────────┘  │
├──────────────────────────┼──────────────────────────────────────────────┤
│                          │  ◀─────────────●─────────────▶ 23%          │
│                          │  [⏹ Reset]  Speed: [━━━━●━━] 30 FPS         │
│                          │  0:05.123 / 0:22.000 (567 / 2,456 ops)      │
└──────────────────────────┴──────────────────────────────────────────────┘
```

### 4.3 レスポンシブ対応

**デスクトップ（1920x1080以上）**
- 2カラムレイアウト: 左側に統計、右側に大きな可視化エリア
- 可視化エリア: 画面の75-80%を占める
- 統計情報とコントロールは固定サイドバー

**タブレット（768px - 1280px）**
- 可視化エリアを上部60-70%に配置
- 統計情報を下部に折りたたみ可能なパネルとして配置
- コントロールはフローティングバーとして表示

**モバイル（〜767px）**
- 可視化エリアをフルスクリーン表示
- 統計情報は下部スワイプアップパネル
- コントロールは最小化されたフローティングボタン
- 円形モードは画面サイズに応じて縮小

### 4.4 可視化モード切り替え

- トグルボタンまたはドロップダウンで可視化モードを切り替え
- `[Bar Graph] [Circular] [Spiral] [Dot Plot]`
- 各モードで操作の可視化方法が異なる：
  - **棒グラフ**: バーの色変化でアクセスを表現
  - **円形**: 円周上の線分の色や太さで表現
  - **螺旋**: 螺旋上の点の移動で表現
  - **ドット**: 散布図上の点の位置変化で表現

### 4.5 色とテーマ

**ダークモード（デフォルト）** - 添付画像参照
- 背景: `#1A1A1A` (ダークグレー)
- テキスト: `#FFFFFF` (白)
- 操作色: 前述の色分けスキーム（鮮やかな色）

**ライトモード**
- 背景: `#FFFFFF` (白)
- テキスト: `#1A1A1A` (ダークグレー)
- 操作色: やや彩度を下げた色

**円形モードの色**
- 値に応じてHSLカラーグラデーション
- 例: 小さい値（赤） → 中間値（緑/黄） → 大きい値（青/紫）
- 操作中は輝度やアルファ値で強調

### 4.6 チュートリアル

チュートリアルページを別途用意して、基本的なソートアルゴリズムの動作を少数のマーブルアニメーションで説明するコンテンツを提供します。

VisualizationWeb_tutorial.md参照

## 5. パフォーマンス要件

### 5.1 描画最適化
- **Canvas描画**: HTML5 Canvasを使用した高速レンダリング
  - 棒グラフモード: 2D Canvas API
  - 円形モード: Canvas Arc/Line API または WebGL（将来）
- **差分更新**: 変更があった要素のみ再描画
- **requestAnimationFrame**: ブラウザの再描画サイクルに同期
- **オフスクリーンキャンバス**: バックグラウンドでの前処理（将来）
- **描画バッチング**: 複数操作をまとめて描画して再描画回数を削減

### 5.2 メモリ管理
- 大量の操作イベント（O(n²)で最大256²=65,536イベント）を効率的に管理
- 配列スナップショットは必要最小限に抑える（デバッグモード時のみ）
- 再生完了後はイベントリストをクリア
- 円形モードでは線分描画を最適化（事前計算とキャッシュ）

### 5.3 応答性
- Web Workerを使用してソート実行をバックグラウンド化（将来的な拡張）
- UIスレッドをブロックしない非同期処理
- 大量の要素（1024以上）では描画のスロットリング

## 6. 開発ステップ

### Phase 1: 基本構造
1. Blazor WebAssembly プロジェクト作成
2. SortAlgorithm プロジェクトへの参照追加
3. 基本的なUIレイアウト構築（2カラムデザイン）

### Phase 2: 操作記録機能
1. `SortOperation` モデル定義
2. `VisualizationMode` 列挙型定義
3. `VisualizationContext` を使った操作記録実装
4. 単一アルゴリズム（例: BubbleSort）での動作確認

### Phase 3: 棒グラフモード可視化
1. Canvas描画コンポーネント実装（棒グラフ専用）
2. 棒グラフレンダリング
3. 色分け表示ロジック
4. 複数配列（メイン+バッファー）表示
5. 画面の75-80%を占める大きな表示エリアの実装

### Phase 4: 円形モード可視化
1. 円形描画コンポーネント実装
2. 円周上に要素を配置するアルゴリズム
3. HSLグラデーションカラーリング
4. 操作のアニメーション効果
5. 複数配列の同心円表示

### Phase 5: アニメーション制御
1. FPS制御機能
2. 再生/一時停止/ステップ実行
3. 進捗管理
4. モード切り替え機能

### Phase 6: 統計情報とUI改善
1. リアルタイム統計情報表示（サイドバー）
2. アルゴリズム選択UI（全52種類）
3. レスポンシブデザイン対応
4. ダーク/ライトモード切り替え

### Phase 7: 高度な機能
1. 配列パターン選択（ランダム、逆順、ほぼソート済みなど）
2. 複数アルゴリズム比較モード
3. 螺旋モード、ドットモードの実装（オプション）
4. エクスポート機能（GIF/動画）（オプション）

## 7. テスト戦略

### 7.1 単体テスト
- `SortExecutor` の操作記録精度
- `AnimationService` のFPS制御精度
- 各操作タイプの正確な色分け

### 7.2 統合テスト
- 全アルゴリズムでの完全な可視化フロー
- 異なる配列サイズでのパフォーマンス

### 7.3 手動テスト
- ブラウザ互換性（Chrome, Firefox, Edge, Safari）
- モバイルデバイスでの操作性

## 8. 将来的な拡張

1. **比較モード**: 2つのアルゴリズムを並べて比較
2. **カスタムデータ**: ユーザーが独自の配列を入力
3. **音響フィードバック**: 操作に応じた音を再生（Sound of Sortingスタイル）
4. **教育コンテンツ**: アルゴリズムの説明、計算量の解説
5. **録画機能**: アニメーションをGIFや動画として保存
6. **コード表示**: 実行中のコードをハイライト表示
7. **3D可視化**: WebGLを使った3D表現
8. **VRモード**: VRヘッドセット対応の没入型可視化

## 9. 参考資料

### 類似プロジェクト（デザイン参考）
- [VisuAlgo](https://visualgo.net/en/sorting) - 教育的な可視化
- [Sorting.at](http://sorting.at/) - 円形可視化の先駆け
- [Sound of Sorting](https://github.com/bingmann/sound-of-sorting) - 音響付き可視化
- [Sorting Visualizer](https://www.sortvisualizer.com/) - 多様な表示モード

### 技術ドキュメント
- [Blazor WebAssembly](https://learn.microsoft.com/aspnet/core/blazor/)
- [HTML5 Canvas](https://developer.mozilla.org/docs/Web/API/Canvas_API)
- [Canvas Tutorial](https://developer.mozilla.org/docs/Web/API/Canvas_API/Tutorial)
- [requestAnimationFrame](https://developer.mozilla.org/docs/Web/API/window/requestAnimationFrame)
- [CSS Grid Layout](https://developer.mozilla.org/docs/Web/CSS/CSS_Grid_Layout) - レスポンシブレイアウト

## 10. デザインガイドライン

### 添付画像からの要件
1. **画面占有率**: 可視化エリアは画面の75-80%を占める
2. **ダークテーマ**: 黒背景に鮮やかな色でコントラストを強調
3. **統計情報**: 左側サイドバーに整理して配置
4. **円形表示**: 添付画像1のような楕円形・円形の可視化をサポート
5. **棒グラフ表示**: 添付画像2のような従来の棒グラフも大きく表示
6. **カラフル**: 値に応じたグラデーションまたは操作に応じた色分け

### 参照画像の実装ポイント

**画像1（円形モード）:**
- 円周上に要素を放射状に配置
- HSLカラーモデルで色相環を表現（0° = 赤、120° = 緑、240° = 青）
- ソート進行に伴い、色の並びが整列していく様子を可視化
- 操作中の要素は太線や異なる色で強調

**画像2（棒グラフモード）:**
- 画面右側の大部分を棒グラフ表示に使用
- 左側に統計情報を縦に配置
- 白い背景に黒い棒グラフで見やすく表示
- メイン配列とAuxiliary配列を分けて表示

---

**Document Version**: 1.1
**Last Updated**: 2024
**Author**: SortAlgorithmLab Team
**Changelog**: v1.1 - 可視化モード（棒グラフ/円形）追加、画面レイアウト最適化、添付画像参照仕様追加
