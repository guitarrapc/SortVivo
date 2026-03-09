### SortViz - Phase 4.6 チュートリアル仕様書

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
| Compare | 1回の比較 = 1ステップ | 「インデックス2と3を比較 → 5 > 3」 |
| Swap | 1回の入れ替え = 1ステップ | 「位置1と位置2の値を入れ替え」 |
| **Insertion (grouped)** | **Read + シフトWrite群 + 挿入Write = 1ステップ** | **「値2をインデックス7→2へ挿入（5要素を右シフト）」** |
| IndexRead | 単独の読み取り = 1ステップ | 「位置3の値を読み取る」 |
| IndexWrite | 単独の書き込み = 1ステップ | 「位置3に値5を書き込む」 |
| RangeCopy | 1回のコピー範囲 = 1ステップ | 「左半分をバッファへコピー」 |

##### Insertion グループ化ロジック

`TutorialStepBuilder.TryDetectInsertionGroup` が Insertion Sort パターンを検出し、複数の `SortOperation` を1つの `TutorialStep` にまとめる。

**検出パターン:**
1. `IndexRead`（メイン配列、値 `v` を読み取り）
2. `Compare` / `IndexWrite`（シフト）が交互に続く
3. 最後の `IndexWrite` が値 `v` を**異なる位置**に書き込む
4. 全体で3操作以上（Read + 少なくとも1つの中間操作 + 挿入Write）

**グループ化前（7ステップ）→ グループ化後（1ステップ）:**
```
Before:                          After:
  Step 1: Read index 7 (val 2)     Step 1: Insert value 2:
  Step 2: Write index 7 ← val 4           move from index 7 to index 2
  Step 3: Write index 6 ← val 7           (shifting 5 elements right)
  Step 4: Write index 5 ← val 9
  Step 5: Write index 4 ← val 1     Result:
  Step 6: Write index 3 ← val 8     [5] [3] [2] [8] [1] [9] [7] [4]
  Step 7: Write index 2 ← val 2          ↑ 挿入先（ハイライト + 矢印）
```

**生成される TutorialStep:**
- `HighlightType`: `IndexWrite`
- `HighlightIndices`: `[destIndex]`（挿入先）
- `WriteSourceIndex`: `sourceIndex`（元の Read 位置）
- `Narrative`: `"Insert value 2: move from index 7 to index 2 (shifting 5 elements right)"`
- `ArraySnapshot`: 全操作適用後の最終状態

グループ化ロジックは `TutorialStepBuilder` として実装し、`SortOperation` 列から `TutorialStep` 列を生成する。

```csharp
public record TutorialStep
{
    public int OperationIndex { get; init; }

    public int[] ArraySnapshot { get; init; } = [];
    public Dictionary<int, int[]> BufferSnapshots { get; init; } = new();

    public int[] HighlightIndices { get; init; } = [];
    public Dictionary<int, int[]> BufferHighlightIndices { get; init; } = new();

    public OperationType HighlightType { get; init; }

    public int? CompareResult { get; init; }
    public int? WriteSourceIndex { get; init; }
    public int? WritePreviousValue { get; init; }

    public string Phase { get; init; } = string.Empty;
    public Dictionary<int, RoleType> Roles { get; init; } = new();

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

#### フェーズ・役割マーカー（Phase / Role Tracker）

アルゴリズムの意味論的なポイントを伝えるため、各ステップに**フェーズ（局面）**と**役割マーカー（Role）**を付与する。

##### フェーズ（Phase）

複数ステップにまたがる「今何をしているか」を1行テキストで表示する。

| アルゴリズム | フェーズテキスト例 |
|---|---|
| Bubble Sort | 「Pass 1/7: bubbling max to position 7」 |
| Selection Sort | 「Find minimum in [0..7]」 |
| Quick Sort | 「Partition [0..7] — pivot at [3]」 |
| Radix LSD | 「Radix pass: digit 0 (0=least significant)」 |
| IntroSort / PDQSort / StdSort / BlockQuickSort | 「Size 6 ≤ 16 → switch to InsertionSort [2..7]」 |
| IntroSort / PDQSort / StdSort / BlockQuickSort | 「Depth limit exceeded → switch to HeapSort [0..7]」 |
| PDQSort | 「Already partitioned → try PartialInsertionSort [0..5]」 |
| PDQSort | 「Unbalanced partition (bad=2) → shuffle to break pattern [0..7]」 |

- **UI**: ナラティブパネルの**直上**に `フェーズバー` として表示（薄いグレー背景 + 小フォント）
- **更新タイミング**: フェーズが変わったステップのみ更新。変わらないステップは前のフェーズを継続表示
- **非設定時**: `Phase` が空文字の場合はフェーズバーを非表示

`ISortContext` への追加:

```csharp
void OnPhase(SortPhase phase, int param1 = 0, int param2 = 0, int param3 = 0);
```

`TutorialStepBuilder` の動作:
- `OnPhase` が記録された操作に対応するステップへ `Phase` をセット
- 呼び出しのないステップは直前の `Phase` 値を引き継ぐ

##### 役割マーカー（Role）

特定インデックスに付与する「この要素は今何者か」の情報。マーブルの**上部**に小さいラベルバッジとして表示する。

```csharp
public enum RoleType
{
    None,
    Pivot,
    CurrentMin,
    CurrentMax,
    LeftPointer,
    RightPointer,
}
```

| ロール | 対象アルゴリズム例 | バッジ色 |
|---|---|---|
| `Pivot` | Quick Sort のピボット | 金 `#EAB308` |
| `CurrentMin` | Selection Sort の最小値候補 | 黄 `#FBBF24` |
| `CurrentMax` | Bubble Sort の右端バブル要素 | 橙 `#F97316` |
| `LeftPointer` | 2 ポインタ系の左端 | 水 `#38BDF8` |
| `RightPointer` | 2 ポインタ系の右端 | 緑 `#4ADE80` |

