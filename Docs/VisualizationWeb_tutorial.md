### Phase 4.6 チュートリアル仕様書

チュートリアルページを別途用意して、基本的なソートアルゴリズムの動作を少数のマーブルアニメーションで説明するコンテンツを提供します。

#### パス設計

```
/tutorial              → TutorialPage.razor（アルゴリズム選択画面）
/tutorial/{algorithm}  → TutorialPage.razor（チュートリアル実行画面）
/                      → Index.razor（既存の可視化ページ）
```

#### 概要

- 典型的なソートアルゴリズムの動作を少数のマーブルアニメーションで説明するチュートリアル
- 画面上部: アルゴリズムの説明、計算量の解説を表示する（`AlgorithmMetadata` に `TutorialDescription` フィールドを追加）
- 画面下部: [RxMarbles](https://rxmarbles.com/) のような、少数のマーブルを使ったソートの仕組みをステップごとに説明するものをイメージ

#### 要素数

- **固定8要素**（チュートリアル専用の固定値）
- 初期配列は毎回同じ固定値を使用する: `[5, 3, 8, 1, 9, 2, 7, 4]`
  - ランダムにしないことで「説明とアニメーションの対応」が常に一致する
  - 全値が1〜9の1桁で、重複なし（マーブルに数値を表示したとき見やすい）

#### ステップの粒度

チュートリアル用に `SortOperation` を**論理ステップ**へグループ化する。1 `TutorialStep` = ユーザーが「なるほど」と理解できる1アクション：

| 操作タイプ | グループ化ルール | 例 |
|---|---|---|
| Compare | 1回の比較 = 1ステップ | 「インデックス2と3を比較」 |
| Swap | 直前の Compare と合わせて1ステップ | 「3 > 1 なので入れ替え」 |
| IndexRead + IndexWrite | 連続した読み書きを1ステップ | 「値5を一時保存」 |
| RangeCopy | 1回のコピー範囲 = 1ステップ | 「左半分をバッファへコピー」 |

グループ化ロジックは `TutorialStepBuilder` として実装し、`SortOperation` 列から `TutorialStep` 列を生成する。

```csharp
public record TutorialStep
{
    // 対象となる SortOperation のインデックス範囲
    public int OperationStart { get; init; }
    public int OperationEnd { get; init; }

    // このステップ後の配列スナップショット（常に保持）
    public int[] ArraySnapshot { get; init; } = [];
    public Dictionary<int, int[]> BufferSnapshots { get; init; } = new();

    // 強調表示するインデックス（メイン配列）
    public int[] HighlightIndices { get; init; } = [];
    // 強調表示するインデックス（バッファー配列 BufferId -> indices）
    public Dictionary<int, int[]> BufferHighlightIndices { get; init; } = new();

    // ハイライトの種類（Compare / Swap / Read / Write）
    public OperationType HighlightType { get; init; }

    // 自動生成ナラティブ（日本語）
    public string Narrative { get; init; } = string.Empty;
}
```

#### ナラティブテキスト（自動生成）

`TutorialStepBuilder` が `SortOperation` の内容から日本語テキストを自動生成する。手書き不要。

| 操作 | 生成テキスト例 |
|---|---|
| Compare (result > 0) | 「位置 {i} の値 {vi} と 位置 {j} の値 {vj} を比較 → {vi} > {vj} なので次へ進む」 |
| Compare (result ≤ 0) | 「位置 {i} の値 {vi} と 位置 {j} の値 {vj} を比較 → {vi} ≤ {vj} なので入れ替えが必要」 |
| Swap | 「位置 {i} と 位置 {j} の値を入れ替える」 |
| IndexRead | 「位置 {i} の値 {v} を読み取る」 |
| IndexWrite | 「位置 {i} に値 {v} を書き込む」 |
| RangeCopy | 「位置 {src}〜{src+len-1} をバッファの位置 {dst} へコピーする」 |

#### マーブルのビジュアル仕様

- **形状**: 直径 56px の円形（PC）、48px（タブレット）、36px（スマホ）
- **数値表示**: マーブル中央に値を数字（1桁）で白文字表示

##### 背景色: HSL カラーグラデーション（値ベース）

マーブルの背景色は**値の大小**に応じた HSL カラーグラデーションで決定する。すべてのマーブルが常に固有の色を持ち、ソート中に値がどこへ移動したかを直感的に追跡できる。

- **計算式**: `hsl(210 × (1 − (value − 1) / (maxValue − 1)), 75%, 42%)`
- **色域**: 小さい値 → 青 (hue=210) 〜 大きい値 → 赤 (hue=0)
- **彩度 / 輝度**: 75% / 42% 固定（白文字とのコントラストを確保しつつ鮮やかさを維持）
- `MaxValue` は配列の最大値から自動計算

| 値の例 (max=9) | Hue | 色味 |
|---|---|---|
| 1 | 210 | 青 |
| 3 | 158 | 青緑 |
| 5 | 105 | 緑 |
| 7 | 53 | 黄緑〜黄 |
| 9 | 0 | 赤 |

##### 操作ハイライト: アウトラインリング（操作ベース）

操作対象のマーブルは**背景色を変えず**、操作タイプに応じた**ボーダーリング + グロー**で強調する。背景色（値）とアウトライン（操作）の2軸を同時に伝える。

| 操作タイプ | ボーダー色 | グロー色 |
|---|---|---|
| Compare | `#A855F7`（紫） | `rgba(168, 85, 247, 0.7)` |
| Swap | `#EF4444`（赤） | `rgba(239, 68, 68, 0.7)` |
| IndexRead | `#FBBF24`（黄） | `rgba(251, 191, 36, 0.7)` |
| IndexWrite | `#F97316`（橙） | `rgba(249, 115, 22, 0.7)` |
| RangeCopy | `#F97316`（橙） | `rgba(249, 115, 22, 0.7)` |

- **ボーダー幅**: 3px（PC）/ 2px（タブレット・スマホ）
- **グロー**: `box-shadow: 0 0 0 4px <グロー色>, 0 0 12px <グロー色 0.4>` の2層リングで高い視認性を確保
- **スケール**: 操作対象は `scale(1.15)`（PC）/ `scale(1.1)`（タブレット・スマホ）でポップアップ
- **アニメーション**: `marble-pop` / `marble-pop-sm` キーフレーム（0.3s / 0.25s）
- **通常状態**: `border: 3px solid transparent`（レイアウトシフト防止）

#### マーブルの順序番号（インデックス表示）

- 各マーブルの**下**に現在の配列インデックス（0〜7）を小さくグレー表示
- 「今どの位置にあるか」を常に把握できるようにする

```
 ┌───┐  ┌───┐  ┌───┐  ┌───┐
 │ 5 │  │ 1 │  │ 8 │  │ 3 │  ...
 └───┘  └───┘  └───┘  └───┘
   0      1      2      3
```

#### バッファー配列の表示

Merge sort など補助配列が必要なアルゴリズムは、バッファー配列をメイン配列の**下段**に同じマーブル形式で表示する。

```
メイン: [5][3][8][1][9][2][7][4]
         0  1  2  3  4  5  6  7

バッファ:[  ][  ][  ][  ]
         0  1  2  3       ← 空マーブル（値なし）は枠線のみ
```

バッファー配列が存在しないアルゴリズムはバッファー行を表示しない。

#### ナビゲーション（ステップ操作）

```
[◀◀ 最初へ]  [◀ 前のステップ]  [▶ 次のステップ]  [▶▶ 最後へ]
[▶ 自動再生 / ⏸ 一時停止]   速度: [遅い ●━━━ 速い]

ステップ: 12 / 47
```

- **[前へ] / [次へ]**: 1ステップ単位で移動
- **[最初へ] / [最後へ]**: 先頭/末尾へジャンプ
- **[自動再生]**: ステップを自動で順送り（一時停止可能）
- **速度スライダー**: ステップ間の待機時間を 200ms〜2000ms で調整、デフォルト 800ms
- キーボード: `←`/`→` で前後ステップ移動

#### レイアウト

```
┌────────────────────────────────────────────────────────────────────────┐
│  [← 可視化へ戻る]          Tutorial: Bubble Sort                       │
├────────────────────────────────────────────────────────────────────────┤
│  [Bubble Sort ▼]  ← アルゴリズム選択ドロップダウン                    │
├────────────────────────────────────────────────────────────────────────┤
│  ┌──────────────────────────────────────────────────────────────────┐  │
│  │ アルゴリズム説明パネル（上部）                                   │  │
│  │ Bubble Sort（バブルソート）                                      │  │
│  │ 隣り合う2要素を比較し、大きい方を右に移動させる操作を繰り返す。 │  │
│  │ 時間計算量: O(n²)  空間計算量: O(1)  安定: ✓                    │  │
│  └──────────────────────────────────────────────────────────────────┘  │
├────────────────────────────────────────────────────────────────────────┤
│  ステップ説明テキスト（中部）                                          │
│  ┌──────────────────────────────────────────────────────────────────┐  │
│  │ 位置 2 の値 8 と 位置 3 の値 1 を比較 → 8 > 1 なので入れ替える  │  │
│  └──────────────────────────────────────────────────────────────────┘  │
├────────────────────────────────────────────────────────────────────────┤
│  マーブルエリア（中部〜下部）                                          │
│                                                                        │
│  メイン配列:                                                           │
│   ┌───┐  ┌───┐  ┌───┐  ┌───┐  ┌───┐  ┌───┐  ┌───┐  ┌───┐           │
│   │ 5 │  │ 3 │  │ 8 │  │ 1 │  │ 9 │  │ 2 │  │ 7 │  │ 4 │           │
│   └───┘  └───┘  └───┘  └───┘  └───┘  └───┘  └───┘  └───┘           │
│     0      1      2      3      4      5      6      7               │
│                                                                        │
│  (バッファー配列: Merge Sortのみ表示)                                  │
│                                                                        │
├────────────────────────────────────────────────────────────────────────┤
│  [◀◀ 最初へ] [◀ 前へ]  ステップ: 12 / 47  [次へ ▶] [最後へ ▶▶]       │
│  [▶ 自動再生]  速度: [━━●━━━━] 800ms                                  │
└────────────────────────────────────────────────────────────────────────┘
```

#### 対象アルゴリズム

全アルゴリズムをチュートリアルで利用可能とするが、**Joke Sorts（Bogo/Slow/Stooge）は除外**する（固定8要素でも操作数が膨大になる可能性があるため）。

チュートリアルページ上のアルゴリズム選択ドロップダウンでは、既存の `AlgorithmRegistry` からカテゴリ順に表示する。

#### `AlgorithmMetadata` への追加

```csharp
/// <summary>チュートリアルでの説明文</summary>
public string TutorialDescription { get; init; } = string.Empty;
```

##### TutorialDescription フォーマット

3つのセクションで構成し、C# Raw String Literal（`"""`）で記述する。

```
How it works: <コアの操作を1文で — 何をする操作か>

Key property: <他のソートとの違い・際立つ性質を1文で — 何がユニークか>

Watch for:
- <OperationType>: <その操作で何を観察すべきか>
- <OperationType>: <その操作で何を観察すべきか>
- [End of pass/phase: フェーズ境界の観察点（必要な場合のみ）]
```

| セクション | 目的 | 備考 |
|---|---|---|
| `How it works` | コアの操作を1文で説明する | 操作動詞（Repeatedly / Scans / Picks / Divides …）で始める |
| `Key property` | 他のソートとの比較・際立つ性質を1文で説明する | 比較優位・制約・計算量の特徴など |
| `Watch for` | アニメーションで注目すべき操作を箇条書きで説明する | `SortOperation` 型名（`Compare` / `Swap` / `IndexRead` / `IndexWrite` / `RangeCopy`）を先頭に置く |

`Watch for` の箇条書きはそのアルゴリズムに関係するもののみ記載（全列挙しない）。`End of pass` などフェーズ区切りの観察点は必要な場合のみ追加する。

##### 記述例

```csharp
// Bubble sort — Compare・Swap が主役で、Swap の方向が明確なケース
tutorialDescription: """
    How it works: Repeatedly compares adjacent pairs and swaps them if they are in the wrong order, with each pass moving the largest unsorted value one step closer to its final position at the end.

    Key property: The simplest O(n²) sort — no lookahead, no memory — which is why it performs the most redundant comparisons of any straightforward sort.

    Watch for:
    - Compare: each adjacent pair is tested left-to-right; when already in order the pointer just advances without any swap
    - Swap: fires only when left > right, nudging the larger value exactly one step rightward
    - End of pass: the active region shrinks by one as the rightmost unsorted element settles into its final position
    """

// Merge sort — RangeCopy・Compare・IndexWrite が揃うケース
tutorialDescription: """
    How it works: Recursively splits the array in half until each sub-array holds a single element, then merges adjacent sub-arrays back together in sorted order.

    Key property: Stable and guarantees O(n log n) in all cases, but requires an auxiliary array of the same size as the input to perform the merge step.

    Watch for:
    - RangeCopy: the left and right halves are copied into a temporary buffer before each merge
    - Compare: the merge reads from both buffer halves simultaneously and always picks the smaller value
    - IndexWrite: sorted values are written back from the buffer to the original array one by one
    """
```

#### アーキテクチャへの影響（新規ファイル）

```
Pages/
  TutorialPage.razor           ← チュートリアルメインページ

Components/
  MarbleRenderer.razor         ← マーブル描画コンポーネント
  TutorialAlgorithmPanel.razor ← 説明パネル（上部）
  TutorialNarrativePanel.razor ← ステップ説明テキスト（中部）
  TutorialControls.razor       ← ナビゲーションボタン群

Services/
  TutorialStepBuilder.cs       ← SortOperation → TutorialStep 変換ロジック

Models/
  TutorialStep.cs              ← TutorialStep レコード定義
```

既存の `SortExecutor`・`AlgorithmRegistry`・`PlaybackService` はそのまま再利用する。