- **表示条件**: `TutorialStep.Roles` に含まれるインデックスのマーブルのみ表示
- **複数ロール**: 同一インデックスに複数ロールは付与しない（最後に設定されたものを使用）
- **ロールのクリア**: `RoleType.None` を指定すると該当インデックスのロールを削除

`ISortContext` への追加:

```csharp
void OnRole(int index, int bufferId, RoleType role);
```

- `index`: ロールを付与する配列インデックス
- `bufferId`: メイン配列（`BUFFER_MAIN`）またはバッファー ID
- `role`: 付与するロール（`None` を指定するとロールをクリア）

`TutorialStepBuilder` の動作:
- `OnRole` が記録された操作に対応するステップへ `Roles` をセット
- ロール情報はステップをまたいで保持（`None` で明示的にクリアするまで継続）

##### データフロー

```
ISortContext（TutorialContext 実装）
  OnPhase(SortPhase.HybridToInsertionSort, 2, 7, 16)
  OnRole(pivotIdx, BUFFER_MAIN, RoleType.Pivot)
        ↓
TutorialStepBuilder.Build(operations)
        ↓
TutorialStep.Phase = "Size 6 ≤ 16 → switch to InsertionSort [2..7]"
TutorialStep.Roles = { 3: Pivot }
        ↓
TutorialPage → TutorialNarrativePanel / MarbleRenderer
  ├ フェーズバー（Phase テキスト）
  └ マーブル上部ロールバッジ
```

##### アーキテクチャへの影響

```
Contexts/
  ISortContext.cs                ← 変更: OnPhase / OnRole メソッド追加
  SortPhase.cs                  ← 変更: HybridToInsertionSort / HybridToHeapSort /
                                         PDQPartialInsertionSort / PDQPatternShuffle 追加

Models/
  RoleType.cs                   ← 新規: RoleType enum 定義
  TutorialStep.cs               ← 変更: Phase / Roles プロパティ追加

Services/
  TutorialStepBuilder.cs        ← 変更: Phase / Roles 伝播ロジック追加

Algorithms/Partition/
  IntroSortDotnet.cs            ← 変更: InsertionSort / HeapSort 切替点に OnPhase 追加
  IntroSort.cs                  ← 変更: InsertionSort / HeapSort 切替点に OnPhase 追加
  PDQSort.cs                    ← 変更: InsertionSort / HeapSort /
                                         PDQPartialInsertionSort / PDQPatternShuffle 切替点に OnPhase 追加
  StdSort.cs                    ← 変更: InsertionSort / HeapSort 切替点に OnPhase 追加
  BlockQuickSort.cs             ← 変更: InsertionSort / HeapSort 切替点に OnPhase 追加

Components/
  MarbleRenderer.razor          ← 変更: Roles パラメータ追加、ロールバッジ表示
  TutorialNarrativePanel.razor  ← 変更: フェーズバー表示追加
```

##### ハイブリッドソートのフェーズ一覧（`SortPhase` 追加値）

| SortPhase 値 | 対象アルゴリズム | param1 | param2 | param3 | フェーズバーテキスト |
|---|---|---|---|---|---|
| `HybridToInsertionSort` | IntroSort / PDQSort / StdSort / BlockQuickSort | left | right (incl.) | threshold | `"Size {size} ≤ {threshold} → switch to InsertionSort [{left}..{right}]"` |
| `HybridToHeapSort` | IntroSort / PDQSort / StdSort / BlockQuickSort | left | right (incl.) | — | `"Depth limit exceeded → switch to HeapSort [{left}..{right}]"` |
| `PDQPartialInsertionSort` | PDQSort のみ | begin | end-1 | — | `"Already partitioned → try PartialInsertionSort [{begin}..{end-1}]"` |
| `PDQPatternShuffle` | PDQSort のみ | begin | end-1 | badAllowed remaining | `"Unbalanced partition (bad={badAllowed}) → shuffle to break pattern [{begin}..{end-1}]"` |

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

##### マーブルリフト（操作対象の浮き上がり）

操作対象のマーブルは行から**上方向に浮き上がる**ことで、どのマーブルが操作中かを直感的に示す。`transform` ではなく `position: relative; top` を使用し、既存の `transform` アニメーション（scale、translateX）との競合を回避する。

```
                >               ← 比較記号（矢印エリア内）
   [5]   ┌───┐   [8]   [1]    ← 操作対象が浮き上がる
         │ 3 │
         └───┘
    0     1     2     3
```

- **CSS クラス**: `.marble-slot--lifted`（ハイライト対象に付与）
- **リフト距離**: `top: -20px`（PC）/ `-14px`（タブレット）/ `-10px`（スマホ）
- **トランジション**: `transition: top 0.25s ease` — ステップ切り替え時に滑らかに浮き上がり/着地
- **レイアウト影響なし**: `position: relative` + `top` は他のマーブルの位置に影響しない
- **全操作タイプに適用**: Compare / Swap / IndexRead / IndexWrite / RangeCopy

##### Swap 矢印（方向インジケーター）

Swap 操作時、マーブル行の**上部**に2本の曲線矢印を SVG で描画し、入れ替えの方向を視覚的に示す。

```
       ╭─────────→──╮     ← 高い弧（左→右）
       │  ╭──←─────╮│     ← 低い弧（右→左）
      [3]          [8]
```

- **表示条件**: `HighlightType == Swap` かつ `HighlightIndices` が2要素のとき
- **矢印色**: `#EF4444`（赤、Swap アウトラインと同色）、不透明度 85%
- **矢印の形状**: SVG 二次ベジエ曲線（`Q` コマンド）+ `marker-end` 矢じり
- **2本の弧**: 高い弧（左→右方向）と低い弧（右→左方向）を入れ子状に描画
- **矢印エリア高さ**: 32px（PC）/ 26px（タブレット）/ 22px（スマホ）— 常に確保してレイアウトシフトを防止
- **出現アニメーション**: `arrow-fade-in` (0.3s ease)
- **座標計算**: viewBox ベースの抽象単位（マーブル1個=100単位）でレスポンシブ対応
- **マーカーID**: コンポーネントインスタンスごとに一意な ID を生成（複数 MarbleRenderer の競合回避）

##### Swap スライドアニメーション

Swap 操作時、入れ替え対象のマーブルが**元の位置からスライドして新しい位置に移動する**アニメーションを再生する。矢印が方向を示し、マーブル自体が動くことで直感的に入れ替えを理解できる。

- **仕組み**: `ArraySnapshot` はスワップ後の状態。CSS `@keyframes marble-slide` で「元の位置 → 現在の位置」をスライドアニメーションする
- **オフセット計算**: `--slide-offset` CSS 変数にスロット数差を設定（例: index 2 ↔ 5 なら +3 と -3）
- **スロット幅**: `--marble-slot-width` CSS 変数をブレークポイントごとに定義
  - PC: `calc(56px + 0.75rem)`
  - タブレット: `calc(48px + 0.5rem)`
  - スマホ: `calc(36px + 0.5rem)`
- **アニメーション**: `marble-slide 0.4s ease-out`（自然な減速感）
- **再トリガー**: Blazor `@key` ディレクティブで Swap 対象スロットにステップ固有のキーを付与し、ステップ変更時に DOM 要素を再生成してアニメーションを確実に再生
  - Swap 対象: `@key="s{StepIndex}-{idx}"` → ステップごとに変化
  - その他: `@key=idx` → 安定（不要な再生成を回避）

##### Compare 記号（比較結果インジケーター）

Compare 操作時、2つの比較対象マーブルの**中間上部**に比較結果記号を SVG テキストで描画する。

```
          >
   [5]   [3]   [8]   [1]
    0     1     2     3
```

- **表示条件**: `HighlightType == Compare` かつ `HighlightIndices` が2要素 かつ `CompareResult` が non-null のとき
- **記号**: `>` / `<` / `=`（左に表示されたマーブルを基準とした比較方向）
- **向きの調整**: `HighlightIndices[0]` が元の `Index1`。画面上で左→右に並べ替えた際、Index1 が右側に来た場合は記号を反転して一貫した表示を保つ
- **記号色**: `#A855F7`（紫、Compare アウトラインと同色）、不透明度 90%
- **フォント**: SVG `<text>` 要素、font-size 36（viewBox 単位）、bold
- **配置**: 2つのマーブル位置の水平中央、矢印エリア内の下寄り
- **出現アニメーション**: `arrow-fade-in` (0.3s ease)（Swap 矢印と共通）

##### データフロー

`TutorialStep.CompareResult`（`int?`）フィールドを追加し、`TutorialStepBuilder` が `SortOperation.CompareResult` をそのまま伝播する。Compare 以外の操作では `null`。

##### IndexWrite 矢印 + ゴースト表示

IndexWrite 操作時、**移動元→移動先の矢印**と**上書き前の値のゴースト**を表示して、値がどこから来てどう変化したかを視覚的に示す。

```
         2 → 3               ← 移動元から移動先への片方向弧矢印（橙）
   [8]   [3]   [5]   [1]
    0     1     2     3
                3→5          ← ゴースト: インデックスの下に表示
```

**矢印（移動元インジケーター）:**
- **表示条件**: `HighlightType == IndexWrite` かつ `WriteSourceIndex` が non-null かつ 0 以上のとき
- **矢印色**: `#F97316`（橙、IndexWrite アウトラインと同色）
- **矢印の形状**: SVG 二次ベジエ曲線（`Q` コマンド）+ `marker-end` 矢じり（片方向）
- **出現アニメーション**: `arrow-fade-in` (0.3s ease)

**IndexWrite アウトライン強化:**
- **ボーダー色**: `#FB923C`（明るい橙）、幅 **4px**（他の操作より太い）
- **グロー**: `0 0 0 5px rgba(249,115,22,0.75)` + `0 0 18px rgba(249,115,22,0.5)` の2層（より広く・強く）
- **スケール**: `scale(1.2)`（他の操作の 1.15 より大きい）
- **アニメーション**: `marble-write-pop 0.4s ease`（Write 専用キーフレーム）
  - `0%→25%`: scale(1)→scale(1.35) + オレンジグロー爆発
  - `25%→60%`: scale(1.35)→scale(1.08)（スタンプで押し込む感覚）
  - `60%→100%`: scale(1.08)→scale(1.2) で定着

**ゴースト（上書き前の値）:**
- **表示条件**: Write 先の値が実際に変化した場合のみ表示（`WritePreviousValue != 現在値`）
- **表示形式**: `前の値 → 新しい値`（例: `3→5`）
- **配置**: マーブルスロット内の**インデックスラベルの下**（重なり防止）
  - 並び順: `[marble] → [index] → [ghost]`
- **スタイル**: monospace、**0.8rem**、font-weight 600、色 `#FB923C`（橙）、opacity 1.0

**データフロー:**
- `TutorialStep.WriteSourceIndex`（`int?`）: `TutorialStepBuilder` が操作前の配列から書き込み値と同じ値を持つインデックスを検索
- `TutorialStep.WritePreviousValue`（`int?`）: 操作前の書き込み先にあった値

#### マーブルの順序番号（インデックス表示）

- 各マーブルの**下**に現在の配列インデックス（0〜7）を小さくグレー表示
- IndexWrite 操作時はインデックスの**さらに下**にゴースト（`前の値→新しい値`）を表示

```
 ┌───┐  ┌───┐  ┌───┐  ┌───┐
 │ 5 │  │ 1 │  │ 8 │  │ 3 │  ...
 └───┘  └───┘  └───┘  └───┘
   0      1      2      3
                3→8          ← Write 時のみ表示
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
- **速度スライダー**: 5段階のラベル選択（スライダー UI）、デフォルト Normal

  | ラベル | インターバル |
  |---|---|
  | Fastest | 100 ms |
  | Fast | 250 ms |
  | Normal | 500 ms |
  | Slow | 1000 ms |
  | Slowest | 2000 ms |

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
│  📍 パス 1/7：最大値を右端へバブル中（フェーズバー、Phase 非空時のみ）  │
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

#### ヒープ木表示（Heap Sort 専用ビジュアライゼーション）

Heap Sort カテゴリのアルゴリズム（Heapsort / Bottom-up heapsort）では、マーブル表示に加えて**ヒープ木表示**に切り替え可能とする。配列を二分ヒープの木構造として SVG で描画し、ヒープ構築・抽出の過程を直感的に示す。

##### 対象アルゴリズム

| アルゴリズム | ヒープ木表示 | 備考 |
|---|---|---|
| Heapsort | ✅ | 標準二分ヒープ（子: `2i+1`, `2i+2`） |
| Bottom-up heapsort | ✅ | 同上 |
| Ternary heapsort | ✅ | 三分木（子: `3i+1`, `3i+2`, `3i+3`）、`BranchingFactor=3` で描画 |
| Weak heapsort | ✅ | 二分木レイアウト + reverse bit による distinguished / non-distinguished 辺区別 |
| Smoothsort | ❌（将来対応） | Leonardo ヒープの森で標準二分ヒープと構造が異なる |

##### ヒープ木のレイアウト

配列インデックスからヒープの親子関係を計算し、SVG で二分木を描画する。

```
配列: [9, 7, 8, 1, 5, 2, 4, 3]
ヒープ境界: 8（全要素がヒープ内）

            (9)              ← arr[0] (root)
           /   \
         (7)   (8)           ← arr[1], arr[2]
        / \    / \
      (1) (5)(2) (4)         ← arr[3]~arr[6]
      /
    (3)                      ← arr[7]

抽出後: ヒープ境界 = 7
配列: [8, 7, 4, 1, 5, 2, 3, |9]
                              ↑ ソート済み（木から除外、薄く表示）
            (8)
           /   \
         (7)   (4)
        / \    /
      (1) (5)(2)
      /
    (3)
```

- **親子関係**: `parent(i) = (i-1) / b`、`child_k(i) = b*i+1+k`（`b` = 分岐数、`k = 0..b-1`）
- **分岐数**: `BranchingFactor` パラメータで二分木（2）・三分木（3）を切り替え
- **ヒープ境界**: `HeapBoundary` で指定。`0 ≤ i < HeapBoundary` がヒープ内のノード
- **ソート済み領域**: `HeapBoundary ≤ i < n` のノードは描画しない（マーブル行にのみ表示）

##### ノードの描画仕様

- **形状**: 直径 42px（PC）/ 36px（タブレット）/ 28px（スマホ）の円形
- **背景色**: マーブルと同じ HSL カラーグラデーション（値ベース）
- **数値表示**: 中央に白文字で値を表示
- **ハイライト**: マーブルと同じアウトラインリング + グロー（Compare: 紫、Swap: 赤、Write: 橙）
- **エッジ（辺）**: 親→子の直線、色 `rgba(255, 255, 255, 0.3)`、幅 1.5px
  - ハイライト対象の辺: 操作色と同色、幅 2.5px、不透明度 0.7

##### ノード配置の計算

SVG viewBox ベースの座標計算でレスポンシブ対応。

- **レベル間隔**: viewBox 高さを `depth + 1` 等分
- **水平配置**: レベル `d` のノード数は最大 `2^d`。各ノードの水平位置はレベル内の位置に基づいて等間隔配置
- **座標計算**:
  ```
  b = BranchingFactor (2 or 3)
  depth = ⌊log_b(heapBoundary)⌋
  levelStart(L) = (b^L - 1) / (b - 1)
  levelSize(L)  = b^L
  nodeY(level) = topPad + level × (availH / depth)
  nodeX(level, posInLevel) = (posInLevel + 0.5) × (viewW / levelSize)
  ```

##### ヒープ境界の追跡

`TutorialStep` に `HeapBoundary`（`int?`）を追加する。

- **初期状態**: `null`（ヒープ木表示対象外）
- **ヒープ構築フェーズ**: `HeapBoundary = n`（全要素がヒープ内）
- **抽出フェーズ**: Swap 操作で root と末尾を交換するたびに `HeapBoundary` が 1 減少

`TutorialStepBuilder` がアルゴリズムの `TutorialVisualizationHint == HeapTree` のとき、ヒープ境界を自動追跡する。

**追跡ロジック**:
1. 最初の Swap 操作（index1=0 を含む）が現れるまでは `HeapBoundary = n`（構築フェーズ）
2. Swap で `index1 == 0`（root）かつ `index2 == heapBoundary - 1` のとき、`heapBoundary--`
3. 全ステップに `HeapBoundary` を記録

##### 切り替え UI

```
[🔵 Marble]  [🌳 Heap Tree]     ← セグメントボタン
```

- **表示条件**: `AlgorithmMetadata.TutorialVisualizationHint == HeapTree` のときのみ表示
- **デフォルト**: Heap Tree（教育目的では木表示が主役）
- **切り替え**: マーブル表示と木表示を排他的に切り替え（同時表示はしない）
- **状態保持**: アルゴリズム切り替え時にリセット（デフォルトに戻る）

##### WeakHeap 辺の区別表示

Weak Heap は通常の二分ヒープと異なり、各ノード `i` に **reverse bit `r[i]`** があり、Merge 機流でスワップと同時に履履される。この bit はどの子が「distinguished child」かを決定する。

```
r[i] = false のとき:        r[i] = true のとき (flip 後):
       (i)                          (i)
      /   ₢                        /   ₡
  ⭐(2i)   (2i+1)              (2i)  ⭐(2i+1)

  ⭐ = distinguished child         ⭐ = distinguished child
       (weak heap 性質の保証範囲)       (right side に移動した)
```

**辺の表示ルール:**

| 辺の種類 | 表示 | 意味 |
|---|---|---|
| Distinguished edge | 実線、通常輝度 | weak heap 性質が保証される部分木への辺 |
| Non-distinguished edge | **破線**、低輝度 | 次序保証なしの部分木への辺 |
| Highlighted distinguished | 操作色で実線 | Compare / Swap の対象辺 |
| Highlighted non-distinguished | 操作色で破線 | 強調はあるが次序保証なし |

##### `TutorialVisualizationHint` enum

```csharp
/// <summary>
/// チュートリアルで利用可能な追加ビジュアライゼーションのヒント。
/// アルゴリズムごとに設定し、木表示などの代替表現を有効化する。
/// </summary>
public enum TutorialVisualizationHint
{
    /// <summary>追加表示なし（マーブルのみ）</summary>
    None,
    /// <summary>ヒープ木表示（二分ヒープを SVG ツリーで描画）</summary>
    HeapTree,
    /// <summary>三分ヒープ木表示（三分ヒープを SVG ツリーで描画）</summary>
    TernaryHeapTree,
    /// <summary>弱ヒープ木表示（二分木レイアウト + reverse bit による辺区別）</summary>
    WeakHeapTree,
}
```

`AlgorithmMetadata` に追加:

```csharp
/// <summary>チュートリアルで利用可能な追加ビジュアライゼーション</summary>
public TutorialVisualizationHint TutorialVisualizationHint { get; init; } = TutorialVisualizationHint.None;
```

##### データフロー

```
AlgorithmMetadata.TutorialVisualizationHint == HeapTree
    ↓
TutorialStepBuilder.Build(initialArray, operations, hint)
    ↓ ヒープ境界を自動追跡
TutorialStep.HeapBoundary = 8, 7, 6, ...
    ↓
TutorialPage: ユーザーがトグルで表示切替
    ↓
HeapTreeRenderer.razor
    ├ Values = step.ArraySnapshot
    ├ HeapBoundary = step.HeapBoundary
    ├ HighlightIndices / HighlightType
    └ SVG で二分木を描画
```

##### アーキテクチャへの影響

```
Models/
  TutorialVisualizationHint.cs   ← 変更: WeakHeapTree 追加
  TutorialStep.cs                ← 変更: HeapBoundary + WeakHeapReverseBits プロパティ追加
  AlgorithmMetadata.cs           ← 変更: TutorialVisualizationHint プロパティ追加

Components/
  HeapTreeRenderer.razor         ← 新規: ヒープ木 SVG 描画コンポーネント

Services/
  TutorialStepBuilder.cs         ← 変更: HeapBoundary 追跡ロジック追加
  AlgorithmRegistry.cs           ← 変更: Heapsort / Bottom-up heapsort に HeapTree 設定

Pages/
  TutorialPage.razor             ← 変更: トグル UI + HeapTreeRenderer 呼び出し
  TutorialPage.razor.css         ← 変更: トグル + ヒープ木スタイル追加
```

---

#### 非平衡 BST 表示（BinaryTreeSort 専用ビジュアライゼーション）

`BinaryTreeSort` では二分探索木（BST）を SVG で描画し、挿入フェーズ・走査フェーズを視覚的に示す。ヒープ木と異なり、配列インデックスと木構造に直接対応しない（ノードは挿入順に ID が付与される）。

##### 対象アルゴリズム

| アルゴリズム | BST 表示 | 備考 |
|---|---|---|
| Unbalanced binary tree sort | ✅ | 非平衡 BST（右子 = 値 ≥ 親）、挿入順 ID |
| Balanced binary tree sort (AVL) | ✅ | 平衡 BST（回転後の木を表示）、balance factor ラベル + 回転ノードハイライト |

##### BST のレイアウト

```
配列: [5, 3, 8, 1, 9, 2, 7, 4]  挿入順 → BST

              (5)  ← node 0 (root), depth 0, rank 4
             /   \
           (3)   (8)           ← nodes 1,2, depth 1
           / \   / \
         (1) (4)(7) (9)        ← nodes 3,7,6,4, depth 2
           \
           (2)                 ← node 5, depth 3

in-order traversal: 1, 2, 3, 4, 5, 7, 8, 9
```

- **x 座標**: 中順走査の rank（0..n-1）。左部分木 < 親 < 右部分木が自然に反映される
- **y 座標**: BFS で計算した深さ（0 = 根）
- **ノード ID**: 挿入順（0 = 最初に挿入した要素 = 根）

##### 操作フェーズと表示

| フェーズ | 操作 | 表示 |
|---|---|---|
| **ビルドフェーズ** | `IndexRead(i)` × n | BST に新ノード追加。挿入経路（amber）+ 新ノード（green）をハイライト |
| **走査フェーズ** | `IndexWrite(j, v)` × n | 全ノード挿入済み BST で現在訪問中のノード（orange）をハイライト |

##### ノードのハイライト規則

| ノード種別 | 色 | 意味 |
|---|---|---|
| `InsertionPath` | amber `#FBBF24` | 今回の挿入で比較・通過したノード |
| `NewNode` | green `#4ADE80` | 今回挿入されたノード（挿入先） |
| `ActiveNode` | orange `#F97316` | 中順走査で現在配列に書き戻し中のノード |

##### `BstSnapshot` データモデル

```csharp
public record BstSnapshot
{
    public int    Size;            // 現在のノード数
    public int    Root;            // 根 ID (-1 = 空)
    public int[]  Values;          // ノード ID → 値
    public int[]  Left;            // ノード ID → 左子 ID (-1 = なし)
    public int[]  Right;           // ノード ID → 右子 ID (-1 = なし)
    public int[]  InsertionPath;   // 直前挿入で辿ったノード ID リスト
    public int    NewNode;         // 直前に挿入されたノード ID (-1 = なし)
    public int    ActiveNode;      // 走査中の現在ノード ID (-1 = なし)
    public bool   IsTraversalPhase;
}
```

##### 追跡ロジック（TutorialStepBuilder）

```
IndexRead(i) →
  1. value = mainArray[i] を shadow BST に挿入（BinaryTreeSort と同一ロジック）
  2. 挿入経路 InsertionPath と NewNode を記録
  3. BstSnapshot をスナップショット化

最初の IndexWrite が来たとき →
  1. 中順走査リストを事前計算: BstComputeInorder(root, left, right, size)
  2. IsTraversalPhase = true

IndexWrite(j, v) →
  1. ActiveNode = inorderList[j]  (j 番目の中順訪問ノード)
  2. BstSnapshot を更新
```

##### アーキテクチャへの影響

```
Models/
  BstSnapshot.cs                 ← 新規: BST スナップショットレコード
  TutorialVisualizationHint.cs   ← 変更: BstTree 追加
  TutorialStep.cs                ← 変更: Bst プロパティ追加

Components/
  BstTreeRenderer.razor          ← 新規: BST SVG 描画コンポーネント

Services/
  TutorialStepBuilder.cs         ← 変更: BST シャドウリプレイ + narrative オーバーライド
  AlgorithmRegistry.cs           ← 変更: BinaryTreeSort に BstTree 設定

Pages/
  TutorialPage.razor             ← 変更: IsBstView / GetCurrentBst / BstTreeRenderer
  TutorialPage.razor.css         ← 変更: BST tree スタイル追加
```

---

#### AVL 木表示（BalancedBinaryTreeSort 専用ビジュアライゼーション）

`BalancedBinaryTreeSort` では AVL 木（自己平衡二分探索木）を SVG で描画し、挿入フェーズで自動的に行われる回転操作を可視化する。BST 表示を拡張し、balance factor ラベルと回転ノードのハイライトを追加する。

##### 対象アルゴリズム

| アルゴリズム | AVL 表示 | 備考 |
|---|---|---|
| Balanced binary tree sort (AVL) | ✅ | 回転後の木を表示（Option A: 回転アニメーションなし）、balance factor 付き |

##### AVL 木のレイアウト

BST と同一の in-order rank × BFS depth レイアウトを使用。追加で以下の要素を表示：

```
配列: [5, 3, 8, 1, 9, 2, 7, 4]  挿入順 → AVL 木

              (5)              ← node 0, bf=0
             /   \
           (3)   (8)           ← nodes 1,2, bf=+1, bf=-1
           / \   / \
         (1) (4)(7) (9)        ← nodes 3,7,6,4
         +1  0  0   0
           \
           (2)                 ← node 5, bf=0
            0

balance factor = height(left) - height(right)
```

- **balance factor ラベル**: ノード下部に小さいフォントで表示
  - `bf = 0`: 緑色 `#6EE7B7`（完全平衡）
  - `bf = ±1`: 黄色 `#FCD34D`（許容範囲内）
  - `bf = ±2`: 赤色 `#F87171`（一時的不均衡、回転直前のみ表示される）
- **回転ノードのハイライト**: 直前の挿入で回転に関与したノードを紫リング `#A855F7` で表示

##### 操作フェーズと表示

| フェーズ | 操作 | 表示 |
|---|---|---|
| **ビルドフェーズ** | `IndexRead(i)` × n | AVL 木に新ノード挿入 → 経路を遡りながら回転（もし必要なら）→ 回転後の木を表示 |
| **走査フェーズ** | `IndexWrite(j, v)` × n | 全ノード挿入済み AVL 木で現在訪問中のノード（orange）をハイライト |

##### ノードのハイライト規則（優先順位順）

| ノード種別 | 色 | 意味 | 優先度 |
|---|---|---|---|
| `NewNode` | green `#4ADE80` | 今回挿入されたノード（挿入先） | 1 |
| `RotatedNodes` | purple `#A855F7` | 今回の挿入で回転に関与したノード（回転軸 + その親・子） | 2 |
| `ActiveNode` | orange `#F97316` | 中順走査で現在配列に書き戻し中のノード | 3 |
| `InsertionPath` | amber `#FBBF24` | 今回の挿入で比較・通過したノード | 4 |

##### 回転タイプの表示

narrative に回転タイプを明示する：

```
"Insert 9 at depth 2 — LL at 5"   ← LL rotation（左-左ケース）が node 5 で発生
"Insert 2 at depth 3 — LR at 3"   ← LR rotation（左-右ケース）が node 3 で発生
"Insert 7 at depth 2"             ← 回転なし（バランス OK）
```

回転タイプ:
- **LL**: 左部分木の左子が重い → 右回転
- **RR**: 右部分木の右子が重い → 左回転
- **LR**: 左部分木の右子が重い → 左子を左回転してから右回転
- **RL**: 右部分木の左子が重い → 右子を右回転してから左回転

##### `BstSnapshot` データモデル拡張

AVL 用に `Heights` と `RotatedNodes` を追加：

```csharp
public record BstSnapshot
{
    public int    Size;            // 現在のノード数
    public int    Root;            // 根 ID (-1 = 空)
    public int[]  Values;          // ノード ID → 値
    public int[]  Left;            // ノード ID → 左子 ID (-1 = なし)
    public int[]  Right;           // ノード ID → 右子 ID (-1 = なし)
    public int[]  InsertionPath;   // 直前挿入で辿ったノード ID リスト
    public int    NewNode;         // 直前に挿入されたノード ID (-1 = なし)
    public int    ActiveNode;      // 走査中の現在ノード ID (-1 = なし)
    public bool   IsTraversalPhase;

    // AVL-only fields
    public int[]? Heights;         // ノード ID → height（null = BST モード）
    public int[]  RotatedNodes;    // 直前の挿入で回転に関与したノード ID リスト
}
```

##### 追跡ロジック（TutorialStepBuilder）

```
trackAvl = (hint == TutorialVisualizationHint.AvlTree)

IndexRead(i) →
  1. value = mainArray[i] を shadow BST に挿入（経路記録）
  2. avlHeight[newNode] = 1
  3. 挿入経路を末尾から遡る:
     while (pathTop > 0)
       node = avlPathBuf[--pathTop]
       AvlUpdateHeight(node)
       (newRoot, rotType) = AvlBalance(node)  ← LL/RR/LR/RL 判定
       if rotType != null:
         rotatedNodes.Add(node)
         rotatedNodes.Add(newRoot)  // 回転後の新ルート
         rotationDesc += "{rotType} at {node.Value}"
       subtreeRoot = newRoot
  4. bstRoot = 最終 subtreeRoot
  5. RotatedNodes, narrative に反映

AVL 静的ヘルパー（TutorialStepBuilder 内）:
  - AvlUpdateHeight(i): height[i] = 1 + max(left.height, right.height)
  - AvlGetBalance(i): return left.height - right.height
  - AvlRotateRight(y): x = y.left; y.left = x.right; x.right = y; return x
  - AvlRotateLeft(x): y = x.right; x.right = y.left; y.left = x; return y
  - AvlBalance(node):
      bf = AvlGetBalance(node)
      if bf > 1:        // Left-heavy
        if left.bf < 0: left = RotateLeft(left); rotType = "LR"
        else:           rotType = "LL"
        return (RotateRight(node), rotType)
      if bf < -1:       // Right-heavy
        if right.bf > 0: right = RotateRight(right); rotType = "RL"
        else:            rotType = "RR"
        return (RotateLeft(node), rotType)
      return (node, null)  // already balanced
```

##### BstTreeRenderer の AVL 対応

- **`avlMode = (Bst.Heights != null)`** で AVL モードを判定
- **viewH 調整**: `bfPad = avlMode ? 18 : 0` で balance factor ラベル分のスペースを追加
- **balance factor ラベル**: 各ノード下部に `MarkupString` で SVG `<text>` を出力
  ```csharp
  int bf = ComputeBalanceFactor(i);
  string bfColor = bf == 0 ? "#6EE7B7" : (Math.Abs(bf) == 1 ? "#FCD34D" : "#F87171");
  string bfText = $"{(bf > 0 ? "+" : "")}{bf}";
  @(new MarkupString($"<text x=\"{nx}\" y=\"{ny + nodeR + 11}\" ...>{bfText}</text>"))
  ```
- **回転ノードハイライト**: `rotatedSet.Contains(i)` で紫リング表示
- **フェーズバッジ**: "Building AVL Tree" vs "Building BST"

##### アーキテクチャへの影響

```
Models/
  BstSnapshot.cs                 ← 変更: Heights, RotatedNodes 追加
  TutorialVisualizationHint.cs   ← 変更: AvlTree 追加
  TutorialStep.cs                ← 既存の Bst プロパティで対応

Components/
  BstTreeRenderer.razor          ← 変更: avlMode 判定、balance factor ラベル、rotated node ハイライト

Services/
  TutorialStepBuilder.cs         ← 変更: trackAvl, AVL rebalancing block, AVL static helpers (UpdateHeight/Balance/Rotate)
  AlgorithmRegistry.cs           ← 変更: BalancedBinaryTreeSort に AvlTree 設定

Pages/
  TutorialPage.razor             ← 変更: IsBstView に AvlTree 追加、GetTreeToggleLabel に "AVL Tree" 追加
  TutorialPage.razor.css         ← 既存の bst-* スタイルで対応
```

#### アーキテクチャへの影響（新規ファイル）

```
Pages/
  TutorialPage.razor           ← チュートリアルメインページ

Components/
  MarbleRenderer.razor         ← マーブル描画コンポーネント
  HeapTreeRenderer.razor       ← ヒープ木描画コンポーネント（Heap Sort 用）
  BstTreeRenderer.razor        ← BST 描画コンポーネント（BinaryTreeSort 用）
  TutorialAlgorithmPanel.razor ← 説明パネル（上部）
  TutorialNarrativePanel.razor ← ステップ説明テキスト（中部）
  TutorialControls.razor       ← ナビゲーションボタン群

Services/
  TutorialStepBuilder.cs       ← SortOperation → TutorialStep 変換ロジック

Models/
  TutorialStep.cs              ← TutorialStep レコード定義
  TutorialVisualizationHint.cs ← チュートリアル追加表示ヒント
  BstSnapshot.cs               ← BST スナップショットレコード
  RoleType.cs                  ← RoleType enum 定義（Phase / Role Tracker）
```

既存の `SortExecutor`・`AlgorithmRegistry`・`PlaybackService` はそのまま再利用する。
